using System;
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
            public decimal FinanceIncomeTotal { get; set; }
            public decimal FinanceExpenseTotal { get; set; }
            public decimal FinanceNetTotal { get; set; }
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
                || HasPermission(user, "stock.view")
                || HasPermission(user, "finance.view");
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
                ReceivableBalance = RoundMoney(receivableTotal - receivedTotal),
                PayableTotal = RoundMoney(payableTotal),
                PaidTotal = RoundMoney(paidTotal),
                PayableBalance = RoundMoney(payableTotal - paidTotal),
                InventoryQuantityTotal = inventoryQty,
                InventoryAmountTotal = inventoryAmount,
                FinanceIncomeTotal = RoundMoney(financeIncome),
                FinanceExpenseTotal = RoundMoney(financeExpense),
                FinanceNetTotal = RoundMoney(financeIncome - financeExpense),
                UpdatedAt = BizUpdatedAtNow(),
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
    }
}
