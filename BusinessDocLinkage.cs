using System;
using System.Collections.Generic;
using System.Linq;

namespace SupplierErpApp
{
    public static partial class Program
    {
        static SalesOrderLine BuildLegacySalesOrderLine(SalesOrder order)
        {
            if (order == null) return null;
            if (string.IsNullOrWhiteSpace(order.MaterialId) && string.IsNullOrWhiteSpace(order.MaterialName)
                && string.IsNullOrWhiteSpace(order.ModelCostId) && order.Quantity <= 0)
                return null;
            return new SalesOrderLine
            {
                LineId = Guid.NewGuid().ToString("N"),
                ItemType = NormalizeSalesItemType(order.ItemType),
                ModelCostId = order.ModelCostId,
                BomId = order.BomId,
                MaterialId = order.MaterialId,
                MaterialCode = order.MaterialCode,
                MaterialName = order.MaterialName,
                Quantity = order.Quantity,
                TaxExcludedSalePrice = order.TaxExcludedSalePrice > 0 ? order.TaxExcludedSalePrice : order.UnitPrice,
                TaxIncludedSalePrice = order.TaxIncludedSalePrice,
                TaxExcludedSaleAmount = order.TaxExcludedSaleAmount,
                TaxIncludedSaleAmount = order.TaxIncludedSaleAmount,
                UnitPrice = order.UnitPrice,
                Amount = order.Amount,
                Note = ""
            };
        }

        static PurchaseOrderLine BuildLegacyPurchaseOrderLine(PurchaseOrder order)
        {
            if (order == null) return null;
            if (string.IsNullOrWhiteSpace(order.MaterialId) && string.IsNullOrWhiteSpace(order.MaterialName) && order.Quantity <= 0)
                return null;
            return new PurchaseOrderLine
            {
                LineId = Guid.NewGuid().ToString("N"),
                MaterialId = order.MaterialId,
                MaterialCode = order.MaterialCode,
                MaterialName = order.MaterialName,
                Quantity = order.Quantity,
                UnitPrice = order.UnitPrice,
                PriceType = order.PriceType,
                Amount = order.Amount,
                Note = ""
            };
        }

        static void EnsureSalesOrderItemsForRead(SalesOrder order)
        {
            if (order == null) return;
            if (order.Items == null || order.Items.Count == 0)
            {
                var legacy = BuildLegacySalesOrderLine(order);
                order.Items = legacy == null ? new List<SalesOrderLine>() : new List<SalesOrderLine> { legacy };
            }
            foreach (var line in order.Items)
                if (string.IsNullOrWhiteSpace(line.LineId)) line.LineId = Guid.NewGuid().ToString("N");
            ApplySalesOrderAggregateFromLines(order);
        }

        static void EnsurePurchaseOrderItemsForRead(PurchaseOrder order)
        {
            if (order == null) return;
            if (order.Items == null || order.Items.Count == 0)
            {
                var legacy = BuildLegacyPurchaseOrderLine(order);
                order.Items = legacy == null ? new List<PurchaseOrderLine>() : new List<PurchaseOrderLine> { legacy };
            }
            foreach (var line in order.Items)
                if (string.IsNullOrWhiteSpace(line.LineId)) line.LineId = Guid.NewGuid().ToString("N");
            ApplyPurchaseOrderAggregateFromLines(order);
        }

