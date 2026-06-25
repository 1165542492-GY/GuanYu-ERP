using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ClosedXML.Excel;

namespace SupplierErpApp
{
    public static partial class Program
    {
        class CustomerReconciliationDetailRow
        {
            public string OrderDate;
            public string DocNo;
            public string ProductSpec;
            public decimal Quantity;
            public decimal UnitPrice;
            public decimal Amount;
            public decimal ReceiptAmount;
            public decimal Remaining;
            public string ReceiptDate;
            public string Note;
            public bool IsReceiptRow;
        }

        class CustomerReconciliationStatement
        {
            public string Title;
            public string CustomerName;
            public string PeriodText;
            public string ExportDate;
            public decimal OpeningBalance;
            public decimal PeriodNewReceivable;
            public decimal PeriodReceipts;
            public decimal ClosingUnreceived;
            public string RemittanceInfo;
            public List<CustomerReconciliationDetailRow> Details = new List<CustomerReconciliationDetailRow>();
        }

        static DateTime? ParseBizDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            DateTime d;
            if (DateTime.TryParse(text, out d)) return d.Date;
            return null;
        }

        static bool TryParseReconciliationMonth(string month, out DateTime monthStart, out DateTime monthEnd, out string error)
        {
            monthStart = default;
            monthEnd = default;
            error = null;
            if (string.IsNullOrWhiteSpace(month))
            {
                error = "请选择对账月份";
                return false;
            }
            if (!DateTime.TryParseExact(month.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out monthStart))
            {
                error = "月份格式无效，请使用 yyyy-MM";
                return false;
            }
            monthEnd = monthStart.AddMonths(1);
            return true;
        }

