using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Nodes;

namespace SupplierErpApp
{
    public class OperationImpactItemDto
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public int Count { get; set; }
        public string Description { get; set; }
    }

    public class OperationImpactResultDto
    {
        public bool Allow { get; set; }
        public bool NeedConfirm { get; set; }
        public string Level { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string ObjectLabel { get; set; }
        public string[] BlockingReasons { get; set; }
        public string[] Warnings { get; set; }
        public OperationImpactItemDto[] ImpactItems { get; set; }
        public string ConfirmText { get; set; }
    }

    public class OperationImpactPreviewRequest
    {
        public string Module { get; set; }
        public string Operation { get; set; }
        public string Id { get; set; }
        public JsonObject Payload { get; set; }
    }

    public sealed class ImpactBusinessException : Exception
    {
        public int StatusCode { get; private set; }
        public string[] BlockingReasons { get; private set; }
        public OperationImpactItemDto[] ImpactItems { get; private set; }

        public ImpactBusinessException(OperationImpactResultDto result)
            : base(BuildMessage(result))
        {
            StatusCode = result.Allow ? 400 : 409;
            BlockingReasons = result.BlockingReasons ?? new string[0];
            ImpactItems = result.ImpactItems ?? new OperationImpactItemDto[0];
        }

        static string BuildMessage(OperationImpactResultDto r)
        {
            if (r.BlockingReasons != null && r.BlockingReasons.Length > 0) return r.BlockingReasons[0];
            if (!string.IsNullOrWhiteSpace(r.Summary)) return r.Summary;
            return "当前操作不能继续";
        }
    }

    public static partial class Program
    {
        static OperationImpactResultDto NewImpact(string title, string objectLabel = null)
        {
            return new OperationImpactResultDto
            {
                Title = title,
                ObjectLabel = objectLabel ?? "",
                Level = "info",
                Allow = true,
                NeedConfirm = false,
                BlockingReasons = new string[0],
                Warnings = new string[0],
                ImpactItems = new OperationImpactItemDto[0],
                ConfirmText = "确认继续"
            };
        }

        static void AddImpact(List<OperationImpactItemDto> items, string type, string title, int count, string description)
        {
            if (count <= 0 && string.IsNullOrWhiteSpace(description)) return;
            items.Add(new OperationImpactItemDto { Type = type, Title = title, Count = count, Description = description });
        }

        static void FinalizeImpact(OperationImpactResultDto r, List<OperationImpactItemDto> items, List<string> blocking, List<string> warnings)
        {
            r.ImpactItems = items.ToArray();
            r.BlockingReasons = blocking.ToArray();
            r.Warnings = warnings.ToArray();
            r.Allow = blocking.Count == 0;
            if (!r.Allow)
            {
                r.NeedConfirm = false;
                r.Level = "blocked";
                r.ConfirmText = "我知道了";
                r.Summary = blocking[0];
                return;
            }
            if (warnings.Count > 0 || items.Count > 0)
            {
                r.NeedConfirm = true;
                if (r.Level == "info") r.Level = warnings.Any(w => w.IndexOf("危险", StringComparison.Ordinal) >= 0 || w.IndexOf("禁止", StringComparison.Ordinal) >= 0) ? "danger" : "warning";
            }
            else r.NeedConfirm = true;
            if (string.IsNullOrWhiteSpace(r.Summary))
            {
                if (items.Count > 0) r.Summary = "该操作可能影响 " + string.Join("、", items.Select(x => x.Title + (x.Count > 0 ? "（" + x.Count + "）" : "")));
                else r.Summary = "请确认是否继续该操作。";
            }
            if (r.Level == "danger") r.ConfirmText = "确认继续";
            else if (r.Level == "warning") r.ConfirmText = "确认继续";
            else r.ConfirmText = "确认继续";
        }

        static void EnforceOperationImpact(OperationImpactResultDto result, UserSession user, HttpListenerContext ctx, string auditModule, string auditAction, string auditObject)
        {
            if (result == null || result.Allow) return;
            try
            {
                string detail = auditObject + " | 影响预检拦截: " + string.Join("；", result.BlockingReasons ?? new string[0]);
                WriteStructuredOperationLog(user, ctx, "影响预检拦截", detail, "Failed", detail, null, "/api/operation-impact/preview");
            }
            catch { }
            throw new ImpactBusinessException(result);
        }

        static OperationImpactResultDto EvaluateOperationImpact(string module, string operation, string id, JsonObject payload, UserSession user)
        {
            module = (module ?? "").Trim();
            operation = (operation ?? "delete").Trim().ToLowerInvariant();
            id = (id ?? "").Trim();
            switch (module.ToLowerInvariant())
            {
                case "salesorder": return ImpactSalesOrder(operation, id, payload);
                case "salesoutbound": return ImpactSalesOutbound(operation, id, payload);
                case "purchaseorder": return ImpactPurchaseOrder(operation, id, payload);
                case "purchaseinbound": return ImpactPurchaseInbound(operation, id, payload);
                case "productionpick": return ImpactProductionPick(operation, id, payload);
                case "finishedinbound": return ImpactFinishedInbound(operation, id, payload);
                case "receivable": return ImpactReceivable(operation, id, payload);
                case "payable": return ImpactPayable(operation, id, payload);
                case "customer": return ImpactCustomer(operation, id, payload);
                case "supplier": return ImpactSupplier(operation, id, payload);
                case "material": return ImpactMaterial(operation, id, payload);
                case "bom": return ImpactBom(operation, id, payload);
                case "modelcost": return ImpactModelCost(operation, id, payload);
                case "contract": return ImpactContract(operation, id, payload);
                case "contractsetting": return ImpactContractSetting(operation, id, payload);
                case "user": return ImpactUser(operation, id, payload, user);
                case "dictionary": return ImpactDictionary(operation, id, payload);
                case "taxrate": return ImpactTaxRate(operation, id, payload);
                case "cleartestdata": return ImpactClearTestData(operation, id, payload);
                case "clearallbusinessdata": return ImpactClearAllBusinessData(operation, id, payload);
                case "backuprestore": return ImpactBackupRestore(operation, id, payload);
                default: return NewImpact("操作影响预检");
            }
        }

        static OperationImpactResultDto ImpactSalesOrder(string operation, string id, JsonObject payload)
        {
            var order = LoadSalesOrders().FirstOrDefault(x => x.Id == id);
            var label = order != null ? (order.Code + " " + (order.CustomerName ?? "")) : id;
            var r = NewImpact("销售订单影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (order == null && operation == "delete") { blocking.Add("销售订单不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }

            var outbounds = LoadSalesOutbounds().Where(x => x.SalesOrderId == id).ToList();
            var receivables = LoadReceivables().Where(x => x.SalesOrderId == id).ToList();
            var receiptCount = receivables.Sum(x => (x.ReceiptDetails ?? new List<ReceiptDetail>()).Count);
            var receivedAmt = receivables.Sum(x => x.ReceivedAmount);

            if (operation == "delete")
            {
                if (outbounds.Count > 0)
                {
                    blocking.Add("该销售订单已被 " + outbounds.Count + " 条销售出库引用，不能删除。请先处理相关出库单。");
                    AddImpact(items, "salesOutbound", "关联销售出库", outbounds.Count, "删除前需先删除或调整相关出库单");
                }
                if (receiptCount > 0 || receivedAmt > 0)
                {
                    blocking.Add("该销售订单已有收款明细（" + receiptCount + " 条），不能删除。删除会导致应收账款不一致。");
                    AddImpact(items, "receipt", "收款明细", receiptCount, "已有收款记录，不能删除订单");
                }
                else if (receivables.Count > 0)
                {
                    r.Level = "danger";
                    AddImpact(items, "receivable", "关联应收款", receivables.Count, "删除后将同步移除自动应收款");
                    warnings.Add("该销售订单有关联应收款但未收款，删除后应收记录将一并移除。");
                }
                else if (IsConfirmedStatus(order != null ? order.Status : ""))
                {
                    r.Level = "warning";
                    warnings.Add("该销售订单已确认，删除后不可恢复。");
                }
                AddImpact(items, "audit", "操作记录", 1, "将记录本次删除");
            }
            else if (operation == "confirm")
            {
                r.Level = "warning";
                AddImpact(items, "receivable", "应收款", 1, "确认后将生成或更新应收款");
                AddImpact(items, "customer", "客户应收余额", 1, "将影响客户应收余额");
                warnings.Add("保存为「已确认」后，系统将自动生成或更新应收款，并影响客户应收余额。");
            }
            else if (operation == "edit")
            {
                if (order != null && IsConfirmedStatus(order.Status))
                {
                    if (receivables.Any(x => x.ReceivedAmount > 0 || (x.ReceiptDetails != null && x.ReceiptDetails.Count > 0)))
                        blocking.Add("该销售订单已有收款，不能修改金额相关字段。");
                    else if (receivables.Count > 0)
                    {
                        r.Level = "warning";
                        warnings.Add("该订单已确认并有关联应收款，修改金额将影响应收余额。");
                    }
                    if (outbounds.Count > 0)
                    {
                        r.Level = "warning";
                        warnings.Add("该订单已有 " + outbounds.Count + " 条销售出库，修改数量可能导致出库差异。");
                    }
                }
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactSalesOutbound(string operation, string id, JsonObject payload)
        {
            var doc = LoadSalesOutbounds().FirstOrDefault(x => x.Id == id);
            var label = doc != null ? doc.Code : id;
            var r = NewImpact("销售出库影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (operation == "delete")
            {
                if (doc == null) { blocking.Add("销售出库不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }
                if (IsConfirmedStatus(doc.Status))
                {
                    r.Level = "danger";
                    AddImpact(items, "stock", "库存", 1, "删除已确认出库后，库存将回滚增加");
                    warnings.Add("删除已确认的销售出库后，相关物料库存将回滚。");
                    if (!string.IsNullOrWhiteSpace(doc.SalesOrderId))
                        warnings.Add("删除后将影响来源销售订单的已出库数量统计。");
                }
                AddImpact(items, "audit", "操作记录", 1, "将记录本次删除");
            }
            else if (operation == "confirm")
            {
                decimal qty = payload != null && payload["Quantity"] != null ? payload["Quantity"].GetValue<decimal>() : (doc != null ? doc.Quantity : 0);
                string mid = payload != null && payload["MaterialId"] != null ? payload["MaterialId"].GetValue<string>() : (doc != null ? doc.MaterialId : "");
                string mcode = payload != null && payload["MaterialCode"] != null ? payload["MaterialCode"].GetValue<string>() : (doc != null ? doc.MaterialCode : "");
                string mname = payload != null && payload["MaterialName"] != null ? payload["MaterialName"].GetValue<string>() : (doc != null ? doc.MaterialName : "");
                decimal available = GetMaterialAvailableQty(mid, mcode, mname, new StockMapOptions { ExcludeSalesOutboundId = id });
                if (qty > 0 && available + 0.0001m < qty)
                {
                    blocking.Add(string.Format("物料库存不足，当前可用 {0}，需要 {1}，不能确认出库。", RoundMoney(available), RoundMoney(qty)));
                    AddImpact(items, "stock", "库存", 1, "库存不足，禁止确认");
                }
                else
                {
                    r.Level = "warning";
                    AddImpact(items, "stock", "库存", 1, "确认后将减少库存");
                    if (!string.IsNullOrWhiteSpace(doc != null ? doc.SalesOrderId : (payload != null && payload["SalesOrderId"] != null ? payload["SalesOrderId"].GetValue<string>() : "")))
                        AddImpact(items, "salesOrder", "销售订单", 1, "将更新销售订单已出库数量");
                    warnings.Add("确认后将减少库存，并更新关联销售订单的出库统计。");
                }
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactPurchaseOrder(string operation, string id, JsonObject payload)
        {
            var order = LoadPurchaseOrders().FirstOrDefault(x => x.Id == id);
            var label = order != null ? order.Code : id;
            var r = NewImpact("采购单影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (order == null && operation == "delete") { blocking.Add("采购单不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }

            var inbounds = LoadPurchaseInbounds().Where(x => x.PurchaseOrderId == id).ToList();
            var payables = LoadPayables().Where(x => x.PurchaseOrderId == id).ToList();
            var paymentCount = payables.Sum(x => (x.PaymentDetails ?? new List<PaymentDetail>()).Count);
            var paidAmt = payables.Sum(x => x.PaidAmount);

            if (operation == "delete")
            {
                if (inbounds.Count > 0)
                {
                    blocking.Add("该采购单已被 " + inbounds.Count + " 条采购入库引用，不能删除。");
                    AddImpact(items, "purchaseInbound", "关联采购入库", inbounds.Count, "请先删除相关入库单");
                }
                if (paymentCount > 0 || paidAmt > 0)
                {
                    blocking.Add("该采购单已有付款明细，不能删除。");
                    AddImpact(items, "payment", "付款明细", paymentCount, "已有付款记录");
                }
                else if (payables.Count > 0)
                {
                    r.Level = "danger";
                    AddImpact(items, "payable", "关联应付款", payables.Count, "删除后将同步移除自动应付款");
                    warnings.Add("该采购单有关联应付款但未付款，删除后应付记录将一并移除。");
                }
                else if (order != null && IsConfirmedStatus(order.Status))
                {
                    r.Level = "warning";
                    warnings.Add("该采购单已确认，删除后不可恢复。");
                }
                AddImpact(items, "audit", "操作记录", 1, "将记录本次删除");
            }
            else if (operation == "confirm")
            {
                r.Level = "warning";
                AddImpact(items, "payable", "应付款", 1, "确认后将生成或更新应付款");
                AddImpact(items, "supplier", "供应商应付余额", 1, "将影响供应商应付余额");
                warnings.Add("保存为「已确认」后，系统将自动生成或更新应付款。");
            }
            else if (operation == "edit" && order != null && IsConfirmedStatus(order.Status))
            {
                if (payables.Any(x => x.PaidAmount > 0 || (x.PaymentDetails != null && x.PaymentDetails.Count > 0)))
                    blocking.Add("该采购单已有付款，不能修改金额相关字段。");
                else if (payables.Count > 0) warnings.Add("修改金额将影响应付余额。");
                if (inbounds.Count > 0) blocking.Add("该采购单已有采购入库，不能修改数量或金额关键字段。");
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactPurchaseInbound(string operation, string id, JsonObject payload)
        {
            var doc = LoadPurchaseInbounds().FirstOrDefault(x => x.Id == id);
            var label = doc != null ? doc.Code : id;
            var r = NewImpact("采购入库影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (operation == "delete")
            {
                if (doc == null) { blocking.Add("采购入库不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }
                if (IsConfirmedStatus(doc.Status))
                {
                    r.Level = "danger";
                    AddImpact(items, "stock", "库存", 1, "删除已确认入库后，库存将回滚减少");
                    warnings.Add("删除已确认的采购入库后，相关物料库存将回滚。");
                }
                AddImpact(items, "audit", "操作记录", 1, "将记录本次删除");
            }
            else if (operation == "confirm")
            {
                r.Level = "warning";
                AddImpact(items, "stock", "库存", 1, "确认后将增加库存");
                AddImpact(items, "purchaseOrder", "采购单", 1, "将更新采购单已入库数量");
                warnings.Add("确认后将增加库存，并更新关联采购单的入库统计。");
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactProductionPick(string operation, string id, JsonObject payload)
        {
            var doc = LoadProductionPicks().FirstOrDefault(x => x.Id == id);
            var label = doc != null ? doc.Code : id;
            var r = NewImpact("生产领用影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (operation == "delete")
            {
                if (doc == null) { blocking.Add("生产领用不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }
                if (IsConfirmedStatus(doc.Status))
                {
                    r.Level = "danger";
                    AddImpact(items, "stock", "库存", 1, "删除已确认领用后，库存将回滚增加");
                    warnings.Add("删除已确认的生产领用后，相关物料库存将回滚。");
                }
                if (!string.IsNullOrWhiteSpace(doc.BomId))
                    AddImpact(items, "bom", "关联BOM", 1, "该领用关联 BOM 生产用料记录");
            }
            else if (operation == "confirm")
            {
                decimal qty = payload != null && payload["Quantity"] != null ? payload["Quantity"].GetValue<decimal>() : (doc != null ? doc.Quantity : 0);
                string mid = doc != null ? doc.MaterialId : "";
                string mcode = doc != null ? doc.MaterialCode : "";
                string mname = doc != null ? doc.MaterialName : "";
                decimal available = GetMaterialAvailableQty(mid, mcode, mname, new StockMapOptions { ExcludeProductionPickId = id });
                if (qty > 0 && available + 0.0001m < qty)
                {
                    blocking.Add(string.Format("物料库存不足，当前可用 {0}，需要 {1}，不能确认领用。", RoundMoney(available), RoundMoney(qty)));
                }
                else
                {
                    r.Level = "warning";
                    AddImpact(items, "stock", "库存", 1, "确认后将减少库存");
                    warnings.Add("确认后将减少库存。");
                }
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactFinishedInbound(string operation, string id, JsonObject payload)
        {
            var doc = LoadFinishedInbounds().FirstOrDefault(x => x.Id == id);
            var label = doc != null ? doc.Code : id;
            var r = NewImpact("成品入库影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (operation == "delete")
            {
                if (doc == null) { blocking.Add("成品入库不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }
                if (IsConfirmedStatus(doc.Status))
                {
                    r.Level = "danger";
                    AddImpact(items, "stock", "成品库存", 1, "删除已确认入库后，成品库存将回滚");
                    warnings.Add("删除已确认的成品入库后，成品库存将回滚。");
                }
                if (!string.IsNullOrWhiteSpace(doc.ModelCostId))
                    AddImpact(items, "modelCost", "机型成本", 1, "关联机型成本，影响产品成本追溯");
            }
            else if (operation == "confirm")
            {
                r.Level = "warning";
                AddImpact(items, "stock", "成品库存", 1, "确认后将增加成品库存");
                warnings.Add("确认后将使用当前机型成本快照计入成品库存。");
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactReceivable(string operation, string id, JsonObject payload)
        {
            var item = LoadReceivables().FirstOrDefault(x => x.Id == id);
            var label = item != null ? item.Code : id;
            var r = NewImpact("应收款影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (item == null && (operation == "delete" || operation == "edit")) { blocking.Add("应收款不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }

            if (operation == "delete")
            {
                if ((item.ReceiptDetails != null && item.ReceiptDetails.Count > 0) || item.ReceivedAmount > 0)
                {
                    blocking.Add("该应收款已有收款明细或已收金额大于 0，不能删除。");
                    AddImpact(items, "receipt", "收款明细", item.ReceiptDetails != null ? item.ReceiptDetails.Count : 0, "删除会导致账款不一致");
                }
                else if (!string.IsNullOrWhiteSpace(item.SalesOrderId))
                {
                    r.Level = "danger";
                    warnings.Add("该应收款关联销售订单，删除后可能影响订单收款状态。");
                }
            }
            else if (operation == "edit")
            {
                if (item.ReceivedAmount > 0 || (item.ReceiptDetails != null && item.ReceiptDetails.Count > 0))
                    blocking.Add("已有收款明细时，不能修改应收金额。");
                else
                {
                    r.Level = "warning";
                    warnings.Add("修改应收金额将影响客户应收余额。");
                }
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactPayable(string operation, string id, JsonObject payload)
        {
            var item = LoadPayables().FirstOrDefault(x => x.Id == id);
            var label = item != null ? item.Code : id;
            var r = NewImpact("应付款影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (item == null && (operation == "delete" || operation == "edit")) { blocking.Add("应付款不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }

            if (operation == "delete")
            {
                if ((item.PaymentDetails != null && item.PaymentDetails.Count > 0) || item.PaidAmount > 0)
                {
                    blocking.Add("该应付款已有付款明细或已付金额大于 0，不能删除。");
                    AddImpact(items, "payment", "付款明细", item.PaymentDetails != null ? item.PaymentDetails.Count : 0, "删除会导致账款不一致");
                }
                else if (!string.IsNullOrWhiteSpace(item.PurchaseOrderId))
                {
                    r.Level = "danger";
                    warnings.Add("该应付款关联采购单，删除后可能影响采购单付款状态。");
                }
            }
            else if (operation == "edit")
            {
                if (item.PaidAmount > 0 || (item.PaymentDetails != null && item.PaymentDetails.Count > 0))
                    blocking.Add("已有付款明细时，不能修改应付金额。");
                else
                {
                    r.Level = "warning";
                    warnings.Add("修改应付金额将影响供应商应付余额。");
                }
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactCustomer(string operation, string id, JsonObject payload)
        {
            var item = LoadCustomers().FirstOrDefault(x => x.Id == id);
            var label = item != null ? (item.Code + " " + item.Company) : id;
            var r = NewImpact("客户影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (item == null && operation == "delete") { blocking.Add("客户不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }
            if (operation == "delete" && item != null)
            {
                var receivables = LoadReceivables().Where(x => string.Equals(x.CustomerName, item.Company, StringComparison.OrdinalIgnoreCase)).ToList();
                decimal unreceived = receivables.Sum(x => x.ReceivableAmount - x.ReceivedAmount) + item.Receivable;
                if (unreceived > 0.0001m)
                {
                    blocking.Add("该客户有未收款（约 " + RoundMoney(unreceived) + " 元），不能删除。");
                    AddImpact(items, "receivable", "未收款", receivables.Count, "存在未结清应收");
                }
                if (IsCustomerReferenced(item.Id, item.Code, item.Company))
                {
                    blocking.Add("该客户已被销售订单、应收款或合同等业务引用，不能删除。");
                    AddImpact(items, "reference", "业务引用", 1, "存在关联业务单据");
                }
                if (blocking.Count == 0) { r.Level = "warning"; warnings.Add("删除客户后不可恢复。"); }
            }
            else if (operation == "edit" && item != null && IsCustomerReferenced(item.Id, item.Code, item.Company))
            {
                r.Level = "info";
                warnings.Add("该客户已被业务引用，修改关键资料只影响后续单据，不改历史快照。");
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactSupplier(string operation, string id, JsonObject payload)
        {
            var item = LoadSuppliers().FirstOrDefault(x => x.Id == id);
            var label = item != null ? (item.Code + " " + item.Company) : id;
            var r = NewImpact("供应商影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (item == null && operation == "delete") { blocking.Add("供应商不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }
            if (operation == "delete" && item != null)
            {
                var payables = LoadPayables().Where(x => string.Equals(x.SupplierName, item.Company, StringComparison.OrdinalIgnoreCase)).ToList();
                decimal unpaid = payables.Sum(x => x.PayableAmount - x.PaidAmount) + item.Payable;
                if (unpaid > 0.0001m)
                {
                    blocking.Add("该供应商有未付款（约 " + RoundMoney(unpaid) + " 元），不能删除。");
                    AddImpact(items, "payable", "未付款", payables.Count, "存在未结清应付");
                }
                if (IsSupplierReferencedByBusiness(item.Company))
                {
                    if (IsSupplierUsedByMaterial(item.Company))
                        blocking.Add("该供应商已被物料使用，不能删除。");
                    else
                        blocking.Add("该供应商已被采购单、入库或应付款引用，不能删除。");
                }
                if (blocking.Count == 0) { r.Level = "warning"; warnings.Add("删除供应商后不可恢复。"); }
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactMaterial(string operation, string id, JsonObject payload)
        {
            var item = LoadMaterials().FirstOrDefault(x => x.Id == id);
            var label = item != null ? (item.Code + " " + item.NameSpec) : id;
            var r = NewImpact("物料影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (item == null && operation == "delete") { blocking.Add("物料不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }
            if (operation == "delete" && item != null)
            {
                if (IsMaterialUsedByBom(item.Id, item.Code))
                {
                    blocking.Add("该物料已被 BOM 引用，不能删除。");
                    AddImpact(items, "bom", "BOM引用", GetBomsUsingMaterial(item.Id, item.Code).Count, "被BOM表引用");
                }
                else if (IsMaterialReferenced(item.Id, item.Code))
                {
                    blocking.Add("该物料已被业务单据引用或库存不为 0，不能删除。");
                }
            }
            else if (operation == "edit" && item != null)
            {
                bool priceChanged = payload != null && (payload["TaxPrice"] != null || payload["NoTaxPrice"] != null);
                if (IsMaterialReferenced(item.Id, item.Code))
                {
                    r.Level = "warning";
                    warnings.Add("该物料已被引用，修改名称/规格/单位/价格只影响后续业务，不改历史快照。");
                    if (priceChanged)
                    {
                        AddImpact(items, "bom", "BOM成本", 1, "价格变更可能影响后续 BOM 和机型成本刷新");
                        warnings.Add("修改价格可能影响后续 BOM 成本和机型成本计算。");
                    }
                }
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactBom(string operation, string id, JsonObject payload)
        {
            var item = LoadBom().FirstOrDefault(x => x.Id == id);
            var label = item != null ? item.Code : id;
            var r = NewImpact("BOM影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (item == null && operation == "delete") { blocking.Add("BOM不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }
            if (operation == "delete" && item != null)
            {
                if (IsBomUsedByModelCost(id))
                {
                    blocking.Add("该 BOM 已被机型成本引用，不能删除。");
                    AddImpact(items, "modelCost", "机型成本", GetModelCostsUsingBom(id).Count, "被机型成本引用");
                }
                else if (string.Equals(item.Status ?? "", "启用", StringComparison.OrdinalIgnoreCase))
                {
                    blocking.Add("启用状态的 BOM 不能删除，请先停用。");
                }
                else
                {
                    r.Level = "warning";
                    warnings.Add("删除 BOM 后不可恢复。");
                }
            }
            else if (operation == "edit" && item != null && IsBomUsedByModelCost(id))
            {
                r.Level = "warning";
                warnings.Add("该 BOM 被机型成本引用，修改将影响后续成本计算，不改历史快照。");
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactModelCost(string operation, string id, JsonObject payload)
        {
            var item = LoadModelCostsWithCurrentPrices().FirstOrDefault(x => x.Id == id);
            var label = item != null ? (item.ModelCode + " " + item.ModelName) : id;
            var r = NewImpact("机型成本影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (item == null && (operation == "delete" || operation == "enable")) { blocking.Add("机型成本不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }

            if (operation == "delete" && item != null)
            {
                if (string.Equals(item.Status ?? "", "启用", StringComparison.OrdinalIgnoreCase))
                    blocking.Add("启用状态的机型成本不能删除，请先停用。");
                else
                {
                    var usedByFin = LoadFinishedInbounds().Any(x => x.ModelCostId == id);
                    if (usedByFin)
                    {
                        blocking.Add("该机型成本已被成品入库引用，不能删除。");
                        AddImpact(items, "finishedInbound", "成品入库", 1, "被成品入库引用");
                    }
                    else
                    {
                        r.Level = "warning";
                        warnings.Add("删除机型成本后不可恢复。");
                    }
                }
            }
            else if (operation == "enable" && item != null)
            {
                var bom = LoadBom().FirstOrDefault(x => x.Id == item.BomId);
                if (bom == null) blocking.Add("关联 BOM 不存在，不能启用。");
                else if (bom.Items == null || bom.Items.Count == 0) blocking.Add("关联 BOM 明细为空，不能启用。");
                else
                {
                    r.Level = "warning";
                    AddImpact(items, "finishedInbound", "成品入库", 1, "启用后将作为后续成品入库成本来源");
                    warnings.Add("启用后将作为后续成品入库的成本来源。");
                }
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactContract(string operation, string id, JsonObject payload)
        {
            var item = LoadContracts().FirstOrDefault(x => x.Id == id);
            var label = item != null ? item.Code : id;
            var r = NewImpact("合同影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (item == null && operation == "delete") { blocking.Add("合同不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }
            if (operation == "delete" && item != null)
            {
                if (string.Equals(item.Status ?? "", "已生效", StringComparison.OrdinalIgnoreCase))
                {
                    r.Level = "danger";
                    warnings.Add("删除已生效合同前请确认，删除后不可恢复。");
                }
                if (string.Equals(item.Status ?? "", "作废", StringComparison.OrdinalIgnoreCase))
                    r.Level = "warning";
            }
            else if (operation == "void" && item != null)
            {
                r.Level = "warning";
                warnings.Add("作废后该合同不再作为有效合同使用。");
                AddImpact(items, "audit", "操作记录", 1, "将记录合同作废");
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactContractSetting(string operation, string id, JsonObject payload)
        {
            var item = LoadContractSettings().FirstOrDefault(x => x.Id == id);
            var label = item != null ? (item.Code + " " + item.Name) : id;
            var r = NewImpact("合同资料影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (item == null && operation == "delete") { blocking.Add("合同资料不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }
            if (operation == "delete" && item != null)
            {
                var contracts = LoadContracts();
                int refCount = contracts.Count(c => c.TemplateId == id);
                if (refCount > 0)
                {
                    blocking.Add("该合同资料已被 " + refCount + " 份合同引用为模板，不能删除。");
                    AddImpact(items, "contract", "合同", refCount, "被合同引用为模板");
                }
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactUser(string operation, string id, JsonObject payload, UserSession actor)
        {
            var username = (id ?? "").Trim();
            var r = NewImpact("子账号影响预检", username);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (operation == "delete")
            {
                if (actor != null && string.Equals(actor.Username, username, StringComparison.OrdinalIgnoreCase))
                    blocking.Add("不能删除当前登录账号。");
                if (IsAdminUsername(username))
                    blocking.Add("admin 主账号不能删除。");
                else
                {
                    r.Level = "warning";
                    warnings.Add("删除子账号后，历史操作记录仍保留原账号名称。建议优先停用而非删除。");
                    AddImpact(items, "audit", "操作记录", 1, "历史操作记录仍保留账号名称");
                }
            }
            else if (operation == "editPermissions")
            {
                var highRisk = new[] { "settings", "admin", "backup", "operation" };
                r.Level = "danger";
                warnings.Add("修改权限可能影响系统管理员、数据维护、操作记录等高权限功能，请谨慎操作。");
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactDictionary(string operation, string id, JsonObject payload)
        {
            var item = LoadDictionaryOptions().FirstOrDefault(x => x.Id == id);
            var label = item != null ? item.Name : id;
            var r = NewImpact("字典选项影响预检", label);
            var items = new List<OperationImpactItemDto>();
            var blocking = new List<string>();
            var warnings = new List<string>();
            if (item == null && operation == "delete") { blocking.Add("字典项不存在"); FinalizeImpact(r, items, blocking, warnings); return r; }
            if (operation == "delete" && item != null && IsDictionaryOptionInUse(item))
            {
                blocking.Add("该字典项已被业务数据使用，不能删除。请改为停用。");
                AddImpact(items, "reference", "业务引用", 1, "已被业务使用");
            }
            FinalizeImpact(r, items, blocking, warnings);
            return r;
        }

        static OperationImpactResultDto ImpactTaxRate(string operation, string id, JsonObject payload)
        {
            var r = NewImpact("税率修改影响预检");
            r.Level = "warning";
            var warnings = new List<string> { "税率修改只影响后续新单据和 BOM 含税价换算，不改历史单据。" };
            FinalizeImpact(r, new List<OperationImpactItemDto>(), new List<string>(), warnings);
            return r;
        }

        static OperationImpactResultDto ImpactClearTestData(string operation, string id, JsonObject payload)
        {
            var r = NewImpact("清理测试数据影响预检");
            r.Level = "warning";
            var items = new List<OperationImpactItemDto>();
            AddImpact(items, "testData", "AUTO_TEST 测试数据", 1, "只删除名称/编号/备注含 AUTO_TEST 的数据");
            AddImpact(items, "backup", "自动备份", 1, "清空前系统将自动备份");
            var warnings = new List<string> { "不会删除正式业务数据。清空前系统将自动备份。" };
            FinalizeImpact(r, items, new List<string>(), warnings);
            return r;
        }

        static OperationImpactResultDto ImpactClearAllBusinessData(string operation, string id, JsonObject payload)
        {
            var r = NewImpact("清空全部业务数据影响预检");
            r.Level = "danger";
            var items = new List<OperationImpactItemDto>();
            string[] modules = { "供应商", "客户", "物料", "BOM", "机型成本", "销售订单", "销售出库", "采购单", "采购入库", "生产领用", "成品入库", "库存", "应收款", "应付款", "合同", "合同资料", "财务收支", "财务期初余额", "测试数据" };
            foreach (var m in modules) AddImpact(items, "module", m, 1, "将被清空");
            AddImpact(items, "retain", "保留项", 1, "admin、系统配置、权限、字典、合同范本、操作记录将保留");
            AddImpact(items, "backup", "自动备份", 1, "清空前系统将自动备份；备份失败则禁止继续");
            var warnings = new List<string> { "此操作会清空全部业务数据，系统会先自动备份。备份失败将禁止继续清空。" };
            FinalizeImpact(r, items, new List<string>(), warnings);
            return r;
        }

        static OperationImpactResultDto ImpactBackupRestore(string operation, string id, JsonObject payload)
        {
            var r = NewImpact("数据恢复影响预检");
            r.Level = "danger";
            var items = new List<OperationImpactItemDto>();
            string backupName = payload != null && payload["BackupName"] != null ? payload["BackupName"].GetValue<string>() : id;
            AddImpact(items, "data", "当前正式数据", 1, "恢复后将覆盖当前全部业务数据");
            AddImpact(items, "backup", "备份文件", 1, "将使用备份：" + (backupName ?? "-"));
            var warnings = new List<string> { "数据恢复会覆盖当前正式数据，恢复前系统会先自动备份当前数据。恢复后需重启 ERP 并 Ctrl+F5 刷新。" };
            FinalizeImpact(r, items, new List<string>(), warnings);
            return r;
        }

        static void PreviewOperationImpact(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<OperationImpactPreviewRequest>(ReadBody(ctx.Request));
            if (req == null || string.IsNullOrWhiteSpace(req.Module))
            {
                WriteJson(ctx, new { message = "请指定 module" }, 400);
                return;
            }
            var result = EvaluateOperationImpact(req.Module, req.Operation ?? "delete", req.Id ?? "", req.Payload, user);
            WriteJson(ctx, result);
        }

        static void LogOperationImpactEvent(HttpListenerContext ctx, UserSession user, OperationImpactPreviewRequest req, string eventType)
        {
            if (user == null || req == null) return;
            string detail = (req.Module ?? "") + " / " + (req.Operation ?? "") + " / " + (req.Id ?? "") + " | 事件:" + eventType;
            WriteStructuredOperationLog(user, ctx, "影响预检" + (eventType == "cancel" ? "取消" : "记录"), detail, "Success", null, null, "/api/operation-impact/log");
        }

        static void LogOperationImpactCancel(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<OperationImpactPreviewRequest>(ReadBody(ctx.Request));
            LogOperationImpactEvent(ctx, user, req, "cancel");
            WriteJson(ctx, new { ok = true });
        }

        static OperationImpactResultDto CheckDeleteImpact(string module, string id, UserSession user)
        {
            return EvaluateOperationImpact(module, "delete", id, null, user);
        }

        static void EnforceDeleteImpact(string module, string id, UserSession user, HttpListenerContext ctx, string auditObject)
        {
            var result = CheckDeleteImpact(module, id, user);
            EnforceOperationImpact(result, user, ctx, module, "delete", auditObject);
        }

        static void EnforceBatchDeleteImpact(string module, string[] ids, UserSession user, HttpListenerContext ctx)
        {
            if (ids == null) return;
            foreach (var id in ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                EnforceDeleteImpact(module, id, user, ctx, id);
        }

        static string AuditKeyToImpactModule(string auditKey)
        {
            switch ((auditKey ?? "").Trim().ToLowerInvariant())
            {
                case "sales_order": return "salesOrder";
                case "sales_outbound": return "salesOutbound";
                case "purchase_order": return "purchaseOrder";
                case "purchase_inbound": return "purchaseInbound";
                case "production_pick": return "productionPick";
                case "finished_inbound": return "finishedInbound";
                case "receivable": return "receivable";
                case "payable": return "payable";
                default: return auditKey;
            }
        }
    }
}
