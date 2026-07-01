using System;
using System.Collections.Generic;
using System.Linq;

namespace SupplierErpApp
{
    public static partial class Program
    {
        class StockMapOptions
        {
            public string ExcludeSalesOutboundId;
            public string ExcludeProductionPickId;
            public string ExcludeAfterSalesServiceOrderId;
        }

        class InventoryImpactLine
        {
            public string ItemType;
            public string ItemId;
            public string ItemCode;
            public string ItemName;
            public string Spec;
            public string Unit;
            public decimal Quantity;
            public decimal UnitCost;
            public string LineId;
            public int LineNo;
            public string ActionType;
            public string WarehouseName;
        }

        static List<InventoryImpactLine> GetSalesOutboundStockImpacts(SalesOutbound item)
        {
            var rows = new List<InventoryImpactLine>();
            if (item == null || !IsConfirmedStatus(item.Status)) return rows;
            foreach (var line in SalesOutboundLinesForUse(item))
            {
                if (line == null) continue;
                decimal qty = SalesOutboundLineFinalQuantity(line);
                if (qty <= 0) continue;
                if (IsFinishedProductOutboundLine(item, line))
                {
                    string pid = GetFinishedProductStockId(line.ModelCostId, line.BomId);
                    rows.Add(new InventoryImpactLine
                    {
                        ItemType = "成品",
                        ItemId = pid,
                        ItemCode = line.BomCode ?? "",
                        ItemName = line.MaterialName ?? "",
                        Spec = line.Spec ?? "",
                        Unit = string.IsNullOrWhiteSpace(line.Unit) ? "台" : line.Unit,
                        Quantity = -qty,
                        UnitCost = line.CostPrice,
                        LineId = line.Id ?? "",
                        LineNo = line.LineNo,
                        ActionType = NormalizeSalesOutboundLineType(line.LineType),
                        WarehouseName = line.WarehouseName ?? ""
                    });
                }
                else
                {
                    string mid, mcode, mname, mspec, munit;
                    ResolveMaterialFields(line.MaterialId, line.MaterialName, out mid, out mcode, out mname, out mspec, out munit);
                    if (!string.IsNullOrWhiteSpace(line.MaterialCode)) mcode = line.MaterialCode;
                    rows.Add(new InventoryImpactLine
                    {
                        ItemType = "物料",
                        ItemId = mid,
                        ItemCode = mcode,
                        ItemName = mname,
                        Spec = string.IsNullOrWhiteSpace(line.Spec) ? mspec : line.Spec,
                        Unit = string.IsNullOrWhiteSpace(line.Unit) ? munit : line.Unit,
                        Quantity = -qty,
                        UnitCost = line.CostPrice,
                        LineId = line.Id ?? "",
                        LineNo = line.LineNo,
                        ActionType = NormalizeSalesOutboundLineType(line.LineType),
                        WarehouseName = line.WarehouseName ?? ""
                    });
                }
            }
            return rows;
        }

        static List<InventoryImpactLine> GetAfterSalesServiceStockImpacts(AfterSalesServiceOrder item)
        {
            var rows = new List<InventoryImpactLine>();
            if (item == null || !AfterSalesServiceInventoryActive(item) || item.Parts == null) return rows;
            foreach (var line in item.Parts)
            {
                if (line == null || !AfterSalesPartHasInventoryFields(line)) continue;
                decimal finalUsed = ComputeAfterSalesPartFinalUsedQuantity(line);
                if (finalUsed == 0) continue;
                string mid, mcode, mname, mspec, munit;
                ResolveMaterialFields(line.MaterialId, line.MaterialName, out mid, out mcode, out mname, out mspec, out munit);
                if (!string.IsNullOrWhiteSpace(line.MaterialCode)) mcode = line.MaterialCode;
                rows.Add(new InventoryImpactLine
                {
                    ItemType = "物料",
                    ItemId = mid,
                    ItemCode = mcode,
                    ItemName = mname,
                    Spec = string.IsNullOrWhiteSpace(line.Spec) ? mspec : line.Spec,
                    Unit = string.IsNullOrWhiteSpace(line.Unit) ? munit : line.Unit,
                    Quantity = -finalUsed,
                    UnitCost = line.CostPrice > 0 ? line.CostPrice : line.UnitPrice,
                    LineId = line.Id ?? "",
                    LineNo = line.LineNo,
                    ActionType = NormalizeAfterSalesPartActionType(line.ActionType),
                    WarehouseName = line.WarehouseName ?? ""
                });
            }
            return rows;
        }

