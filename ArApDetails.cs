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
            public string FinanceAccountType { get; set; }
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
                saved = item;
                return new JsonMutationResult<Receivable>(item, true);
            });
            Audit(user, auditAction, BuildReceivableAuditDetail(saved));
            return saved;
        }

        static Payable PersistPayableDetailMutation(string payableId, string clientUpdatedAt, UserSession user, Action<Payable> mutate, string auditAction)
        {
            Payable saved = null;
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
                saved = item;
                return new JsonMutationResult<Payable>(item, true);
            });
            Audit(user, auditAction, BuildPayableAuditDetail(saved));
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

        static readonly string FinanceReceiptLinkPrefix = "AR-LINK|";
        static readonly string[] ReceiptFinanceAccountTypes = { "公户", "公司私户", "个人私户" };

        static bool IsFinanceReceiptLinked(FinanceTransaction item)
        {
            return item != null && (item.Note ?? "").StartsWith(FinanceReceiptLinkPrefix, StringComparison.Ordinal);
        }

        static string BuildFinanceReceiptLinkNote(string receivableId, string detailId, string receivableCode, string salesOrderNo)
        {
            return FinanceReceiptLinkPrefix + receivableId + "|" + detailId + "|" + (receivableCode ?? "") + "|" + (salesOrderNo ?? "");
        }

        static string ResolveReceiptFinanceAccountType(Receivable receivable, string overrideType)
        {
            overrideType = (overrideType ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(overrideType))
            {
                var match = ReceiptFinanceAccountTypes.FirstOrDefault(x => string.Equals(x, overrideType, StringComparison.OrdinalIgnoreCase));
                if (match == null) BizFail("财务账户类型无效，请选择：公户、公司私户、个人私户");
                return match;
            }
            if (receivable != null && !string.IsNullOrWhiteSpace(receivable.SalesOrderId))
            {
                var order = LoadSalesOrders().FirstOrDefault(x => x.Id == receivable.SalesOrderId);
                if (order != null)
                {
                    if (order.TaxExcludedSaleAmount > 0) return "公司私户";
                    if (order.TaxIncludedSaleAmount > 0) return "公户";
                }
            }
            return "公户";
        }

        static FinanceTransaction BuildLinkedFinanceIncome(Receivable receivable, ReceiptDetail detail, string financeAccountType, UserSession user)
        {
            var finance = new FinanceTransaction
            {
                Id = Guid.NewGuid().ToString("N"),
                Date = detail.ReceiptDate,
                AccountType = financeAccountType,
                Receipt = detail.Amount,
                Payment = 0,
                PaymentMethod = detail.PaymentMethod,
                Purpose = "应收收款",
                Counterparty = (receivable.CustomerName ?? "").Trim(),
                Note = BuildFinanceReceiptLinkNote(receivable.Id, detail.Id, receivable.Code, receivable.SalesOrderNo),
                UpdatedAt = ProfileUpdatedAtNow(),
                UpdatedBy = user.DisplayName
            };
            ValidateFinance(finance);
            return finance;
        }

        static FinanceTransaction FindLinkedFinance(List<FinanceTransaction> financeList, ReceiptDetail detail)
        {
            if (detail == null) return null;
            if (!string.IsNullOrWhiteSpace(detail.FinanceTransactionId))
            {
                var byId = financeList.FirstOrDefault(x => x.Id == detail.FinanceTransactionId);
                if (byId != null) return byId;
            }
            return financeList.FirstOrDefault(x => IsFinanceReceiptLinked(x) && (x.Note ?? "").IndexOf("|" + detail.Id + "|", StringComparison.Ordinal) >= 0);
        }

        static void ApplyLinkedFinanceFields(FinanceTransaction finance, Receivable receivable, ReceiptDetail detail, string financeAccountType, UserSession user)
        {
            if (finance == null || detail == null || receivable == null) BizFail("联动财务收支失败");
            finance.Date = detail.ReceiptDate;
            finance.AccountType = financeAccountType;
            finance.Receipt = detail.Amount;
            finance.Payment = 0;
            finance.PaymentMethod = detail.PaymentMethod;
            finance.Purpose = "应收收款";
            finance.Counterparty = (receivable.CustomerName ?? "").Trim();
            finance.Note = BuildFinanceReceiptLinkNote(receivable.Id, detail.Id, receivable.Code, receivable.SalesOrderNo);
            finance.UpdatedAt = ProfileUpdatedAtNow();
            finance.UpdatedBy = user.DisplayName;
            ValidateFinance(finance);
        }

        static Receivable PersistReceivableReceiptWithFinance(string receivableId, string clientUpdatedAt, UserSession user, Action<Receivable, List<FinanceTransaction>> mutate, string auditAction)
        {
            Receivable saved = null;
            RunUnderDataLock(() =>
            {
                var receivables = ReadJsonListCore<Receivable>(ReceivablesFile);
                var finance = ReadJsonListCore<FinanceTransaction>(FinanceFile);
                var item = receivables.FirstOrDefault(x => x.Id == receivableId);
                if (item == null) throw new BusinessException("应收款不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, clientUpdatedAt);
                if (item.ReceiptDetails == null) item.ReceiptDetails = new List<ReceiptDetail>();
                mutate(item, finance);
                SyncReceivableAmountsFromDetails(item);
                ValidateReceivableTotals(item);
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                saved = item;
                WriteJsonListCore(ReceivablesFile, "receivables", receivables);
                WriteJsonListCore(FinanceFile, "finance", finance);
            });
            Audit(user, auditAction, BuildReceivableAuditDetail(saved));
            return saved;
        }

        static void AddReceivableReceipt(HttpListenerContext ctx, UserSession user, string receivableId)
        {
            var input = ReadReceiptDetailMutationRequest(ctx.Request);
            var saved = PersistReceivableReceiptWithFinance(receivableId, input == null ? "" : input.UpdatedAt, user, (item, finance) =>
            {
                EnsureReceivableDetailTotal(item, input.Amount);
                var detail = new ReceiptDetail
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CreatedAt = BizUpdatedAtNow(),
                    UpdatedAt = BizUpdatedAtNow()
                };
                ApplyReceiptDetailFields(detail, input);
                var financeAccountType = ResolveReceiptFinanceAccountType(item, input.FinanceAccountType);
                var linkNote = BuildFinanceReceiptLinkNote(item.Id, detail.Id, item.Code, item.SalesOrderNo);
                if (finance.Any(x => string.Equals(x.Note, linkNote, StringComparison.Ordinal)))
                    BizFail("财务流水已存在，请勿重复提交", 409);
                var fin = BuildLinkedFinanceIncome(item, detail, financeAccountType, user);
                finance.Insert(0, fin);
                detail.FinanceTransactionId = fin.Id;
                item.ReceiptDetails.Insert(0, detail);
            }, "新增收款明细");
            WriteJson(ctx, saved, 201);
        }

        static void UpdateReceivableReceipt(HttpListenerContext ctx, UserSession user, string receivableId, string detailId)
        {
            var input = ReadReceiptDetailMutationRequest(ctx.Request);
            var saved = PersistReceivableReceiptWithFinance(receivableId, input == null ? "" : input.UpdatedAt, user, (item, finance) =>
            {
                var detail = item.ReceiptDetails.FirstOrDefault(x => x.Id == detailId);
                if (detail == null) throw new BusinessException("收款明细不存在", 404);
                EnsureReceivableDetailTotal(item, input.Amount, detailId);
                ApplyReceiptDetailFields(detail, input);
                detail.UpdatedAt = BizUpdatedAtNow();
                var linked = FindLinkedFinance(finance, detail);
                if (linked != null)
                {
                    var financeAccountType = ResolveReceiptFinanceAccountType(item, input.FinanceAccountType);
                    ApplyLinkedFinanceFields(linked, item, detail, financeAccountType, user);
                }
            }, "修改收款明细");
            WriteJson(ctx, saved);
        }

        static void DeleteReceivableReceipt(HttpListenerContext ctx, UserSession user, string receivableId, string detailId)
        {
            var input = ReadReceiptDetailMutationRequest(ctx.Request);
            var saved = PersistReceivableReceiptWithFinance(receivableId, input == null ? "" : input.UpdatedAt, user, (item, finance) =>
            {
                var detail = item.ReceiptDetails.FirstOrDefault(x => x.Id == detailId);
                if (detail == null) throw new BusinessException("收款明细不存在", 404);
                var linked = FindLinkedFinance(finance, detail);
                if (linked != null) finance.Remove(linked);
                item.ReceiptDetails.Remove(detail);
                if (item.ReceiptDetails.Count == 0)
                    item.ReceivedAmount = 0;
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
                if (item.PaymentDetails.Count == 0)
                    item.PaidAmount = 0;
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
