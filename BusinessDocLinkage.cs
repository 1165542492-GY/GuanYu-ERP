using System;
using System.Collections.Generic;
using System.Linq;

namespace SupplierErpApp
{
    public static partial class Program
    {
        class ImportBatchContext
        {
            readonly List<SalesOutbound> _pendingConfirmedOutbounds = new List<SalesOutbound>();
            readonly List<PurchaseInbound> _pendingConfirmedInbounds = new List<PurchaseInbound>();
            readonly Dictionary<string, StockAgg> _stockMap;

            public ImportBatchContext()
            {
                _stockMap = BuildStockMap();
            }

            public decimal GetPendingOutboundQty(string salesOrderId, string excludeOutboundId = null)
            {
                if (string.IsNullOrWhiteSpace(salesOrderId)) return 0;
                return _pendingConfirmedOutbounds
                    .Where(x => IsConfirmedStatus(x.Status)
                        && string.Equals(x.SalesOrderId ?? "", salesOrderId, StringComparison.OrdinalIgnoreCase)
                        && (excludeOutboundId == null || !string.Equals(x.Id, excludeOutboundId, StringComparison.OrdinalIgnoreCase)))
                    .Sum(x => x.Quantity);
            }

            public decimal GetPendingInboundQty(string purchaseOrderId, string excludeInboundId = null)
            {
                if (string.IsNullOrWhiteSpace(purchaseOrderId)) return 0;
                return _pendingConfirmedInbounds
                    .Where(x => IsConfirmedStatus(x.Status)
                        && string.Equals(x.PurchaseOrderId ?? "", purchaseOrderId, StringComparison.OrdinalIgnoreCase)
                        && (excludeInboundId == null || !string.Equals(x.Id, excludeInboundId, StringComparison.OrdinalIgnoreCase)))
                    .Sum(x => x.Quantity);
            }

            public void ValidateStockForConfirmedOutbound(SalesOutbound item)
            {
                if (item == null || !IsConfirmedStatus(item.Status)) return;
                if (IsFinishedProductOutbound(item))
                    EnsureFinishedProductStockOnMap(_stockMap, item.Quantity, item.ModelCostId, item.BomId, item.MaterialName);
                else
                    EnsureMaterialStockOnMap(_stockMap, item.Quantity, item.MaterialId, item.MaterialCode, item.MaterialName);
            }

            public void RecordSalesOutbound(SalesOutbound item)
            {
                if (item == null || !IsConfirmedStatus(item.Status)) return;
                _pendingConfirmedOutbounds.Add(item);
                StockDeductForOutbound(_stockMap, item);
            }

            public void RecordPurchaseInbound(PurchaseInbound item)
            {
                if (item == null || !IsConfirmedStatus(item.Status)) return;
                _pendingConfirmedInbounds.Add(item);
                StockAddForInbound(_stockMap, item);
            }
        }

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