        static void ApplySalesOrderLineDefaults(SalesOrderLine line)
        {
            if (line == null) BizFail("Sales order line cannot be empty");
            line.LineId = string.IsNullOrWhiteSpace(line.LineId) ? Guid.NewGuid().ToString("N") : line.LineId.Trim();
            line.ItemType = NormalizeSalesItemType(line.ItemType);
            line.ModelCostId = (line.ModelCostId ?? "").Trim();
            line.BomId = (line.BomId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(line.ModelCostId)) line.ItemType = "FinishedProduct";
            if (line.ItemType == "FinishedProduct")
            {
                if (string.IsNullOrWhiteSpace(line.ModelCostId)) BizFail("Finished product line must choose model cost");
                var mc = LoadModelCosts().FirstOrDefault(x => x.Id == line.ModelCostId);
                if (mc == null) BizFail("Selected model cost does not exist");
                if ((mc.Status ?? "启用") != "启用") BizFail("Selected model cost is disabled");
                line.BomId = mc.BomId ?? "";
                line.MaterialCode = mc.ModelCode ?? "";
                line.MaterialName = !string.IsNullOrWhiteSpace(mc.ProductName) ? mc.ProductName : mc.ModelName;
                line.MaterialId = "";
            }
            else
            {
                string mid, mcode, mname, mspec, munit;
                ResolveMaterialFields(line.MaterialId, line.MaterialName, out mid, out mcode, out mname, out mspec, out munit);
                line.MaterialId = mid;
                line.MaterialCode = mcode;
                line.MaterialName = mname;
                line.ModelCostId = "";
                line.BomId = "";
                if (string.IsNullOrWhiteSpace(line.MaterialName)) BizFail("Please choose material for every sales order line");
            }
            if (line.Quantity <= 0) BizFail("Sales order line quantity must be greater than 0");
            if (line.TaxExcludedSalePrice <= 0 && line.TaxIncludedSalePrice <= 0 && line.UnitPrice > 0)
                line.TaxExcludedSalePrice = line.UnitPrice;
            if (line.TaxExcludedSalePrice < 0 || line.TaxIncludedSalePrice < 0)
                BizFail("Sales order line price cannot be negative");
            line.TaxExcludedSaleAmount = CalcLineAmount(line.Quantity, line.TaxExcludedSalePrice);
            line.TaxIncludedSaleAmount = CalcLineAmount(line.Quantity, line.TaxIncludedSalePrice);
            line.UnitPrice = line.TaxExcludedSalePrice;
            line.Amount = line.TaxExcludedSaleAmount > 0 ? line.TaxExcludedSaleAmount : line.TaxIncludedSaleAmount;
            line.Note = (line.Note ?? "").Trim();
        }

        static void ApplyPurchaseOrderLineDefaults(PurchaseOrderLine line)
        {
            if (line == null) BizFail("Purchase order line cannot be empty");
            line.LineId = string.IsNullOrWhiteSpace(line.LineId) ? Guid.NewGuid().ToString("N") : line.LineId.Trim();
            string mid, mcode, mname, mspec, munit;
            ResolveMaterialFields(line.MaterialId, line.MaterialName, out mid, out mcode, out mname, out mspec, out munit);
            line.MaterialId = mid;
            line.MaterialCode = mcode;
            line.MaterialName = mname;
            if (string.IsNullOrWhiteSpace(line.MaterialName)) BizFail("Please choose material for every purchase order line");
            if (line.UnitPrice <= 0 && !string.IsNullOrWhiteSpace(line.MaterialId))
            {
                var material = LoadMaterials().FirstOrDefault(x => x.Id == line.MaterialId);
                if (material != null)
                {
                    line.UnitPrice = MaterialDisplayUnitPrice(material);
                    if (string.IsNullOrWhiteSpace(line.PriceType)) line.PriceType = NormalizePriceType(material.PriceType);
                }
            }
            line.PriceType = NormalizePriceType(line.PriceType);
            if (line.Quantity <= 0) BizFail("Purchase order line quantity must be greater than 0");
            if (line.UnitPrice < 0) BizFail("Purchase order line price cannot be negative");
            line.Amount = CalcLineAmount(line.Quantity, line.UnitPrice);
            line.Note = (line.Note ?? "").Trim();
        }