        static bool IsAutoSource(string sourceType)
        {
            return string.Equals(sourceType ?? "", "auto", StringComparison.OrdinalIgnoreCase);
        }

        static decimal GetMaterialAvailableQty(string materialId, string materialCode, string materialName, StockMapOptions options = null)
        {
            var map = BuildStockMap(options);
            string mid, mcode, mname, mspec, munit;
            ResolveMaterialFields(materialId, materialName, out mid, out mcode, out mname, out mspec, out munit);
            if (!string.IsNullOrWhiteSpace(materialCode)) mcode = materialCode;
            string key = StockKey("物料", mid, mname);
            StockAgg agg;
            return map.TryGetValue(key, out agg) ? agg.Quantity : 0;
        }

        static void EnsureMaterialStockAvailable(decimal requiredQty, string materialId, string materialCode, string materialName, StockMapOptions options = null)
        {
            if (requiredQty <= 0) return;
            decimal available = GetMaterialAvailableQty(materialId, materialCode, materialName, options);
            if (available + 0.0001m < requiredQty)
            {
                string mid, mcode, mname, mspec, munit;
                ResolveMaterialFields(materialId, materialName, out mid, out mcode, out mname, out mspec, out munit);
                string display = !string.IsNullOrWhiteSpace(mcode) ? mcode + " " + mname : (!string.IsNullOrWhiteSpace(mname) ? mname : (materialName ?? "未知物料"));
                if (available <= 0.0001m)
                {
                    string zeroMessage = string.Format("该物料「{0}」当前库存为 0，不能出库/领用", display);
                    AuditInventoryProtectionBlock(zeroMessage);
                    BizFail(zeroMessage, 409);
                }
                string message = string.Format("物料「{0}」库存不足，当前可用 {1}，需要 {2}", display, RoundMoney(available), RoundMoney(requiredQty));
                AuditInventoryProtectionBlock(message);
                BizFail(message, 409);
            }
        }

        static decimal GetFinishedProductAvailableQty(string modelCostId, string bomId, string productName, StockMapOptions options = null)
        {
            var map = BuildStockMap(options);
            string pid = GetFinishedProductStockId(modelCostId, bomId);
            if (string.IsNullOrWhiteSpace(pid)) return 0;
            string key = StockKey("成品", pid, productName ?? "");
            StockAgg agg;
            return map.TryGetValue(key, out agg) ? agg.Quantity : 0;
        }

        static void EnsureFinishedProductStockAvailable(decimal requiredQty, string modelCostId, string bomId, string productName, StockMapOptions options = null)
        {
            if (requiredQty <= 0) return;
            decimal available = GetFinishedProductAvailableQty(modelCostId, bomId, productName, options);
            if (available + 0.0001m < requiredQty)
            {
                string display = !string.IsNullOrWhiteSpace(productName) ? productName : "成品";
                if (available <= 0.0001m)
                {
                    string zeroMessage = string.Format("该成品「{0}」当前库存为 0，不能出库", display);
                    AuditInventoryProtectionBlock(zeroMessage);
                    BizFail(zeroMessage, 409);
                }
                string message = string.Format("成品「{0}」库存不足，当前可用 {1}，需要 {2}", display, RoundMoney(available), RoundMoney(requiredQty));
                AuditInventoryProtectionBlock(message);
                BizFail(message, 409);
            }
        }

        static void ValidateStockForConfirmedOutbound(SalesOutbound item, string excludeId = null)
        {
            if (item == null || !IsConfirmedStatus(item.Status)) return;
            var options = new StockMapOptions { ExcludeSalesOutboundId = excludeId };
            var impacts = GetSalesOutboundStockImpacts(item);
            foreach (var group in impacts.Where(x => x.Quantity < 0).GroupBy(x => StockKey(x.ItemType, x.ItemId, x.ItemName)))
            {
                var first = group.First();
                decimal required = -group.Sum(x => x.Quantity);
                if (first.ItemType == "成品") EnsureFinishedProductStockAvailable(required, first.ItemId, "", first.ItemName, options);
                else EnsureMaterialStockAvailable(required, first.ItemId, first.ItemCode, first.ItemName, options);
            }
        }

