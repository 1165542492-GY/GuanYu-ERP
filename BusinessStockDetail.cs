using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace SupplierErpApp
{
    public class BusinessStockDetail
    {
        public string Id { get; set; }
        public string SourceType { get; set; }
        public string SourceId { get; set; }
        public string SourceNo { get; set; }
        public string LineType { get; set; }
        public string MaterialId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public string Spec { get; set; }
        public string Unit { get; set; }
        public string WarehouseId { get; set; }
        public string WarehouseName { get; set; }
        public decimal PlannedQuantity { get; set; }
        public decimal OutQuantity { get; set; }
        public decimal ReturnQuantity { get; set; }
        public decimal ExtraQuantity { get; set; }
        public decimal FinalQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
        public string Remark { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string ConfirmedAt { get; set; }
        public string OperatorName { get; set; }
        public string CreatedBy { get; set; }
    }

    public class BusinessStockDetailSummary
    {
        public decimal PlannedQuantityTotal { get; set; }
        public decimal OutQuantityTotal { get; set; }
        public decimal ReturnQuantityTotal { get; set; }
        public decimal ExtraQuantityTotal { get; set; }
        public decimal FinalQuantityTotal { get; set; }
        public decimal AmountTotal { get; set; }
    }

    public class BusinessStockDetailListResult
    {
        public BusinessStockDetail[] Items { get; set; }
        public BusinessStockDetailSummary Summary { get; set; }
    }

    public static partial class Program
    {
        static readonly string[] SalesAccessoryLineTypes = { "随货配件", "赠品", "补发", "退回" };
        static readonly string[] RepairMaterialLineTypes = { "维修领用", "维修退料", "维修补领", "维修换料" };
        static readonly string SourceTypeSalesOutbound = "SalesOutbound";
        static readonly string SourceTypeRepairOrder = "RepairOrder";

        static List<BusinessStockDetail> LoadBusinessStockDetails()
        {
            return LoadJsonList<BusinessStockDetail>(BusinessStockDetailsFile);
        }

        static string NormalizeBusinessStockDetailStatus(string status)
        {
            status = (status ?? "").Trim();
            if (status == "草稿" || status == "已确认" || status == "已取消") return status;
            return "草稿";
        }

        static bool IsReturnLineType(string lineType)
        {
            lineType = (lineType ?? "").Trim();
            return lineType == "退回" || lineType == "维修退料";
        }

        static decimal CalcBusinessStockNetEffect(BusinessStockDetail d)
        {
            if (d == null) return 0;
            decimal o = Math.Max(0, d.OutQuantity);
            decimal e = Math.Max(0, d.ExtraQuantity);
            decimal r = Math.Max(0, d.ReturnQuantity);
            if (IsReturnLineType(d.LineType)) return -r;
            return o + e - r;
        }

        static decimal CalcBusinessStockFinalQty(BusinessStockDetail d)
        {
            if (d == null) return 0;
            if (IsReturnLineType(d.LineType))
                return RoundMoney(Math.Max(0, d.ReturnQuantity));
            decimal net = CalcBusinessStockNetEffect(d);
            return net > 0 ? RoundMoney(net) : 0;
        }

        static decimal CalcBusinessStockSummaryQty(BusinessStockDetail d)
        {
            return CalcBusinessStockNetEffect(d);
        }

        static void SyncBusinessStockDetailComputed(BusinessStockDetail d)
        {
            if (d == null) return;
            d.PlannedQuantity = Math.Max(0, d.PlannedQuantity);
            d.OutQuantity = Math.Max(0, d.OutQuantity);
            d.ReturnQuantity = Math.Max(0, d.ReturnQuantity);
            d.ExtraQuantity = Math.Max(0, d.ExtraQuantity);
            d.FinalQuantity = CalcBusinessStockFinalQty(d);
            d.UnitCost = Math.Max(0, d.UnitCost);
            decimal amount = RoundMoney(d.FinalQuantity * d.UnitCost);
            d.Amount = IsReturnLineType(d.LineType) ? -amount : amount;
        }

        static void ValidateBusinessStockDetailQuantities(BusinessStockDetail d)
        {
            if (d == null) BizFail("明细不能为空");
            if (d.OutQuantity < 0 || d.ReturnQuantity < 0 || d.ExtraQuantity < 0 || d.PlannedQuantity < 0)
                BizFail("数量不能为负数");
            decimal net = CalcBusinessStockNetEffect(d);
            if (net < 0 && !IsReturnLineType(d.LineType))
                BizFail("最终出库/耗用数量不能为负数，请检查领出、补领与退回数量");
            string lt = (d.LineType ?? "").Trim();
            if (string.Equals(d.SourceType, SourceTypeSalesOutbound, StringComparison.OrdinalIgnoreCase))
            {
                if (!SalesAccessoryLineTypes.Contains(lt)) BizFail("销售明细类型无效，请选择：随货配件、赠品、补发、退回");
            }
            else if (string.Equals(d.SourceType, SourceTypeRepairOrder, StringComparison.OrdinalIgnoreCase))
            {
                if (!RepairMaterialLineTypes.Contains(lt)) BizFail("维修明细类型无效，请选择：维修领用、维修退料、维修补领、维修换料");
            }
            else BizFail("来源类型无效");
            if (IsReturnLineType(lt))
            {
                if (d.ReturnQuantity <= 0) BizFail("退回/退料数量必须大于 0");
            }
            else if (d.OutQuantity + d.ExtraQuantity <= 0 && d.ReturnQuantity <= 0)
                BizFail("请填写领出/出库、补发/补领或退回数量");
        }

        static void ResolveBusinessStockDetailMaterial(BusinessStockDetail d)
        {
            d.MaterialId = (d.MaterialId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(d.MaterialId)) BizFail("请选择物料");
            var mat = LoadMaterials().FirstOrDefault(x => x.Id == d.MaterialId);
            if (mat == null) BizFail("物料不存在");
            d.MaterialCode = mat.Code ?? "";
            d.MaterialName = mat.NameSpec ?? "";
            d.Spec = "-";
            d.Unit = ParseMaterialQtyUnitParts(mat.QuantityUnit).Unit;
            if (string.IsNullOrWhiteSpace(d.WarehouseName))
                d.WarehouseName = string.IsNullOrWhiteSpace(mat.DefaultWarehouse) ? "默认仓库" : mat.DefaultWarehouse;
            if (d.UnitCost <= 0)
            {
                decimal price = MaterialDisplayUnitPrice(mat);
                if (price > 0) d.UnitCost = price;
            }
        }

        static void ResolveBusinessStockDetailSource(BusinessStockDetail d)
        {
            d.SourceType = (d.SourceType ?? "").Trim();
            d.SourceId = (d.SourceId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(d.SourceId)) BizFail("来源业务单不存在");
            if (string.Equals(d.SourceType, SourceTypeSalesOutbound, StringComparison.OrdinalIgnoreCase))
            {
                var outbound = LoadSalesOutbounds().FirstOrDefault(x => x.Id == d.SourceId);
                if (outbound == null) BizFail("销售出库单不存在", 404);
                d.SourceNo = outbound.Code ?? "";
            }
            else if (string.Equals(d.SourceType, SourceTypeRepairOrder, StringComparison.OrdinalIgnoreCase))
            {
                var repair = LoadAfterSalesServiceOrders().FirstOrDefault(x => x.Id == d.SourceId);
                if (repair == null) BizFail("维修单不存在", 404);
                d.SourceNo = repair.ServiceNo ?? "";
            }
            else BizFail("来源类型无效");
        }

        static BusinessStockDetailSummary BuildBusinessStockDetailSummary(IEnumerable<BusinessStockDetail> items)
        {
            var list = items ?? Enumerable.Empty<BusinessStockDetail>();
            return new BusinessStockDetailSummary
            {
                PlannedQuantityTotal = RoundMoney(list.Sum(x => x.PlannedQuantity)),
                OutQuantityTotal = RoundMoney(list.Sum(x => x.OutQuantity)),
                ReturnQuantityTotal = RoundMoney(list.Sum(x => x.ReturnQuantity)),
                ExtraQuantityTotal = RoundMoney(list.Sum(x => x.ExtraQuantity)),
                FinalQuantityTotal = RoundMoney(Math.Max(0, list.Sum(x => CalcBusinessStockSummaryQty(x)))),
                AmountTotal = RoundMoney(list.Sum(x => x.Amount))
            };
        }

        static void ApplyBusinessStockDetailToMap(Dictionary<string, StockAgg> map, BusinessStockDetail d)
        {
            if (d == null || !string.Equals(NormalizeBusinessStockDetailStatus(d.Status), "已确认", StringComparison.Ordinal)) return;
            decimal net = CalcBusinessStockNetEffect(d);
            if (Math.Abs(net) <= 0.0001m) return;
            StockAddMaterial(map, d.MaterialId, d.MaterialCode, d.MaterialName, -net, d.UnitCost);
        }

        static void EnsureBusinessStockAvailable(decimal requiredNet, string materialId, string materialCode, string materialName, StockMapOptions options = null, IEnumerable<BusinessStockDetail> pendingConfirms = null, string excludeDetailId = null)
        {
            if (requiredNet <= 0) return;
            var map = BuildStockMap(options);
            if (pendingConfirms != null)
            {
                foreach (var p in pendingConfirms)
                {
                    if (!string.IsNullOrWhiteSpace(excludeDetailId) && p.Id == excludeDetailId) continue;
                    ApplyBusinessStockDetailToMap(map, p);
                }
            }
            EnsureMaterialStockOnMap(map, requiredNet, materialId, materialCode, materialName);
        }

        static void ValidateBusinessStockDetailConfirm(BusinessStockDetail item, string excludeId = null, BusinessStockDetailBatchContext batch = null)
        {
            if (item == null) return;
            SyncBusinessStockDetailComputed(item);
            decimal net = CalcBusinessStockNetEffect(item);
            if (net <= 0) return;
            if (batch != null)
            {
                batch.ValidateConfirm(item, excludeId);
                return;
            }
            var options = new StockMapOptions { ExcludeBusinessStockDetailId = excludeId };
            EnsureBusinessStockAvailable(net, item.MaterialId, item.MaterialCode, item.MaterialName, options);
        }

        static void ValidateBusinessStockDetailDelta(BusinessStockDetail oldItem, BusinessStockDetail newItem)
        {
            if (oldItem == null || newItem == null) return;
            if (!string.Equals(NormalizeBusinessStockDetailStatus(oldItem.Status), "已确认", StringComparison.Ordinal)) return;
            decimal oldNet = CalcBusinessStockNetEffect(oldItem);
            decimal newNet = CalcBusinessStockNetEffect(newItem);
            decimal delta = RoundMoney(newNet - oldNet);
            if (delta > 0)
                EnsureBusinessStockAvailable(delta, newItem.MaterialId, newItem.MaterialCode, newItem.MaterialName,
                    new StockMapOptions { ExcludeBusinessStockDetailId = oldItem.Id });
        }

        static bool RequireBusinessStockDetailPermission(HttpListenerContext ctx, UserSession user, string sourceType, string action)
        {
            sourceType = (sourceType ?? "").Trim();
            if (string.Equals(sourceType, SourceTypeSalesOutbound, StringComparison.OrdinalIgnoreCase))
            {
                string perm = action == "view" ? "sales_outbound.view" : action == "add" ? "sales_outbound.add" : "sales_outbound.edit";
                if (action == "delete") perm = "sales_outbound.delete";
                return RequirePermission(ctx, user, perm);
            }
            if (string.Equals(sourceType, SourceTypeRepairOrder, StringComparison.OrdinalIgnoreCase))
            {
                string perm = action == "view" ? "after_sales.view" : action == "add" ? "after_sales.add" : "after_sales.edit";
                if (action == "delete") perm = "after_sales.delete";
                return RequirePermission(ctx, user, perm);
            }
            WriteJson(ctx, new { error = "来源类型无效" }, 400);
            return false;
        }

        static BusinessStockDetailMutationRequest ReadBusinessStockDetailRequest(HttpListenerRequest req)
        {
            string body = ReadBody(req);
            if (string.IsNullOrWhiteSpace(body)) return new BusinessStockDetailMutationRequest();
            return Json.Deserialize<BusinessStockDetailMutationRequest>(body) ?? new BusinessStockDetailMutationRequest();
        }

        class BusinessStockDetailMutationRequest
        {
            public string UpdatedAt { get; set; }
            public string SourceType { get; set; }
            public string SourceId { get; set; }
            public string LineType { get; set; }
            public string MaterialId { get; set; }
            public string WarehouseName { get; set; }
            public decimal PlannedQuantity { get; set; }
            public decimal OutQuantity { get; set; }
            public decimal ReturnQuantity { get; set; }
            public decimal ExtraQuantity { get; set; }
            public decimal UnitCost { get; set; }
            public string Reason { get; set; }
            public string Remark { get; set; }
        }

        static void ApplyBusinessStockDetailMutation(BusinessStockDetail target, BusinessStockDetailMutationRequest input, UserSession user, bool isNew)
        {
            if (target == null || input == null) BizFail("数据不能为空");
            if (isNew)
            {
                target.SourceType = (input.SourceType ?? "").Trim();
                target.SourceId = (input.SourceId ?? "").Trim();
            }
            target.LineType = (input.LineType ?? "").Trim();
            target.MaterialId = (input.MaterialId ?? "").Trim();
            target.WarehouseName = (input.WarehouseName ?? "").Trim();
            target.PlannedQuantity = input.PlannedQuantity;
            target.OutQuantity = input.OutQuantity;
            target.ReturnQuantity = input.ReturnQuantity;
            target.ExtraQuantity = input.ExtraQuantity;
            target.UnitCost = input.UnitCost;
            target.Reason = (input.Reason ?? "").Trim();
            target.Remark = (input.Remark ?? "").Trim();
            ResolveBusinessStockDetailSource(target);
            ResolveBusinessStockDetailMaterial(target);
            ValidateBusinessStockDetailQuantities(target);
            SyncBusinessStockDetailComputed(target);
            target.UpdatedAt = BizUpdatedAtNow();
            if (isNew)
            {
                target.CreatedAt = target.UpdatedAt;
                target.CreatedBy = user.DisplayName;
            }
            target.OperatorName = user.DisplayName;
        }

        static string AuditBusinessStockDetailLabel(BusinessStockDetail d)
        {
            return (d.SourceNo ?? "") + " " + (d.LineType ?? "") + " " + (d.MaterialCode ?? d.MaterialName ?? "");
        }

        static void ListBusinessStockDetails(HttpListenerContext ctx, UserSession user)
        {
            string sourceType = (ctx.Request.QueryString["sourceType"] ?? "").Trim();
            string sourceId = (ctx.Request.QueryString["sourceId"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceId))
            {
                WriteJson(ctx, new { error = "请指定 sourceType 与 sourceId" }, 400);
                return;
            }
            if (!RequireBusinessStockDetailPermission(ctx, user, sourceType, "view")) return;
            var items = LoadBusinessStockDetails()
                .Where(x => string.Equals(x.SourceType ?? "", sourceType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.SourceId ?? "", sourceId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
            WriteJson(ctx, new BusinessStockDetailListResult
            {
                Items = items.ToArray(),
                Summary = BuildBusinessStockDetailSummary(items)
            });
        }

        static void AddBusinessStockDetail(HttpListenerContext ctx, UserSession user)
        {
            var input = ReadBusinessStockDetailRequest(ctx.Request);
            if (!RequireBusinessStockDetailPermission(ctx, user, input.SourceType, "add")) return;
            BusinessStockDetail saved = null;
            MutateJsonList<BusinessStockDetail, BusinessStockDetail>(BusinessStockDetailsFile, "business_stock_details", list =>
            {
                var item = new BusinessStockDetail
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Status = "草稿"
                };
                ApplyBusinessStockDetailMutation(item, input, user, true);
                list.Insert(0, item);
                saved = item;
                return new JsonMutationResult<BusinessStockDetail>(item, true);
            });
            Audit(user, "新增业务出入库明细", AuditBusinessStockDetailLabel(saved));
            WriteJson(ctx, saved, 201);
        }

        static void UpdateBusinessStockDetail(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = ReadBusinessStockDetailRequest(ctx.Request);
            var existing = LoadBusinessStockDetails().FirstOrDefault(x => x.Id == id);
            if (existing == null) { WriteJson(ctx, new { error = "业务明细不存在" }, 404); return; }
            if (!RequireBusinessStockDetailPermission(ctx, user, existing.SourceType, "edit")) return;
            BusinessStockDetail saved = null;
            string auditLabel = null;
            MutateJsonList<BusinessStockDetail, BusinessStockDetail>(BusinessStockDetailsFile, "business_stock_details", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("业务明细不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                var status = NormalizeBusinessStockDetailStatus(item.Status);
                if (status == "已取消") BizFail("已取消的明细不能修改");
                var snapshot = Json.Deserialize<BusinessStockDetail>(Json.Serialize(item));
                ApplyBusinessStockDetailMutation(item, input, user, false);
                if (status == "已确认")
                    ValidateBusinessStockDetailDelta(snapshot, item);
                auditLabel = AuditBusinessStockDetailLabel(item);
                saved = item;
                return new JsonMutationResult<BusinessStockDetail>(item, true);
            });
            Audit(user, "修改业务出入库明细", auditLabel);
            WriteJson(ctx, saved);
        }

        static void DeleteBusinessStockDetail(HttpListenerContext ctx, UserSession user, string id)
        {
            var existing = LoadBusinessStockDetails().FirstOrDefault(x => x.Id == id);
            if (existing == null) { WriteJson(ctx, new { error = "业务明细不存在" }, 404); return; }
            if (!RequireBusinessStockDetailPermission(ctx, user, existing.SourceType, "delete")) return;
            string auditLabel = null;
            MutateJsonList<BusinessStockDetail, object>(BusinessStockDetailsFile, "business_stock_details", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("业务明细不存在", 404);
                var status = NormalizeBusinessStockDetailStatus(item.Status);
                if (status == "已确认") BizFail("已确认的明细不能删除，请先取消确认");
                if (status != "草稿") BizFail("仅草稿明细可以删除");
                auditLabel = AuditBusinessStockDetailLabel(item);
                list.Remove(item);
                return new JsonMutationResult<object>(null, true);
            });
            Audit(user, "删除业务出入库明细", auditLabel);
            WriteJson(ctx, new { ok = true });
        }

        static void ConfirmBusinessStockDetail(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = ReadBusinessStockDetailRequest(ctx.Request);
            var existing = LoadBusinessStockDetails().FirstOrDefault(x => x.Id == id);
            if (existing == null) { WriteJson(ctx, new { error = "业务明细不存在" }, 404); return; }
            if (!RequireBusinessStockDetailPermission(ctx, user, existing.SourceType, "edit")) return;
            BusinessStockDetail saved = null;
            string auditLabel = null;
            MutateJsonList<BusinessStockDetail, BusinessStockDetail>(BusinessStockDetailsFile, "business_stock_details", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("业务明细不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                if (NormalizeBusinessStockDetailStatus(item.Status) == "已确认") BizFail("明细已确认，请勿重复确认");
                if (NormalizeBusinessStockDetailStatus(item.Status) == "已取消") BizFail("已取消的明细不能确认");
                SyncBusinessStockDetailComputed(item);
                ValidateBusinessStockDetailConfirm(item, id);
                item.Status = "已确认";
                item.ConfirmedAt = BizUpdatedAtNow();
                item.UpdatedAt = item.ConfirmedAt;
                item.OperatorName = user.DisplayName;
                auditLabel = AuditBusinessStockDetailLabel(item);
                saved = item;
                return new JsonMutationResult<BusinessStockDetail>(item, true);
            });
            Audit(user, "确认业务出入库明细", auditLabel);
            WriteJson(ctx, saved);
        }

        static void CancelBusinessStockDetail(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = ReadBusinessStockDetailRequest(ctx.Request);
            var existing = LoadBusinessStockDetails().FirstOrDefault(x => x.Id == id);
            if (existing == null) { WriteJson(ctx, new { error = "业务明细不存在" }, 404); return; }
            if (!RequireBusinessStockDetailPermission(ctx, user, existing.SourceType, "edit")) return;
            BusinessStockDetail saved = null;
            string auditLabel = null;
            MutateJsonList<BusinessStockDetail, BusinessStockDetail>(BusinessStockDetailsFile, "business_stock_details", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("业务明细不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                if (NormalizeBusinessStockDetailStatus(item.Status) != "已确认") BizFail("仅已确认明细可以取消确认");
                item.Status = "已取消";
                item.UpdatedAt = BizUpdatedAtNow();
                item.OperatorName = user.DisplayName;
                auditLabel = AuditBusinessStockDetailLabel(item);
                saved = item;
                return new JsonMutationResult<BusinessStockDetail>(item, true);
            });
            Audit(user, "取消业务出入库明细", auditLabel);
            WriteJson(ctx, saved);
        }

        class BusinessStockDetailBatchContext
        {
            readonly Dictionary<string, StockAgg> _stockMap;
            readonly List<BusinessStockDetail> _pending = new List<BusinessStockDetail>();

            public BusinessStockDetailBatchContext()
            {
                _stockMap = BuildStockMap();
            }

            public void ValidateConfirm(BusinessStockDetail item, string excludeId = null)
            {
                decimal net = CalcBusinessStockNetEffect(item);
                if (net <= 0) return;
                var sim = new Dictionary<string, StockAgg>(_stockMap, StringComparer.OrdinalIgnoreCase);
                foreach (var p in _pending)
                {
                    if (!string.IsNullOrWhiteSpace(excludeId) && p.Id == excludeId) continue;
                    ApplyBusinessStockDetailToMap(sim, p);
                }
                EnsureMaterialStockOnMap(sim, net, item.MaterialId, item.MaterialCode, item.MaterialName);
            }

            public void RecordConfirm(BusinessStockDetail item)
            {
                if (item == null) return;
                _pending.Add(item);
                ApplyBusinessStockDetailToMap(_stockMap, item);
            }
        }

        static bool TryHandleBusinessStockDetailRoutes(HttpListenerContext ctx, UserSession user, string path)
        {
            if (path == "/api/business-stock-details" && ctx.Request.HttpMethod == "GET")
            {
                ListBusinessStockDetails(ctx, user);
                return true;
            }
            if (path == "/api/business-stock-details" && ctx.Request.HttpMethod == "POST")
            {
                AddBusinessStockDetail(ctx, user);
                return true;
            }
            if (path.StartsWith("/api/business-stock-details/") && path.EndsWith("/confirm") && ctx.Request.HttpMethod == "POST")
            {
                string id = path.Substring("/api/business-stock-details/".Length, path.Length - "/api/business-stock-details/".Length - "/confirm".Length);
                ConfirmBusinessStockDetail(ctx, user, id);
                return true;
            }
            if (path.StartsWith("/api/business-stock-details/") && path.EndsWith("/cancel") && ctx.Request.HttpMethod == "POST")
            {
                string id = path.Substring("/api/business-stock-details/".Length, path.Length - "/api/business-stock-details/".Length - "/cancel".Length);
                CancelBusinessStockDetail(ctx, user, id);
                return true;
            }
            if (path.StartsWith("/api/business-stock-details/") && ctx.Request.HttpMethod == "PUT")
            {
                string id = path.Substring("/api/business-stock-details/".Length);
                UpdateBusinessStockDetail(ctx, user, id);
                return true;
            }
            if (path.StartsWith("/api/business-stock-details/") && ctx.Request.HttpMethod == "DELETE")
            {
                string id = path.Substring("/api/business-stock-details/".Length);
                DeleteBusinessStockDetail(ctx, user, id);
                return true;
            }
            return false;
        }

        static bool TryHandleRepairOrderAliasRoutes(HttpListenerContext ctx, UserSession user, string path)
        {
            if (path == "/api/repair-orders" && ctx.Request.HttpMethod == "GET")
            {
                if (!RequirePermission(ctx, user, "after_sales.view")) return true;
                WriteJson(ctx, LoadAfterSalesServiceOrders());
                return true;
            }
            if (path == "/api/repair-orders" && ctx.Request.HttpMethod == "POST")
            {
                if (!RequirePermission(ctx, user, "after_sales.add")) return true;
                AddAfterSalesServiceOrder(ctx, user);
                return true;
            }
            if (path.StartsWith("/api/repair-orders/") && ctx.Request.HttpMethod == "GET")
            {
                if (!RequirePermission(ctx, user, "after_sales.view")) return true;
                GetAfterSalesServiceOrder(ctx, user, path.Substring("/api/repair-orders/".Length));
                return true;
            }
            if (path.StartsWith("/api/repair-orders/") && ctx.Request.HttpMethod == "PUT")
            {
                if (!RequirePermission(ctx, user, "after_sales.edit")) return true;
                UpdateAfterSalesServiceOrder(ctx, user, path.Substring("/api/repair-orders/".Length));
                return true;
            }
            if (path.StartsWith("/api/repair-orders/") && ctx.Request.HttpMethod == "DELETE")
            {
                if (!RequirePermission(ctx, user, "after_sales.delete")) return true;
                DeleteAfterSalesServiceOrder(ctx, user, path.Substring("/api/repair-orders/".Length));
                return true;
            }
            return false;
        }
    }
}
