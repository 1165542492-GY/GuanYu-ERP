using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace SupplierErpApp
{
    public static partial class Program
    {
        public class BusinessDashboardResult
        {
            public decimal SalesTotal { get; set; }
            public decimal PurchaseTotal { get; set; }
            public decimal ReceivableTotal { get; set; }
            public decimal ReceivedTotal { get; set; }
            public decimal ReceivableBalance { get; set; }
            public decimal PayableTotal { get; set; }
            public decimal PaidTotal { get; set; }
            public decimal PayableBalance { get; set; }
            public decimal InventoryQuantityTotal { get; set; }
            public decimal InventoryAmountTotal { get; set; }
            public decimal StockAmount { get; set; }
            public decimal OperatingNetAsset { get; set; }
            public decimal FinanceIncomeTotal { get; set; }
            public decimal FinanceExpenseTotal { get; set; }
            public decimal FinanceNetTotal { get; set; }
            public string UpdatedAt { get; set; }
            public string Range { get; set; }
        }

        public class DashboardSummaryCard
        {
            public string Key { get; set; }
            public string Label { get; set; }
            public decimal Value { get; set; }
            public string Hint { get; set; }
        }

        public class DashboardWarningCard
        {
            public string Key { get; set; }
            public string Label { get; set; }
            public int Count { get; set; }
        }

        public class DashboardReceivableTopItem
        {
            public string CustomerCode { get; set; }
            public string CustomerName { get; set; }
            public decimal ReceivableTotal { get; set; }
            public decimal ReceivedAmount { get; set; }
            public decimal UnreceivedAmount { get; set; }
        }

        public class DashboardPayableTopItem
        {
            public string SupplierCode { get; set; }
            public string SupplierName { get; set; }
            public decimal PayableTotal { get; set; }
            public decimal PaidAmount { get; set; }
            public decimal UnpaidAmount { get; set; }
        }

        public class DashboardInventoryTopItem
        {
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public string ItemType { get; set; }
            public decimal Quantity { get; set; }
            public string Unit { get; set; }
            public decimal StockValue { get; set; }
            public bool SortByAmount { get; set; }
        }

        public class DashboardInventoryWarningItem
        {
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public string ItemType { get; set; }
            public decimal Quantity { get; set; }
            public string Unit { get; set; }
            public string Reason { get; set; }
        }

        public class DashboardFinanceSummaryItem
        {
            public string Date { get; set; }
            public string AccountType { get; set; }
            public decimal Receipt { get; set; }
            public decimal Payment { get; set; }
            public string Purpose { get; set; }
        }

        public class OwnerDashboardSummaryResult
        {
            public DashboardSummaryCard[] SummaryCards { get; set; }
            public DashboardWarningCard[] WarningCards { get; set; }
            public DashboardReceivableTopItem[] ReceivableTop { get; set; }
            public DashboardPayableTopItem[] PayableTop { get; set; }
            public DashboardInventoryTopItem[] InventoryTop { get; set; }
            public DashboardInventoryWarningItem[] InventoryWarnings { get; set; }
            public DashboardFinanceSummaryItem[] FinanceSummary { get; set; }
            public string InventoryTopTitle { get; set; }
            public string UpdatedAt { get; set; }
            public string Range { get; set; }
        }

        static bool CanAccessBusinessDashboard(UserSession user)
        {
            if (user == null) return false;
            if (IsAdminUser(user)) return true;
            return HasPermission(user, "customer.view")
                || HasPermission(user, "supplier.view")
                || HasPermission(user, "sales_order.view")
                || HasPermission(user, "purchase_order.view")
                || HasPermission(user, "receivable.view")
                || HasPermission(user, "payable.view")
                || HasPermission(user, "stock_summary.view")
                || HasPermission(user, "finance.view");
        }

        static bool CanViewReceivableDashboard(UserSession user)
        {
            return IsAdminUser(user) || HasPermission(user, "receivable.view") || HasPermission(user, "customer.view");
        }

        static bool CanViewPayableDashboard(UserSession user)
        {
            return IsAdminUser(user) || HasPermission(user, "payable.view") || HasPermission(user, "supplier.view");
        }

        static string NormalizeDashboardRange(string range)
        {
            range = (range ?? "").Trim().ToLowerInvariant();
            if (range == "today" || range == "month" || range == "year" || range == "all") return range;
            return "all";
        }

        static bool DashboardDateInRange(string dateText, string range)
        {
            if (range == "all") return true;
            DateTime date;
            if (!TryParseFinanceDate(dateText, out date)) return false;
            DateTime today = DateTime.Today;
            if (range == "today") return date.Date == today;
            if (range == "month") return date.Year == today.Year && date.Month == today.Month;
            if (range == "year") return date.Year == today.Year;
            return true;
        }

        static decimal SalesOrderDashboardAmount(SalesOrder order)
        {
            if (order == null) return 0;
            if (order.TaxIncludedSaleAmount > 0) return order.TaxIncludedSaleAmount;
            if (order.TaxExcludedSaleAmount > 0) return order.TaxExcludedSaleAmount;
            return order.Amount;
        }

        static bool IsCancelledStatus(string status)
        {
            return string.Equals(status ?? "", "已取消", StringComparison.OrdinalIgnoreCase);
        }

        static BusinessDashboardResult BuildBusinessDashboard(string range)
        {
            range = NormalizeDashboardRange(range);
            decimal salesTotal = 0;
            foreach (var x in LoadSalesOrders())
            {
                if (!IsConfirmedStatus(x.Status)) continue;
                if (!DashboardDateInRange(x.OrderDate, range)) continue;
                salesTotal += SalesOrderDashboardAmount(x);
            }

            decimal purchaseTotal = 0;
            foreach (var x in LoadPurchaseOrders())
            {
                if (!IsConfirmedStatus(x.Status)) continue;
                if (!DashboardDateInRange(x.OrderDate, range)) continue;
                purchaseTotal += x.Amount;
            }

            decimal receivableTotal = 0;
            decimal receivedTotal = 0;
            foreach (var x in LoadReceivables())
            {
                receivableTotal += x.ReceivableAmount;
                receivedTotal += x.ReceivedAmount;
            }

            decimal payableTotal = 0;
            decimal paidTotal = 0;
            foreach (var x in LoadPayables())
            {
                payableTotal += x.PayableAmount;
                paidTotal += x.PaidAmount;
            }

            var stockSummary = BuildStockSummary();
            decimal inventoryQty = stockSummary.TotalQuantity;
            decimal inventoryAmount = RoundMoney(stockSummary.Items.Sum(x => x.StockAmount));
            decimal receivableBalance = RoundMoney(receivableTotal - receivedTotal);
            decimal payableBalance = RoundMoney(payableTotal - paidTotal);
            decimal operatingNetAsset = RoundMoney(receivableBalance + inventoryAmount - payableBalance);

            decimal financeIncome = 0;
            decimal financeExpense = 0;
            foreach (var x in LoadFinance())
            {
                if (!DashboardDateInRange(x.Date, range)) continue;
                if (x.Receipt > 0) financeIncome += x.Receipt;
                if (x.Payment > 0) financeExpense += x.Payment;
            }

            return new BusinessDashboardResult
            {
                SalesTotal = RoundMoney(salesTotal),
                PurchaseTotal = RoundMoney(purchaseTotal),
                ReceivableTotal = RoundMoney(receivableTotal),
                ReceivedTotal = RoundMoney(receivedTotal),
                ReceivableBalance = receivableBalance,
                PayableTotal = RoundMoney(payableTotal),
                PaidTotal = RoundMoney(paidTotal),
                PayableBalance = payableBalance,
                InventoryQuantityTotal = inventoryQty,
                InventoryAmountTotal = inventoryAmount,
                StockAmount = inventoryAmount,
                OperatingNetAsset = operatingNetAsset,
                FinanceIncomeTotal = RoundMoney(financeIncome),
                FinanceExpenseTotal = RoundMoney(financeExpense),
                FinanceNetTotal = RoundMoney(financeIncome - financeExpense),
                UpdatedAt = BizUpdatedAtNow(),
                Range = range
            };
        }

        static DashboardSummaryCard[] BuildSummaryCards(BusinessDashboardResult biz)
        {
            biz = biz ?? new BusinessDashboardResult();
            return new[]
            {
                new DashboardSummaryCard { Key = "sales", Label = "销售总额", Value = biz.SalesTotal, Hint = "销售订单金额合计" },
                new DashboardSummaryCard { Key = "received", Label = "已收金额", Value = biz.ReceivedTotal, Hint = "应收款已收金额合计" },
                new DashboardSummaryCard { Key = "receivable", Label = "应收余额", Value = biz.ReceivableBalance, Hint = "应收款未收金额合计" },
                new DashboardSummaryCard { Key = "purchase", Label = "采购总额", Value = biz.PurchaseTotal, Hint = "采购单金额合计" },
                new DashboardSummaryCard { Key = "paid", Label = "已付金额", Value = biz.PaidTotal, Hint = "应付款已付金额合计" },
                new DashboardSummaryCard { Key = "payable", Label = "应付余额", Value = biz.PayableBalance, Hint = "应付款未付金额合计" },
                new DashboardSummaryCard { Key = "stockAmount", Label = "库存金额", Value = biz.StockAmount, Hint = "库存汇总金额合计" },
                new DashboardSummaryCard { Key = "operatingNetAsset", Label = "经营净资产", Value = biz.OperatingNetAsset, Hint = "应收余额 + 库存金额 - 应付余额" }
            };
        }

        static string LookupCustomerCode(string customerName, Dictionary<string, string> codeByName)
        {
            customerName = (customerName ?? "").Trim();
            if (string.IsNullOrEmpty(customerName)) return "-";
            string code;
            if (codeByName != null && codeByName.TryGetValue(customerName, out code) && !string.IsNullOrWhiteSpace(code))
                return code;
            return "-";
        }

        static string LookupSupplierCode(string supplierName, Dictionary<string, string> codeByName)
        {
            supplierName = (supplierName ?? "").Trim();
            if (string.IsNullOrEmpty(supplierName)) return "-";
            string code;
            if (codeByName != null && codeByName.TryGetValue(supplierName, out code) && !string.IsNullOrWhiteSpace(code))
                return code;
            return "-";
        }

        static Dictionary<string, string> BuildCustomerCodeMap()
        {
            return LoadCustomers()
                .Where(x => !string.IsNullOrWhiteSpace(x.Company))
                .GroupBy(x => x.Company.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Code ?? "-", StringComparer.OrdinalIgnoreCase);
        }

        static Dictionary<string, string> BuildSupplierCodeMap()
        {
            return LoadSuppliers()
                .Where(x => !string.IsNullOrWhiteSpace(x.Company))
                .GroupBy(x => x.Company.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Code ?? "-", StringComparer.OrdinalIgnoreCase);
        }

        // 当前数据模型无「已完成」状态：已确认销售单若已确认出库数量小于订单数量，或仍为草稿，则计为未出库。
        static int CountPendingSalesOrders()
        {
            var outbounds = LoadSalesOutbounds().Where(x => IsConfirmedStatus(x.Status)).ToList();
            int count = 0;
            foreach (var order in LoadSalesOrders())
            {
                if (IsCancelledStatus(order.Status)) continue;
                if (!IsConfirmedStatus(order.Status))
                {
                    if (string.Equals(order.Status ?? "", "草稿", StringComparison.OrdinalIgnoreCase))
                        count++;
                    continue;
                }
                decimal outboundQty = outbounds.Where(x => x.SalesOrderId == order.Id).Sum(x => x.Quantity);
                if (outboundQty + 0.0001m < order.Quantity)
                    count++;
            }
            return count;
        }

        // 当前数据模型无「已完成」状态：已确认采购单若已确认入库数量小于订单数量，或仍为草稿，则计为未入库。
        static int CountPendingPurchaseOrders()
        {
            var inbounds = LoadPurchaseInbounds().Where(x => IsConfirmedStatus(x.Status)).ToList();
            int count = 0;
            foreach (var order in LoadPurchaseOrders())
            {
                if (IsCancelledStatus(order.Status)) continue;
                if (!IsConfirmedStatus(order.Status))
                {
                    if (string.Equals(order.Status ?? "", "草稿", StringComparison.OrdinalIgnoreCase))
                        count++;
                    continue;
                }
                decimal inboundQty = inbounds.Where(x => x.PurchaseOrderId == order.Id).Sum(x => x.Quantity);
                if (inboundQty + 0.0001m < order.Quantity)
                    count++;
            }
            return count;
        }

        static OwnerDashboardSummaryResult BuildOwnerDashboardSummary(UserSession user, string range)
        {
            range = NormalizeDashboardRange(range);
            var biz = BuildBusinessDashboard(range);
            var customerCodes = BuildCustomerCodeMap();
            var supplierCodes = BuildSupplierCodeMap();
            var stockItems = BuildStockItems();

            var warningCards = new List<DashboardWarningCard>();
            var receivableTop = new DashboardReceivableTopItem[0];
            var payableTop = new DashboardPayableTopItem[0];
            var inventoryTop = new DashboardInventoryTopItem[0];
            var inventoryWarnings = new DashboardInventoryWarningItem[0];
            var financeSummary = new DashboardFinanceSummaryItem[0];
            string inventoryTopTitle = "库存价值 Top10";

            if (CanViewReceivableDashboard(user))
            {
                var receivableGroups = LoadReceivables()
                    .GroupBy(x => (x.CustomerName ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        decimal total = RoundMoney(g.Sum(x => x.ReceivableAmount));
                        decimal received = RoundMoney(g.Sum(x => x.ReceivedAmount));
                        decimal unreceived = RoundMoney(total - received);
                        return new DashboardReceivableTopItem
                        {
                            CustomerCode = LookupCustomerCode(g.Key, customerCodes),
                            CustomerName = string.IsNullOrWhiteSpace(g.Key) ? "-" : g.Key,
                            ReceivableTotal = total,
                            ReceivedAmount = received,
                            UnreceivedAmount = unreceived
                        };
                    })
                    .Where(x => x.UnreceivedAmount > 0)
                    .OrderByDescending(x => x.UnreceivedAmount)
                    .Take(10)
                    .ToArray();
                receivableTop = receivableGroups;

                int pendingReceivableCustomers = LoadReceivables()
                    .GroupBy(x => (x.CustomerName ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                    .Count(g => RoundMoney(g.Sum(x => x.ReceivableAmount - x.ReceivedAmount)) > 0);
                warningCards.Add(new DashboardWarningCard { Key = "receivableCustomers", Label = "待收款客户数", Count = pendingReceivableCustomers });
            }
            else
            {
                warningCards.Add(new DashboardWarningCard { Key = "receivableCustomers", Label = "待收款客户数", Count = 0 });
            }

            if (CanViewPayableDashboard(user))
            {
                payableTop = LoadPayables()
                    .GroupBy(x => (x.SupplierName ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        decimal total = RoundMoney(g.Sum(x => x.PayableAmount));
                        decimal paid = RoundMoney(g.Sum(x => x.PaidAmount));
                        decimal unpaid = RoundMoney(total - paid);
                        return new DashboardPayableTopItem
                        {
                            SupplierCode = LookupSupplierCode(g.Key, supplierCodes),
                            SupplierName = string.IsNullOrWhiteSpace(g.Key) ? "-" : g.Key,
                            PayableTotal = total,
                            PaidAmount = paid,
                            UnpaidAmount = unpaid
                        };
                    })
                    .Where(x => x.UnpaidAmount > 0)
                    .OrderByDescending(x => x.UnpaidAmount)
                    .Take(10)
                    .ToArray();

                int pendingPayableSuppliers = LoadPayables()
                    .GroupBy(x => (x.SupplierName ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                    .Count(g => RoundMoney(g.Sum(x => x.PayableAmount - x.PaidAmount)) > 0);
                warningCards.Add(new DashboardWarningCard { Key = "payableSuppliers", Label = "待付款供应商数", Count = pendingPayableSuppliers });
            }
            else
            {
                warningCards.Add(new DashboardWarningCard { Key = "payableSuppliers", Label = "待付款供应商数", Count = 0 });
            }

            if (IsAdminUser(user) || HasPermission(user, "sales_order.view"))
                warningCards.Add(new DashboardWarningCard { Key = "pendingSalesOrders", Label = "未出库销售订单数", Count = CountPendingSalesOrders() });
            else
                warningCards.Add(new DashboardWarningCard { Key = "pendingSalesOrders", Label = "未出库销售订单数", Count = 0 });

            if (IsAdminUser(user) || HasPermission(user, "purchase_order.view"))
                warningCards.Add(new DashboardWarningCard { Key = "pendingPurchaseOrders", Label = "未入库采购单数", Count = CountPendingPurchaseOrders() });
            else
                warningCards.Add(new DashboardWarningCard { Key = "pendingPurchaseOrders", Label = "未入库采购单数", Count = 0 });

            if (IsAdminUser(user) || HasPermission(user, "stock_summary.view"))
            {
                int negativeQtyCount = stockItems.Count(x => x.CurrentQuantity < 0);
                int abnormalCount = stockItems.Count(x => x.CurrentQuantity < 0 || x.StockAmount < 0);
                warningCards.Add(new DashboardWarningCard { Key = "negativeStock", Label = "负库存数量", Count = negativeQtyCount });
                warningCards.Add(new DashboardWarningCard { Key = "stockAbnormal", Label = "库存异常数量", Count = abnormalCount });

                bool sortByAmount = stockItems.Any(x => x.StockAmount > 0);
                inventoryTopTitle = sortByAmount ? "库存价值 Top10" : "库存数量 Top10";
                inventoryTop = stockItems
                    .OrderByDescending(x => sortByAmount ? x.StockAmount : x.CurrentQuantity)
                    .ThenBy(x => x.ItemCode)
                    .Take(10)
                    .Select(x => new DashboardInventoryTopItem
                    {
                        ItemCode = x.ItemCode ?? "-",
                        ItemName = x.ItemName ?? "-",
                        ItemType = x.ItemType ?? "-",
                        Quantity = x.CurrentQuantity,
                        Unit = x.Unit ?? "-",
                        StockValue = x.StockAmount,
                        SortByAmount = sortByAmount
                    })
                    .ToArray();

                inventoryWarnings = stockItems
                    .Where(x => x.CurrentQuantity < 0 || x.StockAmount < 0)
                    .OrderBy(x => x.CurrentQuantity)
                    .ThenBy(x => x.ItemCode)
                    .Select(x =>
                    {
                        string reason = x.CurrentQuantity < 0 && x.StockAmount < 0 ? "负库存且负金额"
                            : x.CurrentQuantity < 0 ? "负库存" : "负库存金额";
                        return new DashboardInventoryWarningItem
                        {
                            ItemCode = x.ItemCode ?? "-",
                            ItemName = x.ItemName ?? "-",
                            ItemType = x.ItemType ?? "-",
                            Quantity = x.CurrentQuantity,
                            Unit = x.Unit ?? "-",
                            Reason = reason
                        };
                    })
                    .ToArray();
            }
            else
            {
                warningCards.Add(new DashboardWarningCard { Key = "negativeStock", Label = "负库存数量", Count = 0 });
                warningCards.Add(new DashboardWarningCard { Key = "stockAbnormal", Label = "库存异常数量", Count = 0 });
            }

            if (IsAdminUser(user) || HasPermission(user, "finance.view"))
            {
                financeSummary = LoadFinance()
                    .Where(x => DashboardDateInRange(x.Date, range))
                    .OrderByDescending(x => Math.Max(x.Receipt, x.Payment))
                    .ThenByDescending(x => x.Date ?? "")
                    .Take(10)
                    .Select(x => new DashboardFinanceSummaryItem
                    {
                        Date = x.Date ?? "-",
                        AccountType = x.AccountType ?? "-",
                        Receipt = RoundMoney(x.Receipt),
                        Payment = RoundMoney(x.Payment),
                        Purpose = x.Purpose ?? "-"
                    })
                    .ToArray();
            }

            return new OwnerDashboardSummaryResult
            {
                SummaryCards = BuildSummaryCards(biz),
                WarningCards = warningCards.ToArray(),
                ReceivableTop = receivableTop,
                PayableTop = payableTop,
                InventoryTop = inventoryTop,
                InventoryWarnings = inventoryWarnings,
                FinanceSummary = financeSummary,
                InventoryTopTitle = inventoryTopTitle,
                UpdatedAt = biz.UpdatedAt,
                Range = range
            };
        }

        static void GetBusinessDashboard(HttpListenerContext ctx, UserSession user)
        {
            if (!CanAccessBusinessDashboard(user))
            {
                WriteJson(ctx, new { error = "无权限操作" }, 403);
                return;
            }
            string range = NormalizeDashboardRange(ctx.Request.QueryString["range"]);
            WriteJson(ctx, BuildBusinessDashboard(range));
        }

        static void GetOwnerDashboardSummary(HttpListenerContext ctx, UserSession user)
        {
            if (!CanAccessBusinessDashboard(user))
            {
                WriteJson(ctx, new { error = "无权限操作" }, 403);
                return;
            }
            string range = NormalizeDashboardRange(ctx.Request.QueryString["range"]);
            WriteJson(ctx, BuildOwnerDashboardSummary(user, range));
        }
    }
}