        static void ApplySalesOrderAggregateFromLines(SalesOrder order)
        {
            if (order == null) return;
            var lines = order.Items ?? new List<SalesOrderLine>();
            order.Quantity = RoundMoney(lines.Sum(x => x.Quantity));
            order.TaxExcludedSaleAmount = RoundMoney(lines.Sum(x => x.TaxExcludedSaleAmount));
            order.TaxIncludedSaleAmount = RoundMoney(lines.Sum(x => x.TaxIncludedSaleAmount));
            order.Amount = order.TaxExcludedSaleAmount > 0 ? order.TaxExcludedSaleAmount : order.TaxIncludedSaleAmount;
            if (lines.Count == 1)
            {
                var line = lines[0];
                order.ItemType = line.ItemType;
                order.ModelCostId = line.ModelCostId;
                order.BomId = line.BomId;
                order.MaterialId = line.MaterialId;
                order.MaterialCode = line.MaterialCode;
                order.MaterialName = line.MaterialName;
                order.TaxExcludedSalePrice = line.TaxExcludedSalePrice;
                order.TaxIncludedSalePrice = line.TaxIncludedSalePrice;
                order.UnitPrice = line.UnitPrice;
            }
            else if (lines.Count > 1)
            {
                order.ItemType = "Material";
                order.ModelCostId = "";
                order.BomId = "";
                order.MaterialId = "";
                order.MaterialCode = "";
                order.MaterialName = "多明细 " + lines.Count + " 行";
                order.TaxExcludedSalePrice = 0;
                order.TaxIncludedSalePrice = 0;
                order.UnitPrice = 0;
            }
        }

        static void ApplyPurchaseOrderAggregateFromLines(PurchaseOrder order)
        {
            if (order == null) return;
            var lines = order.Items ?? new List<PurchaseOrderLine>();
            order.Quantity = RoundMoney(lines.Sum(x => x.Quantity));
            order.Amount = RoundMoney(lines.Sum(x => x.Amount));
            if (lines.Count == 1)
            {
                var line = lines[0];
                order.MaterialId = line.MaterialId;
                order.MaterialCode = line.MaterialCode;
                order.MaterialName = line.MaterialName;
                order.UnitPrice = line.UnitPrice;
                order.PriceType = line.PriceType;
            }
            else if (lines.Count > 1)
            {
                order.MaterialId = "";
                order.MaterialCode = "";
                order.MaterialName = "多明细 " + lines.Count + " 行";
                order.UnitPrice = 0;
                order.PriceType = "";
            }
        }

        static void NormalizeSalesOrderItemsForSave(SalesOrder order)
        {
            if (order.Items == null || order.Items.Count == 0)
            {
                var legacy = BuildLegacySalesOrderLine(order);
                order.Items = legacy == null ? new List<SalesOrderLine>() : new List<SalesOrderLine> { legacy };
            }
            if (order.Items.Count == 0) BizFail("Sales order must contain at least one line");
            foreach (var line in order.Items) ApplySalesOrderLineDefaults(line);
            ApplySalesOrderAggregateFromLines(order);
        }

        static void NormalizePurchaseOrderItemsForSave(PurchaseOrder order)
        {
            if (order.Items == null || order.Items.Count == 0)
            {
                var legacy = BuildLegacyPurchaseOrderLine(order);
                order.Items = legacy == null ? new List<PurchaseOrderLine>() : new List<PurchaseOrderLine> { legacy };
            }
            if (order.Items.Count == 0) BizFail("Purchase order must contain at least one line");
            foreach (var line in order.Items) ApplyPurchaseOrderLineDefaults(line);
            ApplyPurchaseOrderAggregateFromLines(order);
        }

