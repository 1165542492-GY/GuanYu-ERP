using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;

namespace SupplierErpApp
{
    public static partial class Program
    {
        const string AutoTestMarker = "AUTO_TEST";
        const string ClearTestDataConfirmText = "确认清理测试数据";
        const string ClearAllBusinessConfirmText = "确认清空全部业务数据";

        public class ClearAutoTestResult
        {
            public string Message { get; set; }
            public Dictionary<string, int> Removed { get; set; }
            public int TotalRemoved { get; set; }
            public string BackupPath { get; set; }
        }

        static bool ContainsAutoTestMarker(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && text.IndexOf(AutoTestMarker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool ObjectHasAutoTestMarker(object obj)
        {
            if (obj == null) return false;
            foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType != typeof(string) || prop.GetIndexParameters().Length > 0) continue;
                var val = prop.GetValue(obj, null) as string;
                if (ContainsAutoTestMarker(val)) return true;
            }
            return false;
        }

        static HashSet<string> CollectAutoTestIds<T>(IEnumerable<T> items, Func<T, string> idSelector, params Func<T, string>[] fields)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (items == null) return set;
            foreach (var item in items)
            {
                if (item == null) continue;
                if (ObjectHasAutoTestMarker(item)) { set.Add(idSelector(item)); continue; }
                foreach (var f in fields)
                {
                    if (ContainsAutoTestMarker(f(item))) { set.Add(idSelector(item)); break; }
                }
            }
            return set;
        }

        static ClearAutoTestResult PurgeAutoTestBusinessData()
        {
            var removed = new Dictionary<string, int>();

            var suppliers = LoadSuppliers();
            var customers = LoadCustomers();
            var materials = LoadMaterials();
            var autoSupplierIds = CollectAutoTestIds(suppliers, x => x.Id, x => x.Company, x => x.Code, x => x.Goods, x => x.Contact);
            var autoCustomerIds = CollectAutoTestIds(customers, x => x.Id, x => x.Company, x => x.Code, x => x.Contact);
            var autoMaterialIds = CollectAutoTestIds(materials, x => x.Id, x => x.Code, x => x.NameSpec, x => x.Supplier, x => x.Note);
            var autoSupplierNames = new HashSet<string>(suppliers.Where(x => autoSupplierIds.Contains(x.Id)).Select(x => x.Company ?? "").Where(x => x.Length > 0), StringComparer.Ordinal);
            var autoCustomerNames = new HashSet<string>(customers.Where(x => autoCustomerIds.Contains(x.Id)).Select(x => x.Company ?? "").Where(x => x.Length > 0), StringComparer.Ordinal);

            suppliers = suppliers.Where(x => !autoSupplierIds.Contains(x.Id)).ToList();
            customers = customers.Where(x => !autoCustomerIds.Contains(x.Id)).ToList();
            materials = materials.Where(x => !autoMaterialIds.Contains(x.Id)).ToList();

            var boms = LoadBom();
            var autoBomIds = CollectAutoTestIds(boms, x => x.Id, x => x.Code, x => x.ModelName, x => x.ProductName, x => x.ModelCode, x => x.Note);
            foreach (var b in boms)
            {
                if (b.Items != null && b.Items.Any(i => autoMaterialIds.Contains(i.MaterialId ?? "") || ContainsAutoTestMarker(i.MaterialName) || ContainsAutoTestMarker(i.MaterialCode)))
                    autoBomIds.Add(b.Id);
            }
            boms = boms.Where(x => !autoBomIds.Contains(x.Id)).ToList();

            var modelCosts = LoadModelCostsWithCurrentPrices();
            var autoModelCostIds = CollectAutoTestIds(modelCosts, x => x.Id, x => x.ModelCode, x => x.ModelName, x => x.ProductName, x => x.Note);
            modelCosts = modelCosts.Where(x => !autoModelCostIds.Contains(x.Id) && !autoBomIds.Contains(x.BomId ?? "")).ToList();

            var salesOrders = LoadSalesOrders();
            var autoSalesOrderIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var x in salesOrders)
            {
                if (ObjectHasAutoTestMarker(x) || autoCustomerIds.Contains(x.CustomerId ?? "") || autoCustomerNames.Contains(x.CustomerName ?? "") || autoMaterialIds.Contains(x.MaterialId ?? ""))
                    autoSalesOrderIds.Add(x.Id);
            }
            salesOrders = salesOrders.Where(x => !autoSalesOrderIds.Contains(x.Id)).ToList();

