using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ClosedXML.Excel;

namespace SupplierErpApp
{
    public static partial class Program
    {
        static readonly Dictionary<string, string> TestDataSheetMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "供应商管理", "suppliers" },
            { "客户管理", "customers" },
            { "物料管理", "materials" },
            { "BOM表", "boms" },
            { "BOM明细", "boms" },
            { "机型成本", "modelCosts" },
            { "销售订单", "salesOrders" },
            { "销售出库", "salesOutbounds" },
            { "采购单", "purchaseOrders" },
            { "采购入库", "purchaseInbounds" },
            { "生产领用", "productionPicks" },
            { "成品入库", "finishedInbounds" },
            { "应收款", "receivables" },
            { "应付款", "payables" },
            { "财务收支", "financeTransactions" },
            { "财务期初余额", "financeOpening" },
            { "库存汇总", "stockReference" },
            { "异常测试数据", "testValidation" }
        };

        static readonly Dictionary<string, string> TestDataModuleLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "suppliers", "供应商管理" },
            { "customers", "客户管理" },
            { "materials", "物料管理" },
            { "boms", "BOM表" },
            { "modelCosts", "机型成本" },
            { "salesOrders", "销售订单" },
            { "salesOutbounds", "销售出库" },
            { "purchaseOrders", "采购单" },
            { "purchaseInbounds", "采购入库" },
            { "productionPicks", "生产领用" },
            { "finishedInbounds", "成品入库" },
            { "receivables", "应收款" },
            { "payables", "应付款" },
            { "financeTransactions", "财务收支" },
            { "financeOpening", "财务期初余额" },
            { "stockReference", "库存汇总" },
            { "testValidation", "异常测试数据" }
        };

        static readonly string[] TestDataImportOrder = {
            "suppliers", "customers", "materials", "boms", "modelCosts",
            "purchaseOrders", "purchaseInbounds", "salesOrders", "salesOutbounds",
            "productionPicks", "finishedInbounds", "receivables", "payables",
            "financeTransactions", "financeOpening"
        };

        static bool RequireTestDataAccess(HttpListenerContext ctx, UserSession user)
        {
            if (user == null) { WriteJson(ctx, new { error = "请先登录" }, 401); return false; }
            if (!IsAdminUser(user)) { WriteJson(ctx, new { error = "仅管理员可操作测试数据导入导出" }, 403); return false; }
            return true;
        }

        static void ExportTestDataAll(HttpListenerContext ctx, UserSession user)
        {
            using (var wb = new XLWorkbook())
            {
                WriteSupplierSheet(wb);
                WriteCustomerSheet(wb);
                WriteMaterialSheet(wb);
                WriteBomSheet(wb);
                WriteModelCostSheet(wb);
                WriteSalesOrderSheet(wb);
                WriteSalesOutboundSheet(wb);
                WritePurchaseOrderSheet(wb);
                WritePurchaseInboundSheet(wb);
                WriteProductionPickSheet(wb);
                WriteFinishedInboundSheet(wb);
                WriteReceivableSheet(wb);
                WritePayableSheet(wb);
                WriteFinanceSheet(wb);
                WriteFinanceOpeningSheet(wb);
                WriteStockReferenceSheet(wb);
                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    WriteXlsxDownload(ctx, "ERP测试数据总表" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx", ms.ToArray());
                }
            }
            Audit(user, "导出测试数据总表", "ERP测试数据总表");
        }

        static void ImportTestDataPreview(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<TestDataImportRequest>(ReadBody(ctx.Request));
            var parsed = ParseTestDataWorkbook(req);
            var sheets = new List<TestDataSheetInfo>();
            var unknown = new List<string>();
            foreach (var kv in parsed.Sheets)
            {
                string moduleKey;
                if (!TestDataSheetMap.TryGetValue(kv.Key, out moduleKey))
                {
                    unknown.Add(kv.Key);
                    continue;
                }
                bool reference = moduleKey == "stockReference" || moduleKey == "testValidation";
                var info = new TestDataSheetInfo
                {
                    SheetName = kv.Key,
                    ModuleKey = moduleKey,
                    ModuleLabel = TestDataModuleLabels.ContainsKey(moduleKey) ? TestDataModuleLabels[moduleKey] : kv.Key,
                    Importable = !reference,
                    ReferenceOnly = reference,
                    RowCount = kv.Value.Count,
                    Errors = reference ? new string[0] : PreviewModuleErrors(moduleKey, kv.Value).Take(20).ToArray()
                };
                sheets.Add(info);
            }
            WriteJson(ctx, new TestDataPreviewResult
            {
                FileName = parsed.FileName,
                Sheets = sheets.ToArray(),
                UnknownSheets = unknown.ToArray()
            });
        }

        static void ImportTestDataRun(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<TestDataImportRequest>(ReadBody(ctx.Request));
            var parsed = ParseTestDataWorkbook(req);
            var results = new List<TestDataModuleResult>();
            foreach (var moduleKey in TestDataImportOrder)
            {
                var rows = parsed.Sheets.Where(x => TestDataSheetMap.TryGetValue(x.Key, out var mk) && mk == moduleKey).SelectMany(x => x.Value).ToList();
                if (rows.Count == 0) continue;
                results.Add(ImportModuleRows(moduleKey, rows, user));
            }
            WriteJson(ctx, new TestDataRunResult
            {
                FileName = parsed.FileName,
                Modules = results.ToArray(),
                TotalAdded = results.Sum(x => x.Added),
                TotalUpdated = results.Sum(x => x.Updated),
                TotalSkipped = results.Sum(x => x.Skipped),
                TotalFailed = results.Sum(x => x.Failed)
            });
            Audit(user, "导入测试数据总表", parsed.FileName + " 新增" + results.Sum(x => x.Added) + " 更新" + results.Sum(x => x.Updated));
        }

        class ParsedTestWorkbook { public string FileName; public Dictionary<string, List<Dictionary<string, string>>> Sheets = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase); }

        static ParsedTestWorkbook ParseTestDataWorkbook(TestDataImportRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Data)) throw new BusinessException("请选择 Excel 文件");
            if (!string.IsNullOrEmpty(req.FileName) && !req.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new BusinessException("仅支持 .xlsx 格式文件");
            string encoded = req.Data.Trim();
            if (encoded.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int comma = encoded.IndexOf(',');
                if (comma >= 0) encoded = encoded.Substring(comma + 1);
            }
            byte[] bytes;
            try { bytes = Convert.FromBase64String(encoded); }
            catch { throw new BusinessException("Excel 文件内容无效"); }
            if (bytes.Length > 50 * 1024 * 1024) throw new BusinessException("Excel 文件不能超过 50MB");
            var parsed = new ParsedTestWorkbook { FileName = req.FileName ?? "import.xlsx" };
            using (var ms = new MemoryStream(bytes))
            using (var wb = new XLWorkbook(ms))
            {
                foreach (var ws in wb.Worksheets)
                {
                    var rows = ReadWorksheetRows(ws);
                    if (rows.Count > 0) parsed.Sheets[ws.Name.Trim()] = rows;
                }
            }
            return parsed;
        }

        static List<Dictionary<string, string>> ReadWorksheetRows(IXLWorksheet ws)
        {
            var result = new List<Dictionary<string, string>>();
            var used = ws.RangeUsed();
            if (used == null) return result;
            int firstRow = used.FirstRow().RowNumber();
            int lastRow = used.LastRow().RowNumber();
            int firstCol = used.FirstColumn().ColumnNumber();
            int lastCol = used.LastColumn().ColumnNumber();
            var headers = new List<string>();
            for (int c = firstCol; c <= lastCol; c++)
            {
                headers.Add((ws.Cell(firstRow, c).GetFormattedString() ?? "").Trim());
            }
            if (headers.All(string.IsNullOrWhiteSpace)) return result;
            for (int r = firstRow + 1; r <= lastRow; r++)
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bool hasValue = false;
                for (int c = firstCol; c <= lastCol; c++)
                {
                    string h = headers[c - firstCol];
                    if (string.IsNullOrWhiteSpace(h)) continue;
                    string val = (ws.Cell(r, c).GetFormattedString() ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(val)) hasValue = true;
                    row[h] = val;
                }
                if (hasValue) result.Add(row);
            }
            return result;
        }

        static void WriteXlsxDownload(HttpListenerContext ctx, string fileName, byte[] bytes)
        {
            ctx.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            ctx.Response.AddHeader("Content-Disposition", BuildExportContentDisposition(fileName));
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        static IXLWorksheet AddSheet(XLWorkbook wb, string name, string[] headers)
        {
            var ws = wb.Worksheets.Add(name);
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            ws.Row(1).Style.Font.Bold = true;
            return ws;
        }

        static void WriteRow(IXLWorksheet ws, int row, params object[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                var v = values[i];
                if (v is decimal d) ws.Cell(row, i + 1).Value = d;
                else if (v is double dbl) ws.Cell(row, i + 1).Value = dbl;
                else if (v is int n) ws.Cell(row, i + 1).Value = n;
                else ws.Cell(row, i + 1).Value = v == null ? "" : v.ToString();
            }
        }

        static string Money2(decimal v) { return v.ToString("0.00"); }
        static string DateOnly(string v) { return string.IsNullOrWhiteSpace(v) ? "" : (v.Length >= 10 ? v.Substring(0, 10) : v); }

        static void WriteSupplierSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "供应商管理", new[] { "供应商编号", "供应商名称", "联系人", "联系电话", "供应商品", "单位地址", "开户行", "银行账号", "开户行行号", "当前应付款", "状态", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadSuppliers())
                WriteRow(ws, r++, x.Code, x.Company, x.Contact, x.Phone, x.Goods, x.Address, x.Bank, x.Account, x.BankNo, Money2(x.Payable), x.Status, x.UpdatedAt, x.UpdatedBy);
        }

        static void WriteCustomerSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "客户管理", new[] { "客户编号", "客户名称", "联系人", "联系电话", "开户行", "银行账号", "开户行行号", "地址", "实时当前应收款", "状态", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadCustomers())
                WriteRow(ws, r++, x.Code, x.Company, x.Contact, x.Phone, x.Bank, x.Account, x.BankNo, x.Address, Money2(x.Receivable), x.Status, x.UpdatedAt, x.UpdatedBy);
        }

        static void WriteMaterialSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "物料管理", new[] { "物料编号", "供应商", "物料名称/规格", "数量/单位", "含税价", "不含税价", "价格类型", "备注", "状态", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadMaterials())
                WriteRow(ws, r++, x.Code, x.Supplier, x.NameSpec, x.QuantityUnit, Money2(x.TaxPrice), Money2(x.NoTaxPrice), ExportPriceTypeLabel(x.PriceType), x.Note, x.Status, x.UpdatedAt, x.UpdatedBy);
        }

        static void WriteBomSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "BOM表", BomCsvHeaders);
            int r = 2;
            foreach (var bom in LoadBom())
            {
                var items = bom.Items ?? new List<BomDetail>();
                if (items.Count == 0) { WriteBomExportRow(ws, ref r, bom, null, 0); continue; }
                for (int i = 0; i < items.Count; i++) WriteBomExportRow(ws, ref r, bom, items[i], i + 1);
            }
        }

        static void WriteBomExportRow(IXLWorksheet ws, ref int r, BomItem bom, BomDetail line, int lineNo)
        {
            if (line == null)
                WriteRow(ws, r++, bom.Code, bom.Version, bom.ProductName, bom.ModelCode, bom.ModelName, Money2(bom.TotalMaterialCost), bom.Note, bom.CreatedAt, bom.UpdatedAt, "", "", "", "", "", "", "", "", "", "", "", "", "");
            else
                WriteRow(ws, r++, bom.Code, bom.Version, bom.ProductName, bom.ModelCode, bom.ModelName, Money2(bom.TotalMaterialCost), bom.Note, bom.CreatedAt, bom.UpdatedAt,
                    lineNo, line.MaterialCode, line.MaterialName, line.Spec, line.Unit, line.Quantity, line.OriginalPrice, ExportPriceTypeLabel(line.PriceType), line.TaxRate, line.NoTaxPrice, Money2(line.Amount), line.PriceSourceTime, line.Note);
        }

        static void WriteModelCostSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "机型成本", ModelCostCsvHeaders);
            int r = 2;
            foreach (var x in LoadModelCosts())
                WriteRow(ws, r++, x.ModelCode, x.ModelName, x.ProductName, x.BomCode, x.BomVersion, Money2(x.MaterialCost), Money2(x.TotalCost), x.Note, string.IsNullOrWhiteSpace(x.Status) ? "启用" : x.Status, x.CreatedAt, x.UpdatedAt);
        }

        static void WriteSalesOrderSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "销售订单", new[] { "订单编号", "订单日期", "客户编号", "客户名称", "物料编号", "物料名称", "数量", "不含税销售单价", "含税销售单价", "不含税销售金额", "含税销售金额", "状态", "备注", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadSalesOrders())
                WriteRow(ws, r++, x.Code, DateOnly(x.OrderDate), x.CustomerCode, x.CustomerName, x.MaterialCode, x.MaterialName, x.Quantity, Money2(x.TaxExcludedSalePrice), Money2(x.TaxIncludedSalePrice), Money2(x.TaxExcludedSaleAmount), Money2(x.TaxIncludedSaleAmount), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy);
        }

        static void WriteSalesOutboundSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "销售出库", new[] { "出库编号", "出库日期", "销售订单号", "客户名称", "物料编号", "物料名称", "出库数量", "成本单价", "成本金额", "状态", "备注", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadSalesOutbounds())
                WriteRow(ws, r++, x.Code, DateOnly(x.OutboundDate), x.SalesOrderNo, x.CustomerName, x.MaterialCode, x.MaterialName, x.Quantity, Money2(x.CostPrice), Money2(x.CostAmount), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy);
        }

        static void WritePurchaseOrderSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "采购单", new[] { "采购编号", "订单日期", "供应商名称", "物料编号", "物料名称", "数量", "采购单价", "采购金额", "状态", "备注", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadPurchaseOrders())
                WriteRow(ws, r++, x.Code, DateOnly(x.OrderDate), x.SupplierName, x.MaterialCode, x.MaterialName, x.Quantity, Money2(x.UnitPrice), Money2(x.Amount), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy);
        }

        static void WritePurchaseInboundSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "采购入库", new[] { "入库编号", "入库日期", "采购单号", "供应商名称", "物料编号", "物料名称", "入库数量", "入库单价", "入库金额", "状态", "备注", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadPurchaseInbounds())
                WriteRow(ws, r++, x.Code, DateOnly(x.InboundDate), x.PurchaseNo, x.SupplierName, x.MaterialCode, x.MaterialName, x.Quantity, Money2(x.InboundPrice), Money2(x.Amount), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy);
        }

        static void WriteProductionPickSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "生产领用", new[] { "领用编号", "BOM名称", "物料编号", "物料名称", "领用数量", "成本单价", "成本金额", "领用日期", "状态", "备注", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadProductionPicks())
                WriteRow(ws, r++, x.Code, x.BomName, x.MaterialCode, x.MaterialName, x.Quantity, Money2(x.CostPrice), Money2(x.CostAmount), DateOnly(x.PickDate), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy);
        }

        static void WriteFinishedInboundSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "成品入库", new[] { "入库编号", "BOM编号", "产品名称", "入库数量", "单台成本", "入库金额", "入库日期", "状态", "备注", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadFinishedInbounds())
                WriteRow(ws, r++, x.Code, x.BomCode, x.ProductName, x.Quantity, Money2(x.UnitCost), Money2(x.Amount), DateOnly(x.InboundDate), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy);
        }

        static void WriteReceivableSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "应收款", new[] { "应收编号", "销售订单号", "客户名称", "应收金额", "已收金额", "未收金额", "到期日期", "状态", "备注", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadReceivables())
                WriteRow(ws, r++, x.Code, x.SalesOrderNo, x.CustomerName, Money2(x.ReceivableAmount), Money2(x.ReceivedAmount), Money2(x.UnreceivedAmount), DateOnly(x.DueDate), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy);
            ws.Cell(r, 1).Value = "说明：收款明细保存在 receivables.json 的 ReceiptDetails 数组；本表仅导出汇总金额，导入时按已收/未收汇总写入，不重复生成明细。";
        }

        static void WritePayableSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "应付款", new[] { "应付编号", "采购单号", "供应商名称", "应付金额", "已付金额", "未付金额", "到期日期", "状态", "备注", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadPayables())
                WriteRow(ws, r++, x.Code, x.PurchaseNo, x.SupplierName, Money2(x.PayableAmount), Money2(x.PaidAmount), Money2(x.UnpaidAmount), DateOnly(x.DueDate), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy);
            ws.Cell(r, 1).Value = "说明：付款明细保存在 payables.json 的 PaymentDetails 数组；本表仅导出汇总金额，导入时按已付/未付汇总写入，不重复生成明细。";
        }

        static void WriteFinanceSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "财务收支", new[] { "记录ID", "日期", "账户类型", "收款金额", "付款金额", "收付款方式", "收付款用途", "对方账户主体", "备注", "最后更新", "操作人" });
            int r = 2;
            foreach (var x in LoadFinance())
                WriteRow(ws, r++, x.Id, DateOnly(x.Date), x.AccountType, Money2(x.Receipt), Money2(x.Payment), x.PaymentMethod, x.Purpose, x.Counterparty, x.Note, x.UpdatedAt, x.UpdatedBy);
        }

        static void WriteFinanceOpeningSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "财务期初余额", new[] { "公户期初余额", "公司私户期初余额", "个人私户期初余额", "最后更新" });
            var o = LoadOpeningBalances();
            WriteRow(ws, 2, Money2(o.PublicAccount), Money2(o.CompanyPrivate), Money2(o.PersonalPrivate), o.UpdatedAt ?? "");
        }

        static void WriteStockReferenceSheet(XLWorkbook wb)
        {
            var ws = AddSheet(wb, "库存汇总", new[] { "类型", "编码", "名称", "规格", "单位", "仓库", "当前数量", "成本价", "库存金额" });
            int r = 2;
            foreach (var x in BuildStockItems())
                WriteRow(ws, r++, x.ItemType, x.ItemCode, x.ItemName, x.Spec, x.Unit, x.WarehouseName, x.CurrentQuantity, Money2(x.CostPrice), Money2(x.StockAmount));
            ws.Cell(r, 1).Value = "（仅供参考，不参与导入）";
        }

        static List<string> PreviewModuleErrors(string moduleKey, List<Dictionary<string, string>> rows)
        {
            var result = ImportModuleRows(moduleKey, rows, null, true);
            return (result.Errors ?? new string[0]).ToList();
        }

        static TestDataModuleResult ImportModuleRows(string moduleKey, List<Dictionary<string, string>> rows, UserSession user, bool previewOnly = false)
        {
            switch (moduleKey)
            {
                case "suppliers": return ImportSuppliersTest(rows, user, previewOnly);
                case "customers": return ImportCustomersTest(rows, user, previewOnly);
                case "materials": return ImportMaterialsTest(rows, user, previewOnly);
                case "boms": return ImportBomsTest(rows, user, previewOnly);
                case "modelCosts": return ImportModelCostsTest(rows, user, previewOnly);
                case "salesOrders": return ImportSalesOrdersTest(rows, user, previewOnly);
                case "salesOutbounds": return ImportSalesOutboundsTest(rows, user, previewOnly);
                case "purchaseOrders": return ImportPurchaseOrdersTest(rows, user, previewOnly);
                case "purchaseInbounds": return ImportPurchaseInboundsTest(rows, user, previewOnly);
                case "productionPicks": return ImportProductionPicksTest(rows, user, previewOnly);
                case "finishedInbounds": return ImportFinishedInboundsTest(rows, user, previewOnly);
                case "receivables": return ImportReceivablesTest(rows, user, previewOnly);
                case "payables": return ImportPayablesTest(rows, user, previewOnly);
                case "financeTransactions": return ImportFinanceTransactionsTest(rows, user, previewOnly);
                case "financeOpening": return ImportFinanceOpeningTest(rows, user, previewOnly);
                default: return new TestDataModuleResult { ModuleKey = moduleKey, ModuleLabel = moduleKey, Skipped = rows.Count };
            }
        }

        static TestDataModuleResult NewModuleResult(string key)
        {
            return new TestDataModuleResult { ModuleKey = key, ModuleLabel = TestDataModuleLabels.ContainsKey(key) ? TestDataModuleLabels[key] : key, Errors = new string[0] };
        }

        static void AddErr(TestDataModuleResult res, List<string> errors, int rowNo, string msg)
        {
            res.Failed++;
            if (errors.Count < 30) errors.Add("第" + rowNo + "行：" + msg);
        }

        static TestDataModuleResult ImportSuppliersTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("suppliers");
            var errors = new List<string>();
            bool changed = false;
            int rowNo = 1;
            Action<List<Supplier>> importLoop = list =>
            {
                foreach (var row in rows)
                {
                    rowNo++;
                    string code = Cell(row, "供应商编号"), company = Cell(row, "供应商名称", "供应商公司名");
                    if (Placeholder(company)) { res.Skipped++; continue; }
                    if (Money(Cell(row, "当前应付款")) < 0) { AddErr(res, errors, rowNo, "当前应付款不能为负数"); continue; }
                    var existing = list.FirstOrDefault(x => (!Placeholder(code) && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) || string.Equals(x.Company, company, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        if (previewOnly) { res.Updated++; continue; }
                        existing.Contact = Cell(row, "联系人"); existing.Phone = Cell(row, "联系电话"); existing.Goods = Cell(row, "供应商品");
                        existing.Address = Cell(row, "单位地址"); existing.Bank = Cell(row, "开户行"); existing.Account = Cell(row, "银行账号");
                        existing.BankNo = Cell(row, "开户行行号"); existing.Payable = Money(Cell(row, "当前应付款"));
                        if (!string.IsNullOrWhiteSpace(Cell(row, "状态"))) existing.Status = Cell(row, "状态");
                        existing.UpdatedAt = ProfileUpdatedAtNow(); existing.UpdatedBy = user.DisplayName;
                        res.Updated++; changed = true;
                    }
                    else
                    {
                        if (previewOnly) { res.Added++; continue; }
                        var item = new Supplier
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Code = Placeholder(code) ? NextCode(SupplierSequenceFile, "SRM", list.Select(x => x.Code), "GY") : code.Trim(),
                            Company = company, Contact = Cell(row, "联系人"), Phone = Cell(row, "联系电话"), Goods = Cell(row, "供应商品"),
                            Address = Cell(row, "单位地址"), Bank = Cell(row, "开户行"), Account = Cell(row, "银行账号"), BankNo = Cell(row, "开户行行号"),
                            Payable = Money(Cell(row, "当前应付款")), Status = string.IsNullOrWhiteSpace(Cell(row, "状态")) ? "启用" : Cell(row, "状态"),
                            UpdatedAt = ProfileUpdatedAtNow(), UpdatedBy = user.DisplayName
                        };
                        list.Insert(0, item); res.Added++; changed = true;
                    }
                }
            };
            if (previewOnly) importLoop(LoadSuppliers());
            else MutateJsonList<Supplier, object>(DataFile, "auto", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportCustomersTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("customers");
            var errors = new List<string>();
            bool changed = false;
            int rowNo = 1;
            Action<List<Customer>> importLoop = list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                string code = Cell(row, "客户编号"), company = Cell(row, "客户名称", "公司名");
                if (Placeholder(company)) { res.Skipped++; continue; }
                if (Money(Cell(row, "实时当前应收款")) < 0) { AddErr(res, errors, rowNo, "实时当前应收款不能为负数"); continue; }
                var existing = list.FirstOrDefault(x => (!Placeholder(code) && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) || string.Equals(x.Company, company, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (previewOnly) { res.Updated++; continue; }
                    existing.Contact = Cell(row, "联系人"); existing.Phone = Cell(row, "联系电话"); existing.Bank = Cell(row, "开户行");
                    existing.Account = Cell(row, "银行账号"); existing.BankNo = Cell(row, "开户行行号"); existing.Address = Cell(row, "地址");
                    existing.Receivable = Money(Cell(row, "实时当前应收款"));
                    if (!string.IsNullOrWhiteSpace(Cell(row, "状态"))) existing.Status = Cell(row, "状态");
                    existing.UpdatedAt = ProfileUpdatedAtNow(); existing.UpdatedBy = user.DisplayName;
                    res.Updated++; changed = true;
                }
                else
                {
                    if (previewOnly) { res.Added++; continue; }
                    var item = new Customer
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Code = Placeholder(code) ? NextCode(CustomerSequenceFile, "CRM", list.Select(x => x.Code), "KH") : code.Trim(),
                        Company = company, Contact = Cell(row, "联系人"), Phone = Cell(row, "联系电话"), Bank = Cell(row, "开户行"),
                        Account = Cell(row, "银行账号"), BankNo = Cell(row, "开户行行号"), Address = Cell(row, "地址"),
                        Receivable = Money(Cell(row, "实时当前应收款")), Status = string.IsNullOrWhiteSpace(Cell(row, "状态")) ? "启用" : Cell(row, "状态"),
                        UpdatedAt = ProfileUpdatedAtNow(), UpdatedBy = user.DisplayName
                    };
                    list.Insert(0, item); res.Added++; changed = true;
                }
            }
            };
            if (previewOnly) importLoop(LoadCustomers());
            else MutateJsonList<Customer, object>(CustomerFile, "customers", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportMaterialsTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("materials");
            var errors = new List<string>();
            var suppliers = LoadSuppliers();
            bool changed = false;
            int rowNo = 1;
            Action<List<Material>> importLoop = list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                string code = Cell(row, "物料编号"), supplier = Cell(row, "供应商"), name = Cell(row, "物料名称/规格", "物料名称");
                if (Placeholder(name)) { res.Skipped++; continue; }
                if (!string.IsNullOrWhiteSpace(supplier) && !suppliers.Any(x => string.Equals(x.Company, supplier, StringComparison.OrdinalIgnoreCase)))
                { AddErr(res, errors, rowNo, "供应商不存在，请先在供应商管理中添加"); continue; }
                if (Money(Cell(row, "含税价")) < 0 || Money(Cell(row, "不含税价")) < 0) { AddErr(res, errors, rowNo, "价格不能为负数"); continue; }
                var existing = list.FirstOrDefault(x => (!Placeholder(code) && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) || (string.Equals(x.NameSpec, name, StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(supplier) || string.Equals(x.Supplier, supplier, StringComparison.OrdinalIgnoreCase))));
                if (existing != null)
                {
                    if (previewOnly) { res.Updated++; continue; }
                    existing.Supplier = supplier; existing.NameSpec = name; existing.QuantityUnit = Cell(row, "数量/单位");
                    existing.TaxPrice = Money(Cell(row, "含税价")); existing.NoTaxPrice = Money(Cell(row, "不含税价"));
                    existing.PriceType = NormalizePriceType(Cell(row, "价格类型")); existing.Note = Cell(row, "备注");
                    if (!string.IsNullOrWhiteSpace(Cell(row, "状态"))) existing.Status = Cell(row, "状态");
                    existing.UpdatedAt = ProfileUpdatedAtNow(); existing.UpdatedBy = user.DisplayName;
                    res.Updated++; changed = true;
                }
                else
                {
                    if (previewOnly) { res.Added++; continue; }
                    var item = new Material
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Code = Placeholder(code) ? NextCode(MaterialSequenceFile, "MAT", list.Select(x => x.Code), "WL") : code.Trim(),
                        Supplier = supplier, NameSpec = name, QuantityUnit = Cell(row, "数量/单位"),
                        TaxPrice = Money(Cell(row, "含税价")), NoTaxPrice = Money(Cell(row, "不含税价")),
                        PriceType = NormalizePriceType(Cell(row, "价格类型")), Note = Cell(row, "备注"),
                        Status = string.IsNullOrWhiteSpace(Cell(row, "状态")) ? "启用" : Cell(row, "状态"),
                        UpdatedAt = ProfileUpdatedAtNow(), UpdatedBy = user.DisplayName
                    };
                    list.Insert(0, item); res.Added++; changed = true;
                }
            }
            };
            if (previewOnly) importLoop(LoadMaterials());
            else MutateJsonList<Material, object>(MaterialFile, "materials", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportBomsTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("boms");
            var errors = new List<string>();
            var warnings = new List<string>();
            var materials = LoadMaterials();
            decimal taxRate = LoadSystemSettings().TaxRate;
            var groups = rows.GroupBy(r => BomConflictKey(Cell(r, "BOM编号"), Cell(r, "BOM版本"))).Where(g => !string.IsNullOrWhiteSpace(g.Key)).ToList();
            bool changed = false;
            int firstRowNo = 1;
            Action<List<BomItem>> importLoop = list =>
            {
            foreach (var g in groups)
            {
                firstRowNo++;
                var groupRows = g.ToList();
                int failed = 0;
                var bom = BuildBomFromImportGroup(groupRows, taxRate, materials, warnings, errors, ref failed, firstRowNo);
                if (bom == null) { res.Failed += failed; continue; }
                var existing = list.FirstOrDefault(x => string.Equals(x.Code, bom.Code, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Version, bom.Version, StringComparison.OrdinalIgnoreCase));
                if (existing != null) { res.Skipped++; if (errors.Count < 30) errors.Add("BOM " + bom.Code + " 版本 " + bom.Version + " 已存在，已跳过"); continue; }
                if (previewOnly) { res.Added++; continue; }
                bom.Id = Guid.NewGuid().ToString("N"); bom.CreatedAt = NowTimeString(); bom.UpdatedAt = NowTimeString();
                RecalcBomLines(bom, taxRate);
                list.Insert(0, bom); res.Added++; changed = true;
            }
            };
            if (previewOnly) importLoop(LoadBom());
            else MutateJsonList<BomItem, object>(BomFile, "bom", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.Concat(warnings).Take(30).ToArray(); return res;
        }

        static TestDataModuleResult ImportModelCostsTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("modelCosts");
            var errors = new List<string>();
            var boms = LoadBom();
            bool changed = false;
            int rowNo = 1;
            Action<List<ModelCost>> importLoop = list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                string modelCode = Cell(row, "机型编号"), modelName = Cell(row, "机型名称"), bomCode = Cell(row, "BOM编号");
                if (Placeholder(modelCode) && Placeholder(modelName)) { res.Skipped++; continue; }
                if (Money(Cell(row, "材料成本")) < 0 || Money(Cell(row, "总成本")) < 0) { AddErr(res, errors, rowNo, "成本不能为负数"); continue; }
                var existing = list.FirstOrDefault(x => (!Placeholder(modelCode) && string.Equals(x.ModelCode, modelCode, StringComparison.OrdinalIgnoreCase)) || (!Placeholder(modelName) && string.Equals(x.ModelName, modelName, StringComparison.OrdinalIgnoreCase)));
                if (existing != null) { res.Skipped++; continue; }
                var bom = boms.FirstOrDefault(x => string.Equals(x.Code, bomCode, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(bomCode) || bom == null) { AddErr(res, errors, rowNo, "BOM不存在，请先在BOM表中创建"); continue; }
                if (previewOnly) { res.Added++; continue; }
                var item = new ModelCost
                {
                    Id = Guid.NewGuid().ToString("N"), ModelCode = modelCode, ModelName = modelName,
                    ProductName = Cell(row, "产品名称"), BomId = bom.Id, BomCode = bom.Code, BomVersion = Cell(row, "BOM版本", bom.Version),
                    MaterialCost = Money(Cell(row, "材料成本")), TotalCost = Money(Cell(row, "总成本", Cell(row, "材料成本"))),
                    Note = Cell(row, "备注"), Status = string.IsNullOrWhiteSpace(Cell(row, "状态")) ? "启用" : Cell(row, "状态"),
                    CreatedAt = NowTimeString(), UpdatedAt = NowTimeString()
                };
                list.Insert(0, item); res.Added++; changed = true;
            }
            };
            if (previewOnly) importLoop(LoadModelCosts());
            else MutateJsonList<ModelCost, object>(ModelCostFile, "model_costs", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportSalesOrdersTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("salesOrders");
            var errors = new List<string>();
            bool changed = false;
            int rowNo = 1;
            Action<List<SalesOrder>> importLoop = list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    string code = Cell(row, "订单编号", "销售单号");
                    if (!Placeholder(code) && list.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) { res.Skipped++; continue; }
                    var item = new SalesOrder
                    {
                        CustomerCode = Cell(row, "客户编号"), CustomerName = Cell(row, "客户名称"),
                        MaterialName = Cell(row, "物料名称", "产品名称"), Quantity = Money(Cell(row, "数量")),
                        TaxExcludedSalePrice = Money(Cell(row, "不含税销售单价", "销售单价", "单价")),
                        TaxIncludedSalePrice = Money(Cell(row, "含税销售单价")),
                        OrderDate = Cell(row, "订单日期", "销售日期"), Status = Cell(row, "状态"), Note = Cell(row, "备注"), Code = code
                    };
                    if (Placeholder(item.CustomerName) && Placeholder(item.CustomerCode)) { AddErr(res, errors, rowNo, "请选择客户"); continue; }
                    if (item.Quantity < 0) { AddErr(res, errors, rowNo, "数量不能为负数"); continue; }
                    ApplySalesOrder(item);
                    if (previewOnly) { res.Added++; continue; }
                    item.Id = Guid.NewGuid().ToString("N");
                    if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(SalesOrderSequenceFile, "SO", list.Select(x => x.Code), "XSDD");
                    item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName;
                    list.Insert(0, item); res.Added++; changed = true;
                }
                catch (Exception ex) { AddErr(res, errors, rowNo, ex.Message); }
            }
            };
            if (previewOnly) importLoop(LoadSalesOrders());
            else MutateJsonList<SalesOrder, object>(SalesOrdersFile, "sales_orders", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportSalesOutboundsTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("salesOutbounds");
            var errors = new List<string>();
            bool changed = false;
            int rowNo = 1;
            Action<List<SalesOutbound>> importLoop = list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    string code = Cell(row, "出库编号", "出库单号");
                    if (!Placeholder(code) && list.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) { res.Skipped++; continue; }
                    string orderNo = Cell(row, "销售订单号", "关联销售单号");
                    if (Placeholder(orderNo)) { AddErr(res, errors, rowNo, "请选择来源销售订单"); continue; }
                    var order = LoadSalesOrders().FirstOrDefault(x => string.Equals(x.Code, orderNo, StringComparison.OrdinalIgnoreCase));
                    if (order == null) { AddErr(res, errors, rowNo, "来源销售订单不存在"); continue; }
                    var item = new SalesOutbound
                    {
                        SalesOrderId = order.Id, SalesOrderNo = order.Code,
                        CustomerName = Cell(row, "客户名称"), MaterialName = Cell(row, "物料名称", "产品名称"),
                        Quantity = Money(Cell(row, "出库数量", "数量")), CostPrice = Money(Cell(row, "成本单价", "单价")),
                        OutboundDate = Cell(row, "出库日期"), Status = Cell(row, "状态"), Note = Cell(row, "备注"), Code = code
                    };
                    if (item.Quantity < 0 || item.CostPrice < 0) { AddErr(res, errors, rowNo, "数量/单价不能为负数"); continue; }
                    ApplySalesOutbound(item);
                    if (previewOnly) { res.Added++; continue; }
                    item.Id = Guid.NewGuid().ToString("N");
                    if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(SalesOutboundSequenceFile, "SOUT", list.Select(x => x.Code), "XSCK");
                    item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName;
                    list.Insert(0, item); res.Added++; changed = true;
                }
                catch (Exception ex) { AddErr(res, errors, rowNo, ex.Message); }
            }
            };
            if (previewOnly) importLoop(LoadSalesOutbounds());
            else MutateJsonList<SalesOutbound, object>(SalesOutboundsFile, "sales_outbounds", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportPurchaseOrdersTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("purchaseOrders");
            var errors = new List<string>();
            var suppliers = LoadSuppliers();
            bool changed = false;
            int rowNo = 1;
            Action<List<PurchaseOrder>> importLoop = list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    string code = Cell(row, "采购编号", "采购单号");
                    if (!Placeholder(code) && list.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) { res.Skipped++; continue; }
                    string supplierName = Cell(row, "供应商名称");
                    if (!Placeholder(supplierName) && !suppliers.Any(x => string.Equals(x.Company, supplierName, StringComparison.OrdinalIgnoreCase)))
                    { AddErr(res, errors, rowNo, "供应商不存在，请先在供应商管理中添加"); continue; }
                    var item = new PurchaseOrder
                    {
                        SupplierName = supplierName, MaterialName = Cell(row, "物料名称"), Quantity = Money(Cell(row, "数量")),
                        UnitPrice = Money(Cell(row, "采购单价", "单价")), OrderDate = Cell(row, "订单日期", "采购日期"),
                        Status = Cell(row, "状态"), Note = Cell(row, "备注"), Code = code
                    };
                    if (item.Quantity < 0 || item.UnitPrice < 0) { AddErr(res, errors, rowNo, "数量/单价不能为负数"); continue; }
                    ApplyPurchaseOrder(item);
                    if (previewOnly) { res.Added++; continue; }
                    item.Id = Guid.NewGuid().ToString("N");
                    if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(PurchaseOrderSequenceFile, "PO", list.Select(x => x.Code), "CGDD");
                    item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName;
                    list.Insert(0, item); res.Added++; changed = true;
                }
                catch (Exception ex) { AddErr(res, errors, rowNo, ex.Message); }
            }
            };
            if (previewOnly) importLoop(LoadPurchaseOrders());
            else MutateJsonList<PurchaseOrder, object>(PurchaseOrdersFile, "purchase_orders", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportPurchaseInboundsTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("purchaseInbounds");
            var errors = new List<string>();
            bool changed = false;
            int rowNo = 1;
            Action<List<PurchaseInbound>> importLoop = list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    string code = Cell(row, "入库编号", "入库单号");
                    if (!Placeholder(code) && list.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) { res.Skipped++; continue; }
                    string poNo = Cell(row, "采购单号", "关联采购单号");
                    if (Placeholder(poNo)) { AddErr(res, errors, rowNo, "请选择来源采购单"); continue; }
                    var po = LoadPurchaseOrders().FirstOrDefault(x => string.Equals(x.Code, poNo, StringComparison.OrdinalIgnoreCase));
                    if (po == null) { AddErr(res, errors, rowNo, "来源采购单不存在"); continue; }
                    var item = new PurchaseInbound
                    {
                        PurchaseOrderId = po.Id, PurchaseNo = po.Code, SupplierName = Cell(row, "供应商名称", po.SupplierName),
                        MaterialName = Cell(row, "物料名称"), Quantity = Money(Cell(row, "入库数量", "数量")),
                        InboundPrice = Money(Cell(row, "入库单价", "单价")), InboundDate = Cell(row, "入库日期"),
                        Status = Cell(row, "状态"), Note = Cell(row, "备注"), Code = code
                    };
                    if (item.Quantity < 0 || item.InboundPrice < 0) { AddErr(res, errors, rowNo, "数量/单价不能为负数"); continue; }
                    ApplyPurchaseInbound(item);
                    if (previewOnly) { res.Added++; continue; }
                    item.Id = Guid.NewGuid().ToString("N");
                    if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(PurchaseInboundSequenceFile, "PIN", list.Select(x => x.Code), "CGRK");
                    item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName;
                    list.Insert(0, item); res.Added++; changed = true;
                }
                catch (Exception ex) { AddErr(res, errors, rowNo, ex.Message); }
            }
            };
            if (previewOnly) importLoop(LoadPurchaseInbounds());
            else MutateJsonList<PurchaseInbound, object>(PurchaseInboundsFile, "purchase_inbounds", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportProductionPicksTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("productionPicks");
            var errors = new List<string>();
            bool changed = false;
            int rowNo = 1;
            Action<List<ProductionPick>> importLoop = list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    string code = Cell(row, "领用编号");
                    if (!Placeholder(code) && list.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) { res.Skipped++; continue; }
                    string matCode = Cell(row, "物料编号");
                    var mat = LoadMaterials().FirstOrDefault(x => string.Equals(x.Code, matCode, StringComparison.OrdinalIgnoreCase));
                    var item = new ProductionPick
                    {
                        BomName = Cell(row, "BOM名称"), MaterialId = mat != null ? mat.Id : "", MaterialCode = matCode,
                        MaterialName = Cell(row, "物料名称"), Quantity = Money(Cell(row, "领用数量", "数量")),
                        CostPrice = Money(Cell(row, "成本单价", "单价")), PickDate = Cell(row, "领用日期"),
                        Status = Cell(row, "状态"), Note = Cell(row, "备注"), Code = code
                    };
                    if (string.IsNullOrWhiteSpace(item.MaterialName) && mat == null) { AddErr(res, errors, rowNo, "物料不存在"); continue; }
                    if (item.Quantity < 0 || item.CostPrice < 0) { AddErr(res, errors, rowNo, "数量/单价不能为负数"); continue; }
                    ApplyProductionPick(item);
                    if (previewOnly) { res.Added++; continue; }
                    item.Id = Guid.NewGuid().ToString("N");
                    if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(ProductionPickSequenceFile, "PL", list.Select(x => x.Code), "SCLL");
                    item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName;
                    list.Insert(0, item); res.Added++; changed = true;
                }
                catch (Exception ex) { AddErr(res, errors, rowNo, ex.Message); }
            }
            };
            if (previewOnly) importLoop(LoadProductionPicks());
            else MutateJsonList<ProductionPick, object>(ProductionPicksFile, "production_picks", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportFinishedInboundsTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("finishedInbounds");
            var errors = new List<string>();
            bool changed = false;
            int rowNo = 1;
            Action<List<FinishedInbound>> importLoop = list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    string code = Cell(row, "入库编号");
                    if (!Placeholder(code) && list.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) { res.Skipped++; continue; }
                    string bomCode = Cell(row, "BOM编号");
                    var bom = LoadBom().FirstOrDefault(x => string.Equals(x.Code, bomCode, StringComparison.OrdinalIgnoreCase));
                    var item = new FinishedInbound
                    {
                        BomId = bom != null ? bom.Id : "", BomCode = bomCode, ProductName = Cell(row, "产品名称"),
                        Quantity = Money(Cell(row, "入库数量", "数量")), UnitCost = Money(Cell(row, "单台成本", "成本单价")),
                        InboundDate = Cell(row, "入库日期"), Status = Cell(row, "状态"), Note = Cell(row, "备注"), Code = code
                    };
                    if (Placeholder(item.ProductName)) { AddErr(res, errors, rowNo, "请填写产品名称"); continue; }
                    if (item.Quantity < 0 || item.UnitCost < 0) { AddErr(res, errors, rowNo, "数量/成本不能为负数"); continue; }
                    ApplyFinishedInbound(item);
                    if (previewOnly) { res.Added++; continue; }
                    item.Id = Guid.NewGuid().ToString("N");
                    if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(FinishedInboundSequenceFile, "FGI", list.Select(x => x.Code), "CPRK");
                    item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName;
                    list.Insert(0, item); res.Added++; changed = true;
                }
                catch (Exception ex) { AddErr(res, errors, rowNo, ex.Message); }
            }
            };
            if (previewOnly) importLoop(LoadFinishedInbounds());
            else MutateJsonList<FinishedInbound, object>(FinishedInboundsFile, "finished_inbounds", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportReceivablesTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("receivables");
            var errors = new List<string>();
            bool changed = false;
            int rowNo = 1;
            Action<List<Receivable>> importLoop = list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    string code = Cell(row, "应收编号");
                    if (!Placeholder(code) && list.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) { res.Skipped++; continue; }
                    string orderNo = Cell(row, "销售订单号");
                    if (Placeholder(orderNo)) { AddErr(res, errors, rowNo, "来源销售订单不能为空"); continue; }
                    var order = LoadSalesOrders().FirstOrDefault(x => string.Equals(x.Code, orderNo, StringComparison.OrdinalIgnoreCase));
                    if (order == null) { AddErr(res, errors, rowNo, "来源销售订单不存在"); continue; }
                    var item = new Receivable
                    {
                        SalesOrderId = order.Id, SalesOrderNo = order.Code, CustomerName = Cell(row, "客户名称", order.CustomerName),
                        ReceivableAmount = Money(Cell(row, "应收金额")), ReceivedAmount = Money(Cell(row, "已收金额")),
                        DueDate = Cell(row, "到期日期"), Note = Cell(row, "备注"), Code = code
                    };
                    if (item.ReceivableAmount < 0 || item.ReceivedAmount < 0) { AddErr(res, errors, rowNo, "金额不能为负数"); continue; }
                    ApplyReceivable(item);
                    if (previewOnly) { res.Added++; continue; }
                    item.Id = Guid.NewGuid().ToString("N");
                    if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(ReceivableSequenceFile, "AR", list.Select(x => x.Code), "YS");
                    item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName;
                    list.Insert(0, item); res.Added++; changed = true;
                }
                catch (Exception ex) { AddErr(res, errors, rowNo, ex.Message); }
            }
            };
            if (previewOnly) importLoop(LoadReceivables());
            else MutateJsonList<Receivable, object>(ReceivablesFile, "receivables", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportPayablesTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("payables");
            var errors = new List<string>();
            bool changed = false;
            int rowNo = 1;
            Action<List<Payable>> importLoop = list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    string code = Cell(row, "应付编号");
                    if (!Placeholder(code) && list.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) { res.Skipped++; continue; }
                    string poNo = Cell(row, "采购单号");
                    if (Placeholder(poNo)) { AddErr(res, errors, rowNo, "来源采购单不能为空"); continue; }
                    var po = LoadPurchaseOrders().FirstOrDefault(x => string.Equals(x.Code, poNo, StringComparison.OrdinalIgnoreCase));
                    if (po == null) { AddErr(res, errors, rowNo, "来源采购单不存在"); continue; }
                    var item = new Payable
                    {
                        PurchaseOrderId = po.Id, PurchaseNo = po.Code, SupplierName = Cell(row, "供应商名称", po.SupplierName),
                        PayableAmount = Money(Cell(row, "应付金额")), PaidAmount = Money(Cell(row, "已付金额")),
                        DueDate = Cell(row, "到期日期"), Note = Cell(row, "备注"), Code = code
                    };
                    if (item.PayableAmount < 0 || item.PaidAmount < 0) { AddErr(res, errors, rowNo, "金额不能为负数"); continue; }
                    ApplyPayable(item);
                    if (previewOnly) { res.Added++; continue; }
                    item.Id = Guid.NewGuid().ToString("N");
                    if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(PayableSequenceFile, "AP", list.Select(x => x.Code), "YF");
                    item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName;
                    list.Insert(0, item); res.Added++; changed = true;
                }
                catch (Exception ex) { AddErr(res, errors, rowNo, ex.Message); }
            }
            };
            if (previewOnly) importLoop(LoadPayables());
            else MutateJsonList<Payable, object>(PayablesFile, "payables", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportFinanceTransactionsTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("financeTransactions");
            var errors = new List<string>();
            bool changed = false;
            int rowNo = 1;
            Action<List<FinanceTransaction>> importLoop = list =>
            {
                foreach (var row in rows)
                {
                    rowNo++;
                    try
                    {
                        string id = Cell(row, "记录ID", "ID");
                        var item = new FinanceTransaction
                        {
                            Date = Cell(row, "日期"),
                            AccountType = Cell(row, "账户类型"),
                            Receipt = Money(Cell(row, "收款金额")),
                            Payment = Money(Cell(row, "付款金额")),
                            PaymentMethod = Cell(row, "收付款方式"),
                            Purpose = Cell(row, "收付款用途"),
                            Counterparty = Cell(row, "对方账户主体"),
                            Note = Cell(row, "备注")
                        };
                        ValidateFinance(item);
                        var existing = !Placeholder(id) ? list.FirstOrDefault(x => string.Equals(x.Id, id.Trim(), StringComparison.OrdinalIgnoreCase)) : null;
                        if (existing != null)
                        {
                            if (previewOnly) { res.Updated++; continue; }
                            existing.Date = item.Date; existing.AccountType = item.AccountType; existing.Receipt = item.Receipt; existing.Payment = item.Payment;
                            existing.PaymentMethod = item.PaymentMethod; existing.Purpose = item.Purpose; existing.Counterparty = item.Counterparty; existing.Note = item.Note;
                            existing.UpdatedAt = ProfileUpdatedAtNow(); existing.UpdatedBy = user.DisplayName;
                            res.Updated++; changed = true;
                        }
                        else
                        {
                            if (previewOnly) { res.Added++; continue; }
                            item.Id = Placeholder(id) ? Guid.NewGuid().ToString("N") : id.Trim();
                            item.UpdatedAt = ProfileUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                            list.Add(item); res.Added++; changed = true;
                        }
                    }
                    catch (Exception ex) { AddErr(res, errors, rowNo, ex.Message); }
                }
            };
            if (previewOnly) importLoop(LoadFinance());
            else MutateJsonList<FinanceTransaction, object>(FinanceFile, "finance", list => { importLoop(list); return new JsonMutationResult<object>(null, changed); });
            res.Errors = errors.ToArray(); return res;
        }

        static TestDataModuleResult ImportFinanceOpeningTest(List<Dictionary<string, string>> rows, UserSession user, bool previewOnly)
        {
            var res = NewModuleResult("financeOpening");
            var errors = new List<string>();
            var row = rows.FirstOrDefault(x => !string.IsNullOrWhiteSpace(Cell(x, "公户期初余额")) || !string.IsNullOrWhiteSpace(Cell(x, "公司私户期初余额")) || !string.IsNullOrWhiteSpace(Cell(x, "个人私户期初余额")));
            if (row == null) { res.Skipped = rows.Count; res.Errors = errors.ToArray(); return res; }
            try
            {
                var value = new OpeningBalances
                {
                    PublicAccount = Money(Cell(row, "公户期初余额")),
                    CompanyPrivate = Money(Cell(row, "公司私户期初余额")),
                    PersonalPrivate = Money(Cell(row, "个人私户期初余额"))
                };
                if (previewOnly) { res.Updated = 1; res.Errors = errors.ToArray(); return res; }
                RunUnderDataLock(() =>
                {
                    value.UpdatedAt = ProfileUpdatedAtNow();
                    if (File.Exists(OpeningFile)) File.Copy(OpeningFile, Path.Combine(BackupDir, "opening_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json"), true);
                    File.WriteAllText(OpeningFile, Json.Serialize(value), new UTF8Encoding(false));
                    CleanBackups();
                });
                res.Updated = 1;
            }
            catch (Exception ex) { AddErr(res, errors, 2, ex.Message); }
            res.Errors = errors.ToArray(); return res;
        }
    }
}
