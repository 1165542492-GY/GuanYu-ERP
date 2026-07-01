using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SupplierErpApp
{
    public static partial class Program
    {
        static string AuditText(string value)
        {
            value = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length > 80 ? value.Substring(0, 80) : value;
        }

        static string AuditNumber(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static void AddAuditPart(List<string> parts, string key, string value)
        {
            value = AuditText(value);
            if (!string.IsNullOrWhiteSpace(value)) parts.Add(key + "=" + value);
        }

        static void AddAuditPart(List<string> parts, string key, decimal value)
        {
            if (value != 0) parts.Add(key + "=" + AuditNumber(value));
        }

        static string JoinAuditParts(IEnumerable<string> parts)
        {
            return string.Join("; ", parts);
        }

        static bool IsCanceledStatus(string status)
        {
            return string.Equals((status ?? "").Trim(), "已取消", StringComparison.OrdinalIgnoreCase);
        }

        static void AuditStatusTransition(UserSession user, string confirmAction, string cancelAction, string beforeStatus, string afterStatus, string detail)
        {
            if (!IsConfirmedStatus(beforeStatus) && IsConfirmedStatus(afterStatus))
                Audit(user, confirmAction, detail);
            if (!IsCanceledStatus(beforeStatus) && IsCanceledStatus(afterStatus))
                Audit(user, cancelAction, detail);
        }

        static bool HasSalesOrderSource(SalesOutbound item)
        {
            return item != null && (!string.IsNullOrWhiteSpace(item.SalesOrderId) || !string.IsNullOrWhiteSpace(item.SalesOrderNo));
        }

        static bool HasPurchaseOrderSource(PurchaseInbound item)
        {
            return item != null && (!string.IsNullOrWhiteSpace(item.PurchaseOrderId) || !string.IsNullOrWhiteSpace(item.PurchaseNo));
        }

        static string BuildSalesOrderAuditDetail(SalesOrder item)
        {
            if (item == null) return "";
            var parts = new List<string> { AuditText(item.Code) };
            AddAuditPart(parts, "customer", item.CustomerName);
            AddAuditPart(parts, "item", item.MaterialName);
            AddAuditPart(parts, "qty", item.Quantity);
            AddAuditPart(parts, "amount", item.Amount);
            AddAuditPart(parts, "status", item.Status);
            return JoinAuditParts(parts);
        }

        static string BuildSalesOutboundAuditDetail(SalesOutbound item)
        {
            if (item == null) return "";
            var parts = new List<string> { AuditText(item.Code) };
            AddAuditPart(parts, "source", item.SalesOrderNo);
            AddAuditPart(parts, "customer", item.CustomerName);
            AddAuditPart(parts, "item", item.MaterialName);
            AddAuditPart(parts, "qty", item.Quantity);
            AddAuditPart(parts, "amount", item.CostAmount);
            AddAuditPart(parts, "status", item.Status);
            return JoinAuditParts(parts);
        }

        static string BuildPurchaseOrderAuditDetail(PurchaseOrder item)
        {
            if (item == null) return "";
            var parts = new List<string> { AuditText(item.Code) };
            AddAuditPart(parts, "supplier", item.SupplierName);
            AddAuditPart(parts, "item", item.MaterialName);
            AddAuditPart(parts, "qty", item.Quantity);
            AddAuditPart(parts, "amount", item.Amount);
            AddAuditPart(parts, "status", item.Status);
            return JoinAuditParts(parts);
        }

        static string BuildPurchaseInboundAuditDetail(PurchaseInbound item)
        {
            if (item == null) return "";
            var parts = new List<string> { AuditText(item.Code) };
            AddAuditPart(parts, "source", item.PurchaseNo);
            AddAuditPart(parts, "supplier", item.SupplierName);
            AddAuditPart(parts, "item", item.MaterialName);
            AddAuditPart(parts, "qty", item.Quantity);
            AddAuditPart(parts, "amount", item.Amount);
            AddAuditPart(parts, "status", item.Status);
            return JoinAuditParts(parts);
        }

        static string BuildProductionPickAuditDetail(ProductionPick item)
        {
            if (item == null) return "";
            var parts = new List<string> { AuditText(item.Code) };
            AddAuditPart(parts, "bom", item.BomName);
            AddAuditPart(parts, "item", item.MaterialName);
            AddAuditPart(parts, "qty", item.Quantity);
            AddAuditPart(parts, "amount", item.CostAmount);
            AddAuditPart(parts, "status", item.Status);
            return JoinAuditParts(parts);
        }

        static string BuildFinishedInboundAuditDetail(FinishedInbound item)
        {
            if (item == null) return "";
            var parts = new List<string> { AuditText(item.Code) };
            AddAuditPart(parts, "bom", item.BomCode);
            AddAuditPart(parts, "product", item.ProductName);
            AddAuditPart(parts, "qty", item.Quantity);
            AddAuditPart(parts, "amount", item.Amount);
            AddAuditPart(parts, "status", item.Status);
            return JoinAuditParts(parts);
        }

        static string BuildReceivableAuditDetail(Receivable item)
        {
            if (item == null) return "";
            var parts = new List<string> { AuditText(item.Code) };
            AddAuditPart(parts, "source", item.SalesOrderNo);
            AddAuditPart(parts, "customer", item.CustomerName);
            AddAuditPart(parts, "amount", item.ReceivableAmount);
            AddAuditPart(parts, "received", item.ReceivedAmount);
            AddAuditPart(parts, "status", item.Status);
            return JoinAuditParts(parts);
        }

        static string BuildPayableAuditDetail(Payable item)
        {
            if (item == null) return "";
            var parts = new List<string> { AuditText(item.Code) };
            AddAuditPart(parts, "source", item.PurchaseNo);
            AddAuditPart(parts, "supplier", item.SupplierName);
            AddAuditPart(parts, "amount", item.PayableAmount);
            AddAuditPart(parts, "paid", item.PaidAmount);
            AddAuditPart(parts, "status", item.Status);
            return JoinAuditParts(parts);
        }

        static string BuildProductionWorkOrderAuditDetail(ProductionWorkOrder item)
        {
            if (item == null) return "";
            var parts = new List<string> { AuditText(item.WorkOrderNo) };
            AddAuditPart(parts, "source", item.SalesOrderNo);
            AddAuditPart(parts, "customer", item.CustomerName);
            AddAuditPart(parts, "product", item.ProductName);
            AddAuditPart(parts, "qty", item.Quantity);
            AddAuditPart(parts, "status", item.Status);
            return JoinAuditParts(parts);
        }
    }
}
