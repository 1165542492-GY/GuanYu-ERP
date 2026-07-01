using System;
using System.Linq;

namespace SupplierErpApp
{
    public static partial class Program
    {
        static decimal GetConfirmedOutboundQtyForSalesOrder(string salesOrderId, string excludeOutboundId = null)
        {
            if (string.IsNullOrWhiteSpace(salesOrderId)) return 0;
            return LoadSalesOutbounds()
                .Where(x => IsConfirmedStatus(x.Status)
                    && string.Equals(x.SalesOrderId ?? "", salesOrderId, StringComparison.OrdinalIgnoreCase)
                    && (excludeOutboundId == null || !string.Equals(x.Id, excludeOutboundId, StringComparison.OrdinalIgnoreCase)))
                .Sum(x => x.Quantity);
        }

        static decimal GetConfirmedInboundQtyForPurchaseOrder(string purchaseOrderId, string excludeInboundId = null)
        {
            if (string.IsNullOrWhiteSpace(purchaseOrderId)) return 0;
            return LoadPurchaseInbounds()
                .Where(x => IsConfirmedStatus(x.Status)
                    && string.Equals(x.PurchaseOrderId ?? "", purchaseOrderId, StringComparison.OrdinalIgnoreCase)
                    && (excludeInboundId == null || !string.Equals(x.Id, excludeInboundId, StringComparison.OrdinalIgnoreCase)))
                .Sum(x => x.Quantity);
        }

        static void EnrichSalesOrderFulfillmentFields(System.Collections.Generic.IEnumerable<SalesOrder> orders)
        {
            if (orders == null) return;
            foreach (var order in orders)
            {
                if (order == null) continue;
                var shipped = RoundMoney(GetConfirmedOutboundQtyForSalesOrder(order.Id));
                var remaining = RoundMoney(order.Quantity - shipped);
                order.ShippedQuantity = shipped;
                order.RemainingQuantity = remaining > 0 ? remaining : 0;
                if (shipped <= 0) order.OutboundStatus = "未出库";
                else if (shipped + 0.0001m < order.Quantity) order.OutboundStatus = "部分出库";
                else order.OutboundStatus = "已出库";
            }
        }

        static void EnrichPurchaseOrderFulfillmentFields(System.Collections.Generic.IEnumerable<PurchaseOrder> orders)
        {
            if (orders == null) return;
            foreach (var order in orders)
            {
                if (order == null) continue;
                var received = RoundMoney(GetConfirmedInboundQtyForPurchaseOrder(order.Id));
                var remaining = RoundMoney(order.Quantity - received);
                order.ReceivedQuantity = received;
                order.RemainingQuantity = remaining > 0 ? remaining : 0;
                if (received <= 0) order.InboundStatus = "未入库";
                else if (received + 0.0001m < order.Quantity) order.InboundStatus = "部分入库";
                else order.InboundStatus = "已入库";
            }
        }

        static void ValidateSalesOutboundRemainingQty(SalesOutbound item, string excludeOutboundId = null)
        {
            if (item == null || !IsConfirmedStatus(item.Status) || string.IsNullOrWhiteSpace(item.SalesOrderId)) return;
            var order = LoadSalesOrders().FirstOrDefault(x => x.Id == item.SalesOrderId);
            if (order == null) return;
            if (!IsConfirmedStatus(order.Status)) BizFail("来源销售订单尚未确认，不能出库");
            decimal shipped = GetConfirmedOutboundQtyForSalesOrder(item.SalesOrderId, excludeOutboundId);
            decimal remaining = RoundMoney(order.Quantity - shipped);
            if (item.Quantity > remaining + 0.0001m)
                BizFail("本次出库数量不能超过未出库数量（剩余 " + remaining.ToString("0.##") + "）");
        }

        static void ValidatePurchaseInboundRemainingQty(PurchaseInbound item, string excludeInboundId = null)
        {
            if (item == null || !IsConfirmedStatus(item.Status) || string.IsNullOrWhiteSpace(item.PurchaseOrderId)) return;
            var order = LoadPurchaseOrders().FirstOrDefault(x => x.Id == item.PurchaseOrderId);
            if (order == null) return;
            if (!IsConfirmedStatus(order.Status)) BizFail("来源采购单尚未确认，不能入库");
            decimal received = GetConfirmedInboundQtyForPurchaseOrder(item.PurchaseOrderId, excludeInboundId);
            decimal remaining = RoundMoney(order.Quantity - received);
            if (item.Quantity > remaining + 0.0001m)
                BizFail("本次入库数量不能超过未入库数量（剩余 " + remaining.ToString("0.##") + "）");
        }