        static void ValidateStockForConfirmedPick(ProductionPick item, string excludeId = null)
        {
            if (item == null || !IsConfirmedStatus(item.Status)) return;
            EnsureMaterialStockAvailable(item.Quantity, item.MaterialId, item.MaterialCode, item.MaterialName,
                new StockMapOptions { ExcludeProductionPickId = excludeId });
        }

        static void ValidateStockForAfterSalesServiceOrder(AfterSalesServiceOrder item, string excludeId = null)
        {
            if (item == null || !AfterSalesServiceInventoryActive(item)) return;
            var options = new StockMapOptions { ExcludeAfterSalesServiceOrderId = excludeId };
            foreach (var group in GetAfterSalesServiceStockImpacts(item).Where(x => x.Quantity < 0).GroupBy(x => StockKey(x.ItemType, x.ItemId, x.ItemName)))
            {
                var first = group.First();
                decimal required = -group.Sum(x => x.Quantity);
                EnsureMaterialStockAvailable(required, first.ItemId, first.ItemCode, first.ItemName, options);
            }
        }

        static decimal GetPositiveSourceRollbackQty(decimal currentQty, string currentStatus, decimal nextQty, string nextStatus)
        {
            if (!IsConfirmedStatus(currentStatus)) return 0;
            if (!IsConfirmedStatus(nextStatus)) return currentQty;
            var rollbackQty = currentQty - nextQty;
            return rollbackQty > 0 ? rollbackQty : 0;
        }

        static void EnsureMaterialStockRollbackAvailable(decimal rollbackQty, string materialId, string materialCode, string materialName, string docName)
        {
            if (rollbackQty <= 0) return;
            decimal available = GetMaterialAvailableQty(materialId, materialCode, materialName);
            if (available + 0.0001m >= rollbackQty) return;

            string mid, mcode, mname, mspec, munit;
            ResolveMaterialFields(materialId, materialName, out mid, out mcode, out mname, out mspec, out munit);
            if (!string.IsNullOrWhiteSpace(materialCode)) mcode = materialCode;
            string display = !string.IsNullOrWhiteSpace(mcode) ? mcode + " " + mname : (!string.IsNullOrWhiteSpace(mname) ? mname : (materialName ?? "未知物料"));
            string message = string.Format("物料「{0}」库存不足，不能回滚{1}。当前可用 {2}，需要回滚 {3}", display, docName, RoundMoney(available), RoundMoney(rollbackQty));
            AuditInventoryProtectionBlock(message);
            BizFail(message, 409);
        }

        static void EnsureFinishedProductStockRollbackAvailable(decimal rollbackQty, string modelCostId, string bomId, string productName, string docName)
        {
            if (rollbackQty <= 0) return;
            decimal available = GetFinishedProductAvailableQty(modelCostId, bomId, productName);
            if (available + 0.0001m >= rollbackQty) return;

            string display = !string.IsNullOrWhiteSpace(productName) ? productName : "成品";
            string message = string.Format("成品「{0}」库存不足，不能回滚{1}。当前可用 {2}，需要回滚 {3}", display, docName, RoundMoney(available), RoundMoney(rollbackQty));
            AuditInventoryProtectionBlock(message);
            BizFail(message, 409);
        }

        static void ValidatePurchaseInboundRollbackStock(PurchaseInbound current, PurchaseInbound next = null)
        {
            if (current == null) return;
            var nextQty = next == null ? 0 : next.Quantity;
            var nextStatus = next == null ? "" : next.Status;
            var rollbackQty = GetPositiveSourceRollbackQty(current.Quantity, current.Status, nextQty, nextStatus);
            EnsureMaterialStockRollbackAvailable(rollbackQty, current.MaterialId, current.MaterialCode, current.MaterialName, "采购入库");
        }

