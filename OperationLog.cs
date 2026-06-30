using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace SupplierErpApp
{
    public class OperationLogEntry
    {
        public string Id { get; set; }
        public string Time { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string Ip { get; set; }
        public string Module { get; set; }
        public string Action { get; set; }
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string EntityCode { get; set; }
        public string EntityName { get; set; }
        public string Result { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public string Api { get; set; }
        public string UserAgent { get; set; }
        public string BackupPath { get; set; }
    }

    public class OperationLogQueryResult
    {
        public OperationLogEntry[] Items { get; set; }
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public static partial class Program
    {
        static string OperationLogsFilePath => Path.Combine(DataDir, "operation_logs.json");
        static string OperationLogSequenceFilePath => Path.Combine(DataDir, "operation_log_sequence.json");
        const int OperationLogMaxEntries = 50000;
        [ThreadStatic] static HttpListenerContext _auditContext;

        static void BeginAuditContext(HttpListenerContext ctx) { _auditContext = ctx; }
        static void EndAuditContext() { _auditContext = null; }

        static void EnsureOperationLogsFile()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                if (!File.Exists(OperationLogsFilePath))
                    File.WriteAllText(OperationLogsFilePath, "[]", new UTF8Encoding(false));
                if (!File.Exists(OperationLogSequenceFilePath))
                    File.WriteAllText(OperationLogSequenceFilePath, "0", new UTF8Encoding(false));
            }
            catch { }
        }

        static string NextOperationLogCode()
        {
            int seq = ReadSequenceValue(OperationLogSequenceFilePath) + 1;
            WriteAllTextAtomic(OperationLogSequenceFilePath, seq.ToString());
            return "LOG" + seq.ToString("D6");
        }

        static string GetClientIp(HttpListenerContext ctx)
        {
            if (ctx == null || ctx.Request == null) return "";
            try
            {
                string forwarded = ctx.Request.Headers["X-Forwarded-For"];
                if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded.Split(',')[0].Trim();
                if (ctx.Request.RemoteEndPoint != null) return ctx.Request.RemoteEndPoint.Address.ToString();
            }
            catch { }
            return "";
        }

        static string SanitizeLogText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            if (text.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0) return "[已隐藏敏感信息]";
            return text.Length > 500 ? text.Substring(0, 500) : text;
        }

        static void ParseAuditAction(string action, string detail, out string module, out string act, out string entityType, out string entityCode, out string entityName)
        {
            module = "系统"; act = action ?? ""; entityType = ""; entityCode = ""; entityName = detail ?? "";
            if (string.IsNullOrWhiteSpace(action)) return;
            if (action.IndexOf("供应商", StringComparison.Ordinal) >= 0) { module = "供应商管理"; entityType = "Supplier"; }
            else if (action.IndexOf("客户", StringComparison.Ordinal) >= 0) { module = "客户管理"; entityType = "Customer"; }
            else if (action.IndexOf("物料", StringComparison.Ordinal) >= 0) { module = "物料管理"; entityType = "Material"; }
            else if (action.IndexOf("BOM", StringComparison.OrdinalIgnoreCase) >= 0) { module = "BOM表"; entityType = "BomItem"; }
            else if (action.IndexOf("机型成本", StringComparison.Ordinal) >= 0) { module = "机型成本"; entityType = "ModelCost"; }
            else if (action.IndexOf("销售订单", StringComparison.Ordinal) >= 0) { module = "销售订单"; entityType = "SalesOrder"; }
            else if (action.IndexOf("销售出库", StringComparison.Ordinal) >= 0) { module = "销售出库"; entityType = "SalesOutbound"; }
            else if (action.IndexOf("采购单", StringComparison.Ordinal) >= 0) { module = "采购单"; entityType = "PurchaseOrder"; }
            else if (action.IndexOf("采购入库", StringComparison.Ordinal) >= 0) { module = "采购入库"; entityType = "PurchaseInbound"; }
            else if (action.IndexOf("生产领用", StringComparison.Ordinal) >= 0) { module = "生产领用"; entityType = "ProductionPick"; }
            else if (action.IndexOf("成品入库", StringComparison.Ordinal) >= 0) { module = "成品入库"; entityType = "FinishedInbound"; }
            else if (action.IndexOf("售后维修", StringComparison.Ordinal) >= 0) { module = "售后维修工单"; entityType = "AfterSalesServiceOrder"; }
            else if (action.IndexOf("业务出入库明细", StringComparison.Ordinal) >= 0 || action.IndexOf("业务明细", StringComparison.Ordinal) >= 0) { module = "业务出入库"; entityType = "BusinessStockDetail"; }
            else if (action.IndexOf("应收", StringComparison.Ordinal) >= 0) { module = "应收款"; entityType = "Receivable"; }
            else if (action.IndexOf("应付", StringComparison.Ordinal) >= 0) { module = "应付款"; entityType = "Payable"; }
            else if (action.IndexOf("收款", StringComparison.Ordinal) >= 0) { module = "应收款"; entityType = "ReceiptDetail"; }
            else if (action.IndexOf("付款", StringComparison.Ordinal) >= 0) { module = "应付款"; entityType = "PaymentDetail"; }
            else if (action.IndexOf("收支", StringComparison.Ordinal) >= 0 || action.IndexOf("期初", StringComparison.Ordinal) >= 0) { module = "财务收支"; entityType = "FinanceTransaction"; }
            else if (action.IndexOf("字典", StringComparison.Ordinal) >= 0) { module = "系统设置"; entityType = "DictionaryOption"; }
            else if (action.IndexOf("子账号", StringComparison.Ordinal) >= 0 || action.IndexOf("账号", StringComparison.Ordinal) >= 0) { module = "系统设置"; entityType = "User"; }
            else if (action.IndexOf("登录", StringComparison.Ordinal) >= 0 || action.IndexOf("退出", StringComparison.Ordinal) >= 0) { module = "登录权限"; entityType = "Session"; }
            else if (action.IndexOf("测试数据", StringComparison.Ordinal) >= 0 || action.IndexOf("清空", StringComparison.Ordinal) >= 0 || action.IndexOf("备份", StringComparison.Ordinal) >= 0) { module = "数据工具"; entityType = "DataMaintenance"; }
            else if (action.IndexOf("税率", StringComparison.Ordinal) >= 0) { module = "系统设置"; entityType = "SystemSettings"; }

            if (action.StartsWith("新增")) act = "新增";
            else if (action.StartsWith("修改") || action.StartsWith("编辑")) act = "修改";
            else if (action.StartsWith("删除")) act = "删除";
            else if (action.StartsWith("批量添加")) act = "批量新增";
            else if (action.StartsWith("批量删除")) act = "批量删除";
            else if (action.StartsWith("导入")) act = "导入";
            else if (action.StartsWith("导出")) act = "导出";
            else if (action.IndexOf("确认业务", StringComparison.Ordinal) >= 0) act = "确认";
            else if (action.IndexOf("取消业务", StringComparison.Ordinal) >= 0) act = "取消确认";
            else if (action.IndexOf("状态流转", StringComparison.Ordinal) >= 0) act = "状态流转";
            else if (action.IndexOf("生成应收", StringComparison.Ordinal) >= 0) act = "生成应收";
            else if (action.StartsWith("登录")) act = action.IndexOf("失败", StringComparison.Ordinal) >= 0 ? "登录失败" : "登录";
            else if (action.StartsWith("退出")) act = "退出";
            else if (action.IndexOf("清空", StringComparison.Ordinal) >= 0) act = action;
            else act = action;

            if (!string.IsNullOrWhiteSpace(detail))
            {
                var parts = detail.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && parts[0].Length <= 24 && (parts[0].Any(char.IsDigit) || parts[0].StartsWith("SR", StringComparison.OrdinalIgnoreCase)))
                    entityCode = parts[0];
                var custIdx = detail.IndexOf("客户:", StringComparison.Ordinal);
                if (custIdx >= 0)
                {
                    var custPart = detail.Substring(custIdx + 3).Trim();
                    var end = custPart.IndexOfAny(new[] { ' ', '状', '应', '关', '配', '，' });
                    entityName = end > 0 ? custPart.Substring(0, end).Trim() : custPart;
                }
                else if (detail.IndexOf("客户 ", StringComparison.Ordinal) >= 0 || detail.IndexOf("客户：", StringComparison.Ordinal) >= 0)
                {
                    var custIdx2 = detail.IndexOf("客户 ", StringComparison.Ordinal);
                    if (custIdx2 < 0) custIdx2 = detail.IndexOf("客户：", StringComparison.Ordinal);
                    int skip = detail[custIdx2 + 2] == '：' ? 3 : 3;
                    var custPart = detail.Substring(custIdx2 + skip).Trim();
                    var end = custPart.IndexOfAny(new[] { '，', ',', ' ', '问', '状', '应', '关', '配' });
                    entityName = end > 0 ? custPart.Substring(0, end).Trim() : custPart;
                }
                TryResolveAfterSalesLogFromDetail(detail, ref module, ref entityType, ref entityCode, action);
            }
            else if (!string.IsNullOrWhiteSpace(action))
            {
                TryResolveAfterSalesLogFromDetail(action, ref module, ref entityType, ref entityCode, action);
            }
            TryExtractEntityCodeFromText(detail ?? action, ref entityCode);
        }

        static string GetClientUserAgent(HttpListenerContext ctx)
        {
            if (ctx == null || ctx.Request == null) return "";
            try { return (ctx.Request.Headers["User-Agent"] ?? "").Trim(); }
            catch { return ""; }
        }

        static bool IsAmbiguousSystemModule(string module)
        {
            module = (module ?? "").Trim();
            return string.IsNullOrWhiteSpace(module)
                || module == "系统"
                || module.Equals("System", StringComparison.OrdinalIgnoreCase)
                || module == "未知模块";
        }

        static bool HasAfterSalesIndicator(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\bSR20\d{6,}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
            string[] keys = { "售后维修", "售后工单", "维修工单", "after-sales", "after sales", "afterSales", "repair order", "AfterSalesServiceOrder" };
            foreach (var k in keys)
            {
                if (text.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return text.IndexOf("维修单", StringComparison.Ordinal) >= 0
                && text.IndexOf("SR", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsPureSystemAuthRecord(string action, string text)
        {
            string blob = ((action ?? "") + " " + (text ?? "")).Trim();
            if (HasAfterSalesIndicator(text)) return false;
            if (blob.IndexOf("登录", StringComparison.Ordinal) >= 0) return true;
            if (blob.IndexOf("退出", StringComparison.Ordinal) >= 0) return true;
            if (blob.IndexOf("权限不足", StringComparison.Ordinal) >= 0) return true;
            if (blob.IndexOf("未登录", StringComparison.Ordinal) >= 0) return true;
            if (blob.IndexOf("字典", StringComparison.Ordinal) >= 0) return true;
            if (blob.IndexOf("子账号", StringComparison.Ordinal) >= 0) return true;
            if (blob.IndexOf("税率", StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        static void TryExtractEntityCodeFromText(string text, ref string entityCode)
        {
            if (!string.IsNullOrWhiteSpace(entityCode)) return;
            var m = System.Text.RegularExpressions.Regex.Match(text ?? "", @"\bSR20\d{6,}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success) entityCode = m.Value;
        }

        static void TryResolveAfterSalesLogFromDetail(string text, ref string module, ref string entityType, ref string entityCode, string action = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (module == "售后维修工单" && entityType == "AfterSalesServiceOrder")
            {
                TryExtractEntityCodeFromText(text, ref entityCode);
                return;
            }
            if (IsPureSystemAuthRecord(action, text)) return;
            if (!IsAmbiguousSystemModule(module) && module != "售后维修工单") return;
            if (!HasAfterSalesIndicator(text)) return;
            module = "售后维修工单";
            entityType = "AfterSalesServiceOrder";
            TryExtractEntityCodeFromText(text, ref entityCode);
        }

        static string FormatOperationLogMessage(string action, string detail, string module, string act)
        {
            if (!string.IsNullOrWhiteSpace(detail))
            {
                if (detail.IndexOf("售后维修工单", StringComparison.Ordinal) >= 0
                    || detail.IndexOf("维修单 ", StringComparison.Ordinal) >= 0)
                    return detail;
                if (!string.IsNullOrWhiteSpace(act) && detail.IndexOf(act, StringComparison.Ordinal) != 0)
                    return act + "：" + detail;
            }
            return string.IsNullOrWhiteSpace(detail) ? (action ?? "") : detail;
        }

        static string HumanizeAfterSalesFallbackMessage(OperationLogEntry e)
        {
            string code = e.EntityCode ?? "";
            if (string.IsNullOrWhiteSpace(code))
            {
                var m = System.Text.RegularExpressions.Regex.Match(e.Message ?? "", @"\bSR20\d{6,}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success) code = m.Value;
            }
            string act = e.Action ?? "操作";
            if (!string.IsNullOrWhiteSpace(code))
                return string.Format("售后维修工单 {0} 发生了【{1}】操作", code, act);
            return "售后维修相关记录发生了【" + act + "】操作";
        }

        static string HumanizeOperationLogMessage(OperationLogEntry e)
        {
            if (e == null) return "";
            string msg = e.Message ?? "";
            if (string.IsNullOrWhiteSpace(msg)) return msg;
            bool isAs = e.Module == "售后维修工单"
                || (e.EntityCode ?? "").StartsWith("SR", StringComparison.OrdinalIgnoreCase)
                || HasAfterSalesIndicator(msg);
            if (!isAs) return msg;
            if (msg.IndexOf("售后维修工单", StringComparison.Ordinal) >= 0) return msg;
            msg = msg.Replace("新增售后维修工单 ", "新增售后维修工单 ");
            msg = msg.Replace("新增维修单 ", "新增售后维修工单 ");
            msg = msg.Replace("修改维修单 ", "修改售后维修工单 ");
            msg = msg.Replace("删除维修单 ", "删除售后维修工单 ");
            if (msg.IndexOf("售后维修工单", StringComparison.Ordinal) >= 0) return msg;
            if (System.Text.RegularExpressions.Regex.IsMatch(msg, @"\bSR20\d{6,}\b"))
            {
                msg = System.Text.RegularExpressions.Regex.Replace(msg, @"\b(SR20\d{6,})\b", "售后维修工单 $1");
                if (msg.IndexOf("新增", StringComparison.Ordinal) >= 0 && msg.IndexOf("发生了", StringComparison.Ordinal) < 0) return msg;
            }
            return HumanizeAfterSalesFallbackMessage(e);
        }

        static void NormalizeOperationLogEntry(OperationLogEntry e)
        {
            if (e == null) return;
            string msg = e.Message ?? "";
            string code = e.EntityCode ?? "";
            string module = e.Module ?? "";
            string entityType = e.EntityType ?? "";
            TryResolveAfterSalesLogFromDetail(msg + " " + code + " " + (e.Action ?? ""), ref module, ref entityType, ref code, e.Action);
            e.Module = module;
            e.EntityType = entityType;
            if (!string.IsNullOrWhiteSpace(code)) e.EntityCode = code;
            if (e.Module == "售后维修工单" && string.IsNullOrWhiteSpace(e.EntityType))
                e.EntityType = "AfterSalesServiceOrder";
            e.Message = HumanizeOperationLogMessage(e);
        }

        static void WriteStructuredOperationLog(UserSession user, HttpListenerContext ctx, string action, string detail, string result, string error, string backupPath, string apiOverride = null)
        {
            try
            {
                EnsureOperationLogsFile();
                string module, act, entityType, entityCode, entityName;
                ParseAuditAction(action, detail, out module, out act, out entityType, out entityCode, out entityName);
                var entry = new OperationLogEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Username = user == null ? "未登录" : user.Username,
                    DisplayName = user == null ? "未登录" : user.DisplayName,
                    Ip = GetClientIp(ctx ?? _auditContext),
                    Module = module,
                    Action = act,
                    EntityType = entityType,
                    EntityId = "",
                    EntityCode = string.IsNullOrWhiteSpace(entityCode) ? NextOperationLogCode() : entityCode,
                    EntityName = SanitizeLogText(entityName),
                    Result = result ?? "Success",
                    Message = SanitizeLogText(FormatOperationLogMessage(action, detail, module, act)),
                    Error = SanitizeLogText(error),
                    Api = apiOverride ?? (ctx ?? _auditContext)?.Request?.HttpMethod + " " + (ctx ?? _auditContext)?.Request?.Url?.AbsolutePath,
                    UserAgent = SanitizeLogText(GetClientUserAgent(ctx ?? _auditContext)),
                    BackupPath = backupPath ?? ""
                };
                lock (DataLock)
                {
                    var list = ReadJsonListCore<OperationLogEntry>(OperationLogsFilePath) ?? new List<OperationLogEntry>();
                    list.Insert(0, entry);
                    if (list.Count > OperationLogMaxEntries) list = list.Take(OperationLogMaxEntries).ToList();
                    WriteJsonListCore(OperationLogsFilePath, "operation_logs", list);
                }
            }
            catch { }
        }

        static void LogOperationFailure(HttpListenerContext ctx, UserSession user, string message, int statusCode)
        {
            string action = statusCode == 401 ? "未登录访问" : statusCode == 403 ? "权限不足" : statusCode == 409 ? "业务拦截" : statusCode == 422 ? "校验失败" : "操作失败";
            WriteStructuredOperationLog(user, ctx, action, message, "Failed", message, null);
        }

        static void Audit(UserSession user, string action, string detail)
        {
            try
            {
                lock (DataLock)
                    File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + (user == null ? "未登录" : user.Username + "/" + user.DisplayName) + "\t" + action + "\t" + (detail ?? "").Replace("\r", " ").Replace("\n", " ") + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
            string backupPath = "";
            if (!string.IsNullOrWhiteSpace(detail) && detail.IndexOf("备份", StringComparison.Ordinal) >= 0) backupPath = detail;
            string result = (action ?? "").IndexOf("失败", StringComparison.Ordinal) >= 0 ? "Failed" : "Success";
            WriteStructuredOperationLog(user, _auditContext, action, detail, result, null, backupPath);
        }

        static void AuditWithBackup(UserSession user, string action, string detail, string backupPath)
        {
            try
            {
                lock (DataLock)
                    File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + (user == null ? "未登录" : user.Username + "/" + user.DisplayName) + "\t" + action + "\t" + (detail ?? "").Replace("\r", " ").Replace("\n", " ") + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
            WriteStructuredOperationLog(user, _auditContext, action, detail, "Success", null, backupPath ?? "");
        }

        static List<OperationLogEntry> LoadOperationLogs()
        {
            EnsureOperationLogsFile();
            try
            {
                lock (DataLock) return ReadJsonListCore<OperationLogEntry>(OperationLogsFilePath) ?? new List<OperationLogEntry>();
            }
            catch
            {
                try { File.WriteAllText(OperationLogsFilePath, "[]", new UTF8Encoding(false)); } catch { }
                return new List<OperationLogEntry>();
            }
        }

        static OperationLogQueryResult QueryOperationLogs(string from, string to, string username, string module, string action, string result, string keyword, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 500) pageSize = 500;
            var all = LoadOperationLogs();
            foreach (var x in all) NormalizeOperationLogEntry(x);
            IEnumerable<OperationLogEntry> q = all;
            if (!string.IsNullOrWhiteSpace(from)) q = q.Where(x => string.Compare(x.Time, from, StringComparison.Ordinal) >= 0);
            if (!string.IsNullOrWhiteSpace(to)) q = q.Where(x => string.Compare(x.Time, to + " 23:59:59", StringComparison.Ordinal) <= 0);
            if (!string.IsNullOrWhiteSpace(username)) q = q.Where(x => string.Equals(x.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(module)) q = q.Where(x => (x.Module ?? "").IndexOf(module.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(action)) q = q.Where(x => (x.Action ?? "").IndexOf(action.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(result)) q = q.Where(x => string.Equals(x.Result, result.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string kw = keyword.Trim();
                q = q.Where(x => (x.Message ?? "").IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0
                    || (x.EntityName ?? "").IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0
                    || (x.EntityCode ?? "").IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0
                    || (x.Username ?? "").IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            var list = q.ToList();
            int total = list.Count;
            var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
            return new OperationLogQueryResult { Items = items, Total = total, Page = page, PageSize = pageSize };
        }

        static void ListOperationLogs(HttpListenerContext ctx, UserSession user)
        {
            if (!RequireOperationLogAccess(ctx, user)) return;
            var q = ctx.Request.QueryString;
            int page = 1, pageSize = 50;
            int.TryParse(q["page"], out page);
            int.TryParse(q["pageSize"], out pageSize);
            var result = QueryOperationLogs(q["from"], q["to"], q["username"], q["module"], q["action"], q["result"], q["keyword"], page, pageSize);
            WriteJson(ctx, result);
        }

        static void GetOperationLogDetail(HttpListenerContext ctx, UserSession user, string id)
        {
            if (!RequireOperationLogAccess(ctx, user)) return;
            var item = LoadOperationLogs().FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "操作记录不存在" }, 404); return; }
            NormalizeOperationLogEntry(item);
            WriteJson(ctx, item);
        }

        static void ExportOperationLogsCsv(HttpListenerContext ctx, UserSession user)
        {
            if (!RequireOperationLogAccess(ctx, user)) return;
            var q = ctx.Request.QueryString;
            var result = QueryOperationLogs(q["from"], q["to"], q["username"], q["module"], q["action"], q["result"], q["keyword"], 1, 10000);
            var sb = new StringBuilder();
            sb.AppendLine("时间,操作人,IP,UserAgent,模块,操作类型,业务类型,业务ID,业务编号,业务名称,结果,摘要,失败原因,接口,备份路径");
            foreach (var x in result.Items)
            {
                sb.Append(Csv(x.Time)).Append(',')
                    .Append(Csv(x.DisplayName + "(" + x.Username + ")")).Append(',')
                    .Append(Csv(x.Ip)).Append(',')
                    .Append(Csv(x.UserAgent)).Append(',')
                    .Append(Csv(x.Module)).Append(',')
                    .Append(Csv(x.Action)).Append(',')
                    .Append(Csv(x.EntityType)).Append(',')
                    .Append(Csv(x.EntityId)).Append(',')
                    .Append(Csv(x.EntityCode)).Append(',')
                    .Append(Csv(x.EntityName)).Append(',')
                    .Append(Csv(x.Result)).Append(',')
                    .Append(Csv(x.Message)).Append(',')
                    .Append(Csv(x.Error)).Append(',')
                    .Append(Csv(x.Api)).Append(',')
                    .Append(Csv(x.BackupPath)).AppendLine();
            }
            Audit(user, "导出操作记录", "共" + result.Items.Length + "条");
            byte[] bytes = new UTF8Encoding(true).GetBytes(sb.ToString());
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/csv; charset=utf-8";
            ctx.Response.AddHeader("Content-Disposition", "attachment; filename=\"操作记录_" + DateTime.Now.ToString("yyyyMMdd") + ".csv\"");
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        static bool RequireOperationLogAccess(HttpListenerContext ctx, UserSession user)
        {
            if (IsAdminUser(user)) return true;
            LogOperationFailure(ctx, user, "非管理员访问操作记录", 403);
            WriteJson(ctx, new { error = "无权限操作" }, 403);
            return false;
        }
    }
}