        static void ResolveSupplierFields(PurchaseOrder item)
        {
            item.SupplierId = (item.SupplierId ?? "").Trim();
            item.SupplierCode = (item.SupplierCode ?? "").Trim();
            item.SupplierName = (item.SupplierName ?? "").Trim();
            var suppliers = LoadSuppliers();
            Supplier matched = null;
            if (!string.IsNullOrWhiteSpace(item.SupplierId))
                matched = suppliers.FirstOrDefault(x => x.Id == item.SupplierId);
            if (matched == null && !string.IsNullOrWhiteSpace(item.SupplierCode))
                matched = suppliers.FirstOrDefault(x => string.Equals(x.Code, item.SupplierCode, StringComparison.OrdinalIgnoreCase));
            if (matched == null && !string.IsNullOrWhiteSpace(item.SupplierName))
                matched = suppliers.FirstOrDefault(x => string.Equals(x.Company, item.SupplierName, StringComparison.OrdinalIgnoreCase));
            if (matched == null) BizFail("供应商不存在，请先在供应商管理中添加供应商");
            item.SupplierId = matched.Id;
            item.SupplierCode = matched.Code ?? "";
            item.SupplierName = matched.Company ?? "";
        }

        static void ApplyMaterialDefaultPriceToPurchaseOrder(PurchaseOrder item)
        {
            if (string.IsNullOrWhiteSpace(item.MaterialId)) return;
            var material = LoadMaterials().FirstOrDefault(x => x.Id == item.MaterialId);
            if (material == null) return;
            if (item.UnitPrice <= 0)
                item.UnitPrice = MaterialDisplayUnitPrice(material);
            if (string.IsNullOrWhiteSpace(item.PriceType))
                item.PriceType = NormalizePriceType(material.PriceType);
        }

        static void AutoResolveSalesOutboundCost(SalesOutbound item)
        {
            if (item == null) return;
            if (IsFinishedProductOutbound(item))
            {
                var mc = LoadModelCosts().FirstOrDefault(x => x.Id == item.ModelCostId);
                if (mc != null && item.CostPrice <= 0)
                {
                    decimal cost = mc.TotalCost > 0 ? mc.TotalCost : mc.MaterialCost;
                    if (cost > 0) item.CostPrice = cost;
                }
                return;
            }
            if (!string.IsNullOrWhiteSpace(item.MaterialId))
            {
                var material = LoadMaterials().FirstOrDefault(x => x.Id == item.MaterialId);
                if (material != null && item.CostPrice <= 0)
                {
                    decimal price = MaterialDisplayUnitPrice(material);
                    if (price > 0) item.CostPrice = price;
                }
            }
        }

        static void AutoResolveProductionPickCost(ProductionPick item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.MaterialId)) return;
            if (item.CostPrice > 0) return;
            var material = LoadMaterials().FirstOrDefault(x => x.Id == item.MaterialId);
            if (material == null) return;
            decimal price = MaterialDisplayUnitPrice(material);
            if (price > 0) item.CostPrice = price;
        }

        static void AutoResolveFinishedInboundUnitCost(FinishedInbound item)
        {
            if (item == null || item.UnitCost > 0) return;
            if (!string.IsNullOrWhiteSpace(item.ModelCostId))
            {
                var mc = LoadModelCosts().FirstOrDefault(x => x.Id == item.ModelCostId);
                if (mc != null)
                {
                    decimal cost = mc.TotalCost > 0 ? mc.TotalCost : mc.MaterialCost;
                    if (cost > 0) { item.UnitCost = cost; return; }
                }
            }
            if (!string.IsNullOrWhiteSpace(item.BomId))
            {
                var bom = LoadBom().FirstOrDefault(x => x.Id == item.BomId);
                if (bom != null && bom.TotalMaterialCost > 0)
                    item.UnitCost = bom.TotalMaterialCost;
            }
        }

        static string NormalizeStockItemTypeLabel(string itemType)
        {
            var t = (itemType ?? "").Trim();
            if (string.Equals(t, "Material", StringComparison.OrdinalIgnoreCase) || t == "物料") return "原材料";
            if (string.Equals(t, "FinishedProduct", StringComparison.OrdinalIgnoreCase) || t == "成品") return "成品";
            if (t == "半成品") return "半成品";
            if (string.IsNullOrEmpty(t)) return "原材料";
            return t;
        }
    }
}
