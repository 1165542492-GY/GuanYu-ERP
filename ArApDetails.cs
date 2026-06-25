using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace SupplierErpApp
{
    public static partial class Program
    {
        static readonly string[] ArApAccountOptions = { "现金", "银行", "微信", "支付宝", "其他" };

        class ReceiptDetailMutationRequest
        {
            public string UpdatedAt { get; set; }
            public string ReceiptDate { get; set; }
            public decimal Amount { get; set; }
            public string Account { get; set; }
            public string PaymentMethod { get; set; }
            public string Handler { get; set; }
            public string Note { get; set; }
        }

        class PaymentDetailMutationRequest
        {
            public string UpdatedAt { get; set; }
            public string PaymentDate { get; set; }
            public decimal Amount { get; set; }
            public string Account { get; set; }
            public string PaymentMethod { get; set; }
            public string Handler { get; set; }
            public string Note { get; set; }
        }

        static string NormalizeArApAccount(string account, string fieldLabel)
        {
            account = (account ?? "").Trim();
            if (string.IsNullOrWhiteSpace(account)) BizFail("请选择" + fieldLabel);
            var match = ArApAccountOptions.FirstOrDefault(x => string.Equals(x, account, StringComparison.OrdinalIgnoreCase));
            if (match == null) BizFail(fieldLabel + "无效，请选择：现金、银行、微信、支付宝、其他");
            return match;
        }

        static void SyncReceivableAmountsFromDetails(Receivable item)
        {
            if (item == null) return;
            if (item.ReceiptDetails != null && item.ReceiptDetails.Count > 0)
                item.ReceivedAmount = RoundMoney(item.ReceiptDetails.Sum(x => x.Amount));
            item.UnreceivedAmount = RoundMoney(item.ReceivableAmount - item.ReceivedAmount);
            item.Status = NormalizeReceivableStatus(item.ReceivableAmount, item.ReceivedAmount);
        }

        static void SyncPayableAmountsFromDetails(Payable item)
        {
            if (item == null) return;
            if (item.PaymentDetails != null && item.PaymentDetails.Count > 0)
                item.PaidAmount = RoundMoney(item.PaymentDetails.Sum(x => x.Amount));
            item.UnpaidAmount = RoundMoney(item.PayableAmount - item.PaidAmount);
            item.Status = NormalizePayableStatus(item.PayableAmount, item.PaidAmount);
        }

        static void ValidateReceivableTotals(Receivable item)
        {
            if (item.ReceivedAmount < 0) BizFail("已收金额不能为负数");
            if (item.ReceivedAmount > item.ReceivableAmount) BizFail("收款合计不能超过应收金额", 422);
        }

        static void ValidatePayableTotals(Payable item)
        {
            if (item.PaidAmount < 0) BizFail("已付金额不能为负数");
            if (item.PaidAmount > item.PayableAmount) BizFail("付款合计不能超过应付金额", 422);
        }

        static void ApplyReceiptDetailFields(ReceiptDetail detail, ReceiptDetailMutationRequest input)
        {
            if (detail == null || input == null) BizFail("数据不能为空");
            if (input.Amount <= 0) BizFail("收款金额必须大于 0");
            detail.ReceiptDate = string.IsNullOrWhiteSpace(input.ReceiptDate) ? TodayText() : input.ReceiptDate.Trim();
            detail.Amount = RoundMoney(input.Amount);
            detail.Account = NormalizeArApAccount(input.Account, "收款账户");
            detail.PaymentMethod = (input.PaymentMethod ?? "").Trim();
            detail.Handler = (input.Handler ?? "").Trim();
            detail.Note = (input.Note ?? "").Trim();
        }

        static void ApplyPaymentDetailFields(PaymentDetail detail, PaymentDetailMutationRequest input)
        {
            if (detail == null || input == null) BizFail("数据不能为空");
            if (input.Amount <= 0) BizFail("付款金额必须大于 0");
            detail.PaymentDate = string.IsNullOrWhiteSpace(input.PaymentDate) ? TodayText() : input.PaymentDate.Trim();
            detail.Amount = RoundMoney(input.Amount);
            detail.Account = NormalizeArApAccount(input.Account, "付款账户");
            detail.PaymentMethod = (input.PaymentMethod ?? "").Trim();
            detail.Handler = (input.Handler ?? "").Trim();
            detail.Note = (input.Note ?? "").Trim();
        }

        static void EnsureReceivableDetailTotal(Receivable item, decimal newAmount, string excludeDetailId = null)
        {
            decimal total = 0;
            foreach (var d in item.ReceiptDetails ?? new List<ReceiptDetail>())
            {
                if (!string.IsNullOrWhiteSpace(excludeDetailId) && d.Id == excludeDetailId) continue;
                total += d.Amount;
            }
            total += newAmount;
            if (total > item.ReceivableAmount + 0.0001m)
                BizFail("收款合计不能超过应收金额", 422);
        }

        static void EnsurePayableDetailTotal(Payable item, decimal newAmount, string excludeDetailId = null)
        {
            decimal total = 0;
            foreach (var d in item.PaymentDetails ?? new List<PaymentDetail>())
            {
                if (!string.IsNullOrWhiteSpace(excludeDetailId) && d.Id == excludeDetailId) continue;
                total += d.Amount;
            }
            total += newAmount;
            if (total > item.PayableAmount + 0.0001m)
                BizFail("付款合计不能超过应付金额", 422);
        }

        static Receivable PersistReceivableDetailMutation(string receivableId, string clientUpdatedAt, UserSession user, Action<Receivable> mutate, string auditAction)
        {
            Receivable saved = null;
            string auditCode = null;
            MutateJsonList<Receivable, Receivable>(ReceivablesFile, "receivables", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == receivableId);
                if (item == null) throw new BusinessException("应收款不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, clientUpdatedAt);
                if (item.ReceiptDetails == null) item.ReceiptDetails = new List<ReceiptDetail>();
                mutate(item);
                SyncReceivableAmountsFromDetails(item);
                ValidateReceivableTotals(item);
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                auditCode = item.Code;
                saved = item;
                return new JsonMutationResult<Receivable>(item, true);
            });
            Audit(user, auditAction, auditCode);
            return saved;
        }

        static Payable PersistPayableDetailMutation(string payableId, string clientUpdatedAt, UserSession user, Action<Payable> mutate, string auditAction)
        {
            Payable saved = null;
            string auditCode = null;
            MutateJsonList<Payable, Payable>(PayablesFile, "payables", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == payableId);
                if (item == null) throw new BusinessException("应付款不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, clientUpdatedAt);
                if (item.PaymentDetails == null) item.PaymentDetails = new List<PaymentDetail>();
                mutate(item);
                SyncPayableAmountsFromDetails(item);
                ValidatePayableTotals(item);
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                auditCode = item.Code;
                saved = item;
                return new JsonMutationResult<Payable>(item, true);
            });
            Audit(user, auditAction, auditCode);
            return saved;
        }

        static ReceiptDetailMutationRequest ReadReceiptDetailMutationRequest(HttpListenerRequest req)
        {
            string body = ReadBody(req);
            if (string.IsNullOrWhiteSpace(body)) return new ReceiptDetailMutationRequest();
            return Json.Deserialize<ReceiptDetailMutationRequest>(body) ?? new ReceiptDetailMutationRequest();
        }

        static PaymentDetailMutationRequest ReadPaymentDetailMutationRequest(HttpListenerRequest req)
        {
            string body = ReadBody(req);
            if (string.IsNullOrWhiteSpace(body)) return new PaymentDetailMutationRequest();
            return Json.Deserialize<PaymentDetailMutationRequest>(body) ?? new PaymentDetailMutationRequest();
        }

        static void AddReceivableReceipt(HttpListenerContext ctx, UserSession user, string receivableId)
        {
            var input = ReadReceiptDetailMutationRequest(ctx.Request);
            var saved = PersistReceivableDetailMutation(receivableId, input == null ? "" : input.UpdatedAt, user, item =>
            {
                EnsureReceivableDetailTotal(item, input.Amount);
                var detail = new ReceiptDetail
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CreatedAt = BizUpdatedAtNow(),
                    UpdatedAt = BizUpdatedAtNow()
                };
                ApplyReceiptDetailFields(detail, input);
                item.ReceiptDetails.Insert(0, detail);
            }, "新增收款明细");
            WriteJson(ctx, saved, 201);
        }

        static void UpdateReceivableReceipt(HttpListenerContext ctx, UserSession user, string receivableId, string detailId)
        {
            var input = ReadReceiptDetailMutationRequest(ctx.Request);
            var saved = PersistReceivableDetailMutation(receivableId, input == null ? "" : input.UpdatedAt, user, item =>
            {
                var detail = item.ReceiptDetails.FirstOrDefault(x => x.Id == detailId);
                if (detail == null) throw new BusinessException("收款明细不存在", 404);
                EnsureReceivableDetailTotal(item, input.Amount, detailId);
                ApplyReceiptDetailFields(detail, input);
                detail.UpdatedAt = BizUpdatedAtNow();
            }, "修改收款明细");
            WriteJson(ctx, saved);
        }

        static void DeleteReceivableReceipt(HttpListenerContext ctx, UserSession user, string receivableId, string detailId)
        {
            var input = ReadReceiptDetailMutationRequest(ctx.Request);
            var saved = PersistReceivableDetailMutation(receivableId, input == null ? "" : input.UpdatedAt, user, item =>
            {
                var detail = item.ReceiptDetails.FirstOrDefault(x => x.Id == detailId);
                if (detail == null) throw new BusinessException("收款明细不存在", 404);
                item.ReceiptDetails.Remove(detail);
            }, "删除收款明细");
            WriteJson(ctx, saved);
        }

        static void AddPayablePayment(HttpListenerContext ctx, UserSession user, string payableId)
        {
            var input = ReadPaymentDetailMutationRequest(ctx.Request);
            var saved = PersistPayableDetailMutation(payableId, input == null ? "" : input.UpdatedAt, user, item =>
            {
                EnsurePayableDetailTotal(item, input.Amount);
                var detail = new PaymentDetail
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CreatedAt = BizUpdatedAtNow(),
                    UpdatedAt = BizUpdatedAtNow()
                };
                ApplyPaymentDetailFields(detail, input);
                item.PaymentDetails.Insert(0, detail);
            }, "新增付款明细");
            WriteJson(ctx, saved, 201);
        }

        static void UpdatePayablePayment(HttpListenerContext ctx, UserSession user, string payableId, string detailId)
        {
            var input = ReadPaymentDetailMutationRequest(ctx.Request);
            var saved = PersistPayableDetailMutation(payableId, input == null ? "" : input.UpdatedAt, user, item =>
            {
                var detail = item.PaymentDetails.FirstOrDefault(x => x.Id == detailId);
                if (detail == null) throw new BusinessException("付款明细不存在", 404);
                EnsurePayableDetailTotal(item, input.Amount, detailId);
                ApplyPaymentDetailFields(detail, input);
                detail.UpdatedAt = BizUpdatedAtNow();
            }, "修改付款明细");
            WriteJson(ctx, saved);
        }

        static void DeletePayablePayment(HttpListenerContext ctx, UserSession user, string payableId, string detailId)
        {
            var input = ReadPaymentDetailMutationRequest(ctx.Request);
            var saved = PersistPayableDetailMutation(payableId, input == null ? "" : input.UpdatedAt, user, item =>
            {
                var detail = item.PaymentDetails.FirstOrDefault(x => x.Id == detailId);
                if (detail == null) throw new BusinessException("付款明细不存在", 404);
                item.PaymentDetails.Remove(detail);
            }, "删除付款明细");
            WriteJson(ctx, saved);
        }

        static bool TryHandleReceivableReceiptRoutes(HttpListenerContext ctx, UserSession user, string path)
        {
            const string prefix = "/api/receivables/";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            string rest = path.Substring(prefix.Length);
            if (string.IsNullOrWhiteSpace(rest)) return false;
            string[] parts = rest.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !string.Equals(parts[1], "receipts", StringComparison.OrdinalIgnoreCase)) return false;
            string receivableId = parts[0];
            string method = ctx.Request.HttpMethod ?? "";
            if (parts.Length == 2 && method == "POST")
            {
                if (!RequirePermission(ctx, user, "receivable.receipt_add")) return true;
                AddReceivableReceipt(ctx, user, receivableId);
                return true;
            }
            if (parts.Length == 3)
            {
                string detailId = parts[2];
                if (method == "PUT")
                {
                    if (!RequirePermission(ctx, user, "receivable.receipt_edit")) return true;
                    UpdateReceivableReceipt(ctx, user, receivableId, detailId);
                    return true;
                }
                if (method == "DELETE")
                {
                    if (!RequirePermission(ctx, user, "receivable.receipt_delete")) return true;
                    DeleteReceivableReceipt(ctx, user, receivableId, detailId);
                    return true;
                }
            }
            return false;
        }

        static bool TryHandlePayablePaymentRoutes(HttpListenerContext ctx, UserSession user, string path)
        {
            const string prefix = "/api/payables/";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            string rest = path.Substring(prefix.Length);
            if (string.IsNullOrWhiteSpace(rest)) return false;
            string[] parts = rest.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !string.Equals(parts[1], "payments", StringComparison.OrdinalIgnoreCase)) return false;
            string payableId = parts[0];
            string method = ctx.Request.HttpMethod ?? "";
            if (parts.Length == 2 && method == "POST")
            {
                if (!RequirePermission(ctx, user, "payable.payment_add")) return true;
                AddPayablePayment(ctx, user, payableId);
                return true;
            }
            if (parts.Length == 3)
            {
                string detailId = parts[2];
                if (method == "PUT")
                {
                    if (!RequirePermission(ctx, user, "payable.payment_edit")) return true;
                    UpdatePayablePayment(ctx, user, payableId, detailId);
                    return true;
                }
                if (method == "DELETE")
                {
                    if (!RequirePermission(ctx, user, "payable.payment_delete")) return true;
                    DeletePayablePayment(ctx, user, payableId, detailId);
                    return true;
                }
            }
            return false;
        }
    }
}