            var purchaseOrders = LoadPurchaseOrders();
            var autoPurchaseOrderIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var x in purchaseOrders)
            {
                if (ObjectHasAutoTestMarker(x) || autoSupplierNames.Contains(x.SupplierName ?? "") || autoMaterialIds.Contains(x.MaterialId ?? ""))
                    autoPurchaseOrderIds.Add(x.Id);
            }
            purchaseOrders = purchaseOrders.Where(x => !autoPurchaseOrderIds.Contains(x.Id)).ToList();

            var salesOutbounds = LoadSalesOutbounds();
            salesOutbounds = salesOutbounds.Where(x =>
                !ObjectHasAutoTestMarker(x) && !autoSalesOrderIds.Contains(x.SalesOrderId ?? "") && !autoCustomerNames.Contains(x.CustomerName ?? "") && !autoMaterialIds.Contains(x.MaterialId ?? "")).ToList();

            var purchaseInbounds = LoadPurchaseInbounds();
            purchaseInbounds = purchaseInbounds.Where(x =>
                !ObjectHasAutoTestMarker(x) && !autoPurchaseOrderIds.Contains(x.PurchaseOrderId ?? "") && !autoSupplierNames.Contains(x.SupplierName ?? "") && !autoMaterialIds.Contains(x.MaterialId ?? "")).ToList();

            var productionPicks = LoadProductionPicks();
            productionPicks = productionPicks.Where(x =>
                !ObjectHasAutoTestMarker(x) && !autoBomIds.Contains(x.BomId ?? "") && !autoMaterialIds.Contains(x.MaterialId ?? "")).ToList();

            var finishedInbounds = LoadFinishedInbounds();
            finishedInbounds = finishedInbounds.Where(x =>
                !ObjectHasAutoTestMarker(x) && !autoBomIds.Contains(x.BomId ?? "") && !autoModelCostIds.Contains(x.ModelCostId ?? "")).ToList();

            var receivables = LoadReceivables();
            receivables = receivables.Where(x =>
                !ObjectHasAutoTestMarker(x) && !autoSalesOrderIds.Contains(x.SalesOrderId ?? "") && !autoCustomerNames.Contains(x.CustomerName ?? "")).ToList();

            var payables = LoadPayables();
            payables = payables.Where(x =>
                !ObjectHasAutoTestMarker(x) && !autoPurchaseOrderIds.Contains(x.PurchaseOrderId ?? "") && !autoSupplierNames.Contains(x.SupplierName ?? "")).ToList();

            var finance = LoadFinance();
            finance = finance.Where(x => !ObjectHasAutoTestMarker(x)).ToList();

            var contracts = LoadContracts();
            contracts = contracts.Where(x => !ObjectHasAutoTestMarker(x)).ToList();

            var beforeSup = LoadSuppliers().Count;
            SaveSuppliers(suppliers); removed["suppliers"] = beforeSup - suppliers.Count;
            var beforeCust = LoadCustomers().Count; SaveCustomers(customers); removed["customers"] = beforeCust - customers.Count;
            var beforeMat = LoadMaterials().Count; SaveMaterials(materials); removed["materials"] = beforeMat - materials.Count;
            var beforeBom = LoadBom().Count; SaveBom(boms); removed["bom"] = beforeBom - boms.Count;
            var beforeMc = LoadModelCostsWithCurrentPrices().Count;
            lock (DataLock) WriteJsonListCore(ModelCostFile, "model_costs", modelCosts);
            removed["model_costs"] = beforeMc - modelCosts.Count;
            var beforeSo = LoadSalesOrders().Count; SaveSalesOrders(salesOrders); removed["sales_orders"] = beforeSo - salesOrders.Count;
            var beforePo = LoadPurchaseOrders().Count; SavePurchaseOrders(purchaseOrders); removed["purchase_orders"] = beforePo - purchaseOrders.Count;
            var beforeOut = LoadSalesOutbounds().Count; SaveSalesOutbounds(salesOutbounds); removed["sales_outbounds"] = beforeOut - salesOutbounds.Count;
            var beforeIn = LoadPurchaseInbounds().Count; SavePurchaseInbounds(purchaseInbounds); removed["purchase_inbounds"] = beforeIn - purchaseInbounds.Count;
            var beforePick = LoadProductionPicks().Count; SaveProductionPicks(productionPicks); removed["production_picks"] = beforePick - productionPicks.Count;
            var beforeFin = LoadFinishedInbounds().Count; SaveFinishedInbounds(finishedInbounds); removed["finished_inbounds"] = beforeFin - finishedInbounds.Count;
            var beforeRec = LoadReceivables().Count; SaveReceivables(receivables); removed["receivables"] = beforeRec - receivables.Count;
            var beforePay = LoadPayables().Count; SavePayables(payables); removed["payables"] = beforePay - payables.Count;
            var beforeFinance = LoadFinance().Count; SaveFinance(finance); removed["finance"] = beforeFinance - finance.Count;
            var beforeCon = LoadContracts().Count; SaveContracts(contracts); removed["contracts"] = beforeCon - contracts.Count;

            RepairAllSequenceFiles();
            int total = removed.Values.Sum();
            return new ClearAutoTestResult { Removed = removed, TotalRemoved = total };
        }

        static bool ValidateClearDataPassword(string password, out SystemSettings settings)
        {
            settings = LoadSystemSettings();
            return string.Equals(password, settings.ClearDataPassword ?? DefaultClearDataPassword, StringComparison.Ordinal);
        }

        static ClearDataFileSpec[] GetClearAllBusinessDataFileSpecs()
        {
            return GetClearTestDataFileSpecs();
        }

        static BackupFileSpec[] GetAutoTestClearBackupSpecs()
        {
            return GetClearAllBusinessDataFileSpecs().Select(x => new BackupFileSpec { Path = x.Path, FileName = x.FileName }).ToArray();
        }

        static void ClearTestData(HttpListenerContext ctx, UserSession user)
        {
            if (!IsAdminUser(user)) { LogOperationFailure(ctx, user, "非管理员清测试数据", 403); WriteJson(ctx, new { error = "仅管理员可执行此操作" }, 403); return; }
            var req = Json.Deserialize<ClearTestDataRequest>(ReadBody(ctx.Request));
            string password = (req == null ? null : req.Password) ?? "";
            string confirmText = (req == null ? null : req.ConfirmText) ?? "";
            if (!string.Equals(confirmText.Trim(), ClearTestDataConfirmText, StringComparison.Ordinal))
            {
                LogOperationFailure(ctx, user, "清测试数据确认文字不正确", 400);
                WriteJson(ctx, new { message = "确认文字不正确，请准确输入「" + ClearTestDataConfirmText + "」", error = "确认文字不正确" }, 400);
                return;
            }
            SystemSettings settings;
            if (!ValidateClearDataPassword(password, out settings))
            {
                LogOperationFailure(ctx, user, "清测试数据二次密码错误", 403);
                WriteJson(ctx, new { error = "二次密码错误，禁止清理数据" }, 403);
                return;
            }
            string backupFolder = null;
            try
            {
                lock (DataLock)
                {
                    Directory.CreateDirectory(BackupDir);
                    backupFolder = BackupBeforeClearTestData(GetAutoTestClearBackupSpecs());
                    EnsureBackupSucceeded(backupFolder, GetAutoTestClearBackupSpecs());
                    var result = PurgeAutoTestBusinessData();
                    if (result.TotalRemoved == 0)
                    {
                        AuditWithBackup(user, "清理AUTO_TEST测试数据", "未发现AUTO_TEST测试数据", backupFolder);
                        WriteJson(ctx, new { message = "未发现 AUTO_TEST 测试数据，无需清理", removed = result.Removed, totalRemoved = 0, backupPath = backupFolder });
                        return;
                    }
                    var labelMap = new Dictionary<string, string>
                    {
                        ["suppliers"] = "供应商", ["customers"] = "客户", ["materials"] = "物料", ["bom"] = "BOM",
                        ["model_costs"] = "机型成本", ["sales_orders"] = "销售订单", ["purchase_orders"] = "采购单",
                        ["sales_outbounds"] = "销售出库", ["purchase_inbounds"] = "采购入库", ["production_picks"] = "生产领用",
                        ["finished_inbounds"] = "成品入库", ["receivables"] = "应收", ["payables"] = "应付",
                        ["finance"] = "财务业务", ["contracts"] = "合同"
                    };
                    var parts = result.Removed.Where(kv => kv.Value > 0).Select(kv => (labelMap.ContainsKey(kv.Key) ? labelMap[kv.Key] : kv.Key) + " " + kv.Value + "条");
                    string msg = "已清理 AUTO_TEST 测试数据：" + string.Join("、", parts);
                    AuditWithBackup(user, "清理AUTO_TEST测试数据", msg + "；备份目录：" + Path.GetFileName(backupFolder), backupFolder);
                    WriteJson(ctx, new { message = msg, removed = result.Removed, totalRemoved = result.TotalRemoved, backupFolder = Path.GetFileName(backupFolder), backupPath = backupFolder });
                }
            }
            catch (Exception ex)
            {
                LogOperationFailure(ctx, user, "清测试数据失败：" + ex.Message, 500);
                string msg = "清理失败：" + ToUserMessage(ex);
                WriteJson(ctx, new { message = msg, error = msg }, 500);
            }
        }

        static void ClearAllBusinessData(HttpListenerContext ctx, UserSession user)
        {
            if (!IsAdminUser(user)) { LogOperationFailure(ctx, user, "非管理员清空全部业务数据", 403); WriteJson(ctx, new { error = "仅管理员可执行此操作" }, 403); return; }
            var req = Json.Deserialize<ClearTestDataRequest>(ReadBody(ctx.Request));
            string password = (req == null ? null : req.Password) ?? "";
            string confirmText = (req == null ? null : req.ConfirmText) ?? "";
            if (!string.Equals(confirmText.Trim(), ClearAllBusinessConfirmText, StringComparison.Ordinal))
            {
                LogOperationFailure(ctx, user, "清空全部业务数据确认文字不正确", 400);
                WriteJson(ctx, new { message = "确认文字不正确，请准确输入「" + ClearAllBusinessConfirmText + "」", error = "确认文字不正确" }, 400);
                return;
            }
            SystemSettings settings;
            if (!ValidateClearDataPassword(password, out settings))
            {
                LogOperationFailure(ctx, user, "清空全部业务数据二次密码错误", 403);
                WriteJson(ctx, new { error = "二次密码错误，禁止清空数据" }, 403);
                return;
            }
            var backupSpecs = GetAllBackupFileSpecs();
            var clearSpecs = GetClearAllBusinessDataFileSpecs();
            string backupFolder = null;
            try
            {
                lock (DataLock)
                {
                    Directory.CreateDirectory(BackupDir);
                    backupFolder = BackupBeforeClearTestData(backupSpecs);
                    EnsureBackupSucceeded(backupFolder, backupSpecs);
                    foreach (var spec in clearSpecs)
                        WriteAllTextAtomic(spec.Path, spec.EmptyContent);
                }
                EnsureClearedDataIntegrity();
                AuditWithBackup(user, "清空全部业务数据", "备份目录：" + backupFolder, backupFolder);
                WriteJson(ctx, new
                {
                    message = "全部业务数据已清空",
                    backupFolder = Path.GetFileName(backupFolder),
                    backupPath = backupFolder,
                    backupFiles = backupSpecs.Select(x => x.FileName).ToArray(),
                    clearedFiles = clearSpecs.Select(x => x.FileName).ToArray()
                });
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(backupFolder))
                {
                    try { lock (DataLock) RestoreFilesFromFolder(backupFolder, clearSpecs.Select(x => new BackupFileSpec { Path = x.Path, FileName = x.FileName }).ToArray()); }
                    catch { }
                }
                LogOperationFailure(ctx, user, "清空全部业务数据失败：" + ex.Message, 500);
                string msg = "清空失败，已尝试恢复原始数据：" + ToUserMessage(ex);
                WriteJson(ctx, new { message = msg, error = msg }, 500);
            }
        }

        static void ChangeClearDataPassword(HttpListenerContext ctx, UserSession user)
        {
            if (!IsAdminUser(user)) { LogOperationFailure(ctx, user, "非管理员修改二次密码", 403); WriteJson(ctx, new { error = "仅管理员可执行此操作" }, 403); return; }
            var req = Json.Deserialize<ChangeClearDataPasswordRequest>(ReadBody(ctx.Request));
            string oldPwd = (req == null ? null : req.OldPassword) ?? "";
            string newPwd = (req == null ? null : req.NewPassword) ?? "";
            string confirmPwd = (req == null ? null : req.ConfirmPassword) ?? "";
            if (string.IsNullOrWhiteSpace(oldPwd)) { WriteJson(ctx, new { error = "请输入当前二次密码" }, 400); return; }
            if (string.IsNullOrWhiteSpace(newPwd)) { WriteJson(ctx, new { error = "新二次密码不能为空" }, 400); return; }
            if (!string.Equals(newPwd, confirmPwd, StringComparison.Ordinal)) { WriteJson(ctx, new { error = "新密码和确认密码不一致" }, 400); return; }
            SystemSettings settings;
            if (!ValidateClearDataPassword(oldPwd, out settings))
            {
                LogOperationFailure(ctx, user, "修改二次密码失败：当前密码错误", 403);
                WriteJson(ctx, new { error = "当前二次密码错误" }, 403);
                return;
            }
            settings.ClearDataPassword = newPwd;
            settings.UpdatedAt = ProfileUpdatedAtNow();
            SaveSystemSettingsFile(settings);
            Audit(user, "修改危险操作二次密码", "已成功修改");
            WriteJson(ctx, new { message = "二次密码已修改", ok = true });
        }
    }
}