        static List<SalesOrderLine> ParseSalesOrderLinesJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var lines = Json.Deserialize<List<SalesOrderLine>>(json.Trim());
                return lines == null || lines.Count == 0 ? null : lines;
            }
            catch (Exception ex)
            {
                BizFail("Sales order Items JSON is invalid: " + ex.Message);
                return null;
            }
        }

        static List<PurchaseOrderLine> ParsePurchaseOrderLinesJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var lines = Json.Deserialize<List<PurchaseOrderLine>>(json.Trim());
                return lines == null || lines.Count == 0 ? null : lines;
            }
            catch (Exception ex)
            {
                BizFail("Purchase order Items JSON is invalid: " + ex.Message);
                return null;
            }
        }

        static SalesOrderLine SelectSalesOrderLineForOutbound(SalesOrder order, SalesOutbound item)
        {
            EnsureSalesOrderItemsForRead(order);
            var lines = order.Items ?? new List<SalesOrderLine>();
            if (lines.Count == 0) return null;
            string lineId = (item.SalesOrderLineId ?? "").Trim();
            SalesOrderLine line = null;
            if (!string.IsNullOrWhiteSpace(lineId))
                line = lines.FirstOrDefault(x => string.Equals(x.LineId ?? "", lineId, StringComparison.OrdinalIgnoreCase));
            if (line == null && lines.Count == 1) line = lines[0];
            if (line == null && !string.IsNullOrWhiteSpace(item.ModelCostId))
                line = lines.FirstOrDefault(x => string.Equals(x.ModelCostId ?? "", item.ModelCostId, StringComparison.OrdinalIgnoreCase));
            if (line == null && !string.IsNullOrWhiteSpace(item.MaterialId))
                line = lines.FirstOrDefault(x => string.Equals(x.MaterialId ?? "", item.MaterialId, StringComparison.OrdinalIgnoreCase));
            if (line == null && !string.IsNullOrWhiteSpace(item.MaterialName))
                line = lines.FirstOrDefault(x => string.Equals(x.MaterialName ?? "", item.MaterialName, StringComparison.OrdinalIgnoreCase));
            if (line == null && lines.Count > 1) BizFail("Sales order has multiple lines; choose a source line before outbound");
            if (line == null) BizFail("Sales order line does not exist");
            item.SalesOrderLineId = line.LineId ?? "";
            return line;
        }

        static PurchaseOrderLine SelectPurchaseOrderLineForInbound(PurchaseOrder order, PurchaseInbound item)
        {
            EnsurePurchaseOrderItemsForRead(order);
            var lines = order.Items ?? new List<PurchaseOrderLine>();
            if (lines.Count == 0) return null;
            string lineId = (item.PurchaseOrderLineId ?? "").Trim();
            PurchaseOrderLine line = null;
            if (!string.IsNullOrWhiteSpace(lineId))
                line = lines.FirstOrDefault(x => string.Equals(x.LineId ?? "", lineId, StringComparison.OrdinalIgnoreCase));
            if (line == null && lines.Count == 1) line = lines[0];
            if (line == null && !string.IsNullOrWhiteSpace(item.MaterialId))
                line = lines.FirstOrDefault(x => string.Equals(x.MaterialId ?? "", item.MaterialId, StringComparison.OrdinalIgnoreCase));
            if (line == null && !string.IsNullOrWhiteSpace(item.MaterialName))
                line = lines.FirstOrDefault(x => string.Equals(x.MaterialName ?? "", item.MaterialName, StringComparison.OrdinalIgnoreCase));
            if (line == null && lines.Count > 1) BizFail("Purchase order has multiple lines; choose a source line before inbound");
            if (line == null) BizFail("Purchase order line does not exist");
            item.PurchaseOrderLineId = line.LineId ?? "";
            return line;
        }

        class ImportBatchContext
        {
            readonly List<SalesOutbound> _pendingConfirmedOutbounds = new List<SalesOutbound>();
            readonly List<PurchaseInbound> _pendingConfirmedInbounds = new List<PurchaseInbound>();
            readonly Dictionary<string, StockAgg> _stockMap;

            public ImportBatchContext()
            {
                _stockMap = BuildStockMap();
            }

            public decimal GetPendingOutboundQty(string salesOrderId, string salesOrderLineId = null, string excludeOutboundId = null)
            {
                if (string.IsNullOrWhiteSpace(salesOrderId)) return 0;
                return _pendingConfirmedOutbounds
                    .Where(x => IsConfirmedStatus(x.Status)
                        && string.Equals(x.SalesOrderId ?? "", salesOrderId, StringComparison.OrdinalIgnoreCase)
                        && (string.IsNullOrWhiteSpace(salesOrderLineId) || string.Equals(x.SalesOrderLineId ?? "", salesOrderLineId, StringComparison.OrdinalIgnoreCase))
                        && (excludeOutboundId == null || !string.Equals(x.Id, excludeOutboundId, StringComparison.OrdinalIgnoreCase)))
                    .Sum(x => x.Quantity);
            }

            public decimal GetPendingInboundQty(string purchaseOrderId, string purchaseOrderLineId = null, string excludeInboundId = null)
            {
                if (string.IsNullOrWhiteSpace(purchaseOrderId)) return 0;
                return _pendingConfirmedInbounds
                    .Where(x => IsConfirmedStatus(x.Status)
                        && string.Equals(x.PurchaseOrderId ?? "", purchaseOrderId, StringComparison.OrdinalIgnoreCase)
                        && (string.IsNullOrWhiteSpace(purchaseOrderLineId) || string.Equals(x.PurchaseOrderLineId ?? "", purchaseOrderLineId, StringComparison.OrdinalIgnoreCase))
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

        static decimal GetConfirmedOutboundQtyForSalesOrder(string salesOrderId, string salesOrderLineId = null, string excludeOutboundId = null)
        {
            if (string.IsNullOrWhiteSpace(salesOrderId)) return 0;
            return LoadSalesOutbounds()
                .Where(x => IsConfirmedStatus(x.Status)
                    && string.Equals(x.SalesOrderId ?? "", salesOrderId, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(salesOrderLineId) || string.Equals(x.SalesOrderLineId ?? "", salesOrderLineId, StringComparison.OrdinalIgnoreCase))
                    && (excludeOutboundId == null || !string.Equals(x.Id, excludeOutboundId, StringComparison.OrdinalIgnoreCase)))
                .Sum(x => x.Quantity);
        }

        static decimal GetConfirmedInboundQtyForPurchaseOrder(string purchaseOrderId, string purchaseOrderLineId = null, string excludeInboundId = null)
        {
            if (string.IsNullOrWhiteSpace(purchaseOrderId)) return 0;
            return LoadPurchaseInbounds()
                .Where(x => IsConfirmedStatus(x.Status)
                    && string.Equals(x.PurchaseOrderId ?? "", purchaseOrderId, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(purchaseOrderLineId) || string.Equals(x.PurchaseOrderLineId ?? "", purchaseOrderLineId, StringComparison.OrdinalIgnoreCase))
                    && (excludeInboundId == null || !string.Equals(x.Id, excludeInboundId, StringComparison.OrdinalIgnoreCase)))
                .Sum(x => x.Quantity);
        }

        static void ValidateSalesOutboundRemainingQty(SalesOutbound item, string excludeOutboundId = null, ImportBatchContext batch = null, SalesOrder orderOverride = null)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.SalesOrderId)) return;
            var order = orderOverride ?? LoadSalesOrders().FirstOrDefault(x => x.Id == item.SalesOrderId);
            if (order == null) return;
            if (!IsConfirmedStatus(order.Status)) BizFail("来源销售订单尚未确认，不能出库");
            var line = SelectSalesOrderLineForOutbound(order, item);
            string lineId = line == null ? null : line.LineId;
            decimal orderQty = line == null ? order.Quantity : line.Quantity;
            decimal shipped = GetConfirmedOutboundQtyForSalesOrder(item.SalesOrderId, lineId, excludeOutboundId);
            if (batch != null) shipped += batch.GetPendingOutboundQty(item.SalesOrderId, lineId, excludeOutboundId);
            decimal remaining = RoundMoney(orderQty - shipped);
            if (item.Quantity > remaining + 0.0001m)
                BizFail("本次出库数量不能超过未出库数量（剩余 " + remaining.ToString("0.##") + "）");
        }

        static void ValidatePurchaseInboundRemainingQty(PurchaseInbound item, string excludeInboundId = null, ImportBatchContext batch = null, PurchaseOrder orderOverride = null)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.PurchaseOrderId)) return;
            var order = orderOverride ?? LoadPurchaseOrders().FirstOrDefault(x => x.Id == item.PurchaseOrderId);
            if (order == null) return;
            if (!IsConfirmedStatus(order.Status)) BizFail("来源采购单尚未确认，不能入库");
            var line = SelectPurchaseOrderLineForInbound(order, item);
            string lineId = line == null ? null : line.LineId;
            decimal orderQty = line == null ? order.Quantity : line.Quantity;
            decimal received = GetConfirmedInboundQtyForPurchaseOrder(item.PurchaseOrderId, lineId, excludeInboundId);
            if (batch != null) received += batch.GetPendingInboundQty(item.PurchaseOrderId, lineId, excludeInboundId);
            decimal remaining = RoundMoney(orderQty - received);
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
