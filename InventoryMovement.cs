using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SupplierErpApp
{
    public class InventoryMovement
    {
        public string Id { get; set; }
        public string MovementNo { get; set; }
        public string OccurredAt { get; set; }
        public string MaterialId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public string Spec { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }
        public string WarehouseId { get; set; }
        public string WarehouseName { get; set; }
        public string Direction { get; set; }
        public decimal ChangeQuantity { get; set; }
        public decimal BeforeQuantity { get; set; }
        public decimal AfterQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal ChangeAmount { get; set; }
        public string SourceType { get; set; }
        public string SourceId { get; set; }
        public string SourceLineId { get; set; }
        public string SourceNo { get; set; }
        public string ActionType { get; set; }
        public string SourceStatus { get; set; }
        public string BusinessDate { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
        public string CreatedAt { get; set; }
    }

    public static partial class Program
    {
        static string InventoryMovementsFile;
        static string InventoryMovementSequenceFile;

        static List<InventoryMovement> LoadInventoryMovements()
        {
            return LoadJsonList<InventoryMovement>(InventoryMovementsFile);
        }

        static string NextInventoryMovementNo(IEnumerable<string> existingNos)
        {
            string prefix = "INV" + DateTime.Now.ToString("yyyyMMdd");
            return NextCode(InventoryMovementSequenceFile, prefix, existingNos ?? new string[0]);
        }

        static bool SameInventoryToken(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        static string JoinInventorySourceNo(params string[] values)
        {
            return string.Join(" / ", (values ?? new string[0]).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        }

        static bool InventorySourceNoMatches(string sourceNo, string token)
        {
            sourceNo = (sourceNo ?? "").Trim();
            token = (token ?? "").Trim();
            if (string.IsNullOrWhiteSpace(sourceNo) || string.IsNullOrWhiteSpace(token)) return false;
            return string.Equals(sourceNo, token, StringComparison.OrdinalIgnoreCase)
                || sourceNo.Split('/').Any(x => string.Equals(x.Trim(), token, StringComparison.OrdinalIgnoreCase));
        }

        static string InventoryDirectionLabel(string direction)
        {
            switch ((direction ?? "").Trim())
            {
                case "In": return "入库";
                case "Out": return "出库";
                case "Rollback": return "回滚";
                case "Adjust": return "调整";
                default: return direction ?? "";
            }
        }

        static string NormalizeInventoryDirection(string value)
        {
            value = (value ?? "").Trim();
            if (string.Equals(value, "入库", StringComparison.OrdinalIgnoreCase)) return "In";
            if (string.Equals(value, "出库", StringComparison.OrdinalIgnoreCase)) return "Out";
            if (string.Equals(value, "回滚", StringComparison.OrdinalIgnoreCase)) return "Rollback";
            if (string.Equals(value, "调整", StringComparison.OrdinalIgnoreCase)) return "Adjust";
            return value;
        }

        static string InventorySourceStatus(string beforeStatus, string afterStatus, bool deleting)
        {
            if (deleting) return "删除回滚";
            if (!IsConfirmedStatus(beforeStatus) && IsConfirmedStatus(afterStatus)) return "确认";
            if (IsConfirmedStatus(beforeStatus) && !IsConfirmedStatus(afterStatus)) return "取消回滚";
            return "修改差额";
        }

        static void AuditInventoryProtectionBlock(string message)
        {
            try
            {
                Audit(_auditContext == null ? null : Authenticate(_auditContext), "库存保护拦截", message);
            }
            catch { }
        }

        static string InventoryDirectionFromDelta(decimal delta, string sourceStatus, bool positiveSource)
        {
            if (sourceStatus == "取消回滚" || sourceStatus == "删除回滚") return "Rollback";
            if (sourceStatus == "修改差额")
            {
                if (positiveSource && delta < 0) return "Rollback";
                if (!positiveSource && delta > 0) return "Rollback";
            }
            if (delta > 0) return "In";
            if (delta < 0) return "Out";
            return "Adjust";
        }

        static Material FindMaterialForMovement(string materialId, string materialCode)
        {
            var materials = LoadMaterials();
            if (!string.IsNullOrWhiteSpace(materialId))
            {
                var byId = materials.FirstOrDefault(x => string.Equals(x.Id ?? "", materialId, StringComparison.OrdinalIgnoreCase));
                if (byId != null) return byId;
            }
            if (!string.IsNullOrWhiteSpace(materialCode))
            {
                var byCode = materials.FirstOrDefault(x => string.Equals(x.Code ?? "", materialCode, StringComparison.OrdinalIgnoreCase));
                if (byCode != null) return byCode;
            }
            return null;
        }

        static string MovementWarehouseName(string itemType, string materialId, string materialCode)
        {
            if (string.Equals(itemType ?? "", "物料", StringComparison.OrdinalIgnoreCase))
            {
                var mat = FindMaterialForMovement(materialId, materialCode);
                if (mat != null && !string.IsNullOrWhiteSpace(mat.DefaultWarehouse)) return mat.DefaultWarehouse;
            }
            return "默认仓库";
        }

        static string MovementCategory(string itemType, string materialId, string materialCode)
        {
            if (string.Equals(itemType ?? "", "成品", StringComparison.OrdinalIgnoreCase)) return "成品设备";
            var mat = FindMaterialForMovement(materialId, materialCode);
            return mat != null && !string.IsNullOrWhiteSpace(mat.StockType) ? mat.StockType : "外购配件";
        }

        static decimal CurrentMovementStockQty(string itemType, string itemId, string itemName)
        {
            var map = BuildStockMap();
            StockAgg agg;
            return map.TryGetValue(StockKey(itemType, itemId, itemName), out agg) ? RoundMoney(agg.Quantity) : 0;
        }

        static InventoryMovement AppendInventoryMovement(InventoryMovement movement, UserSession user)
        {
            if (movement == null || movement.ChangeQuantity == 0) return null;
            var list = ReadJsonListCore<InventoryMovement>(InventoryMovementsFile) ?? new List<InventoryMovement>();
            movement.Id = string.IsNullOrWhiteSpace(movement.Id) ? Guid.NewGuid().ToString("N") : movement.Id;
            movement.MovementNo = string.IsNullOrWhiteSpace(movement.MovementNo) ? NextInventoryMovementNo(list.Select(x => x.MovementNo)) : movement.MovementNo;
            movement.OccurredAt = string.IsNullOrWhiteSpace(movement.OccurredAt) ? NowTimeString() : movement.OccurredAt;
            movement.CreatedAt = string.IsNullOrWhiteSpace(movement.CreatedAt) ? movement.OccurredAt : movement.CreatedAt;
            movement.Operator = string.IsNullOrWhiteSpace(movement.Operator) ? (user == null ? "系统" : user.DisplayName) : movement.Operator;
            movement.ChangeQuantity = RoundMoney(movement.ChangeQuantity);
            movement.BeforeQuantity = RoundMoney(movement.BeforeQuantity);
            movement.AfterQuantity = RoundMoney(movement.AfterQuantity);
            movement.UnitCost = RoundMoney(movement.UnitCost);
            movement.ChangeAmount = RoundMoney(movement.ChangeAmount);
            list.Insert(0, movement);
            WriteJsonListCore(InventoryMovementsFile, "inventory_movements", list);
            Audit(user, "库存流水生成成功", movement.MovementNo + " " + movement.SourceType + " " + movement.SourceNo + " " + InventoryDirectionLabel(movement.Direction) + " " + movement.ChangeQuantity.ToString("0.##"));
            return movement;
        }

        static void RecordInventoryDelta(UserSession user, string itemType, string itemId, string itemCode, string itemName,
            string spec, string unit, decimal delta, decimal unitCost, string sourceType, string sourceId, string sourceNo,
            string sourceStatus, string businessDate, bool positiveSource, string remark, string actionType = "", string sourceLineId = "", string warehouseName = "")
        {
            if (delta == 0) return;
            string resolvedId = itemId ?? "", resolvedCode = itemCode ?? "", resolvedName = itemName ?? "", resolvedSpec = spec ?? "", resolvedUnit = unit ?? "";
            if (string.Equals(itemType ?? "", "物料", StringComparison.OrdinalIgnoreCase))
            {
                ResolveMaterialFields(itemId, itemName, out resolvedId, out resolvedCode, out resolvedName, out resolvedSpec, out resolvedUnit);
                if (!string.IsNullOrWhiteSpace(itemCode)) resolvedCode = itemCode;
            }

            decimal before = CurrentMovementStockQty(itemType, resolvedId, resolvedName);
            decimal after = before + delta;
            AppendInventoryMovement(new InventoryMovement
            {
                MaterialId = resolvedId,
                MaterialCode = resolvedCode,
                MaterialName = resolvedName,
                Spec = resolvedSpec,
                Category = MovementCategory(itemType, resolvedId, resolvedCode),
                Unit = resolvedUnit,
                WarehouseId = "",
                WarehouseName = string.IsNullOrWhiteSpace(warehouseName) ? MovementWarehouseName(itemType, resolvedId, resolvedCode) : warehouseName,
                Direction = InventoryDirectionFromDelta(delta, sourceStatus, positiveSource),
                ChangeQuantity = delta,
                BeforeQuantity = before,
                AfterQuantity = after,
                UnitCost = unitCost,
                ChangeAmount = Math.Abs(delta) * unitCost,
                SourceType = sourceType,
                SourceId = sourceId,
                SourceLineId = sourceLineId,
                SourceNo = sourceNo,
                ActionType = actionType,
                SourceStatus = sourceStatus,
                BusinessDate = businessDate,
                Operator = user == null ? "系统" : user.DisplayName,
                Remark = remark
            }, user);
        }

        static void RecordPurchaseInboundMovement(UserSession user, PurchaseInbound before, PurchaseInbound after, bool deleting = false)
        {
            decimal beforeImpact = before != null && IsConfirmedStatus(before.Status) ? before.Quantity : 0;
            decimal afterImpact = after != null && IsConfirmedStatus(after.Status) ? after.Quantity : 0;
            decimal delta = afterImpact - beforeImpact;
            if (delta == 0) return;
            var source = after ?? before;
            string status = InventorySourceStatus(before == null ? null : before.Status, after == null ? null : after.Status, deleting);
            RecordInventoryDelta(user, "物料", source.MaterialId, source.MaterialCode, source.MaterialName, "", "", delta, source.InboundPrice,
                "采购入库", source.Id, source.Code, status, source.InboundDate, true, "采购入库库存变动");
        }

        static string InventoryImpactKey(InventoryImpactLine line)
        {
            if (line == null) return "";
            return string.Join("|", new[] {
                line.LineId ?? "",
                line.LineNo.ToString(System.Globalization.CultureInfo.InvariantCulture),
                line.ItemType ?? "",
                line.ItemId ?? "",
                line.ItemCode ?? "",
                line.ItemName ?? "",
                line.ActionType ?? ""
            });
        }

        static Dictionary<string, InventoryImpactLine> AggregateInventoryImpacts(IEnumerable<InventoryImpactLine> rows)
        {
            var dict = new Dictionary<string, InventoryImpactLine>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows ?? Enumerable.Empty<InventoryImpactLine>())
            {
                if (row == null || row.Quantity == 0) continue;
                var key = InventoryImpactKey(row);
                InventoryImpactLine agg;
                if (!dict.TryGetValue(key, out agg))
                {
                    agg = new InventoryImpactLine
                    {
                        ItemType = row.ItemType,
                        ItemId = row.ItemId,
                        ItemCode = row.ItemCode,
                        ItemName = row.ItemName,
                        Spec = row.Spec,
                        Unit = row.Unit,
                        UnitCost = row.UnitCost,
                        LineId = row.LineId,
                        LineNo = row.LineNo,
                        ActionType = row.ActionType,
                        WarehouseName = row.WarehouseName
                    };
                    dict[key] = agg;
                }
                agg.Quantity += row.Quantity;
                if (agg.UnitCost <= 0 && row.UnitCost > 0) agg.UnitCost = row.UnitCost;
            }
            return dict;
        }

        static void RecordSalesOutboundMovement(UserSession user, SalesOutbound before, SalesOutbound after, bool deleting = false)
        {
            var beforeMap = AggregateInventoryImpacts(GetSalesOutboundStockImpacts(before));
            var afterMap = AggregateInventoryImpacts(GetSalesOutboundStockImpacts(after));
            var keys = beforeMap.Keys.Union(afterMap.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (keys.Count == 0) return;
            var source = after ?? before;
            string status = InventorySourceStatus(before == null ? null : before.Status, after == null ? null : after.Status, deleting);
            foreach (var key in keys)
            {
                InventoryImpactLine b, a;
                beforeMap.TryGetValue(key, out b);
                afterMap.TryGetValue(key, out a);
                var line = a ?? b;
                decimal delta = (a == null ? 0 : a.Quantity) - (b == null ? 0 : b.Quantity);
                if (delta == 0 || line == null) continue;
                string remark = "销售出库库存变动";
                if (!string.IsNullOrWhiteSpace(line.ActionType)) remark += "，" + line.ActionType;
                if (!string.IsNullOrWhiteSpace(line.ItemName)) remark += "，" + line.ItemName;
                RecordInventoryDelta(user, line.ItemType, line.ItemId, line.ItemCode, line.ItemName, line.Spec, line.Unit, delta, line.UnitCost,
                    "销售出库", source.Id, source.Code, status, source.OutboundDate, false, remark, line.ActionType, line.LineId, line.WarehouseName);
            }
        }

        static void RecordProductionPickMovement(UserSession user, ProductionPick before, ProductionPick after, bool deleting = false)
        {
            decimal beforeImpact = before != null && IsConfirmedStatus(before.Status) ? -before.Quantity : 0;
            decimal afterImpact = after != null && IsConfirmedStatus(after.Status) ? -after.Quantity : 0;
            decimal delta = afterImpact - beforeImpact;
            if (delta == 0) return;
            var source = after ?? before;
            string status = InventorySourceStatus(before == null ? null : before.Status, after == null ? null : after.Status, deleting);
            string sourceNo = JoinInventorySourceNo(source.WorkOrderNo, source.Code);
            string remark = string.IsNullOrWhiteSpace(source.WorkOrderNo) ? "生产领用库存变动" : "生产领用库存变动，工单 " + source.WorkOrderNo;
            RecordInventoryDelta(user, "物料", source.MaterialId, source.MaterialCode, source.MaterialName, "", "", delta, source.CostPrice,
                "生产领用", source.Id, sourceNo, status, source.PickDate, false, remark);
        }

        static void RecordFinishedInboundMovement(UserSession user, FinishedInbound before, FinishedInbound after, bool deleting = false)
        {
            decimal beforeImpact = before != null && IsConfirmedStatus(before.Status) ? before.Quantity : 0;
            decimal afterImpact = after != null && IsConfirmedStatus(after.Status) ? after.Quantity : 0;
            decimal delta = afterImpact - beforeImpact;
            if (delta == 0) return;
            var source = after ?? before;
            string status = InventorySourceStatus(before == null ? null : before.Status, after == null ? null : after.Status, deleting);
            string pid = GetFinishedProductStockId(source.ModelCostId, source.BomId);
            string sourceNo = JoinInventorySourceNo(source.WorkOrderNo, source.Code);
            string remark = string.IsNullOrWhiteSpace(source.WorkOrderNo) ? "成品入库库存变动" : "成品入库库存变动，工单 " + source.WorkOrderNo;
            RecordInventoryDelta(user, "成品", pid, source.BomCode ?? "", source.ProductName, "", "台", delta, source.UnitCost,
                "成品入库", source.Id, sourceNo, status, source.InboundDate, true, remark);
        }

        static string AfterSalesInventorySourceStatus(AfterSalesServiceOrder before, AfterSalesServiceOrder after, bool deleting)
        {
            if (deleting) return "删除回滚";
            bool beforeActive = AfterSalesServiceInventoryActive(before);
            bool afterActive = AfterSalesServiceInventoryActive(after);
            if (!beforeActive && afterActive) return "维修领料";
            if (beforeActive && !afterActive) return "取消回滚";
            return "维修变更";
        }

        static void RecordAfterSalesServiceMovement(UserSession user, AfterSalesServiceOrder before, AfterSalesServiceOrder after, bool deleting = false)
        {
            var beforeMap = AggregateInventoryImpacts(GetAfterSalesServiceStockImpacts(before));
            var afterMap = AggregateInventoryImpacts(GetAfterSalesServiceStockImpacts(after));
            var keys = beforeMap.Keys.Union(afterMap.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (keys.Count == 0) return;
            var source = after ?? before;
            string status = AfterSalesInventorySourceStatus(before, after, deleting);
            foreach (var key in keys)
            {
                InventoryImpactLine b, a;
                beforeMap.TryGetValue(key, out b);
                afterMap.TryGetValue(key, out a);
                var line = a ?? b;
                decimal delta = (a == null ? 0 : a.Quantity) - (b == null ? 0 : b.Quantity);
                if (delta == 0 || line == null) continue;
                string action = string.IsNullOrWhiteSpace(line.ActionType) ? "维修物料" : line.ActionType;
                string remark = "维修业务库存变动，" + action;
                if (!string.IsNullOrWhiteSpace(line.ItemName)) remark += "，" + line.ItemName;
                RecordInventoryDelta(user, "物料", line.ItemId, line.ItemCode, line.ItemName, line.Spec, line.Unit, delta, line.UnitCost,
                    "维修业务", source.Id, source.ServiceNo, status, source.ServiceDate, false, remark, action, line.LineId, line.WarehouseName);
                try { Audit(user, "维修" + action, (source.ServiceNo ?? "") + " " + (line.ItemName ?? "") + " " + Math.Abs(delta).ToString("0.##")); } catch { }
            }
        }

        static IEnumerable<InventoryMovement> FilterInventoryMovements(HttpListenerContext ctx, IEnumerable<InventoryMovement> source)
        {
            string keyword = (GetQueryParam(ctx, "keyword") ?? "").Trim().ToLowerInvariant();
            string materialId = (GetQueryParam(ctx, "materialId") ?? "").Trim();
            string itemId = (GetQueryParam(ctx, "itemId") ?? "").Trim();
            string itemCode = (GetQueryParam(ctx, "itemCode") ?? "").Trim();
            string warehouseName = (GetQueryParam(ctx, "warehouseName") ?? "").Trim();
            string sourceType = (GetQueryParam(ctx, "sourceType") ?? "").Trim();
            string sourceNo = (GetQueryParam(ctx, "sourceNo") ?? "").Trim();
            string direction = NormalizeInventoryDirection(GetQueryParam(ctx, "direction"));
            string dateFrom = (GetQueryParam(ctx, "dateFrom") ?? "").Trim();
            string dateTo = (GetQueryParam(ctx, "dateTo") ?? "").Trim();

            return source.Where(x =>
            {
                if (!string.IsNullOrWhiteSpace(materialId) && !SameInventoryToken(x.MaterialId, materialId)) return false;
                if (!string.IsNullOrWhiteSpace(itemId) && !SameInventoryToken(x.MaterialId, itemId)) return false;
                if (!string.IsNullOrWhiteSpace(itemCode) && !SameInventoryToken(x.MaterialCode, itemCode)) return false;
                if (!string.IsNullOrWhiteSpace(warehouseName) && (x.WarehouseName ?? "").IndexOf(warehouseName, StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrWhiteSpace(sourceType) && !string.Equals(x.SourceType ?? "", sourceType, StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.IsNullOrWhiteSpace(sourceNo) && (x.SourceNo ?? "").IndexOf(sourceNo, StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrWhiteSpace(direction) && !string.Equals(x.Direction ?? "", direction, StringComparison.OrdinalIgnoreCase)) return false;
                string date = (x.OccurredAt ?? x.CreatedAt ?? "").Length >= 10 ? (x.OccurredAt ?? x.CreatedAt).Substring(0, 10) : "";
                if (!string.IsNullOrWhiteSpace(dateFrom) && string.Compare(date, dateFrom, StringComparison.Ordinal) < 0) return false;
                if (!string.IsNullOrWhiteSpace(dateTo) && string.Compare(date, dateTo, StringComparison.Ordinal) > 0) return false;
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string hay = string.Join(" ", new[] { x.MovementNo, x.MaterialCode, x.MaterialName, x.Spec, x.Category, x.WarehouseName, x.SourceType, x.SourceNo, x.SourceLineId, x.ActionType, x.SourceStatus, x.Remark }).ToLowerInvariant();
                    if (hay.IndexOf(keyword, StringComparison.Ordinal) < 0) return false;
                }
                return true;
            });
        }

        static object MovementWithSource(InventoryMovement x)
        {
            return new
            {
                x.Id,
                x.MovementNo,
                x.OccurredAt,
                x.MaterialId,
                x.MaterialCode,
                x.MaterialName,
                x.Spec,
                x.Category,
                x.Unit,
                x.WarehouseId,
                x.WarehouseName,
                x.Direction,
                DirectionLabel = InventoryDirectionLabel(x.Direction),
                x.ChangeQuantity,
                x.BeforeQuantity,
                x.AfterQuantity,
                x.UnitCost,
                x.ChangeAmount,
                x.SourceType,
                x.SourceId,
                x.SourceLineId,
                x.SourceNo,
                x.ActionType,
                x.SourceStatus,
                x.BusinessDate,
                x.Operator,
                x.Remark,
                x.CreatedAt,
                Source = BuildInventoryMovementSourceSummary(x)
            };
        }

        static void ListInventoryMovements(HttpListenerContext ctx)
        {
            var rows = FilterInventoryMovements(ctx, LoadInventoryMovements())
                .OrderByDescending(x => x.OccurredAt ?? x.CreatedAt ?? "")
                .ThenByDescending(x => x.MovementNo ?? "")
                .Take(1000)
                .Select(MovementWithSource)
                .ToArray();
            WriteJson(ctx, rows);
        }

        static bool StockItemMatchesMovement(StockItem item, InventoryMovement movement)
        {
            if (item == null || movement == null) return false;
            return SameInventoryToken(item.ItemId, movement.MaterialId)
                || SameInventoryToken(item.ItemCode, movement.MaterialCode)
                || (!string.IsNullOrWhiteSpace(item.ItemName) && string.Equals(item.ItemName, movement.MaterialName ?? "", StringComparison.OrdinalIgnoreCase));
        }

        static StockItem FindStockItemForDetail(HttpListenerContext ctx)
        {
            string itemId = (GetQueryParam(ctx, "itemId") ?? "").Trim();
            if (string.IsNullOrWhiteSpace(itemId)) itemId = (GetQueryParam(ctx, "materialId") ?? "").Trim();
            string itemCode = (GetQueryParam(ctx, "itemCode") ?? "").Trim();
            if (string.IsNullOrWhiteSpace(itemCode)) itemCode = (GetQueryParam(ctx, "materialCode") ?? "").Trim();
            string itemName = (GetQueryParam(ctx, "itemName") ?? "").Trim();
            if (string.IsNullOrWhiteSpace(itemName)) itemName = (GetQueryParam(ctx, "materialName") ?? "").Trim();
            return BuildStockItems().FirstOrDefault(x =>
                SameInventoryToken(x.ItemId, itemId)
                || SameInventoryToken(x.ItemCode, itemCode)
                || (!string.IsNullOrWhiteSpace(itemName) && string.Equals(x.ItemName ?? "", itemName, StringComparison.OrdinalIgnoreCase)));
        }

        static void GetStockDetail(HttpListenerContext ctx)
        {
            var item = FindStockItemForDetail(ctx);
            var movements = LoadInventoryMovements();
            if (item != null) movements = movements.Where(x => StockItemMatchesMovement(item, x)).ToList();
            else movements = FilterInventoryMovements(ctx, movements).ToList();
            var recentMovementRows = movements.OrderByDescending(x => x.OccurredAt ?? x.CreatedAt ?? "").Take(20).ToList();
            var recent = recentMovementRows.Select(MovementWithSource).ToArray();
            var sourceStats = movements.GroupBy(x => x.SourceType ?? "未知")
                .Select(g => new { SourceType = g.Key, Count = g.Count(), Quantity = RoundMoney(g.Sum(x => x.ChangeQuantity)), Amount = RoundMoney(g.Sum(x => x.ChangeAmount)) })
                .OrderByDescending(x => x.Count)
                .ToArray();
            WriteJson(ctx, new
            {
                Stock = item,
                Current = item,
                LastChangedAt = movements.Select(x => x.OccurredAt ?? x.CreatedAt ?? "").OrderByDescending(x => x).FirstOrDefault() ?? "",
                RecentMovements = recent,
                SourceStats = sourceStats,
                SourceDocuments = recentMovementRows.Select(BuildInventoryMovementSourceSummary).ToArray()
            });
        }

        static void ListStockMovements(HttpListenerContext ctx, string itemId)
        {
            itemId = (itemId ?? "").Trim();
            var rows = LoadInventoryMovements()
                .Where(x => SameInventoryToken(x.MaterialId, itemId) || SameInventoryToken(x.MaterialCode, itemId))
                .OrderByDescending(x => x.OccurredAt ?? x.CreatedAt ?? "")
                .Take(500)
                .Select(MovementWithSource)
                .ToArray();
            WriteJson(ctx, rows);
        }

        static object BuildInventoryMovementSourceSummary(InventoryMovement movement)
        {
            if (movement == null) return null;
            string type = movement.SourceType ?? "";
            string id = movement.SourceId ?? "";
            string no = movement.SourceNo ?? "";
            string businessDate = movement.BusinessDate ?? "";
            string status = movement.SourceStatus ?? "";
            string title = "";
            string workOrderId = "";
            string workOrderNo = "";
            string documentNo = no;
            string sourceLineId = movement.SourceLineId ?? "";
            string actionType = movement.ActionType ?? "";
            bool found = false;

            if (type == "采购入库")
            {
                var x = LoadPurchaseInbounds().FirstOrDefault(v => SameInventoryToken(v.Id, id) || SameInventoryToken(v.Code, no));
                if (x != null) { found = true; businessDate = x.InboundDate; title = x.SupplierName + " / " + x.MaterialName; }
            }
            else if (type == "销售出库")
            {
                var x = LoadSalesOutbounds().FirstOrDefault(v => SameInventoryToken(v.Id, id) || SameInventoryToken(v.Code, no));
                if (x != null)
                {
                    found = true;
                    businessDate = x.OutboundDate;
                    documentNo = x.Code ?? no;
                    var line = SalesOutboundLinesForUse(x).FirstOrDefault(v => SameInventoryToken(v.Id, sourceLineId));
                    if (line != null)
                    {
                        if (string.IsNullOrWhiteSpace(actionType)) actionType = NormalizeSalesOutboundLineType(line.LineType);
                        title = x.CustomerName + " / " + NormalizeSalesOutboundLineType(line.LineType) + " / " + line.MaterialName;
                    }
                    else title = x.CustomerName + " / " + x.MaterialName;
                }
            }
            else if (type == "生产领用")
            {
                var picks = LoadProductionPicks();
                var x = picks.FirstOrDefault(v => SameInventoryToken(v.Id, id) || InventorySourceNoMatches(no, v.Code))
                    ?? picks.FirstOrDefault(v => InventorySourceNoMatches(no, v.WorkOrderNo));
                if (x != null)
                {
                    found = true;
                    businessDate = x.PickDate;
                    status = string.IsNullOrWhiteSpace(x.Status) ? status : x.Status;
                    workOrderId = x.WorkOrderId ?? "";
                    workOrderNo = x.WorkOrderNo ?? "";
                    documentNo = x.Code ?? no;
                    title = JoinInventorySourceNo(x.WorkOrderNo, x.Code, x.BomName, x.MaterialName);
                }
            }
            else if (type == "成品入库")
            {
                var inbounds = LoadFinishedInbounds();
                var x = inbounds.FirstOrDefault(v => SameInventoryToken(v.Id, id) || InventorySourceNoMatches(no, v.Code))
                    ?? inbounds.FirstOrDefault(v => InventorySourceNoMatches(no, v.WorkOrderNo));
                if (x != null)
                {
                    found = true;
                    businessDate = x.InboundDate;
                    status = string.IsNullOrWhiteSpace(x.Status) ? status : x.Status;
                    workOrderId = x.WorkOrderId ?? "";
                    workOrderNo = x.WorkOrderNo ?? "";
                    documentNo = x.Code ?? no;
                    title = JoinInventorySourceNo(x.WorkOrderNo, x.Code, x.ProductName);
                }
            }
            else if (type == "维修业务")
            {
                var x = LoadAfterSalesServiceOrders().FirstOrDefault(v => SameInventoryToken(v.Id, id) || SameInventoryToken(v.ServiceNo, no));
                if (x != null)
                {
                    found = true;
                    businessDate = x.ServiceDate;
                    status = string.IsNullOrWhiteSpace(x.Status) ? status : x.Status;
                    documentNo = x.ServiceNo ?? no;
                    var line = (x.Parts ?? new List<AfterSalesPartLine>()).FirstOrDefault(v => SameInventoryToken(v.Id, sourceLineId));
                    if (line != null)
                    {
                        if (string.IsNullOrWhiteSpace(actionType)) actionType = NormalizeAfterSalesPartActionType(line.ActionType);
                        title = JoinInventorySourceNo(x.ServiceNo, actionType, line.MaterialName);
                    }
                    else title = JoinInventorySourceNo(x.ServiceNo, x.CustomerName, x.MachineName);
                }
            }

            return new
            {
                SourceType = type,
                SourceId = id,
                SourceLineId = sourceLineId,
                SourceNo = no,
                DocumentNo = documentNo,
                WorkOrderId = workOrderId,
                WorkOrderNo = workOrderNo,
                ActionType = actionType,
                BusinessDate = businessDate,
                SourceStatus = status,
                Found = found,
                Title = string.IsNullOrWhiteSpace(title) ? movement.MaterialName : title,
                Summary = type + " " + no + " / " + status + (string.IsNullOrWhiteSpace(workOrderNo) ? "" : " / 工单 " + workOrderNo)
            };
        }

        static void GetInventoryMovementSource(HttpListenerContext ctx, string id)
        {
            var movement = LoadInventoryMovements().FirstOrDefault(x => SameInventoryToken(x.Id, id) || SameInventoryToken(x.MovementNo, id));
            if (movement == null) throw new BusinessException("库存流水不存在", 404);
            WriteJson(ctx, BuildInventoryMovementSourceSummary(movement));
        }

        static void ExportInventoryMovementsCsv(HttpListenerContext ctx, UserSession user)
        {
            var sb = new StringBuilder();
            sb.AppendLine("时间,方向,物料编码,物料名称,规格型号,仓库,变化数量,变动前,变动后,单位,单价,金额,来源类型,来源单号,来源行ID,动作类型,来源状态,操作人,备注");
            foreach (var x in FilterInventoryMovements(ctx, LoadInventoryMovements()).OrderByDescending(x => x.OccurredAt ?? x.CreatedAt ?? ""))
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    x.OccurredAt, InventoryDirectionLabel(x.Direction), x.MaterialCode, x.MaterialName, x.Spec, x.WarehouseName,
                    x.ChangeQuantity.ToString("0.##"), x.BeforeQuantity.ToString("0.##"), x.AfterQuantity.ToString("0.##"),
                    x.Unit, x.UnitCost.ToString("0.00"), x.ChangeAmount.ToString("0.00"), x.SourceType, x.SourceNo, x.SourceLineId, x.ActionType, x.SourceStatus, x.Operator, x.Remark
                }.Select(Csv)));
            }
            Audit(user, "导出库存流水", "库存流水");
            WriteCsvDownload(ctx, BuildExportFileName("库存流水"), sb.ToString());
        }
    }
}
