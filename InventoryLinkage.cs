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
                string display = !string.IsNullOrWhiteSpace(mname) ? mname : (materialName ?? "未知物料");
                BizFail(string.Format("物料「{0}」库存不足，当前可用 {1}，需要 {2}", display, RoundMoney(available), RoundMoney(requiredQty)), 409);
            }
        }

        static void ValidateStockForConfirmedOutbound(SalesOutbound item, string excludeId = null)
        {
            if (item == null || !IsConfirmedStatus(item.Status)) return;
            EnsureMaterialStockAvailable(item.Quantity, item.MaterialId, item.MaterialCode, item.MaterialName,
                new StockMapOptions { ExcludeSalesOutboundId = excludeId });
        }

        static void ValidateStockForConfirmedPick(ProductionPick item, string excludeId = null)
        {
            if (item == null || !IsConfirmedStatus(item.Status)) return;
            EnsureMaterialStockAvailable(item.Quantity, item.MaterialId, item.MaterialCode, item.MaterialName,
                new StockMapOptions { ExcludeProductionPickId = excludeId });
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
                var item = orders.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("销售订单不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                item.CustomerId = input.CustomerId; item.CustomerCode = input.CustomerCode; item.CustomerName = input.CustomerName;
                item.CustomerContact = input.CustomerContact; item.CustomerPhone = input.CustomerPhone; item.CustomerAddress = input.CustomerAddress;
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
                var item = orders.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("销售订单不存在", 404);
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
                var item = orders.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("采购单不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
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
                var item = orders.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("采购单不存在", 404);
                RemoveAutoPayableForPurchaseOrder(id, payables);
                orders.Remove(item);
                WriteJsonListCore(PurchaseOrdersFile, "purchase_orders", orders);
                WriteJsonListCore(PayablesFile, "payables", payables);
            });
        }
    }
}
