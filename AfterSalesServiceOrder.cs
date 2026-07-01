using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace SupplierErpApp
{
    public static partial class Program
    {
        static List<AfterSalesServiceOrder> LoadAfterSalesServiceOrders() { return LoadJsonList<AfterSalesServiceOrder>(AfterSalesServiceOrdersFile); }

        static string NextAfterSalesServiceNo(IEnumerable<AfterSalesServiceOrder> list)
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string prefix = "SR" + datePart;
            int max = 0;
            foreach (var x in list ?? Enumerable.Empty<AfterSalesServiceOrder>())
            {
                var code = x.ServiceNo ?? "";
                if (!code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                int seq;
                if (code.Length > prefix.Length && int.TryParse(code.Substring(prefix.Length), out seq))
                    max = Math.Max(max, seq);
            }
            return prefix + (max + 1).ToString("D3");
        }

        static string NormalizeAfterSalesServiceStatus(string status)
        {
            status = (status ?? "").Trim();
            if (status == "草稿" || status == "待派工" || status == "维修中" || status == "已完成" || status == "已结算" || status == "已取消") return status;
            return "草稿";
        }

        static bool CanEditAfterSalesServiceOrder(string status)
        {
            status = NormalizeAfterSalesServiceStatus(status);
            return status == "草稿" || status == "待派工" || status == "维修中" || status == "已完成";
        }

        static bool CanDeleteAfterSalesServiceOrder(AfterSalesServiceOrder item)
        {
            if (item == null) return false;
            var status = NormalizeAfterSalesServiceStatus(item.Status);
            if (!string.IsNullOrWhiteSpace(item.ReceivableId)) return false;
            return status == "草稿" || status == "已取消";
        }

        static decimal AfterSalesMaterialUnitPrice(Material m)
        {
            if (m == null) return 0;
            if (m.TaxPrice > 0) return m.TaxPrice;
            if (m.NoTaxPrice > 0) return m.NoTaxPrice;
            return 0;
        }

        static string NormalizeAfterSalesPartActionType(string value)
        {
            value = (value ?? "").Trim();
            if (value == "退料" || value.Equals("return", StringComparison.OrdinalIgnoreCase)) return "退料";
            if (value == "补领" || value.Equals("extra", StringComparison.OrdinalIgnoreCase)) return "补领";
            if (value == "耗用调整" || value.Equals("adjust", StringComparison.OrdinalIgnoreCase)) return "耗用调整";
            return "领料";
        }

        static bool AfterSalesPartHasInventoryFields(AfterSalesPartLine line)
        {
            if (line == null) return false;
            return !string.IsNullOrWhiteSpace(line.ActionType)
                || !string.IsNullOrWhiteSpace(line.WarehouseName)
                || !string.IsNullOrWhiteSpace(line.Reason)
                || line.PlannedQuantity != 0
                || line.PickedQuantity != 0
                || line.ReturnedQuantity != 0
                || line.ExtraQuantity != 0
                || line.AdjustQuantity != 0
                || line.FinalUsedQuantity != 0
                || line.CostPrice != 0
                || line.CostAmount != 0;
        }

        static bool RequireAfterSalesPartActionPermissions(HttpListenerContext ctx, UserSession user, AfterSalesServiceOrder item)
        {
            if (item == null || item.Parts == null) return true;
            foreach (var line in item.Parts.Where(x => x != null))
            {
                string action = string.IsNullOrWhiteSpace(line.ActionType) ? "" : NormalizeAfterSalesPartActionType(line.ActionType);
                if ((line.PickedQuantity > 0 || action == "领料") && !RequirePermission(ctx, user, "after_sales.pick")) return false;
                if ((line.ReturnedQuantity > 0 || action == "退料") && !RequirePermission(ctx, user, "after_sales.return")) return false;
                if ((line.ExtraQuantity > 0 || action == "补领") && !RequirePermission(ctx, user, "after_sales.extra_pick")) return false;
            }
            return true;
        }

        static AfterSalesServiceOrder FilterAfterSalesPartsForPermission(AfterSalesServiceOrder item, UserSession user)
        {
            if (item == null) return null;
            var copy = CloneAfterSalesServiceOrder(item);
            if (!HasPermission(user, "after_sales.parts_view"))
            {
                copy.Parts = new List<AfterSalesPartLine>();
                return copy;
            }
            if (!HasPermission(user, "after_sales.final_usage") && copy.Parts != null)
            {
                foreach (var part in copy.Parts.Where(x => x != null))
                    part.FinalUsedQuantity = 0;
            }
            return copy;
        }

        static List<AfterSalesServiceOrder> FilterAfterSalesPartsForPermission(IEnumerable<AfterSalesServiceOrder> items, UserSession user)
        {
            return (items ?? Enumerable.Empty<AfterSalesServiceOrder>()).Select(x => FilterAfterSalesPartsForPermission(x, user)).ToList();
        }

        static decimal ComputeAfterSalesPartFinalUsedQuantity(AfterSalesPartLine line)
        {
            if (line == null) return 0;
            return RoundMoney(line.PickedQuantity - line.ReturnedQuantity + line.ExtraQuantity + line.AdjustQuantity);
        }

        static bool AfterSalesServiceInventoryActive(AfterSalesServiceOrder item)
        {
            if (item == null) return false;
            var status = NormalizeAfterSalesServiceStatus(item.Status);
            return status != "草稿" && status != "已取消";
        }

        static AfterSalesServiceOrder CloneAfterSalesServiceOrder(AfterSalesServiceOrder item)
        {
            return item == null ? null : Json.Deserialize<AfterSalesServiceOrder>(Json.Serialize(item));
        }

        static void ResolveAfterSalesServiceCustomer(AfterSalesServiceOrder item)
        {
            item.CustomerId = (item.CustomerId ?? "").Trim();
            item.CustomerName = (item.CustomerName ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(item.CustomerId))
            {
                var byId = LoadCustomers().FirstOrDefault(x => x.Id == item.CustomerId);
                if (byId != null)
                {
                    item.CustomerId = byId.Id;
                    item.CustomerName = byId.Company ?? "";
                    if (string.IsNullOrWhiteSpace(item.ContactName)) item.ContactName = byId.Contact ?? "";
                    if (string.IsNullOrWhiteSpace(item.ContactPhone)) item.ContactPhone = byId.Phone ?? "";
                    return;
                }
            }
            if (!string.IsNullOrWhiteSpace(item.CustomerName))
            {
                var byName = LoadCustomers().FirstOrDefault(x => string.Equals(x.Company, item.CustomerName, StringComparison.OrdinalIgnoreCase));
                if (byName != null)
                {
                    item.CustomerId = byName.Id;
                    item.CustomerName = byName.Company ?? "";
                    if (string.IsNullOrWhiteSpace(item.ContactName)) item.ContactName = byName.Contact ?? "";
                    if (string.IsNullOrWhiteSpace(item.ContactPhone)) item.ContactPhone = byName.Phone ?? "";
                    return;
                }
            }
            BizFail("请选择客户");
        }

        static void ResolveAfterSalesPartLine(AfterSalesPartLine line)
        {
            if (line == null) return;
            line.Id = string.IsNullOrWhiteSpace(line.Id) ? Guid.NewGuid().ToString("N") : line.Id.Trim();
            line.MaterialId = (line.MaterialId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(line.MaterialId)) BizFail("配件明细请选择物料");
            var mat = LoadMaterials().FirstOrDefault(x => x.Id == line.MaterialId);
            if (mat == null) BizFail("配件物料不存在");
            line.MaterialCode = mat.Code ?? "";
            line.MaterialName = mat.NameSpec ?? "";
            line.Spec = "-";
            line.Unit = ParseMaterialQtyUnitParts(mat.QuantityUnit).Unit;
            if (line.UnitPrice <= 0) line.UnitPrice = AfterSalesMaterialUnitPrice(mat);
            bool affectsStock = AfterSalesPartHasInventoryFields(line);
            if (affectsStock)
            {
                line.ActionType = NormalizeAfterSalesPartActionType(line.ActionType);
                line.WarehouseName = (line.WarehouseName ?? "").Trim();
                line.Reason = (line.Reason ?? "").Trim();
                line.PlannedQuantity = RoundMoney(Math.Max(0, line.PlannedQuantity));
                line.PickedQuantity = RoundMoney(Math.Max(0, line.PickedQuantity));
                line.ReturnedQuantity = RoundMoney(Math.Max(0, line.ReturnedQuantity));
                line.ExtraQuantity = RoundMoney(Math.Max(0, line.ExtraQuantity));
                line.AdjustQuantity = RoundMoney(line.AdjustQuantity);
                if (line.PickedQuantity == 0 && line.ReturnedQuantity == 0 && line.ExtraQuantity == 0 && line.AdjustQuantity == 0 && line.FinalUsedQuantity != 0)
                    line.AdjustQuantity = line.FinalUsedQuantity;
                if (line.PickedQuantity == 0 && line.ReturnedQuantity == 0 && line.ExtraQuantity == 0 && line.AdjustQuantity == 0 && line.Quantity > 0)
                {
                    if (line.ActionType == "退料") line.ReturnedQuantity = line.Quantity;
                    else if (line.ActionType == "补领") line.ExtraQuantity = line.Quantity;
                    else line.PickedQuantity = line.Quantity;
                }
                line.FinalUsedQuantity = ComputeAfterSalesPartFinalUsedQuantity(line);
                if (line.FinalUsedQuantity == 0) BizFail("维修物料最终耗用数量不能为 0");
                if (line.CostPrice <= 0) line.CostPrice = AfterSalesMaterialUnitPrice(mat);
                if (line.CostPrice < 0) BizFail("维修物料成本单价不能为负数");
                line.CostAmount = RoundMoney(Math.Abs(line.FinalUsedQuantity) * line.CostPrice);
                line.Quantity = RoundMoney(Math.Max(0, line.FinalUsedQuantity));
            }
            if (line.Quantity <= 0 && !affectsStock) BizFail("配件数量必须大于 0");
            line.Amount = RoundMoney(line.Quantity * line.UnitPrice);
            line.Remark = (line.Remark ?? "").Trim();
        }

        static void SyncAfterSalesServiceAmounts(AfterSalesServiceOrder item)
        {
            if (item == null) return;
            if (item.Parts == null) item.Parts = new List<AfterSalesPartLine>();
            decimal partsTotal = 0;
            foreach (var p in item.Parts)
            {
                if (p == null) continue;
                p.Quantity = Math.Max(0, p.Quantity);
                p.UnitPrice = Math.Max(0, p.UnitPrice);
                p.Amount = RoundMoney(p.Quantity * p.UnitPrice);
                partsTotal += p.Amount;
            }
            item.PartsAmount = RoundMoney(partsTotal);
            item.LaborAmount = RoundMoney(Math.Max(0, item.LaborAmount));
            item.OtherAmount = RoundMoney(Math.Max(0, item.OtherAmount));
            decimal totalBefore = RoundMoney(item.PartsAmount + item.LaborAmount + item.OtherAmount);
            if (item.DiscountAmount < 0) item.DiscountAmount = 0;
            if (item.DiscountAmount > totalBefore) BizFail("优惠/减免金额不能大于费用合计");
            item.DiscountAmount = RoundMoney(item.DiscountAmount);
            item.ReceivableAmount = RoundMoney(Math.Max(0, totalBefore - item.DiscountAmount));
            if (item.ReceivedAmount < 0) item.ReceivedAmount = 0;
            if (item.ReceivedAmount > item.ReceivableAmount) BizFail("已收金额不能大于应收合计");
            item.ReceivedAmount = RoundMoney(item.ReceivedAmount);
            item.UnreceivedAmount = RoundMoney(Math.Max(0, item.ReceivableAmount - item.ReceivedAmount));
            var status = NormalizeAfterSalesServiceStatus(item.Status);
            if (status == "已结算")
            {
                item.ReceivedAmount = item.ReceivableAmount;
                item.UnreceivedAmount = 0;
            }
        }

        static void ApplyAfterSalesServiceTypeHints(AfterSalesServiceOrder item)
        {
            if (item == null) return;
            var type = (item.ServiceType ?? "").Trim();
            if (type == "保内免费")
            {
                if (item.DiscountAmount < item.PartsAmount + item.LaborAmount + item.OtherAmount)
                {
                    // 允许应收为 0；不强制改优惠，仅在后端重算后校验
                }
            }
        }

        static void ApplyAfterSalesServiceOrder(AfterSalesServiceOrder item, bool preserveReceivableLink)
        {
            if (item == null) BizFail("数据不能为空");
            ResolveAfterSalesServiceCustomer(item);
            item.ContactName = (item.ContactName ?? "").Trim();
            item.ContactPhone = (item.ContactPhone ?? "").Trim();
            item.MachineName = (item.MachineName ?? "").Trim();
            item.MachineSpec = (item.MachineSpec ?? "").Trim();
            item.FaultDescription = (item.FaultDescription ?? "").Trim();
            item.ServiceType = string.IsNullOrWhiteSpace(item.ServiceType) ? "保外收费" : item.ServiceType.Trim();
            item.AssignedWorker = (item.AssignedWorker ?? "").Trim();
            item.VisitDate = (item.VisitDate ?? "").Trim();
            item.RepairResult = (item.RepairResult ?? "").Trim();
            item.Remark = (item.Remark ?? "").Trim();
            item.ServiceDate = string.IsNullOrWhiteSpace(item.ServiceDate) ? TodayText() : item.ServiceDate.Trim();
            if (item.Parts == null) item.Parts = new List<AfterSalesPartLine>();
            int lineNo = 1;
            foreach (var line in item.Parts)
            {
                if (line != null && line.LineNo <= 0) line.LineNo = lineNo;
                ResolveAfterSalesPartLine(line);
                lineNo++;
            }
            if (!preserveReceivableLink)
            {
                item.ReceivableId = (item.ReceivableId ?? "").Trim();
                item.ReceivableNo = (item.ReceivableNo ?? "").Trim();
            }
            item.Status = NormalizeAfterSalesServiceStatus(item.Status);
            if (string.IsNullOrWhiteSpace(item.Status)) item.Status = "草稿";
            ApplyAfterSalesServiceTypeHints(item);
            SyncAfterSalesServiceAmounts(item);
        }

        static void CopyAfterSalesServiceEditableFields(AfterSalesServiceOrder target, AfterSalesServiceOrder input, bool coreEditable)
        {
            target.ServiceDate = input.ServiceDate;
            target.CustomerId = input.CustomerId;
            target.CustomerName = input.CustomerName;
            target.ContactName = input.ContactName;
            target.ContactPhone = input.ContactPhone;
            target.MachineName = input.MachineName;
            target.MachineSpec = input.MachineSpec;
            target.FaultDescription = input.FaultDescription;
            target.ServiceType = input.ServiceType;
            target.AssignedWorker = input.AssignedWorker;
            target.VisitDate = input.VisitDate;
            target.RepairResult = input.RepairResult;
            target.Remark = input.Remark;
            if (coreEditable)
            {
                target.Parts = input.Parts ?? new List<AfterSalesPartLine>();
                target.LaborAmount = input.LaborAmount;
                target.OtherAmount = input.OtherAmount;
                target.DiscountAmount = input.DiscountAmount;
                target.ReceivedAmount = input.ReceivedAmount;
                if (target.Status == "草稿" || target.Status == "待派工")
                    target.Status = NormalizeAfterSalesServiceStatus(input.Status);
            }
        }

        static string FormatAfterSalesAuditDetail(string action, AfterSalesServiceOrder item, string oldStatus = null, string extra = null)
        {
            if (item == null) return "";
            string no = (item.ServiceNo ?? "").Trim();
            string cust = (item.CustomerName ?? "").Trim();
            string status = (item.Status ?? "").Trim();
            string fault = (item.FaultDescription ?? "").Trim();
            if (fault.Length > 40) fault = fault.Substring(0, 40) + "…";
            string amt = item.ReceivableAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(extra) && extra.IndexOf("应收单:", StringComparison.Ordinal) >= 0)
            {
                string recNo = extra.Replace("应收单:", "").Trim();
                return string.Format("售后维修工单 {0} 已生成应收 {1}，客户：{2}，应收 {3} 元", no, recNo, cust, amt);
            }
            if (!string.IsNullOrWhiteSpace(oldStatus) && oldStatus != status)
            {
                if (status == "已取消")
                    return string.Format("关闭售后维修工单 {0}，处理结果：{1}", no, string.IsNullOrWhiteSpace(item.RepairResult) ? status : item.RepairResult.Trim());
                if (oldStatus == "已取消")
                    return string.Format("重新打开售后维修工单 {0}", no);
                string step = string.IsNullOrWhiteSpace(extra) ? "状态变更" : extra;
                return string.Format("售后维修工单 {0} 状态由「{1}」变更为「{2}」", no, oldStatus, status);
            }
            if ((action ?? "").IndexOf("删除", StringComparison.Ordinal) >= 0)
                return string.Format("删除售后维修工单 {0}", no);
            if ((action ?? "").IndexOf("新增", StringComparison.Ordinal) >= 0)
                return string.Format("新增售后维修工单 {0}，客户：{1}，问题：{2}", no, cust, string.IsNullOrWhiteSpace(fault) ? "（未填）" : fault);
            if ((action ?? "").IndexOf("修改", StringComparison.Ordinal) >= 0)
                return string.Format("修改售后维修工单 {0}", no);
            if (!string.IsNullOrWhiteSpace(no))
                return string.Format("售后维修工单 {0} 发生了操作，客户：{1}", no, cust);
            return string.Format("售后维修相关操作，客户：{0}", cust);
        }

        static void AuditAfterSalesServiceOrder(UserSession user, string action, AfterSalesServiceOrder item, string oldStatus = null, string extra = null)
        {
            Audit(user, action, FormatAfterSalesAuditDetail(action, item, oldStatus, extra));
        }

        static void GetAfterSalesServiceOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            var item = LoadAfterSalesServiceOrders().FirstOrDefault(x => x.Id == id);
            if (item == null) throw new BusinessException("售后维修工单不存在", 404);
            WriteJson(ctx, FilterAfterSalesPartsForPermission(item, user));
        }

        static void AddAfterSalesServiceOrder(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<AfterSalesServiceOrder>(ReadBody(ctx.Request));
            if (!RequireAfterSalesPartActionPermissions(ctx, user, item)) return;
            ApplyAfterSalesServiceOrder(item, false);
            ValidateStockForAfterSalesServiceOrder(item);
            string now = BizUpdatedAtNow();
            var saved = MutateJsonList<AfterSalesServiceOrder, AfterSalesServiceOrder>(AfterSalesServiceOrdersFile, "after_sales_service_orders", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.ServiceNo = NextAfterSalesServiceNo(list);
                item.CreatedAt = now;
                item.UpdatedAt = now;
                item.CreatedBy = user.DisplayName;
                item.UpdatedBy = user.DisplayName;
                RecordAfterSalesServiceMovement(user, null, item);
                list.Insert(0, item);
                return new JsonMutationResult<AfterSalesServiceOrder>(item, true);
            });
            AuditAfterSalesServiceOrder(user, "新增售后维修工单", saved);
            WriteJson(ctx, saved, 201);
        }

        static void UpdateAfterSalesServiceOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<AfterSalesServiceOrder>(ReadBody(ctx.Request));
            if (!RequireAfterSalesPartActionPermissions(ctx, user, input)) return;
            var saved = MutateJsonList<AfterSalesServiceOrder, AfterSalesServiceOrder>(AfterSalesServiceOrdersFile, "after_sales_service_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("售后维修工单不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                if (!CanEditAfterSalesServiceOrder(item.Status)) BizFail("当前状态不允许修改", 409);
                bool coreEditable = item.Status == "草稿" || item.Status == "待派工" || item.Status == "维修中";
                var before = CloneAfterSalesServiceOrder(item);
                string receivableId = item.ReceivableId;
                string receivableNo = item.ReceivableNo;
                if (!HasPermission(user, "after_sales.parts_view"))
                    input.Parts = before == null ? item.Parts : before.Parts;
                CopyAfterSalesServiceEditableFields(item, input, coreEditable);
                item.ReceivableId = receivableId;
                item.ReceivableNo = receivableNo;
                ApplyAfterSalesServiceOrder(item, true);
                ValidateStockForAfterSalesServiceOrder(item, id);
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                RecordAfterSalesServiceMovement(user, before, item);
                return new JsonMutationResult<AfterSalesServiceOrder>(item, true);
            });
            AuditAfterSalesServiceOrder(user, "修改售后维修工单", saved);
            WriteJson(ctx, saved);
        }

        static void DeleteAfterSalesServiceOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            AfterSalesServiceOrder auditItem = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<AfterSalesServiceOrder>(AfterSalesServiceOrdersFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("售后维修工单不存在", 404);
                if (!CanDeleteAfterSalesServiceOrder(item))
                {
                    if (!string.IsNullOrWhiteSpace(item.ReceivableId))
                        throw new BusinessException("已生成应收的维修单不能删除，请先处理应收关联", 409);
                    throw new BusinessException("仅草稿或已取消的维修单可以删除", 409);
                }
                auditItem = item;
            });
            EnforceDeleteImpact("afterSalesServiceOrder", id, user, ctx, auditItem == null ? null : auditItem.ServiceNo);
            MutateJsonList<AfterSalesServiceOrder, object>(AfterSalesServiceOrdersFile, "after_sales_service_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("售后维修工单不存在", 404);
                if (!CanDeleteAfterSalesServiceOrder(item))
                {
                    if (!string.IsNullOrWhiteSpace(item.ReceivableId))
                        BizFail("已生成应收的维修单不能删除，请先处理应收关联", 409);
                    BizFail("仅草稿或已取消的维修单可以删除", 409);
                }
                RecordAfterSalesServiceMovement(user, item, null, true);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            AuditAfterSalesServiceOrder(user, "删除售后维修工单", auditItem);
            WriteJson(ctx, new { ok = true });
        }

        static void ConfirmDispatchAfterSalesServiceOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            string oldStatus = null;
            var saved = MutateJsonList<AfterSalesServiceOrder, AfterSalesServiceOrder>(AfterSalesServiceOrdersFile, "after_sales_service_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("售后维修工单不存在", 404);
                if (NormalizeAfterSalesServiceStatus(item.Status) != "草稿") BizFail("仅草稿状态可以确认派工", 409);
                if (string.IsNullOrWhiteSpace(item.AssignedWorker)) BizFail("请先填写派工人员");
                var before = CloneAfterSalesServiceOrder(item);
                oldStatus = item.Status;
                item.Status = "待派工";
                ValidateStockForAfterSalesServiceOrder(item, id);
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                RecordAfterSalesServiceMovement(user, before, item);
                return new JsonMutationResult<AfterSalesServiceOrder>(item, true);
            });
            AuditAfterSalesServiceOrder(user, "售后维修状态流转", saved, oldStatus, "确认派工");
            WriteJson(ctx, saved);
        }

        static void StartAfterSalesServiceOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            string oldStatus = null;
            var saved = MutateJsonList<AfterSalesServiceOrder, AfterSalesServiceOrder>(AfterSalesServiceOrdersFile, "after_sales_service_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("售后维修工单不存在", 404);
                if (NormalizeAfterSalesServiceStatus(item.Status) != "待派工") BizFail("仅待派工状态可以开始维修", 409);
                var before = CloneAfterSalesServiceOrder(item);
                oldStatus = item.Status;
                item.Status = "维修中";
                if (string.IsNullOrWhiteSpace(item.VisitDate)) item.VisitDate = TodayText();
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                RecordAfterSalesServiceMovement(user, before, item);
                return new JsonMutationResult<AfterSalesServiceOrder>(item, true);
            });
            AuditAfterSalesServiceOrder(user, "售后维修状态流转", saved, oldStatus, "开始维修");
            WriteJson(ctx, saved);
        }

        static void FinishAfterSalesServiceOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            string oldStatus = null;
            var saved = MutateJsonList<AfterSalesServiceOrder, AfterSalesServiceOrder>(AfterSalesServiceOrdersFile, "after_sales_service_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("售后维修工单不存在", 404);
                if (NormalizeAfterSalesServiceStatus(item.Status) != "维修中") BizFail("仅维修中状态可以完成维修", 409);
                var before = CloneAfterSalesServiceOrder(item);
                oldStatus = item.Status;
                item.Status = "已完成";
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                RecordAfterSalesServiceMovement(user, before, item);
                return new JsonMutationResult<AfterSalesServiceOrder>(item, true);
            });
            AuditAfterSalesServiceOrder(user, "售后维修状态流转", saved, oldStatus, "完成维修");
            WriteJson(ctx, saved);
        }

        static void SettleAfterSalesServiceOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            string oldStatus = null;
            var saved = MutateJsonList<AfterSalesServiceOrder, AfterSalesServiceOrder>(AfterSalesServiceOrdersFile, "after_sales_service_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("售后维修工单不存在", 404);
                if (NormalizeAfterSalesServiceStatus(item.Status) != "已完成") BizFail("仅已完成状态可以结算", 409);
                var before = CloneAfterSalesServiceOrder(item);
                oldStatus = item.Status;
                item.Status = "已结算";
                item.ReceivedAmount = item.ReceivableAmount;
                item.UnreceivedAmount = 0;
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                RecordAfterSalesServiceMovement(user, before, item);
                return new JsonMutationResult<AfterSalesServiceOrder>(item, true);
            });
            AuditAfterSalesServiceOrder(user, "售后维修状态流转", saved, oldStatus, "结算");
            WriteJson(ctx, saved);
        }

        static void CancelAfterSalesServiceOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            string oldStatus = null;
            var saved = MutateJsonList<AfterSalesServiceOrder, AfterSalesServiceOrder>(AfterSalesServiceOrdersFile, "after_sales_service_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("售后维修工单不存在", 404);
                var st = NormalizeAfterSalesServiceStatus(item.Status);
                if (st == "已完成" || st == "已结算") BizFail("已完成或已结算的维修单不能取消", 409);
                if (!string.IsNullOrWhiteSpace(item.ReceivableId)) BizFail("已生成应收的维修单不能取消，请先处理应收", 409);
                var before = CloneAfterSalesServiceOrder(item);
                oldStatus = item.Status;
                item.Status = "已取消";
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                RecordAfterSalesServiceMovement(user, before, item);
                return new JsonMutationResult<AfterSalesServiceOrder>(item, true);
            });
            AuditAfterSalesServiceOrder(user, "售后维修状态流转", saved, oldStatus, "取消");
            WriteJson(ctx, saved);
        }

        static void GenerateReceivableForAfterSalesServiceOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            AfterSalesServiceOrder savedOrder = null;
            Receivable savedReceivable = null;
            var result = MutateJsonList<AfterSalesServiceOrder, object>(AfterSalesServiceOrdersFile, "after_sales_service_orders", orderList =>
            {
                var order = orderList.FirstOrDefault(x => x.Id == id);
                if (order == null) throw new BusinessException("售后维修工单不存在", 404);
                var st = NormalizeAfterSalesServiceStatus(order.Status);
                if (st != "已完成" && st != "已结算") BizFail("仅已完成或已结算的维修单可以生成应收", 409);
                if (!string.IsNullOrWhiteSpace(order.ReceivableId)) BizFail("该维修单已关联应收款，不能重复生成", 409);
                if (order.ReceivableAmount <= 0) BizFail("应收合计为 0，无需生成应收款", 409);

                var receivableSaved = MutateJsonList<Receivable, Receivable>(ReceivablesFile, "receivables", receivableList =>
                {
                    var existing = receivableList.FirstOrDefault(x => x.ServiceOrderId == order.Id);
                    if (existing != null) BizFail("该维修单已有关联应收款，不能重复生成", 409);

                    var rec = new Receivable
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Code = NextCode(ReceivableSequenceFile, "AR", receivableList.Select(x => x.Code), "YS"),
                        CustomerName = order.CustomerName ?? "",
                        ServiceOrderId = order.Id,
                        ServiceOrderNo = order.ServiceNo ?? "",
                        ReceivableAmount = order.ReceivableAmount,
                        ReceivedAmount = order.ReceivedAmount,
                        UnreceivedAmount = order.UnreceivedAmount,
                        DueDate = TodayText(),
                        Status = order.UnreceivedAmount <= 0 ? "已收清" : (order.ReceivedAmount > 0 ? "部分收款" : "未收款"),
                        Note = "由售后维修工单 " + (order.ServiceNo ?? "") + " 生成",
                        SourceType = "售后维修",
                        ReceiptDetails = new List<ReceiptDetail>(),
                        UpdatedAt = BizUpdatedAtNow(),
                        UpdatedBy = user.DisplayName
                    };
                    ValidateReceivableTotals(rec);
                    receivableList.Insert(0, rec);
                    order.ReceivableId = rec.Id;
                    order.ReceivableNo = rec.Code;
                    order.UpdatedAt = BizUpdatedAtNow();
                    order.UpdatedBy = user.DisplayName;
                    return new JsonMutationResult<Receivable>(rec, true);
                });

                savedOrder = order;
                savedReceivable = receivableSaved;
                return new JsonMutationResult<object>(new { order = order, receivable = receivableSaved }, true);
            });

            AuditAfterSalesServiceOrder(user, "售后维修生成应收", savedOrder, null, "应收单:" + (savedReceivable == null ? "" : savedReceivable.Code));
            WriteJson(ctx, result);
        }

        static void ExportAfterSalesServiceOrdersCsv(HttpListenerContext ctx)
        {
            var list = LoadAfterSalesServiceOrders();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("维修单号,日期,客户,设备/机型,维修类型,派工人员,配件费,人工费,其他费用,优惠,应收合计,已收,未收,状态,关联应收");
            foreach (var x in list)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(x.ServiceNo), Csv(x.ServiceDate), Csv(x.CustomerName),
                    Csv(x.MachineName), Csv(x.ServiceType), Csv(x.AssignedWorker),
                    x.PartsAmount.ToString("0.##"), x.LaborAmount.ToString("0.##"), x.OtherAmount.ToString("0.##"),
                    x.DiscountAmount.ToString("0.##"), x.ReceivableAmount.ToString("0.##"),
                    x.ReceivedAmount.ToString("0.##"), x.UnreceivedAmount.ToString("0.##"),
                    Csv(x.Status), Csv(x.ReceivableNo)
                }));
            }
            WriteCsvDownload(ctx, "售后维修工单_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv", sb.ToString());
        }
    }
}