        static bool ReceivableBelongsToCustomer(Receivable receivable, Customer customer, List<SalesOrder> salesOrders)
        {
            if (receivable == null || customer == null) return false;
            if (string.Equals((receivable.CustomerName ?? "").Trim(), (customer.Company ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.IsNullOrWhiteSpace(receivable.SalesOrderId)) return false;
            var order = salesOrders.FirstOrDefault(x => x.Id == receivable.SalesOrderId);
            if (order == null) return false;
            if (string.Equals(order.CustomerId ?? "", customer.Id ?? "", StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals((order.CustomerName ?? "").Trim(), (customer.Company ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
        }

        static DateTime? GetReceivableOrderDate(Receivable receivable, SalesOrder order)
        {
            if (order != null)
            {
                var od = ParseBizDate(order.OrderDate);
                if (od.HasValue) return od;
            }
            return ParseBizDate(receivable.DueDate);
        }

        static decimal SumReceiptsBefore(Receivable receivable, DateTime beforeDate)
        {
            decimal sum = 0;
            foreach (var detail in receivable.ReceiptDetails ?? new List<ReceiptDetail>())
            {
                var dt = ParseBizDate(detail.ReceiptDate);
                if (dt.HasValue && dt.Value < beforeDate) sum += detail.Amount;
            }
            return RoundMoney(sum);
        }

        static List<ReceiptDetail> GetReceiptsInRange(Receivable receivable, DateTime startInclusive, DateTime endExclusive)
        {
            var list = new List<ReceiptDetail>();
            foreach (var detail in receivable.ReceiptDetails ?? new List<ReceiptDetail>())
            {
                var dt = ParseBizDate(detail.ReceiptDate);
                if (dt.HasValue && dt.Value >= startInclusive && dt.Value < endExclusive)
                    list.Add(detail);
            }
            return list;
        }

        static decimal UnreceivedAt(Receivable receivable, DateTime asOfExclusive)
        {
            return RoundMoney(receivable.ReceivableAmount - SumReceiptsBefore(receivable, asOfExclusive));
        }

        static string FormatReconciliationPeriod(DateTime monthStart)
        {
            return monthStart.ToString("yyyy年M月", CultureInfo.InvariantCulture);
        }

        static string FormatStatementTitle(DateTime monthStart)
        {
            return monthStart.ToString("yyyy年M月", CultureInfo.InvariantCulture) + "客户应收对账单";
        }

        static string GetCompanyRemittanceInfo()
        {
            try
            {
                var setting = LoadContractSettings().FirstOrDefault(x =>
                    string.Equals(x.Name ?? "", "冠誉公司资料", StringComparison.OrdinalIgnoreCase)
                    && (x.Status ?? "启用") == "启用");
                if (setting != null && !string.IsNullOrWhiteSpace(setting.Content))
                    return setting.Content.Trim();
            }
            catch { }
            return "中山市冠誉数控设备有限公司\n开户行：\n账号：\n联系人：王浪\n电话：18988541298";
        }

        static string SanitizeFileNamePart(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "客户";
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char c in name.Trim())
                sb.Append(invalid.Contains(c) ? '_' : c);
            var result = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "客户" : result;
        }

        static CustomerReconciliationStatement BuildCustomerReconciliationStatement(Customer customer, DateTime monthStart, DateTime monthEnd)
        {
            var salesOrders = LoadSalesOrders();
            var receivables = LoadReceivables();
            var outbounds = LoadSalesOutbounds();
            var contracts = LoadContracts();
            var customerReceivables = receivables.Where(x => ReceivableBelongsToCustomer(x, customer, salesOrders)).ToList();

            decimal opening = 0, periodNew = 0, periodReceipts = 0;
            foreach (var rec in customerReceivables)
            {
                var order = salesOrders.FirstOrDefault(x => x.Id == rec.SalesOrderId);
                var orderDate = GetReceivableOrderDate(rec, order);
                if (orderDate.HasValue && orderDate.Value < monthStart)
                    opening += UnreceivedAt(rec, monthStart);
                if (orderDate.HasValue && orderDate.Value >= monthStart && orderDate.Value < monthEnd)
                    periodNew += rec.ReceivableAmount;
                periodReceipts += GetReceiptsInRange(rec, monthStart, monthEnd).Sum(x => x.Amount);
            }
            opening = RoundMoney(opening);
            periodNew = RoundMoney(periodNew);
            periodReceipts = RoundMoney(periodReceipts);
            decimal closing = RoundMoney(opening + periodNew - periodReceipts);

            var statement = new CustomerReconciliationStatement
            {
                Title = FormatStatementTitle(monthStart),
                CustomerName = customer.Company ?? customer.Code ?? "-",
                PeriodText = FormatReconciliationPeriod(monthStart),
                ExportDate = DateTime.Now.ToString("yyyy-MM-dd"),
                OpeningBalance = opening,
                PeriodNewReceivable = periodNew,
                PeriodReceipts = periodReceipts,
                ClosingUnreceived = closing,
                RemittanceInfo = GetCompanyRemittanceInfo()
            };

            var details = new List<CustomerReconciliationDetailRow>();
            foreach (var rec in customerReceivables.OrderBy(x =>
            {
                var o = salesOrders.FirstOrDefault(s => s.Id == x.SalesOrderId);
                return GetReceivableOrderDate(x, o) ?? DateTime.MaxValue;
            }).ThenBy(x => x.Code))
            {
                var order = salesOrders.FirstOrDefault(x => x.Id == rec.SalesOrderId);
                var orderDate = GetReceivableOrderDate(rec, order);
                if (!orderDate.HasValue || orderDate.Value >= monthEnd) continue;

                decimal unreceivedEnd = UnreceivedAt(rec, monthEnd);
                var receiptsInMonth = GetReceiptsInRange(rec, monthStart, monthEnd);
                bool hadActivity = orderDate.Value >= monthStart || receiptsInMonth.Count > 0 || unreceivedEnd > 0;
                if (!hadActivity) continue;

                var outbound = outbounds.FirstOrDefault(x => x.SalesOrderId == order?.Id);
                DateTime? deliveryDate = outbound != null ? ParseBizDate(outbound.OutboundDate) : orderDate;
                var contract = contracts
                    .Where(c => string.Equals(c.CustomerCode ?? "", customer.Code ?? "", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(c.Status ?? "", "已作废", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(c => c.SignDate ?? "")
                    .FirstOrDefault();
                string orderNo = order?.Code ?? rec.SalesOrderNo ?? rec.Code ?? "-";
                string docNo = contract != null && !string.IsNullOrWhiteSpace(contract.Code)
                    ? contract.Code + " / " + orderNo
                    : orderNo;

                decimal qty = order != null ? order.Quantity : 0;
                decimal unitPrice = 0;
                decimal amount = rec.ReceivableAmount;
                if (order != null)
                {
                    amount = order.Amount > 0 ? order.Amount : rec.ReceivableAmount;
                    if (order.TaxIncludedSalePrice > 0) unitPrice = order.TaxIncludedSalePrice;
                    else if (order.TaxIncludedSaleAmount > 0 && qty > 0) unitPrice = RoundMoney(order.TaxIncludedSaleAmount / qty);
                    else if (qty > 0) unitPrice = RoundMoney(amount / qty);
                    else unitPrice = amount;
                }
                else if (qty <= 0)
                {
                    qty = 1;
                    unitPrice = amount;
                }

                details.Add(new CustomerReconciliationDetailRow
                {
                    OrderDate = (deliveryDate ?? orderDate).Value.ToString("yyyy-MM-dd"),
                    DocNo = docNo,
                    ProductSpec = order?.MaterialName ?? "-",
                    Quantity = qty,
                    UnitPrice = unitPrice,
                    Amount = amount,
                    ReceiptAmount = 0,
                    Remaining = unreceivedEnd,
                    ReceiptDate = "",
                    Note = string.IsNullOrWhiteSpace(rec.Note) ? (order?.Note ?? "") : rec.Note,
                    IsReceiptRow = false
                });

                decimal running = amount - SumReceiptsBefore(rec, monthStart);
                foreach (var rd in receiptsInMonth.OrderBy(x => x.ReceiptDate ?? ""))
                {
                    running = RoundMoney(running - rd.Amount);
                    details.Add(new CustomerReconciliationDetailRow
                    {
                        OrderDate = "",
                        DocNo = "",
                        ProductSpec = "",
                        Quantity = 0,
                        UnitPrice = 0,
                        Amount = 0,
                        ReceiptAmount = rd.Amount,
                        Remaining = running < 0 ? 0 : running,
                        ReceiptDate = rd.ReceiptDate ?? "",
                        Note = rd.Note ?? "",
                        IsReceiptRow = true
                    });
                }
            }
            statement.Details = details;
            return statement;
        }

        static void WriteNamedXlsxDownload(HttpListenerContext ctx, string fileName, byte[] bytes)
        {
            ctx.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            string encoded = Uri.EscapeDataString(fileName ?? "export.xlsx");
            ctx.Response.AddHeader("Content-Disposition", "attachment; filename=\"export.xlsx\"; filename*=UTF-8''" + encoded);
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        static byte[] BuildCustomerReconciliationWorkbook(CustomerReconciliationStatement stmt)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("客户对账单");
                const int colCount = 10;
                ws.Column(1).Width = 14;
                ws.Column(2).Width = 22;
                ws.Column(3).Width = 24;
                ws.Column(4).Width = 8;
                ws.Column(5).Width = 12;
                ws.Column(6).Width = 12;
                ws.Column(7).Width = 12;
                ws.Column(8).Width = 12;
                ws.Column(9).Width = 12;
                ws.Column(10).Width = 18;

                int row = 1;
                ws.Range(row, 1, row, colCount).Merge();
                ws.Cell(row, 1).Value = stmt.Title;
                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 16;
                row++;

                ws.Cell(row, 1).Value = "客户名称：" + stmt.CustomerName;
                ws.Cell(row, 1).Style.Font.Bold = true;
                row++;
                ws.Cell(row, 1).Value = "对账期间：" + stmt.PeriodText;
                ws.Cell(row, 5).Value = "导出日期：" + stmt.ExportDate;
                row++;
                row++;

                ws.Cell(row, 1).Value = "期初应收";
                ws.Cell(row, 3).Value = "本期新增应收";
                ws.Cell(row, 5).Value = "本期收款";
                ws.Cell(row, 7).Value = "期末未收";
                ws.Range(row, 1, row, colCount).Style.Font.Bold = true;
                row++;
                ws.Cell(row, 1).Value = stmt.OpeningBalance;
                ws.Cell(row, 3).Value = stmt.PeriodNewReceivable;
                ws.Cell(row, 5).Value = stmt.PeriodReceipts;
                ws.Cell(row, 7).Value = stmt.ClosingUnreceived;
                ws.Range(row, 1, row, 8).Style.NumberFormat.Format = "#,##0.00";
                ws.Range(row, 1, row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                row++;
                row++;

                string[] headers = { "送机日期/订单日期", "合同编号/销售订单号", "机型规格/产品名称", "数量", "单价/含税", "金额/含税", "本次收款", "剩余未收", "收款日期", "备注" };
                for (int i = 0; i < headers.Length; i++)
                    ws.Cell(row, i + 1).Value = headers[i];
                ws.Range(row, 1, row, colCount).Style.Font.Bold = true;
                ws.Range(row, 1, row, colCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                int headerRow = row;
                row++;

                foreach (var d in stmt.Details)
                {
                    ws.Cell(row, 1).Value = d.OrderDate;
                    ws.Cell(row, 2).Value = d.DocNo;
                    ws.Cell(row, 3).Value = d.ProductSpec;
                    if (d.Quantity != 0) ws.Cell(row, 4).Value = d.Quantity;
                    if (d.UnitPrice != 0) ws.Cell(row, 5).Value = d.UnitPrice;
                    if (d.Amount != 0) ws.Cell(row, 6).Value = d.Amount;
                    if (d.ReceiptAmount != 0) ws.Cell(row, 7).Value = d.ReceiptAmount;
                    if (d.Remaining != 0 || !d.IsReceiptRow) ws.Cell(row, 8).Value = d.Remaining;
                    ws.Cell(row, 9).Value = d.ReceiptDate;
                    ws.Cell(row, 10).Value = d.Note;
                    if (d.IsReceiptRow)
                        ws.Range(row, 1, row, colCount).Style.Font.FontColor = XLColor.FromHtml("#555555");
                    row++;
                }

                if (stmt.Details.Count == 0)
                {
                    ws.Range(row, 1, row, colCount).Merge();
                    ws.Cell(row, 1).Value = "本期暂无明细数据";
                    ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    row++;
                }

                ws.Range(headerRow, 4, row - 1, 4).Style.NumberFormat.Format = "#,##0.####";
                ws.Range(headerRow, 5, row - 1, 8).Style.NumberFormat.Format = "#,##0.00";
                ws.Range(headerRow, 5, row - 1, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Range(headerRow, 1, row - 1, colCount).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(headerRow, 1, row - 1, colCount).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                row += 2;
                ws.Range(row, 1, row, colCount).Merge();
                ws.Cell(row, 1).Value = "请核对无误后签字盖章回传，如有疑问请三天内确认并回复，逾期视为无误处理。";
                row += 2;
                ws.Cell(row, 1).Value = "我方汇款资料";
                ws.Cell(row, 1).Style.Font.Bold = true;
                row++;
                ws.Range(row, 1, row + 3, colCount).Merge();
                ws.Cell(row, 1).Value = stmt.RemittanceInfo;
                ws.Cell(row, 1).Style.Alignment.WrapText = true;
                row += 5;
                ws.Cell(row, 1).Value = "客户确认盖章：";
                ws.Cell(row, 6).Value = "日期：";
                row += 2;
                ws.Cell(row, 1).Value = "确认人签名：";

                ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
                ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                ws.PageSetup.FitToPages(1, 0);

                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        static void ListReconciliationCustomers(HttpListenerContext ctx, UserSession user)
        {
            if (!RequirePermission(ctx, user, "reconciliation.customer_view")) return;
            var list = LoadCustomers()
                .Where(x => (x.Status ?? "启用") == "启用")
                .OrderBy(x => x.Company ?? x.Code ?? "")
                .Select(x => new { x.Id, x.Code, x.Company })
                .ToArray();
            WriteJson(ctx, list);
        }

        static void ExportCustomerReconciliation(HttpListenerContext ctx, UserSession user)
        {
            if (!RequirePermission(ctx, user, "reconciliation.customer_export")) return;
            string customerId = ctx.Request.QueryString["customerId"];
            string month = ctx.Request.QueryString["month"];
            DateTime monthStart, monthEnd;
            string monthError;
            if (!TryParseReconciliationMonth(month, out monthStart, out monthEnd, out monthError))
            {
                WriteJson(ctx, new { error = monthError, message = monthError }, 400);
                return;
            }
            if (string.IsNullOrWhiteSpace(customerId))
            {
                WriteJson(ctx, new { error = "请选择客户", message = "请选择客户" }, 400);
                return;
            }
            var customer = LoadCustomers().FirstOrDefault(x => x.Id == customerId);
            if (customer == null)
            {
                WriteJson(ctx, new { error = "客户不存在", message = "客户不存在" }, 404);
                return;
            }

            try
            {
                var stmt = BuildCustomerReconciliationStatement(customer, monthStart, monthEnd);
                byte[] bytes = BuildCustomerReconciliationWorkbook(stmt);
                string fileName = "客户对账单_" + SanitizeFileNamePart(customer.Company ?? customer.Code) + "_" + monthStart.ToString("yyyyMM") + ".xlsx";
                Audit(user, "导出客户对账单", customer.Company + " " + monthStart.ToString("yyyy-MM"));
                WriteNamedXlsxDownload(ctx, fileName, bytes);
            }
            catch (Exception ex)
            {
                string msg = "导出客户对账单失败：" + ToUserMessage(ex);
                WriteJson(ctx, new { error = msg, message = msg }, 500);
            }
        }
    }
}
