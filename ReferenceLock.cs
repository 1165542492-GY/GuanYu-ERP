using System;
using System.Collections.Generic;
using System.Linq;

namespace SupplierErpApp
{
    public static partial class Program
    {
        const string ReferenceLockMessage = "该数据已被业务单据引用，为保证历史数据、库存、成本和财务口径准确，不允许修改或删除。如需变更，请新建新的档案或新版本用于后续业务。";

        static bool MatchesMaterialRef(string docMaterialId, string docMaterialCode, string materialId, string materialCode)
        {
            if (!string.IsNullOrWhiteSpace(materialId) && string.Equals((docMaterialId ?? "").Trim(), materialId, StringComparison.Ordinal))
                return true;
            if (!string.IsNullOrWhiteSpace(materialCode) && string.Equals((docMaterialCode ?? "").Trim(), materialCode, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        static bool IsMaterialReferenced(string materialId, string materialCode)
        {
            if (IsMaterialUsedByBom(materialId, materialCode)) return true;
            if (LoadSalesOrders().Any(x => MatchesMaterialRef(x.MaterialId, x.MaterialCode, materialId, materialCode))) return true;
            if (LoadSalesOutbounds().Any(x => MatchesMaterialRef(x.MaterialId, x.MaterialCode, materialId, materialCode))) return true;
            if (LoadPurchaseOrders().Any(x => MatchesMaterialRef(x.MaterialId, x.MaterialCode, materialId, materialCode))) return true;
            if (LoadPurchaseInbounds().Any(x => MatchesMaterialRef(x.MaterialId, x.MaterialCode, materialId, materialCode))) return true;
            if (LoadProductionPicks().Any(x => MatchesMaterialRef(x.MaterialId, x.MaterialCode, materialId, materialCode))) return true;
            decimal stockQty = GetMaterialAvailableQty(materialId, materialCode, null);
            if (Math.Abs(stockQty) > 0.0001m) return true;
            return false;
        }

        static void EnsureMaterialReferenceLockForEdit(Material existing, Material input)
        {
            if (existing == null || input == null || !IsMaterialReferenced(existing.Id, existing.Code)) return;
            bool keyChanged =
                !string.Equals(existing.Supplier ?? "", input.Supplier ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.NameSpec ?? "", input.NameSpec ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.QuantityUnit ?? "", input.QuantityUnit ?? "", StringComparison.OrdinalIgnoreCase) ||
                existing.TaxPrice != input.TaxPrice ||
                existing.NoTaxPrice != input.NoTaxPrice ||
                !string.Equals(NormalizePriceType(existing.PriceType), NormalizePriceType(input.PriceType), StringComparison.OrdinalIgnoreCase);
            if (keyChanged) BizFail(ReferenceLockMessage, 409);
        }

        static void EnsureMaterialReferenceLockForDelete(Material item)
        {
            if (item == null) return;
            if (IsMaterialReferenced(item.Id, item.Code))
            {
                if (IsMaterialUsedByBom(item.Id, item.Code))
                    BizFail(FormatMaterialDeleteBlockedMessage(GetBomsUsingMaterial(item.Id, item.Code)), 409);
                BizFail(ReferenceLockMessage, 409);
            }
        }

        static bool MatchesCustomerRef(string docCustomerId, string docCustomerCode, string docCustomerName, string customerId, string customerCode, string customerCompany)
        {
            if (!string.IsNullOrWhiteSpace(customerId) && string.Equals((docCustomerId ?? "").Trim(), customerId, StringComparison.Ordinal))
                return true;
            if (!string.IsNullOrWhiteSpace(customerCode) && string.Equals((docCustomerCode ?? "").Trim(), customerCode, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrWhiteSpace(customerCompany) && string.Equals((docCustomerName ?? "").Trim(), customerCompany, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        static bool IsCustomerReferenced(string customerId, string customerCode, string customerCompany)
        {
            if (LoadSalesOrders().Any(x => MatchesCustomerRef(x.CustomerId, x.CustomerCode, x.CustomerName, customerId, customerCode, customerCompany))) return true;
            if (LoadSalesOutbounds().Any(x => string.Equals((x.CustomerName ?? "").Trim(), customerCompany ?? "", StringComparison.OrdinalIgnoreCase))) return true;
            if (LoadReceivables().Any(x => string.Equals((x.CustomerName ?? "").Trim(), customerCompany ?? "", StringComparison.OrdinalIgnoreCase))) return true;
            if (LoadContracts().Any(x =>
                string.Equals((x.PartyBName ?? "").Trim(), customerCompany ?? "", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(customerCode) && string.Equals((x.CustomerCode ?? "").Trim(), customerCode, StringComparison.OrdinalIgnoreCase)))) return true;
            return false;
        }

        static void EnsureCustomerReferenceLockForEdit(Customer existing, Customer input)
        {
            if (existing == null || input == null || !IsCustomerReferenced(existing.Id, existing.Code, existing.Company)) return;
            bool keyChanged =
                !string.Equals(existing.Company ?? "", input.Company ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Contact ?? "", input.Contact ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Phone ?? "", input.Phone ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Bank ?? "", input.Bank ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Account ?? "", input.Account ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.BankNo ?? "", input.BankNo ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Address ?? "", input.Address ?? "", StringComparison.OrdinalIgnoreCase);
            if (keyChanged) BizFail(ReferenceLockMessage, 409);
        }

        static void EnsureCustomerReferenceLockForDelete(Customer item)
        {
            if (item == null) return;
            if (IsCustomerReferenced(item.Id, item.Code, item.Company))
                BizFail(ReferenceLockMessage, 409);
        }

        static bool IsSupplierReferencedByBusiness(string company)
        {
            if (string.IsNullOrWhiteSpace(company)) return false;
            if (IsSupplierUsedByMaterial(company)) return true;
            if (LoadPurchaseOrders().Any(x => string.Equals(x.SupplierName ?? "", company, StringComparison.OrdinalIgnoreCase))) return true;
            if (LoadPurchaseInbounds().Any(x => string.Equals(x.SupplierName ?? "", company, StringComparison.OrdinalIgnoreCase))) return true;
            if (LoadPayables().Any(x => string.Equals(x.SupplierName ?? "", company, StringComparison.OrdinalIgnoreCase))) return true;
            return false;
        }

        static void EnsureSupplierReferenceLockForEdit(Supplier existing, Supplier input)
        {
            if (existing == null || input == null || !IsSupplierReferencedByBusiness(existing.Company)) return;
            bool keyChanged =
                !string.Equals(existing.Company ?? "", input.Company ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Contact ?? "", input.Contact ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Phone ?? "", input.Phone ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Goods ?? "", input.Goods ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Address ?? "", input.Address ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Bank ?? "", input.Bank ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Account ?? "", input.Account ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.BankNo ?? "", input.BankNo ?? "", StringComparison.OrdinalIgnoreCase);
            if (keyChanged) BizFail(ReferenceLockMessage, 409);
        }

        static void EnsureSalesOrderReferenceLockForEdit(SalesOrder existing, SalesOrder input, List<SalesOutbound> outbounds, List<Receivable> receivables)
        {
            if (existing == null || input == null) return;
            bool hasOutbound = outbounds.Any(x => x.SalesOrderId == existing.Id);
            bool hasAutoReceivable = receivables.Any(x => x.SalesOrderId == existing.Id);
            if (!hasOutbound && !hasAutoReceivable) return;
            bool keyChanged =
                !string.Equals(existing.CustomerId ?? "", input.CustomerId ?? "", StringComparison.Ordinal) ||
                !string.Equals(existing.MaterialId ?? "", input.MaterialId ?? "", StringComparison.Ordinal) ||
                !string.Equals(existing.MaterialCode ?? "", input.MaterialCode ?? "", StringComparison.OrdinalIgnoreCase) ||
                existing.Quantity != input.Quantity ||
                existing.TaxExcludedSalePrice != input.TaxExcludedSalePrice ||
                existing.TaxIncludedSalePrice != input.TaxIncludedSalePrice ||
                existing.Amount != input.Amount;
            if (keyChanged) BizFail(ReferenceLockMessage, 409);
        }

        static void EnsurePurchaseOrderReferenceLockForEdit(PurchaseOrder existing, PurchaseOrder input, List<PurchaseInbound> inbounds, List<Payable> payables)
        {
            if (existing == null || input == null) return;
            bool hasInbound = inbounds.Any(x => x.PurchaseOrderId == existing.Id);
            bool hasAutoPayable = payables.Any(x => x.PurchaseOrderId == existing.Id);
            if (!hasInbound && !hasAutoPayable) return;
            bool keyChanged =
                !string.Equals(existing.SupplierName ?? "", input.SupplierName ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.MaterialId ?? "", input.MaterialId ?? "", StringComparison.Ordinal) ||
                !string.Equals(existing.MaterialCode ?? "", input.MaterialCode ?? "", StringComparison.OrdinalIgnoreCase) ||
                existing.Quantity != input.Quantity ||
                existing.UnitPrice != input.UnitPrice ||
                existing.Amount != input.Amount;
            if (keyChanged) BizFail(ReferenceLockMessage, 409);
        }

        static void EnsureConfirmedInventoryDocDeleteBlocked(string status, string docLabel)
        {
            if (IsConfirmedStatus(status))
                BizFail("已确认的" + docLabel + "会影响库存或财务数据，不允许直接删除。如需调整，请先取消确认或联系管理员处理。", 409);
        }

        static void EnsureConfirmedInventoryDocEditBlocked(string existingStatus, string inputStatus, string docLabel)
        {
            if (!IsConfirmedStatus(existingStatus)) return;
            if (!IsConfirmedStatus(inputStatus) || existingStatus != inputStatus)
                BizFail("已确认的" + docLabel + "会影响库存或财务数据，不允许修改状态或关键数量。如需调整，请新建单据。", 409);
        }

        static void EnsureBomReferenceLockForEdit(string bomId, BomItem existing, BomItem input)
        {
            if (existing == null || input == null || !IsBomUsedByModelCost(bomId)) return;
            bool keyChanged =
                !string.Equals(existing.ModelCode ?? "", input.ModelCode ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.ModelName ?? "", input.ModelName ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.ProductName ?? "", input.ProductName ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Version ?? "", input.Version ?? "", StringComparison.OrdinalIgnoreCase) ||
                (existing.Items ?? new List<BomDetail>()).Count != (input.Items ?? new List<BomDetail>()).Count ||
                !BomItemsEquivalent(existing.Items, input.Items);
            if (keyChanged) BizFail(ReferenceLockMessage, 409);
        }

        static bool BomItemsEquivalent(List<BomDetail> a, List<BomDetail> b)
        {
            a = a ?? new List<BomDetail>();
            b = b ?? new List<BomDetail>();
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                var x = a[i]; var y = b[i];
                if (!string.Equals(x.MaterialId ?? "", y.MaterialId ?? "", StringComparison.Ordinal)) return false;
                if (!string.Equals(x.MaterialCode ?? "", y.MaterialCode ?? "", StringComparison.OrdinalIgnoreCase)) return false;
                if (x.Quantity != y.Quantity) return false;
                if (x.OriginalPrice != y.OriginalPrice) return false;
                if (x.NoTaxPrice != y.NoTaxPrice) return false;
            }
            return true;
        }

    }
}