        static void ValidateSalesOutboundRemainingQty(SalesOutbound item, string excludeOutboundId = null, ImportBatchContext batch = null, SalesOrder orderOverride = null)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.SalesOrderId)) return;
            var order = orderOverride ?? LoadSalesOrders().FirstOrDefault(x => x.Id == item.SalesOrderId);
            if (order == null) return;
            if (!IsConfirmedStatus(order.Status)) BizFail("来源销售订单尚未确认，不能出库");
            decimal shipped = GetConfirmedOutboundQtyForSalesOrder(item.SalesOrderId, excludeOutboundId);
            if (batch != null) shipped += batch.GetPendingOutboundQty(item.SalesOrderId, excludeOutboundId);
            decimal remaining = RoundMoney(order.Quantity - shipped);
            if (item.Quantity > remaining + 0.0001m)
                BizFail("本次出库数量不能超过未出库数量（剩余 " + remaining.ToString("0.##") + "）");
        }

        static void ValidatePurchaseInboundRemainingQty(PurchaseInbound item, string excludeInboundId = null, ImportBatchContext batch = null, PurchaseOrder orderOverride = null)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.PurchaseOrderId)) return;
            var order = orderOverride ?? LoadPurchaseOrders().FirstOrDefault(x => x.Id == item.PurchaseOrderId);
            if (order == null) return;
            if (!IsConfirmedStatus(order.Status)) BizFail("来源采购单尚未确认，不能入库");
            decimal received = GetConfirmedInboundQtyForPurchaseOrder(item.PurchaseOrderId, excludeInboundId);
            if (batch != null) received += batch.GetPendingInboundQty(item.PurchaseOrderId, excludeInboundId);
            decimal remaining = RoundMoney(order.Quantity - received);
            if (item.Quantity > remaining + 0.0001m)
                BizFail("本次入库数量不能超过未入库数量（剩余 " + remaining.ToString("0.##") + "）");
        }

        static void EnsureMaterialStockOnMap(Dictionary<string, StockAgg> map, decimal requiredQty, string materialId, string materialCode, string materialName)
        {
            if (requiredQty <= 0) return;
            decimal available = ReadMaterialQtyFromStockMap(map, materialId, materialCode, materialName);
            if (available + 0.0001m < requiredQty)
            {
                string mid, mcode, mname, mspec, munit;
                ResolveMaterialFields(materialId, materialName, out mid, out mcode, out mname, out mspec, out munit);
                string display = !string.IsNullOrWhiteSpace(mcode) ? mcode + " " + mname : (!string.IsNullOrWhiteSpace(mname) ? mname : (materialName ?? "未知物料"));
                if (available <= 0.0001m)
                    BizFail(string.Format("该物料「{0}」当前库存为 0，不能出库/领用", display), 409);
                BizFail(string.Format("物料「{0}」库存不足，当前可用 {1}，需要 {2}", display, RoundMoney(available), RoundMoney(requiredQty)), 409);
            }
        }

        static void EnsureFinishedProductStockOnMap(Dictionary<string, StockAgg> map, decimal requiredQty, string modelCostId, string bomId, string productName)
        {
            if (requiredQty <= 0) return;
            string pid = GetFinishedProductStockId(modelCostId, bomId);
            decimal available = 0;
            if (!string.IsNullOrWhiteSpace(pid))
            {
                string key = StockKey("成品", pid, productName ?? "");
                StockAgg agg;
                if (map.TryGetValue(key, out agg)) available = agg.Quantity;
            }
            if (available + 0.0001m < requiredQty)
            {
                string display = !string.IsNullOrWhiteSpace(productName) ? productName : "成品";
                if (available <= 0.0001m)
                    BizFail(string.Format("该成品「{0}」当前库存为 0，不能出库", display), 409);
                BizFail(string.Format("成品「{0}」库存不足，当前可用 {1}，需要 {2}", display, RoundMoney(available), RoundMoney(requiredQty)), 409);
            }
        }

        static decimal ReadMaterialQtyFromStockMap(Dictionary<string, StockAgg> map, string materialId, string materialCode, string materialName)
        {
            string mid, mcode, mname, mspec, munit;
            ResolveMaterialFields(materialId, materialName, out mid, out mcode, out mname, out mspec, out munit);
            if (!string.IsNullOrWhiteSpace(materialCode)) mcode = materialCode;
            string key = StockKey("物料", mid, mname);
            StockAgg agg;
            return map.TryGetValue(key, out agg) ? agg.Quantity : 0;
        }

        static void StockDeductForOutbound(Dictionary<string, StockAgg> map, SalesOutbound x)
        {
            if (x == null || !IsConfirmedStatus(x.Status)) return;
            if (IsFinishedProductOutbound(x))
            {
                string pid = GetFinishedProductStockId(x.ModelCostId, x.BomId);
                StockAdd(map, "成品", pid, x.BomCode ?? "", x.MaterialName ?? "", "", "", -x.Quantity, x.CostPrice);
            }
            else
                StockAddMaterial(map, x.MaterialId, x.MaterialCode, x.MaterialName, -x.Quantity, x.CostPrice);
        }

        static void StockAddForInbound(Dictionary<string, StockAgg> map, PurchaseInbound x)
        {
            if (x == null || !IsConfirmedStatus(x.Status)) return;
            StockAddMaterial(map, x.MaterialId, x.MaterialCode, x.MaterialName, x.Quantity, x.InboundPrice);
        }

        static void EnsureUniqueNewOrderCode(string code, IEnumerable<string> existingCodes, IEnumerable<string> pendingCodes)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            code = code.Trim();
            if (existingCodes.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase)))
                BizFail("订单编号 " + code + " 已存在，不能重复新增");
            if (pendingCodes.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase)))
                BizFail("订单编号 " + code + " 在本批次中重复");
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