        static void ValidatePurchaseInboundRollbackStock(System.Collections.Generic.IEnumerable<PurchaseInbound> items)
        {
            if (items == null) return;
            var rows = new System.Collections.Generic.List<Tuple<string, string, string, string, decimal>>();
            foreach (var item in items)
            {
                if (item == null || !IsConfirmedStatus(item.Status)) continue;
                string mid, mcode, mname, mspec, munit;
                ResolveMaterialFields(item.MaterialId, item.MaterialName, out mid, out mcode, out mname, out mspec, out munit);
                if (!string.IsNullOrWhiteSpace(item.MaterialCode)) mcode = item.MaterialCode;
                rows.Add(Tuple.Create(StockKey("物料", mid, mname), mid, mcode, mname, item.Quantity));
            }
            foreach (var group in rows.GroupBy(x => x.Item1))
            {
                var first = group.First();
                EnsureMaterialStockRollbackAvailable(group.Sum(x => x.Item5), first.Item2, first.Item3, first.Item4, "采购入库");
            }
        }

        static void ValidateFinishedInboundRollbackStock(FinishedInbound current, FinishedInbound next = null)
        {
            if (current == null) return;
            var nextQty = next == null ? 0 : next.Quantity;
            var nextStatus = next == null ? "" : next.Status;
            var rollbackQty = GetPositiveSourceRollbackQty(current.Quantity, current.Status, nextQty, nextStatus);
            EnsureFinishedProductStockRollbackAvailable(rollbackQty, current.ModelCostId, current.BomId, current.ProductName, "成品入库");
        }

        static void SyncAutoReceivableInMemory(SalesOrder order, List<Receivable> receivables, UserSession user)
        {
            if (order == null || string.IsNullOrWhiteSpace(order.Id)) return;
            var auto = receivables.FirstOrDefault(x => x.SalesOrderId == order.Id && IsAutoSource(x.SourceType));
            if (!IsConfirmedStatus(order.Status))
            {
                if (auto != null)
                {
                    if (auto.ReceivedAmount > 0) BizFail("该销售订单关联的自动应收款已有收款，不能取消或改回草稿");
                    receivables.Remove(auto);
                }
                return;
            }
            if (auto == null)
            {
                auto = new Receivable
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Code = NextCode(ReceivableSequenceFile, "AR", receivables.Select(x => x.Code), "YS"),
                    SalesOrderId = order.Id,
                    SalesOrderNo = order.Code,
                    CustomerName = order.CustomerName,
                    ReceivableAmount = order.Amount,
                    ReceivedAmount = 0,
                    SourceType = "auto",
                    Note = "由销售订单自动生成",
                    UpdatedAt = BizUpdatedAtNow(),
                    UpdatedBy = user.DisplayName
                };
                ApplyReceivable(auto);
                receivables.Insert(0, auto);
                return;
            }
            auto.SalesOrderNo = order.Code;
            auto.CustomerName = order.CustomerName;
            auto.ReceivableAmount = order.Amount;
            ApplyReceivable(auto);
            auto.UpdatedAt = BizUpdatedAtNow();
            auto.UpdatedBy = user.DisplayName;
        }

        static void RemoveAutoReceivableForSalesOrder(string salesOrderId, List<Receivable> receivables)
        {
            var auto = receivables.FirstOrDefault(x => x.SalesOrderId == salesOrderId && IsAutoSource(x.SourceType));
            if (auto == null) return;
            if (auto.ReceivedAmount > 0) BizFail("该销售订单关联的自动应收款已有收款，不能删除");
            receivables.Remove(auto);
        }

        static void SyncAutoPayableInMemory(PurchaseOrder order, List<Payable> payables, UserSession user)
        {
            if (order == null || string.IsNullOrWhiteSpace(order.Id)) return;
            var auto = payables.FirstOrDefault(x => x.PurchaseOrderId == order.Id && IsAutoSource(x.SourceType));
            if (!IsConfirmedStatus(order.Status))
            {
                if (auto != null)
                {
                    if (auto.PaidAmount > 0) BizFail("该采购单关联的自动应付款已有付款，不能取消或改回草稿");
                    payables.Remove(auto);
                }
                return;
            }
            if (auto == null)
            {
                auto = new Payable
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Code = NextCode(PayableSequenceFile, "AP", payables.Select(x => x.Code), "YF"),
                    PurchaseOrderId = order.Id,
                    PurchaseNo = order.Code,
                    SupplierName = order.SupplierName,
                    PayableAmount = order.Amount,
                    PaidAmount = 0,
                    SourceType = "auto",
                    Note = "由采购单自动生成",
                    UpdatedAt = BizUpdatedAtNow(),
                    UpdatedBy = user.DisplayName
                };
                ApplyPayable(auto);
                payables.Insert(0, auto);
                return;
            }
            auto.PurchaseNo = order.Code;
            auto.SupplierName = order.SupplierName;
            auto.PayableAmount = order.Amount;
            ApplyPayable(auto);
            auto.UpdatedAt = BizUpdatedAtNow();
            auto.UpdatedBy = user.DisplayName;
        }

        static void RemoveAutoPayableForPurchaseOrder(string purchaseOrderId, List<Payable> payables)
        {
            var auto = payables.FirstOrDefault(x => x.PurchaseOrderId == purchaseOrderId && IsAutoSource(x.SourceType));
            if (auto == null) return;
            if (auto.PaidAmount > 0) BizFail("该采购单关联的自动应付款已有付款，不能删除");
            payables.Remove(auto);
        }

        static SalesOrder PersistSalesOrderAdd(SalesOrder item, UserSession user)
        {
            SalesOrder saved = null;
            RunUnderDataLock(() =>
            {
                var orders = ReadJsonListCore<SalesOrder>(SalesOrdersFile);
                var receivables = ReadJsonListCore<Receivable>(ReceivablesFile);
                item.Id = Guid.NewGuid().ToString("N");
                item.Code = NextCode(SalesOrderSequenceFile, "SO", orders.Select(x => x.Code), "XSDD");
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                orders.Insert(0, item);
                SyncAutoReceivableInMemory(item, receivables, user);
                WriteJsonListCore(SalesOrdersFile, "sales_orders", orders);
                WriteJsonListCore(ReceivablesFile, "receivables", receivables);
                saved = item;
            });
            return saved;
        }

        static SalesOrder PersistSalesOrderUpdate(string id, SalesOrder input, UserSession user)
        {
            SalesOrder saved = null;
            RunUnderDataLock(() =>
            {
                var orders = ReadJsonListCore<SalesOrder>(SalesOrdersFile);
                var receivables = ReadJsonListCore<Receivable>(ReceivablesFile);
                var outbounds = ReadJsonListCore<SalesOutbound>(SalesOutboundsFile);
                var item = orders.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("销售订单不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                EnsureSalesOrderReferenceLockForEdit(item, input, outbounds, receivables);
                item.CustomerId = input.CustomerId; item.CustomerCode = input.CustomerCode; item.CustomerName = input.CustomerName;
                item.CustomerContact = input.CustomerContact; item.CustomerPhone = input.CustomerPhone; item.CustomerAddress = input.CustomerAddress;
                item.ItemType = input.ItemType; item.ModelCostId = input.ModelCostId; item.BomId = input.BomId;
                item.MaterialId = input.MaterialId; item.MaterialCode = input.MaterialCode;
                item.MaterialName = input.MaterialName; item.Quantity = input.Quantity;
                item.TaxExcludedSalePrice = input.TaxExcludedSalePrice; item.TaxIncludedSalePrice = input.TaxIncludedSalePrice;
                item.TaxExcludedSaleAmount = input.TaxExcludedSaleAmount; item.TaxIncludedSaleAmount = input.TaxIncludedSaleAmount;
                item.UnitPrice = input.UnitPrice; item.Amount = input.Amount; item.OrderDate = input.OrderDate;
                item.Status = input.Status; item.Note = input.Note; item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                SyncAutoReceivableInMemory(item, receivables, user);
                WriteJsonListCore(SalesOrdersFile, "sales_orders", orders);
                WriteJsonListCore(ReceivablesFile, "receivables", receivables);
                saved = item;
            });
            return saved;
        }

        static void PersistSalesOrderDelete(string id, UserSession user)
        {
            RunUnderDataLock(() =>
            {
                var orders = ReadJsonListCore<SalesOrder>(SalesOrdersFile);
                var receivables = ReadJsonListCore<Receivable>(ReceivablesFile);
                var outbounds = ReadJsonListCore<SalesOutbound>(SalesOutboundsFile);
                var item = orders.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("销售订单不存在", 404);
                var linkedOut = outbounds.Where(x => x.SalesOrderId == id).ToList();
                if (linkedOut.Count > 0)
                {
                    var refs = string.Join("、", linkedOut.Take(3).Select(x => x.Code ?? "").Where(x => x.Length > 0));
                    var msg = "该销售订单已被销售出库引用，请先删除相关出库单后再删除订单。";
                    if (refs.Length > 0) msg += " 引用出库：" + refs + (linkedOut.Count > 3 ? " 等" : "");
                    BizFail(msg, 409);
                }
                RemoveAutoReceivableForSalesOrder(id, receivables);
                orders.Remove(item);
                WriteJsonListCore(SalesOrdersFile, "sales_orders", orders);
                WriteJsonListCore(ReceivablesFile, "receivables", receivables);
            });
        }

        static PurchaseOrder PersistPurchaseOrderAdd(PurchaseOrder item, UserSession user)
        {
            PurchaseOrder saved = null;
            RunUnderDataLock(() =>
            {
                var orders = ReadJsonListCore<PurchaseOrder>(PurchaseOrdersFile);
                var payables = ReadJsonListCore<Payable>(PayablesFile);
                item.Id = Guid.NewGuid().ToString("N");
                item.Code = NextCode(PurchaseOrderSequenceFile, "PO", orders.Select(x => x.Code), "CGDD");
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                orders.Insert(0, item);
                SyncAutoPayableInMemory(item, payables, user);
                WriteJsonListCore(PurchaseOrdersFile, "purchase_orders", orders);
                WriteJsonListCore(PayablesFile, "payables", payables);
                saved = item;
            });
            return saved;
        }

        static PurchaseOrder PersistPurchaseOrderUpdate(string id, PurchaseOrder input, UserSession user)
        {
            PurchaseOrder saved = null;
            RunUnderDataLock(() =>
            {
                var orders = ReadJsonListCore<PurchaseOrder>(PurchaseOrdersFile);
                var payables = ReadJsonListCore<Payable>(PayablesFile);
                var inbounds = ReadJsonListCore<PurchaseInbound>(PurchaseInboundsFile);
                var item = orders.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("采购单不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                EnsurePurchaseOrderReferenceLockForEdit(item, input, inbounds, payables);
                item.SupplierName = input.SupplierName; item.MaterialId = input.MaterialId; item.MaterialCode = input.MaterialCode;
                item.MaterialName = input.MaterialName; item.Quantity = input.Quantity;
                item.UnitPrice = input.UnitPrice; item.Amount = input.Amount; item.OrderDate = input.OrderDate;
                item.Status = input.Status; item.Note = input.Note; item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                SyncAutoPayableInMemory(item, payables, user);
                WriteJsonListCore(PurchaseOrdersFile, "purchase_orders", orders);
                WriteJsonListCore(PayablesFile, "payables", payables);
                saved = item;
            });
            return saved;
        }

        static void PersistPurchaseOrderDelete(string id, UserSession user)
        {
            RunUnderDataLock(() =>
            {
                var orders = ReadJsonListCore<PurchaseOrder>(PurchaseOrdersFile);
                var payables = ReadJsonListCore<Payable>(PayablesFile);
                var inbounds = ReadJsonListCore<PurchaseInbound>(PurchaseInboundsFile);
                var item = orders.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("采购单不存在", 404);
                var linkedIn = inbounds.Where(x => x.PurchaseOrderId == id).ToList();
                if (linkedIn.Count > 0)
                {
                    var refs = string.Join("、", linkedIn.Take(3).Select(x => x.Code ?? "").Where(x => x.Length > 0));
                    var msg = "该采购单已被采购入库引用，请先删除相关入库单后再删除采购单。";
                    if (refs.Length > 0) msg += " 引用入库：" + refs + (linkedIn.Count > 3 ? " 等" : "");
                    BizFail(msg, 409);
                }
                RemoveAutoPayableForPurchaseOrder(id, payables);
                orders.Remove(item);
                WriteJsonListCore(PurchaseOrdersFile, "purchase_orders", orders);
                WriteJsonListCore(PayablesFile, "payables", payables);
            });
        }
    }
}
