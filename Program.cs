using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;
using System.Windows.Forms;

namespace SupplierErpApp
{
    public sealed class JsonCodec
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true
        };

        public string Serialize(object value)
        {
            return JsonSerializer.Serialize(value, Options);
        }

        public T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
    }

    public class Supplier
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Company { get; set; }
        public string Contact { get; set; }
        public string Phone { get; set; }
        public string Goods { get; set; }
        public string Address { get; set; }
        public string Bank { get; set; }
        public string Account { get; set; }
        public string BankNo { get; set; }
        public decimal Payable { get; set; }
        public string Status { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class Customer
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Company { get; set; }
        public string Contact { get; set; }
        public string Phone { get; set; }
        public string Bank { get; set; }
        public string Account { get; set; }
        public string BankNo { get; set; }
        public string Address { get; set; }
        public decimal Receivable { get; set; }
        public string Status { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class Material
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Supplier { get; set; }
        public string NameSpec { get; set; }
        public string QuantityUnit { get; set; }
        public decimal TaxPrice { get; set; }
        public decimal NoTaxPrice { get; set; }
        public string PriceType { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class ImportRequest { public string FileName { get; set; } public string Data { get; set; } }
    public class CsvImportRequest { public string FileName { get; set; } public string Data { get; set; } public Dictionary<string, string> ConflictActions { get; set; } }
    public class TableImportResult
    {
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int FailedRows { get; set; }
        public string[] Errors { get; set; }
        public string[] Warnings { get; set; }
        public bool NeedsConflictDecision { get; set; }
        public string[] Conflicts { get; set; }
    }
    public class BatchDeleteRequest { public string[] Ids { get; set; } }
    public class BatchSupplierRequest { public List<Supplier> Items { get; set; } }
    public class BatchCustomerRequest { public List<Customer> Items { get; set; } }
    public class BatchMaterialRequest { public List<Material> Items { get; set; } }

    public class FinanceTransaction
    {
        public string Id { get; set; }
        public string Date { get; set; }
        public string AccountType { get; set; }
        public decimal Receipt { get; set; }
        public decimal Payment { get; set; }
        public string PaymentMethod { get; set; }
        public string Purpose { get; set; }
        public string Counterparty { get; set; }
        public string Note { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class OpeningBalances
    {
        public decimal PublicAccount { get; set; }
        public decimal CompanyPrivate { get; set; }
        public decimal PersonalPrivate { get; set; }
    }

    public class SystemSettings
    {
        public decimal TaxRate { get; set; }
    }

    public class TaxRateRequest { public decimal TaxRate { get; set; } }

    public class BomDetail
    {
        public string MaterialId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public string Spec { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal OriginalUnitPrice
        {
            get { return OriginalPrice; }
            set { OriginalPrice = value; }
        }
        public string PriceType { get; set; }
        public decimal TaxRate { get; set; }
        public decimal NoTaxPrice { get; set; }
        public decimal UnitPriceWithoutTax
        {
            get { return NoTaxPrice; }
            set { NoTaxPrice = value; }
        }
        public decimal Amount { get; set; }
        public string PriceSourceTime { get; set; }
        public string Note { get; set; }
        public bool PriceMissing { get; set; }
    }

    public class BomItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string ModelCode { get; set; }
        public string ModelName { get; set; }
        public string ProductName { get; set; }
        public string Version { get; set; }
        public string Status { get; set; }
        public decimal TotalMaterialCost { get; set; }
        public string Note { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public List<BomDetail> Items { get; set; }
        public bool PriceMissing { get; set; }
        public string MissingPriceMaterials { get; set; }
    }

    public class ModelCost
    {
        public string Id { get; set; }
        public string ModelCode { get; set; }
        public string ModelName { get; set; }
        public string ProductName { get; set; }
        public string BomVersion { get; set; }
        public string BomId { get; set; }
        public string BomCode { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal TotalCost { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public bool PriceMissing { get; set; }
        public string MissingPriceMaterials { get; set; }
    }

    public class DictionaryOption
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Status { get; set; }
        public int SortOrder { get; set; }
        public string Note { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
    }

    public class ContractSetting
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Content { get; set; }
        public bool IsDefault { get; set; }
        public string Status { get; set; }
        public int Sort { get; set; }
        public string Note { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
    }

    public class ContractDetailLine
    {
        public int Seq { get; set; }
        public string DeviceName { get; set; }
        public string ModelSpec { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public bool TaxIncluded { get; set; }
        public decimal TaxRate { get; set; }
        public decimal NoTaxUnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public string Note { get; set; }
    }

    public class ContractAccessoryLine
    {
        public int Seq { get; set; }
        public string Name { get; set; }
        public string Spec { get; set; }
        public string Quantity { get; set; }
        public string Unit { get; set; }
        public string Note { get; set; }
    }

    public class ContractConfigLine
    {
        public int Seq { get; set; }
        public string Category { get; set; }
        public string Item { get; set; }
        public string Brand { get; set; }
        public string Spec { get; set; }
        public string Quantity { get; set; }
        public string Unit { get; set; }
        public string Note { get; set; }
    }

    public class ContractTechParamLine
    {
        public int Seq { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Note { get; set; }
    }

    public class ContractItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string CustomerCode { get; set; }
        public string PartyAName { get; set; }
        public string PartyAContact { get; set; }
        public string PartyAPhone { get; set; }
        public string PartyAAddress { get; set; }
        public string PartyBName { get; set; }
        public string PartyBContact { get; set; }
        public string PartyBPhone { get; set; }
        public string PartyBAddress { get; set; }
        public string SignDate { get; set; }
        public string TemplateId { get; set; }
        public string TemplateCode { get; set; }
        public string TemplateName { get; set; }
        public string TemplateContent { get; set; }
        public string PaymentTerms { get; set; }
        public string DeliveryTime { get; set; }
        public string DeliveryPlace { get; set; }
        public string PackagingMethod { get; set; }
        public string TransportMethod { get; set; }
        public string InvoiceType { get; set; }
        public bool TaxIncluded { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TotalAmount { get; set; }
        public string TotalAmountChinese { get; set; }
        public decimal DepositRatio { get; set; }
        public decimal DepositAmount { get; set; }
        public string DepositAmountChinese { get; set; }
        public decimal BalanceAmount { get; set; }
        public string BalanceAmountChinese { get; set; }
        public int InstallmentMonths { get; set; }
        public decimal InstallmentAmount { get; set; }
        public string InstallmentAmountChinese { get; set; }
        public string InstallmentNote { get; set; }
        public string ReceivingAccount { get; set; }
        public string QualityAcceptanceTerms { get; set; }
        public string AfterSalesTerms { get; set; }
        public string ExcludedWarranty { get; set; }
        public string BreachTerms { get; set; }
        public string InvoiceTitle { get; set; }
        public string TaxNumber { get; set; }
        public string AccountHolder { get; set; }
        public string BankBranch { get; set; }
        public string CompanyAccount { get; set; }
        public string PersonalAccount { get; set; }
        public string BankRoutingNo { get; set; }
        public string Status { get; set; }
        public string InternalNote { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public List<ContractDetailLine> Items { get; set; }
        public List<ContractAccessoryLine> Accessories { get; set; }
        public List<ContractConfigLine> ConfigItems { get; set; }
        public List<ContractTechParamLine> TechParams { get; set; }
    }

    public class ContractPreviewResult { public string Html { get; set; } }

    public class LoginRequest { public string Username { get; set; } public string Password { get; set; } }
    public class UserSession { public string Username { get; set; } public string DisplayName { get; set; } public string Role { get; set; } public bool IsAdmin { get; set; } public string[] Permissions { get; set; } }
    public class UserDef { public string Username { get; set; } public string DisplayName { get; set; } public string Role { get; set; } public string PasswordHash { get; set; } public bool Enabled { get; set; } public string[] Permissions { get; set; } }
    public class UserPublic { public string Username { get; set; } public string DisplayName { get; set; } public string Role { get; set; } public bool Enabled { get; set; } public string[] Permissions { get; set; } public string PermissionSummary { get; set; } }
    public class CreateUserRequest { public string Username { get; set; } public string Password { get; set; } public string DisplayName { get; set; } public bool Enabled { get; set; } public string[] Permissions { get; set; } }
    public class UpdateUserRequest { public string DisplayName { get; set; } public bool Enabled { get; set; } public string Password { get; set; } public string[] Permissions { get; set; } }
    public class ChangePasswordRequest { public string OldPassword { get; set; } public string NewPassword { get; set; } }
    public class PermissionGroup { public string Module { get; set; } public PermissionItem[] Items { get; set; } }
    public class PermissionItem { public string Key { get; set; } public string Label { get; set; } }

    public static class Program
    {
        static readonly object DataLock = new object();
        static readonly object SessionLock = new object();
        static readonly JsonCodec Json = new JsonCodec();
        static readonly Dictionary<string, UserSession> Sessions = new Dictionary<string, UserSession>();
        static List<UserDef> Users = new List<UserDef>();
        static readonly string[] AllPermissionKeys = {
            "supplier.view","supplier.add","supplier.edit","supplier.delete","supplier.batch_delete",
            "customer.view","customer.add","customer.edit","customer.delete","customer.batch_delete",
            "material.view","material.add","material.edit","material.delete","material.batch_delete",
            "finance.view","finance.add","finance.edit","finance.delete","finance.import",
            "bom.view","bom.add","bom.edit","bom.delete","bom.export","bom.import",
            "model_cost.view","model_cost.add","model_cost.edit","model_cost.delete","model_cost.export","model_cost.import",
            "contract.view","contract.add","contract.edit","contract.delete","contract.preview","contract.print",
            "contract_setting.view","contract_setting.add","contract_setting.edit","contract_setting.delete",
            "settings.view","settings.account","settings.password","settings.tax_rate"
        };
        const string AdminUsername = "admin";
        const string DefaultAdminPasswordHash = "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9";

        static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "智造ERP供应商管理");
        static readonly string UsersFile = Path.Combine(DataDir, "users.json");
        static readonly string DataFile = Path.Combine(DataDir, "suppliers.json");
        static readonly string SupplierSequenceFile = Path.Combine(DataDir, "supplier_sequence.json");
        static readonly string CustomerFile = Path.Combine(DataDir, "customers.json");
        static readonly string CustomerSequenceFile = Path.Combine(DataDir, "customer_sequence.json");
        static readonly string MaterialFile = Path.Combine(DataDir, "materials.json");
        static readonly string MaterialSequenceFile = Path.Combine(DataDir, "material_sequence.json");
        static readonly string FinanceFile = Path.Combine(DataDir, "finance.json");
        static readonly string OpeningFile = Path.Combine(DataDir, "finance_opening.json");
        static readonly string BomFile = Path.Combine(DataDir, "bom.json");
        static readonly string BomSequenceFile = Path.Combine(DataDir, "bom_sequence.json");
        static readonly string ModelCostFile = Path.Combine(DataDir, "model_costs.json");
        static readonly string SystemSettingsFile = Path.Combine(DataDir, "system_settings.json");
        static readonly string ContractSettingsFile = Path.Combine(DataDir, "contract_settings.json");
        static readonly string ContractSettingSequenceFile = Path.Combine(DataDir, "contract_setting_sequence.json");
        static readonly string ContractsFile = Path.Combine(DataDir, "contracts.json");
        static readonly string ContractSequenceFile = Path.Combine(DataDir, "contract_sequence.json");
        static readonly string DictionaryOptionsFile = Path.Combine(DataDir, "dictionary_options.json");
        static readonly string BackupDir = Path.Combine(DataDir, "backups");
        static readonly string LogFile = Path.Combine(DataDir, "operation.log");
        const int Port = 8787;
        static HttpListener Listener;
        static NotifyIcon TrayIcon;

        [STAThread]
        public static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "SupplierErpApp_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("供应商管理系统已经在运行。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    Directory.CreateDirectory(DataDir);
                    Directory.CreateDirectory(BackupDir);
                    if (!File.Exists(DataFile)) File.WriteAllText(DataFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(SupplierSequenceFile)) File.WriteAllText(SupplierSequenceFile, "0", new UTF8Encoding(false));
                    if (!File.Exists(CustomerFile)) File.WriteAllText(CustomerFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(CustomerSequenceFile)) File.WriteAllText(CustomerSequenceFile, "0", new UTF8Encoding(false));
                    if (!File.Exists(MaterialFile)) File.WriteAllText(MaterialFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(MaterialSequenceFile)) File.WriteAllText(MaterialSequenceFile, "0", new UTF8Encoding(false));
                    EnsureLegacyCodes();
                    if (!File.Exists(FinanceFile)) File.WriteAllText(FinanceFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(OpeningFile)) File.WriteAllText(OpeningFile, Json.Serialize(new OpeningBalances()), new UTF8Encoding(false));
                    if (!File.Exists(BomFile)) File.WriteAllText(BomFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(BomSequenceFile)) File.WriteAllText(BomSequenceFile, "0", new UTF8Encoding(false));
                    if (!File.Exists(ModelCostFile)) File.WriteAllText(ModelCostFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(SystemSettingsFile)) File.WriteAllText(SystemSettingsFile, Json.Serialize(new SystemSettings { TaxRate = 10 }), new UTF8Encoding(false));
                    if (!File.Exists(ContractSettingsFile)) File.WriteAllText(ContractSettingsFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(ContractSettingSequenceFile)) File.WriteAllText(ContractSettingSequenceFile, "0", new UTF8Encoding(false));
                    if (!File.Exists(ContractsFile)) File.WriteAllText(ContractsFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(ContractSequenceFile)) File.WriteAllText(ContractSequenceFile, "0", new UTF8Encoding(false));
                    EnsureDefaultDictionaryOptions();
                    EnsureDefaultContractSettings();
                    EnsureUsersFile();
                    LoadUsers();
                    StartServer();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    SetupTray();
                    OpenBrowser();
                    Application.Run();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("系统启动失败：\r\n" + ex.Message + "\r\n\r\n请尝试右键选择“以管理员身份运行”。", "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    try { if (Listener != null) Listener.Stop(); } catch { }
                    if (TrayIcon != null) TrayIcon.Dispose();
                }
            }
        }

        static void SetupTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("打开管理系统", null, delegate { OpenBrowser(); });
            menu.Items.Add("打开数据目录", null, delegate { Process.Start("explorer.exe", DataDir); });
            menu.Items.Add("立即备份", null, delegate { ManualBackup(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出系统", null, delegate { Application.Exit(); });
            TrayIcon = new NotifyIcon();
            TrayIcon.Text = "智造ERP供应商管理（运行中）";
            TrayIcon.Icon = System.Drawing.SystemIcons.Application;
            TrayIcon.Visible = true;
            TrayIcon.ContextMenuStrip = menu;
            TrayIcon.DoubleClick += delegate { OpenBrowser(); };
            TrayIcon.ShowBalloonTip(2500, "供应商管理系统已启动", "本机访问：http://127.0.0.1:" + Port, ToolTipIcon.Info);
        }

        static void OpenBrowser()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://127.0.0.1:" + Port + "/",
                UseShellExecute = true
            });
        }

        static void StartServer()
        {
            Listener = new HttpListener();
            Listener.Prefixes.Add("http://+:" + Port + "/");
            Listener.Start();
            ThreadPool.QueueUserWorkItem(delegate
            {
                while (Listener.IsListening)
                {
                    try
                    {
                        var context = Listener.GetContext();
                        ThreadPool.QueueUserWorkItem(delegate { Handle(context); });
                    }
                    catch { if (!Listener.IsListening) return; }
                }
            });
        }

        static void Handle(HttpListenerContext ctx)
        {
            try
            {
                AddSecurityHeaders(ctx.Response);
                string path = ctx.Request.Url.AbsolutePath.TrimEnd('/');
                if (path == "") { ServeApp(ctx); return; }
                if (path == "/favicon.ico") { ctx.Response.StatusCode = 204; ctx.Response.Close(); return; }
                if (path == "/api/login" && ctx.Request.HttpMethod == "POST") { Login(ctx); return; }
                if (path == "/api/logout" && ctx.Request.HttpMethod == "POST") { Logout(ctx); return; }
                UserSession user = Authenticate(ctx);
                if (user == null) { WriteJson(ctx, new { error = "请先登录" }, 401); return; }
                if (path == "/api/me") { WriteJson(ctx, user); return; }
                if (path == "/api/permissions" && ctx.Request.HttpMethod == "GET") { WriteJson(ctx, GetPermissionDefinitions()); return; }
                if (path == "/api/me/password" && ctx.Request.HttpMethod == "PUT") { ChangePassword(ctx, user); return; }
                if (path == "/api/users" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "settings.account")) return; ListUsers(ctx); return; }
                if (path == "/api/users" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "settings.account")) return; CreateUser(ctx, user); return; }
                if (path.StartsWith("/api/users/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "settings.account")) return; UpdateUser(ctx, user, path.Substring("/api/users/".Length)); return; }
                if (path.StartsWith("/api/users/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "settings.account")) return; DeleteUser(ctx, user, path.Substring("/api/users/".Length)); return; }
                if (path == "/api/info") { WriteJson(ctx, new { ip = GetLanIp(), port = Port, dataDir = DataDir }); return; }
                if (path == "/api/suppliers" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "supplier.view")) return; WriteJson(ctx, LoadSuppliers()); return; }
                if (path == "/api/suppliers" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "supplier.add")) return; AddSupplier(ctx, user); return; }
                if (path.StartsWith("/api/suppliers/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "supplier.edit")) return; UpdateSupplier(ctx, user, path.Substring("/api/suppliers/".Length)); return; }
                if (path.StartsWith("/api/suppliers/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "supplier.delete")) return; DeleteSupplier(ctx, user, path.Substring("/api/suppliers/".Length)); return; }
                if (path == "/api/suppliers/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "supplier.add")) return; ImportSuppliers(ctx, user); return; }
                if (path == "/api/suppliers/batch-delete" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "supplier.batch_delete")) return; BatchDeleteSuppliers(ctx, user); return; }
                if (path == "/api/suppliers/batch" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "supplier.add")) return; BatchAddSuppliers(ctx, user); return; }
                if (path == "/api/customers" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "customer.view")) return; WriteJson(ctx, LoadCustomers()); return; }
                if (path == "/api/customers" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "customer.add")) return; AddCustomer(ctx, user); return; }
                if (path.StartsWith("/api/customers/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "customer.edit")) return; UpdateCustomer(ctx, user, path.Substring("/api/customers/".Length)); return; }
                if (path.StartsWith("/api/customers/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "customer.delete")) return; DeleteCustomer(ctx, user, path.Substring("/api/customers/".Length)); return; }
                if (path == "/api/customers/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "customer.add")) return; ImportCustomers(ctx, user); return; }
                if (path == "/api/customers/batch-delete" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "customer.batch_delete")) return; BatchDeleteCustomers(ctx, user); return; }
                if (path == "/api/customers/batch" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "customer.add")) return; BatchAddCustomers(ctx, user); return; }
                if (path == "/api/materials" && ctx.Request.HttpMethod == "GET") { if (!HasPermission(user, "material.view") && !HasPermission(user, "bom.view")) { WriteJson(ctx, new { error = "无权限操作" }, 403); return; } WriteJson(ctx, LoadMaterials()); return; }
                if (path == "/api/materials" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "material.add")) return; AddMaterial(ctx, user); return; }
                if (path.StartsWith("/api/materials/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "material.edit")) return; UpdateMaterial(ctx, user, path.Substring("/api/materials/".Length)); return; }
                if (path.StartsWith("/api/materials/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "material.delete")) return; DeleteMaterial(ctx, user, path.Substring("/api/materials/".Length)); return; }
                if (path == "/api/materials/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "material.add")) return; ImportMaterials(ctx, user); return; }
                if (path == "/api/materials/batch-delete" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "material.batch_delete")) return; BatchDeleteMaterials(ctx, user); return; }
                if (path == "/api/materials/batch" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "material.add")) return; BatchAddMaterials(ctx, user); return; }
                if (path == "/api/finance/opening" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "finance.view")) return; WriteJson(ctx, LoadOpeningBalances()); return; }
                if (path == "/api/finance/opening" && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "finance.edit")) return; SaveOpeningBalances(ctx, user); return; }
                if (path == "/api/finance" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "finance.view")) return; WriteJson(ctx, LoadFinance()); return; }
                if (path == "/api/finance" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "finance.add")) return; AddFinance(ctx, user); return; }
                if (path == "/api/finance/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "finance.import")) return; ImportFinance(ctx, user); return; }
                if (path == "/api/finance/template" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "finance.import")) return; ExportFinanceTemplateCsv(ctx); return; }
                if (path.StartsWith("/api/finance/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "finance.edit")) return; UpdateFinance(ctx, user, path.Substring("/api/finance/".Length)); return; }
                if (path.StartsWith("/api/finance/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "finance.delete")) return; DeleteFinance(ctx, user, path.Substring("/api/finance/".Length)); return; }
                if (path == "/api/export") { if (!RequirePermission(ctx, user, "supplier.view")) return; ExportCsv(ctx); return; }
                if (path == "/api/customers/export") { if (!RequirePermission(ctx, user, "customer.view")) return; ExportCustomersCsv(ctx); return; }
                if (path == "/api/materials/export") { if (!RequirePermission(ctx, user, "material.view")) return; ExportMaterialsCsv(ctx); return; }
                if (path == "/api/backup" && ctx.Request.HttpMethod == "POST") { if (!IsAdminUser(user)) { if (!RequirePermission(ctx, user, "settings.view")) return; } string f = ManualBackup(); Audit(user, "手动备份", Path.GetFileName(f)); WriteJson(ctx, new { ok = true, file = f }); return; }
                if (path == "/api/settings/tax-rate" && ctx.Request.HttpMethod == "GET") { if (!CanReadTaxRate(user)) { WriteJson(ctx, new { error = "无权限操作" }, 403); return; } WriteJson(ctx, LoadSystemSettings()); return; }
                if (path == "/api/settings/tax-rate" && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "settings.tax_rate")) return; SaveTaxRate(ctx, user); return; }
                if (path == "/api/dictionary-options" && ctx.Request.HttpMethod == "GET") { if (!CanReadDictionaryOptions(user)) { WriteJson(ctx, new { error = "无权限操作" }, 403); return; } ListDictionaryOptions(ctx); return; }
                if (path == "/api/dictionary-options" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "settings.dictionary")) return; AddDictionaryOption(ctx, user); return; }
                if (path.StartsWith("/api/dictionary-options/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "settings.dictionary")) return; UpdateDictionaryOption(ctx, user, path.Substring("/api/dictionary-options/".Length)); return; }
                if (path.StartsWith("/api/dictionary-options/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "settings.dictionary")) return; DeleteDictionaryOption(ctx, user, path.Substring("/api/dictionary-options/".Length)); return; }
                if (path == "/api/bom/export" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "bom.export")) return; ExportBomCsv(ctx); return; }
                if (path == "/api/bom/template" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "bom.import")) return; ExportBomTemplateCsv(ctx); return; }
                if (path == "/api/bom/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "bom.import")) return; ImportBomCsv(ctx, user); return; }
                if (path == "/api/bom" && ctx.Request.HttpMethod == "GET") { if (!HasPermission(user, "bom.view") && !HasPermission(user, "model_cost.view")) { WriteJson(ctx, new { error = "无权限操作" }, 403); return; } WriteJson(ctx, LoadBomWithCurrentPrices()); return; }
                if (path == "/api/bom" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "bom.add")) return; AddBom(ctx, user); return; }
                if (path.StartsWith("/api/bom/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "bom.edit")) return; UpdateBom(ctx, user, path.Substring("/api/bom/".Length)); return; }
                if (path.StartsWith("/api/bom/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "bom.delete")) return; DeleteBom(ctx, user, path.Substring("/api/bom/".Length)); return; }
                if (path == "/api/model-costs/export" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "model_cost.export")) return; ExportModelCostsCsv(ctx); return; }
                if (path == "/api/model-costs/template" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "model_cost.import")) return; ExportModelCostTemplateCsv(ctx); return; }
                if (path == "/api/model-costs/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "model_cost.import")) return; ImportModelCostsCsv(ctx, user); return; }
                if (path == "/api/model-costs" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "model_cost.view")) return; WriteJson(ctx, LoadModelCostsWithCurrentPrices()); return; }
                if (path == "/api/model-costs" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "model_cost.add")) return; AddModelCost(ctx, user); return; }
                if (path.StartsWith("/api/model-costs/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "model_cost.edit")) return; UpdateModelCost(ctx, user, path.Substring("/api/model-costs/".Length)); return; }
                if (path.StartsWith("/api/model-costs/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "model_cost.delete")) return; DeleteModelCost(ctx, user, path.Substring("/api/model-costs/".Length)); return; }
                if (path == "/api/contract-settings" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "contract_setting.view")) return; WriteJson(ctx, LoadContractSettings()); return; }
                if (path == "/api/contract-settings" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "contract_setting.add")) return; AddContractSetting(ctx, user); return; }
                if (path.StartsWith("/api/contract-settings/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "contract_setting.edit")) return; UpdateContractSetting(ctx, user, path.Substring("/api/contract-settings/".Length)); return; }
                if (path.StartsWith("/api/contract-settings/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "contract_setting.delete")) return; DeleteContractSetting(ctx, user, path.Substring("/api/contract-settings/".Length)); return; }
                if (path == "/api/contracts" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "contract.view")) return; WriteJson(ctx, LoadContracts()); return; }
                if (path == "/api/contracts" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "contract.add")) return; AddContract(ctx, user); return; }
                if (path.StartsWith("/api/contracts/") && path.EndsWith("/preview") && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "contract.preview")) return; PreviewContract(ctx, path.Substring("/api/contracts/".Length, path.Length - "/api/contracts/".Length - "/preview".Length)); return; }
                if (path.StartsWith("/api/contracts/") && path.EndsWith("/void") && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "contract.edit")) return; VoidContract(ctx, user, path.Substring("/api/contracts/".Length, path.Length - "/api/contracts/".Length - "/void".Length)); return; }
                if (path.StartsWith("/api/contracts/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "contract.edit")) return; UpdateContract(ctx, user, path.Substring("/api/contracts/".Length)); return; }
                if (path.StartsWith("/api/contracts/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "contract.delete")) return; DeleteContract(ctx, user, path.Substring("/api/contracts/".Length)); return; }
                WriteJson(ctx, new { error = "接口不存在" }, 404);
            }
            catch (Exception ex)
            {
                try { WriteJson(ctx, new { error = ex.Message }, 500); } catch { }
            }
        }

        static void ServeApp(HttpListenerContext ctx)
        {
            string html;
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.App.html"))
            {
                if (s == null) throw new Exception("界面资源缺失");
                using (var reader = new StreamReader(s, Encoding.UTF8)) html = reader.ReadToEnd();
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.Finance.html"))
            {
                if (s == null) throw new Exception("财务界面资源缺失");
                using (var reader = new StreamReader(s, Encoding.UTF8)) html = html.Replace("</body>", reader.ReadToEnd() + "</body>");
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.Customer.html"))
            {
                if (s == null) throw new Exception("客户界面资源缺失");
                using (var reader = new StreamReader(s, Encoding.UTF8)) html = html.Replace("</body>", reader.ReadToEnd() + "</body>");
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.Material.html"))
            {
                if (s == null) throw new Exception("物料界面资源缺失");
                using (var reader = new StreamReader(s, Encoding.UTF8)) html = html.Replace("</body>", reader.ReadToEnd() + "</body>");
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.Contract.html"))
            {
                if (s == null) throw new Exception("合同界面资源缺失");
                using (var reader = new StreamReader(s, Encoding.UTF8)) html = html.Replace("</body>", reader.ReadToEnd() + "</body>");
            }
            byte[] bytes = Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        static void Login(HttpListenerContext ctx)
        {
            var req = Json.Deserialize<LoginRequest>(ReadBody(ctx.Request));
            var user = FindUser(req == null ? "" : req.Username);
            if (user == null || !FixedEquals(user.PasswordHash, Sha256(req == null ? "" : req.Password)))
            {
                Audit(null, "登录失败", req == null ? "" : req.Username);
                WriteJson(ctx, new { error = "账号或密码不正确" }, 401); return;
            }
            if (!user.Enabled)
            {
                Audit(null, "登录失败", user.Username + "（已禁用）");
                WriteJson(ctx, new { error = "账号已禁用" }, 403); return;
            }
            string token = Guid.NewGuid().ToString("N");
            var session = ToSession(user);
            lock (SessionLock) Sessions[token] = session;
            var cookie = new Cookie("ERPSESSION", token, "/"); cookie.HttpOnly = true; ctx.Response.Cookies.Add(cookie);
            Audit(session, "登录", "成功");
            WriteJson(ctx, session);
        }

        static void Logout(HttpListenerContext ctx)
        {
            var user = Authenticate(ctx);
            var c = ctx.Request.Cookies["ERPSESSION"];
            if (c != null) lock (SessionLock) Sessions.Remove(c.Value);
            if (user != null) Audit(user, "退出", "");
            var expired = new Cookie("ERPSESSION", "", "/"); expired.Expires = DateTime.Now.AddDays(-1); ctx.Response.Cookies.Add(expired);
            WriteJson(ctx, new { ok = true });
        }

        static UserSession Authenticate(HttpListenerContext ctx)
        {
            var c = ctx.Request.Cookies["ERPSESSION"];
            if (c == null || string.IsNullOrEmpty(c.Value)) return null;
            lock (SessionLock)
            {
                UserSession s;
                if (!Sessions.TryGetValue(c.Value, out s)) return null;
                var user = FindUser(s.Username);
                if (user == null || !user.Enabled) return null;
                return ToSession(user);
            }
        }

        static void EnsureUsersFile()
        {
            if (File.Exists(UsersFile)) return;
            var admin = new List<UserDef>
            {
                new UserDef
                {
                    Username = AdminUsername,
                    DisplayName = "系统管理员",
                    Role = "系统管理员",
                    PasswordHash = DefaultAdminPasswordHash,
                    Enabled = true,
                    Permissions = new string[0]
                }
            };
            File.WriteAllText(UsersFile, Json.Serialize(admin), new UTF8Encoding(false));
        }

        static void LoadUsers()
        {
            lock (DataLock)
            {
                if (!File.Exists(UsersFile)) { Users = new List<UserDef>(); return; }
                string text = File.ReadAllText(UsersFile, Encoding.UTF8);
                Users = Json.Deserialize<List<UserDef>>(text) ?? new List<UserDef>();
            }
        }

        static void SaveUsers()
        {
            lock (DataLock)
            {
                string temp = UsersFile + ".tmp";
                File.WriteAllText(temp, Json.Serialize(Users), new UTF8Encoding(false));
                if (File.Exists(UsersFile)) File.Replace(temp, UsersFile, Path.Combine(BackupDir, "users_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json"));
                else File.Move(temp, UsersFile);
                CleanBackups();
            }
        }

        static UserDef FindUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            return Users.FirstOrDefault(x => string.Equals(x.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        static bool IsAdminUser(UserSession user) { return user != null && user.IsAdmin; }
        static bool IsAdminUsername(string username) { return string.Equals(username, AdminUsername, StringComparison.OrdinalIgnoreCase); }

        static UserSession ToSession(UserDef user)
        {
            bool isAdmin = IsAdminUsername(user.Username);
            return new UserSession
            {
                Username = user.Username,
                DisplayName = user.DisplayName,
                Role = user.Role,
                IsAdmin = isAdmin,
                Permissions = isAdmin ? AllPermissionKeys : NormalizePermissions(user.Permissions)
            };
        }

        static string[] NormalizePermissions(string[] permissions)
        {
            if (permissions == null || permissions.Length == 0) return new string[0];
            return permissions.Where(p => !string.IsNullOrWhiteSpace(p) && AllPermissionKeys.Contains(p)).Distinct().ToArray();
        }

        static bool HasPermission(UserSession user, string permission)
        {
            if (user == null) return false;
            if (user.IsAdmin) return true;
            if (permission == "finance.import" && user.Permissions != null && user.Permissions.Contains("finance.add")) return true;
            return user.Permissions != null && user.Permissions.Contains(permission);
        }

        static bool RequirePermission(HttpListenerContext ctx, UserSession user, string permission)
        {
            if (HasPermission(user, permission)) return true;
            WriteJson(ctx, new { error = "无权限操作" }, 403);
            return false;
        }

        static bool CanReadTaxRate(UserSession user)
        {
            return HasPermission(user, "settings.tax_rate") || HasPermission(user, "bom.view") || HasPermission(user, "model_cost.view");
        }

        static string NormalizePriceType(string priceType)
        {
            return string.Equals(priceType, "含税", StringComparison.OrdinalIgnoreCase) ? "含税" : "不含税";
        }

        static decimal GetMaterialOriginalPrice(Material material)
        {
            if (material == null) return 0;
            return NormalizePriceType(material.PriceType) == "含税" ? material.TaxPrice : material.NoTaxPrice;
        }

        static decimal CalcNoTaxUnitPrice(decimal originalPrice, string priceType, decimal taxRate)
        {
            if (NormalizePriceType(priceType) == "含税")
            {
                if (taxRate < 0) taxRate = 0;
                return Math.Round(originalPrice / (1 + taxRate / 100m), 4);
            }
            return originalPrice;
        }

        static decimal CalcLineAmount(decimal quantity, decimal noTaxPrice)
        {
            return Math.Round(quantity * noTaxPrice, 2);
        }

        static void RecalcBomLines(BomItem item, decimal defaultTaxRate)
        {
            if (item.Items == null) item.Items = new List<BomDetail>();
            decimal total = 0;
            foreach (var line in item.Items)
            {
                line.PriceType = NormalizePriceType(line.PriceType);
                if (line.TaxRate <= 0) line.TaxRate = defaultTaxRate;
                line.NoTaxPrice = CalcNoTaxUnitPrice(line.OriginalPrice, line.PriceType, line.TaxRate);
                line.Amount = CalcLineAmount(line.Quantity, line.NoTaxPrice);
                total += line.Amount;
            }
            item.TotalMaterialCost = Math.Round(total, 2);
        }

        static void ValidateBom(BomItem item)
        {
            if (item == null) throw new Exception("BOM 资料不能为空");
            if (string.IsNullOrWhiteSpace(item.ModelCode)) throw new Exception("机型编号不能为空");
            if (string.IsNullOrWhiteSpace(item.ModelName)) throw new Exception("机型名称不能为空");
            if (string.IsNullOrWhiteSpace(item.ProductName)) throw new Exception("产品名称不能为空");
            item.ModelCode = item.ModelCode.Trim();
            item.ModelName = item.ModelName.Trim();
            item.ProductName = item.ProductName.Trim();
            item.Version = (item.Version ?? "").Trim();
            item.Note = (item.Note ?? "").Trim();
            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "启用" : item.Status.Trim();
            if (item.Items == null || item.Items.Count == 0) throw new Exception("请至少添加一条 BOM 明细");
            foreach (var line in item.Items)
            {
                if (string.IsNullOrWhiteSpace(line.MaterialCode)) throw new Exception("BOM 明细必须选择物料");
                if (line.Quantity <= 0) throw new Exception("BOM 明细用量必须大于零");
                line.MaterialName = (line.MaterialName ?? "").Trim();
                line.Spec = (line.Spec ?? "").Trim();
                line.Unit = (line.Unit ?? "").Trim();
                line.Note = (line.Note ?? "").Trim();
                line.PriceSourceTime = (line.PriceSourceTime ?? "").Trim();
                if (string.IsNullOrWhiteSpace(line.PriceSourceTime))
                    line.PriceSourceTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        static void ValidateModelCost(ModelCost item)
        {
            if (item == null) throw new Exception("机型成本资料不能为空");
            if (string.IsNullOrWhiteSpace(item.BomId)) throw new Exception("请选择 BOM");
            item.Note = (item.Note ?? "").Trim();
            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "启用" : item.Status.Trim();
            if (item.Status != "启用" && item.Status != "停用") throw new Exception("状态只能是启用或停用");
        }

        static bool IsMaterialUsedByBom(string materialId, string materialCode)
        {
            return GetBomsUsingMaterial(materialId, materialCode).Count > 0;
        }

        static List<BomItem> GetBomsUsingMaterial(string materialId, string materialCode)
        {
            return LoadBom().Where(b => (b.Items ?? new List<BomDetail>()).Any(x => x.MaterialId == materialId || (!string.IsNullOrWhiteSpace(materialCode) && x.MaterialCode == materialCode))).ToList();
        }

        static string FormatMaterialDeleteBlockedMessage(List<BomItem> boms)
        {
            string msg = "该物料已被 BOM 使用，不能删除。请先删除相关 BOM 后再删除该物料。";
            if (boms == null || boms.Count == 0) return msg;
            var refs = boms.Take(10).Select(b => (string.IsNullOrWhiteSpace(b.Code) ? "" : b.Code) + " " + b.ModelName).Select(x => x.Trim()).Where(x => x.Length > 0);
            msg += " 引用 BOM：" + string.Join("、", refs);
            if (boms.Count > 10) msg += " 等共" + boms.Count + "条";
            return msg;
        }

        static bool IsBomUsedByModelCost(string bomId)
        {
            return GetModelCostsUsingBom(bomId).Count > 0;
        }

        static List<ModelCost> GetModelCostsUsingBom(string bomId)
        {
            return LoadModelCosts().Where(x => x.BomId == bomId).ToList();
        }

        static string FormatBomDeleteBlockedMessage(List<ModelCost> costs)
        {
            string msg = "该 BOM 已被机型成本使用，不能删除。请先删除相关机型成本后再删除该 BOM。";
            if (costs == null || costs.Count == 0) return msg;
            var refs = costs.Take(10).Select(x => (string.IsNullOrWhiteSpace(x.ModelCode) ? "" : x.ModelCode) + " " + x.ModelName).Select(x => x.Trim()).Where(x => x.Length > 0);
            msg += " 引用机型成本：" + string.Join("、", refs);
            if (costs.Count > 10) msg += " 等共" + costs.Count + "条";
            return msg;
        }

        static bool IsSupplierUsedByMaterial(string company)
        {
            if (string.IsNullOrWhiteSpace(company)) return false;
            return LoadMaterials().Any(x => string.Equals(x.Supplier, company, StringComparison.OrdinalIgnoreCase));
        }

        static void ValidateModelCostFilled(ModelCost item)
        {
            if (string.IsNullOrWhiteSpace(item.ModelCode)) throw new Exception("机型编号不能为空");
            item.ModelName = (item.ModelName ?? "").Trim();
            item.ProductName = (item.ProductName ?? "").Trim();
            item.BomVersion = (item.BomVersion ?? "").Trim();
            item.BomCode = (item.BomCode ?? "").Trim();
        }

        static SystemSettings LoadSystemSettings()
        {
            lock (DataLock)
            {
                if (!File.Exists(SystemSettingsFile)) return new SystemSettings { TaxRate = 10 };
                string text = File.ReadAllText(SystemSettingsFile, Encoding.UTF8);
                var settings = Json.Deserialize<SystemSettings>(text) ?? new SystemSettings();
                if (settings.TaxRate < 0) settings.TaxRate = 0;
                return settings;
            }
        }

        static void SaveSystemSettingsFile(SystemSettings settings)
        {
            lock (DataLock)
            {
                string temp = SystemSettingsFile + ".tmp";
                File.WriteAllText(temp, Json.Serialize(settings), new UTF8Encoding(false));
                if (File.Exists(SystemSettingsFile))
                {
                    string backup = Path.Combine(BackupDir, "system_settings_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json");
                    File.Replace(temp, SystemSettingsFile, backup);
                }
                else File.Move(temp, SystemSettingsFile);
                CleanBackups();
            }
        }

        static void SaveTaxRate(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<TaxRateRequest>(ReadBody(ctx.Request));
            if (req == null) { WriteJson(ctx, new { error = "请求无效" }, 400); return; }
            if (req.TaxRate < 0) { WriteJson(ctx, new { error = "税率不能小于 0" }, 400); return; }
            var settings = LoadSystemSettings();
            settings.TaxRate = req.TaxRate;
            SaveSystemSettingsFile(settings);
            Audit(user, "修改税率", settings.TaxRate.ToString("0.##") + "%");
            WriteJson(ctx, settings);
        }

        static List<BomItem> LoadBom()
        {
            lock (DataLock)
            {
                if (!File.Exists(BomFile)) return new List<BomItem>();
                string text = File.ReadAllText(BomFile, Encoding.UTF8);
                return Json.Deserialize<List<BomItem>>(text) ?? new List<BomItem>();
            }
        }

        static void ApplyCurrentMaterialPrices(BomItem item)
        {
            if (item == null) return;
            var materials = LoadMaterials();
            decimal taxRate = LoadSystemSettings().TaxRate, total = 0;
            var missing = new List<string>();
            foreach (var line in item.Items ?? new List<BomDetail>())
            {
                var material = materials.FirstOrDefault(x => (!string.IsNullOrWhiteSpace(line.MaterialId) && x.Id == line.MaterialId) || (!string.IsNullOrWhiteSpace(line.MaterialCode) && x.Code == line.MaterialCode));
                if (material != null)
                {
                    line.MaterialId = material.Id; line.MaterialCode = material.Code; line.MaterialName = material.NameSpec;
                    line.Unit = string.IsNullOrWhiteSpace(material.QuantityUnit) ? line.Unit : material.QuantityUnit;
                    line.PriceType = NormalizePriceType(material.PriceType); line.OriginalPrice = GetMaterialOriginalPrice(material);
                    line.TaxRate = taxRate; line.PriceSourceTime = material.UpdatedAt;
                }
                line.PriceMissing = material == null || line.OriginalPrice <= 0;
                if (line.PriceMissing) missing.Add(string.IsNullOrWhiteSpace(line.MaterialName) ? (line.MaterialCode ?? "未命名物料") : line.MaterialName);
                line.NoTaxPrice = CalcNoTaxUnitPrice(line.OriginalPrice, line.PriceType, line.TaxRate);
                line.Amount = CalcLineAmount(line.Quantity, line.NoTaxPrice); total += line.Amount;
            }
            item.TotalMaterialCost = Math.Round(total, 2);
            item.PriceMissing = missing.Count > 0;
            item.MissingPriceMaterials = string.Join("、", missing.Distinct());
        }

        static List<BomItem> LoadBomWithCurrentPrices()
        {
            var list = LoadBom();
            foreach (var item in list) ApplyCurrentMaterialPrices(item);
            return list;
        }

        static void SaveBom(List<BomItem> items)
        {
            lock (DataLock)
            {
                string temp = BomFile + ".tmp";
                File.WriteAllText(temp, Json.Serialize(items), new UTF8Encoding(false));
                if (File.Exists(BomFile))
                {
                    string backup = Path.Combine(BackupDir, "bom_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json");
                    File.Replace(temp, BomFile, backup);
                }
                else File.Move(temp, BomFile);
                CleanBackups();
            }
        }

        static void AddBom(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<BomItem>(ReadBody(ctx.Request));
            ValidateBom(item);
            ApplyCurrentMaterialPrices(item);
            var list = LoadBom();
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            item.Id = Guid.NewGuid().ToString("N");
            item.Code = NextCode(BomSequenceFile, "BOM", list.Select(x => x.Code));
            item.CreatedAt = now;
            item.UpdatedAt = now;
            list.Insert(0, item);
            SaveBom(list);
            Audit(user, "新增BOM", item.Code + " " + item.ModelName);
            WriteJson(ctx, item, 201);
        }

        static void UpdateBom(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<BomItem>(ReadBody(ctx.Request));
            ValidateBom(input);
            var list = LoadBom();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "BOM 不存在" }, 404); return; }
            ApplyCurrentMaterialPrices(input);
            input.Id = item.Id;
            input.Code = item.Code;
            input.CreatedAt = item.CreatedAt;
            input.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            list[list.IndexOf(item)] = input;
            SaveBom(list);
            Audit(user, "修改BOM", input.Code + " " + input.ModelName);
            WriteJson(ctx, input);
        }

        static void DeleteBom(HttpListenerContext ctx, UserSession user, string id)
        {
            var list = LoadBom();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "BOM 不存在" }, 404); return; }
            if (IsBomUsedByModelCost(id)) { WriteJson(ctx, new { error = FormatBomDeleteBlockedMessage(GetModelCostsUsingBom(id)) }, 409); return; }
            list.Remove(item);
            SaveBom(list);
            Audit(user, "删除BOM", item.Code + " " + item.ModelName);
            WriteJson(ctx, new { ok = true });
        }

        static List<ModelCost> LoadModelCosts()
        {
            lock (DataLock)
            {
                if (!File.Exists(ModelCostFile)) return new List<ModelCost>();
                string text = File.ReadAllText(ModelCostFile, Encoding.UTF8);
                return Json.Deserialize<List<ModelCost>>(text) ?? new List<ModelCost>();
            }
        }

        static List<ModelCost> LoadModelCostsWithCurrentPrices()
        {
            var list = LoadModelCosts();
            var boms = LoadBomWithCurrentPrices();
            foreach (var item in list)
            {
                var bom = boms.FirstOrDefault(x => x.Id == item.BomId);
                if (bom == null) continue;
                item.MaterialCost = bom.TotalMaterialCost; item.TotalCost = item.MaterialCost;
                item.PriceMissing = bom.PriceMissing; item.MissingPriceMaterials = bom.MissingPriceMaterials;
            }
            return list;
        }

        static void SaveModelCosts(List<ModelCost> items)
        {
            lock (DataLock)
            {
                string temp = ModelCostFile + ".tmp";
                File.WriteAllText(temp, Json.Serialize(items), new UTF8Encoding(false));
                if (File.Exists(ModelCostFile))
                {
                    string backup = Path.Combine(BackupDir, "model_costs_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json");
                    File.Replace(temp, ModelCostFile, backup);
                }
                else File.Move(temp, ModelCostFile);
                CleanBackups();
            }
        }

        static void FillModelCostFromBom(ModelCost item, bool allowDisabledBom = false)
        {
            var bom = LoadBom().FirstOrDefault(x => x.Id == item.BomId);
            if (bom == null) throw new Exception("所选 BOM 不存在");
            if (!allowDisabledBom && (bom.Status ?? "启用") != "启用") throw new Exception("所选 BOM 已停用，不能新建或更换为该 BOM");
            ApplyCurrentMaterialPrices(bom);
            item.ModelCode = bom.ModelCode;
            item.ModelName = bom.ModelName;
            item.ProductName = bom.ProductName;
            item.BomVersion = bom.Version;
            item.BomCode = bom.Code;
            item.MaterialCost = bom.TotalMaterialCost;
            item.TotalCost = item.MaterialCost;
            item.PriceMissing = bom.PriceMissing;
            item.MissingPriceMaterials = bom.MissingPriceMaterials;
        }

        static void AddModelCost(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<ModelCost>(ReadBody(ctx.Request));
            ValidateModelCost(item);
            FillModelCostFromBom(item, false);
            ValidateModelCostFilled(item);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            item.Id = Guid.NewGuid().ToString("N");
            item.CreatedAt = now;
            item.UpdatedAt = now;
            if (string.IsNullOrWhiteSpace(item.Status)) item.Status = "启用";
            var list = LoadModelCosts();
            list.Insert(0, item);
            SaveModelCosts(list);
            Audit(user, "新增机型成本", item.ModelCode + " " + item.ModelName);
            WriteJson(ctx, item, 201);
        }

        static void UpdateModelCost(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<ModelCost>(ReadBody(ctx.Request));
            ValidateModelCost(input);
            var list = LoadModelCosts();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "机型成本记录不存在" }, 404); return; }
            bool sameBom = string.Equals(item.BomId, input.BomId, StringComparison.OrdinalIgnoreCase);
            FillModelCostFromBom(input, sameBom);
            ValidateModelCostFilled(input);
            input.Id = item.Id;
            input.CreatedAt = item.CreatedAt;
            input.Status = string.IsNullOrWhiteSpace(input.Status) ? (item.Status ?? "启用") : input.Status.Trim();
            if (input.Status != "启用" && input.Status != "停用") { WriteJson(ctx, new { error = "状态只能是启用或停用" }, 400); return; }
            if (input.Status == "启用")
            {
                var bomCheck = LoadBom().FirstOrDefault(x => x.Id == input.BomId);
                if (bomCheck != null && (bomCheck.Status ?? "启用") != "启用") { WriteJson(ctx, new { error = "该机型成本关联的 BOM 已停用，不能启用。" }, 409); return; }
            }
            input.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            list[list.IndexOf(item)] = input;
            SaveModelCosts(list);
            Audit(user, "修改机型成本", input.ModelCode + " " + input.ModelName);
            WriteJson(ctx, input);
        }

        static void DeleteModelCost(HttpListenerContext ctx, UserSession user, string id)
        {
            var list = LoadModelCosts();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "机型成本记录不存在" }, 404); return; }
            if ((item.Status ?? "启用") == "启用") { WriteJson(ctx, new { error = "该机型成本当前为启用状态，不能删除。请先停用后再删除。" }, 409); return; }
            list.Remove(item);
            SaveModelCosts(list);
            Audit(user, "删除机型成本", item.ModelCode + " " + item.ModelName);
            WriteJson(ctx, new { ok = true });
        }

        static PermissionGroup[] GetPermissionDefinitions()
        {
            return new[]
            {
                new PermissionGroup { Module = "供应商管理", Items = new[] {
                    new PermissionItem { Key = "supplier.view", Label = "查看" },
                    new PermissionItem { Key = "supplier.add", Label = "新增" },
                    new PermissionItem { Key = "supplier.edit", Label = "修改" },
                    new PermissionItem { Key = "supplier.delete", Label = "删除" },
                    new PermissionItem { Key = "supplier.batch_delete", Label = "批量删除" }
                }},
                new PermissionGroup { Module = "客户管理", Items = new[] {
                    new PermissionItem { Key = "customer.view", Label = "查看" },
                    new PermissionItem { Key = "customer.add", Label = "新增" },
                    new PermissionItem { Key = "customer.edit", Label = "修改" },
                    new PermissionItem { Key = "customer.delete", Label = "删除" },
                    new PermissionItem { Key = "customer.batch_delete", Label = "批量删除" }
                }},
                new PermissionGroup { Module = "物料管理", Items = new[] {
                    new PermissionItem { Key = "material.view", Label = "查看" },
                    new PermissionItem { Key = "material.add", Label = "新增" },
                    new PermissionItem { Key = "material.edit", Label = "修改" },
                    new PermissionItem { Key = "material.delete", Label = "删除" },
                    new PermissionItem { Key = "material.batch_delete", Label = "批量删除" }
                }},
                new PermissionGroup { Module = "财务收支", Items = new[] {
                    new PermissionItem { Key = "finance.view", Label = "查看" },
                    new PermissionItem { Key = "finance.add", Label = "新增" },
                    new PermissionItem { Key = "finance.edit", Label = "修改" },
                    new PermissionItem { Key = "finance.delete", Label = "删除" },
                    new PermissionItem { Key = "finance.import", Label = "导入" }
                }},
                new PermissionGroup { Module = "BOM表", Items = new[] {
                    new PermissionItem { Key = "bom.view", Label = "查看" },
                    new PermissionItem { Key = "bom.add", Label = "新增" },
                    new PermissionItem { Key = "bom.edit", Label = "修改" },
                    new PermissionItem { Key = "bom.delete", Label = "删除" },
                    new PermissionItem { Key = "bom.export", Label = "导出" },
                    new PermissionItem { Key = "bom.import", Label = "导入" }
                }},
                new PermissionGroup { Module = "机型成本", Items = new[] {
                    new PermissionItem { Key = "model_cost.view", Label = "查看" },
                    new PermissionItem { Key = "model_cost.add", Label = "新增" },
                    new PermissionItem { Key = "model_cost.edit", Label = "修改" },
                    new PermissionItem { Key = "model_cost.delete", Label = "删除" },
                    new PermissionItem { Key = "model_cost.export", Label = "导出" },
                    new PermissionItem { Key = "model_cost.import", Label = "导入" }
                }},
                new PermissionGroup { Module = "合同管理", Items = new[] {
                    new PermissionItem { Key = "contract.view", Label = "查看合同" },
                    new PermissionItem { Key = "contract.add", Label = "新增合同" },
                    new PermissionItem { Key = "contract.edit", Label = "修改合同" },
                    new PermissionItem { Key = "contract.delete", Label = "删除合同" },
                    new PermissionItem { Key = "contract.preview", Label = "预览合同" },
                    new PermissionItem { Key = "contract.print", Label = "打印合同" }
                }},
                new PermissionGroup { Module = "合同资料", Items = new[] {
                    new PermissionItem { Key = "contract_setting.view", Label = "查看" },
                    new PermissionItem { Key = "contract_setting.add", Label = "新增" },
                    new PermissionItem { Key = "contract_setting.edit", Label = "修改" },
                    new PermissionItem { Key = "contract_setting.delete", Label = "删除" }
                }},
                new PermissionGroup { Module = "系统设置", Items = new[] {
                    new PermissionItem { Key = "settings.view", Label = "查看系统设置" },
                    new PermissionItem { Key = "settings.account", Label = "账号管理" },
                    new PermissionItem { Key = "settings.password", Label = "修改密码" },
                    new PermissionItem { Key = "settings.tax_rate", Label = "税率修改" },
                    new PermissionItem { Key = "settings.dictionary", Label = "字典选项" }
                }}
            };
        }

        static bool CanReadDictionaryOptions( UserSession user)
        {
            return HasPermission(user, "settings.view") || HasPermission(user, "settings.dictionary");
        }

        static string GetQueryParam(HttpListenerContext ctx, string key)
        {
            string q = ctx.Request.Url.Query;
            if (string.IsNullOrEmpty(q)) return "";
            foreach (var part in q.TrimStart('?').Split('&'))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length == 2 && string.Equals(Uri.UnescapeDataString(kv[0]), key, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(kv[1]);
            }
            return "";
        }

        static readonly string[] DictionaryCategories = new[] {
            "SupplierType", "CustomerType", "MaterialCategory", "MaterialUnit", "FinanceItem", "ContractType", "OrderCategory", "OtherCategory", "BomStatus", "ModelCostStatus"
        };

        static List<DictionaryOption> LoadDictionaryOptions()
        {
            lock (DataLock)
            {
                if (!File.Exists(DictionaryOptionsFile)) return new List<DictionaryOption>();
                string text = File.ReadAllText(DictionaryOptionsFile, Encoding.UTF8);
                return Json.Deserialize<List<DictionaryOption>>(text) ?? new List<DictionaryOption>();
            }
        }

        static void SaveDictionaryOptions(List<DictionaryOption> items)
        {
            lock (DataLock)
            {
                string temp = DictionaryOptionsFile + ".tmp";
                File.WriteAllText(temp, Json.Serialize(items), new UTF8Encoding(false));
                if (File.Exists(DictionaryOptionsFile))
                {
                    string backup = Path.Combine(BackupDir, "dictionary_options_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json");
                    File.Replace(temp, DictionaryOptionsFile, backup);
                }
                else File.Move(temp, DictionaryOptionsFile);
                CleanBackups();
            }
        }

        static void EnsureDefaultDictionaryOptions()
        {
            lock (DataLock)
            {
                var list = File.Exists(DictionaryOptionsFile) ? (Json.Deserialize<List<DictionaryOption>>(File.ReadAllText(DictionaryOptionsFile, Encoding.UTF8)) ?? new List<DictionaryOption>()) : new List<DictionaryOption>();
                if (list.Count > 0) return;
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var defaults = new List<Tuple<string, string, int>>
                {
                    Tuple.Create("SupplierType", "原材料供应商", 1),
                    Tuple.Create("SupplierType", "外协加工厂", 2),
                    Tuple.Create("SupplierType", "设备供应商", 3),
                    Tuple.Create("CustomerType", "终端客户", 1),
                    Tuple.Create("CustomerType", "经销商", 2),
                    Tuple.Create("CustomerType", "代理商", 3),
                    Tuple.Create("MaterialCategory", "原材料", 1),
                    Tuple.Create("MaterialCategory", "半成品", 2),
                    Tuple.Create("MaterialCategory", "辅料", 3),
                    Tuple.Create("MaterialCategory", "包材", 4),
                    Tuple.Create("MaterialUnit", "个", 1),
                    Tuple.Create("MaterialUnit", "件", 2),
                    Tuple.Create("MaterialUnit", "套", 3),
                    Tuple.Create("MaterialUnit", "kg", 4),
                    Tuple.Create("MaterialUnit", "m", 5),
                    Tuple.Create("FinanceItem", "销售收入", 1),
                    Tuple.Create("FinanceItem", "采购支出", 2),
                    Tuple.Create("FinanceItem", "办公费用", 3),
                    Tuple.Create("FinanceItem", "差旅费", 4),
                    Tuple.Create("ContractType", "设备购销合同", 1),
                    Tuple.Create("ContractType", "配件购销合同", 2),
                    Tuple.Create("ContractType", "维保合同", 3),
                    Tuple.Create("BomStatus", "启用", 1),
                    Tuple.Create("BomStatus", "停用", 2),
                    Tuple.Create("ModelCostStatus", "启用", 1),
                    Tuple.Create("ModelCostStatus", "停用", 2)
                };
                foreach (var d in defaults)
                {
                    list.Add(new DictionaryOption
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Category = d.Item1,
                        Name = d.Item2,
                        Status = "启用",
                        SortOrder = d.Item3,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
                SaveDictionaryOptions(list);
            }
        }

        static void ValidateDictionaryOption(DictionaryOption item)
        {
            if (item == null) throw new Exception("字典项不能为空");
            if (string.IsNullOrWhiteSpace(item.Category)) throw new Exception("字典分类不能为空");
            if (Array.IndexOf(DictionaryCategories, item.Category.Trim()) < 0) throw new Exception("字典分类无效");
            if (string.IsNullOrWhiteSpace(item.Name)) throw new Exception("字典名称不能为空");
            item.Category = item.Category.Trim();
            item.Name = item.Name.Trim();
            item.Value = string.IsNullOrWhiteSpace(item.Value) ? item.Name : item.Value.Trim();
            item.Note = (item.Note ?? "").Trim();
            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "启用" : item.Status.Trim();
            if (item.Status != "启用" && item.Status != "停用") throw new Exception("状态只能是启用或停用");
        }

        static bool IsDictionaryOptionInUse(DictionaryOption item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Name)) return false;
            string name = item.Name;
            switch (item.Category)
            {
                case "MaterialUnit":
                    return LoadMaterials().Any(x => string.Equals(x.QuantityUnit, name, StringComparison.OrdinalIgnoreCase));
                case "FinanceItem":
                    return LoadFinance().Any(x => string.Equals(x.Purpose, name, StringComparison.OrdinalIgnoreCase) || string.Equals(x.AccountType, name, StringComparison.OrdinalIgnoreCase));
                case "BomStatus":
                    return LoadBom().Any(x => string.Equals(x.Status ?? "启用", name, StringComparison.OrdinalIgnoreCase));
                case "ModelCostStatus":
                    return LoadModelCosts().Any(x => string.Equals(x.Status ?? "启用", name, StringComparison.OrdinalIgnoreCase));
                default:
                    return false;
            }
        }

        static void ListDictionaryOptions(HttpListenerContext ctx)
        {
            string category = GetQueryParam(ctx, "category");
            var list = LoadDictionaryOptions();
            if (!string.IsNullOrWhiteSpace(category))
                list = list.Where(x => string.Equals(x.Category, category.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            list = list.OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Name).ToList();
            WriteJson(ctx, list);
        }

        static void AddDictionaryOption(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<DictionaryOption>(ReadBody(ctx.Request));
            ValidateDictionaryOption(item);
            var list = LoadDictionaryOptions();
            if (list.Any(x => string.Equals(x.Category, item.Category, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
            { WriteJson(ctx, new { error = "该分类下已存在相同名称的字典项" }, 409); return; }
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            item.Id = Guid.NewGuid().ToString("N");
            if (item.SortOrder <= 0) item.SortOrder = list.Where(x => x.Category == item.Category).Select(x => x.SortOrder).DefaultIfEmpty(0).Max() + 1;
            item.CreatedAt = now;
            item.UpdatedAt = now;
            list.Add(item);
            SaveDictionaryOptions(list);
            Audit(user, "新增字典项", item.Category + " " + item.Name);
            WriteJson(ctx, item, 201);
        }

        static void UpdateDictionaryOption(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<DictionaryOption>(ReadBody(ctx.Request));
            var list = LoadDictionaryOptions();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "字典项不存在" }, 404); return; }
            ValidateDictionaryOption(input);
            if (list.Any(x => x.Id != id && string.Equals(x.Category, input.Category, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Name, input.Name, StringComparison.OrdinalIgnoreCase)))
            { WriteJson(ctx, new { error = "该分类下已存在相同名称的字典项" }, 409); return; }
            item.Category = input.Category;
            item.Name = input.Name;
            item.Value = string.IsNullOrWhiteSpace(input.Value) ? input.Name : input.Value.Trim();
            item.Note = (input.Note ?? "").Trim();
            item.Status = input.Status;
            if (input.SortOrder > 0) item.SortOrder = input.SortOrder;
            item.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveDictionaryOptions(list);
            Audit(user, "修改字典项", item.Category + " " + item.Name);
            WriteJson(ctx, item);
        }

        static void DeleteDictionaryOption(HttpListenerContext ctx, UserSession user, string id)
        {
            var list = LoadDictionaryOptions();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "字典项不存在" }, 404); return; }
            if (IsDictionaryOptionInUse(item)) { WriteJson(ctx, new { error = "该字典项已被业务数据使用，不能删除。请改为停用。" }, 409); return; }
            list.Remove(item);
            SaveDictionaryOptions(list);
            Audit(user, "删除字典项", item.Category + " " + item.Name);
            WriteJson(ctx, new { ok = true });
        }

        static string BuildPermissionSummary(UserDef user)
        {
            if (IsAdminUsername(user.Username)) return "全部权限";
            var perms = NormalizePermissions(user.Permissions);
            if (perms.Length == 0) return "无权限";
            if (perms.Length == AllPermissionKeys.Length) return "全部权限";
            var labels = new Dictionary<string, string>();
            foreach (var g in GetPermissionDefinitions())
                foreach (var item in g.Items) labels[item.Key] = g.Module + "·" + item.Label;
            return string.Join("、", perms.Take(6).Select(p => labels.ContainsKey(p) ? labels[p] : p)) + (perms.Length > 6 ? "…" : "");
        }

        static UserPublic ToPublic(UserDef user)
        {
            return new UserPublic
            {
                Username = user.Username,
                DisplayName = user.DisplayName,
                Role = user.Role,
                Enabled = user.Enabled,
                Permissions = IsAdminUsername(user.Username) ? AllPermissionKeys : NormalizePermissions(user.Permissions),
                PermissionSummary = BuildPermissionSummary(user)
            };
        }

        static void InvalidateUserSessions(string username)
        {
            lock (SessionLock)
            {
                foreach (var key in Sessions.Where(x => string.Equals(x.Value.Username, username, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).ToList())
                    Sessions.Remove(key);
            }
        }

        static void ListUsers(HttpListenerContext ctx)
        {
            WriteJson(ctx, Users.Select(ToPublic).OrderBy(x => x.Username).ToList());
        }

        static void CreateUser(HttpListenerContext ctx, UserSession actor)
        {
            var req = Json.Deserialize<CreateUserRequest>(ReadBody(ctx.Request));
            if (req == null || string.IsNullOrWhiteSpace(req.Username)) { WriteJson(ctx, new { error = "账户名不能为空" }, 400); return; }
            if (string.IsNullOrWhiteSpace(req.Password)) { WriteJson(ctx, new { error = "密码不能为空" }, 400); return; }
            string username = req.Username.Trim();
            if (IsAdminUsername(username)) { WriteJson(ctx, new { error = "不能创建同名主账号" }, 409); return; }
            if (FindUser(username) != null) { WriteJson(ctx, new { error = "账户名已存在" }, 409); return; }
            var user = new UserDef
            {
                Username = username,
                DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? username : req.DisplayName.Trim(),
                Role = "普通用户",
                PasswordHash = Sha256(req.Password),
                Enabled = req.Enabled,
                Permissions = NormalizePermissions(req.Permissions)
            };
            Users.Add(user);
            SaveUsers();
            Audit(actor, "新增子账号", user.Username);
            WriteJson(ctx, ToPublic(user), 201);
        }

        static void UpdateUser(HttpListenerContext ctx, UserSession actor, string username)
        {
            if (IsAdminUsername(username)) { WriteJson(ctx, new { error = "不能修改主账号权限或状态" }, 403); return; }
            var user = FindUser(username);
            if (user == null) { WriteJson(ctx, new { error = "账号不存在" }, 404); return; }
            var req = Json.Deserialize<UpdateUserRequest>(ReadBody(ctx.Request));
            if (req == null) { WriteJson(ctx, new { error = "请求无效" }, 400); return; }
            if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.DisplayName = req.DisplayName.Trim();
            user.Enabled = req.Enabled;
            user.Permissions = NormalizePermissions(req.Permissions);
            if (!string.IsNullOrWhiteSpace(req.Password)) user.PasswordHash = Sha256(req.Password);
            SaveUsers();
            InvalidateUserSessions(user.Username);
            Audit(actor, "编辑子账号", user.Username);
            WriteJson(ctx, ToPublic(user));
        }

        static void DeleteUser(HttpListenerContext ctx, UserSession actor, string username)
        {
            if (IsAdminUsername(username)) { WriteJson(ctx, new { error = "不能删除主账号" }, 403); return; }
            var user = FindUser(username);
            if (user == null) { WriteJson(ctx, new { error = "账号不存在" }, 404); return; }
            Users.Remove(user);
            SaveUsers();
            InvalidateUserSessions(user.Username);
            Audit(actor, "删除子账号", user.Username);
            WriteJson(ctx, new { ok = true });
        }

        static void ChangePassword(HttpListenerContext ctx, UserSession user)
        {
            if (!HasPermission(user, "settings.password")) { WriteJson(ctx, new { error = "无权限操作" }, 403); return; }
            var req = Json.Deserialize<ChangePasswordRequest>(ReadBody(ctx.Request));
            if (req == null || string.IsNullOrWhiteSpace(req.OldPassword)) { WriteJson(ctx, new { error = "请输入旧密码" }, 400); return; }
            if (string.IsNullOrWhiteSpace(req.NewPassword)) { WriteJson(ctx, new { error = "请输入新密码" }, 400); return; }
            var def = FindUser(user.Username);
            if (def == null) { WriteJson(ctx, new { error = "账号不存在" }, 404); return; }
            if (!FixedEquals(def.PasswordHash, Sha256(req.OldPassword))) { WriteJson(ctx, new { error = "旧密码不正确" }, 401); return; }
            def.PasswordHash = Sha256(req.NewPassword);
            SaveUsers();
            InvalidateUserSessions(def.Username);
            Audit(user, "修改密码", def.Username);
            WriteJson(ctx, new { ok = true });
        }

        static List<Supplier> LoadSuppliers()
        {
            lock (DataLock)
            {
                string text = File.ReadAllText(DataFile, Encoding.UTF8);
                return Json.Deserialize<List<Supplier>>(text) ?? new List<Supplier>();
            }
        }

        static void SaveSuppliers(List<Supplier> items)
        {
            lock (DataLock)
            {
                string temp = DataFile + ".tmp";
                File.WriteAllText(temp, Json.Serialize(items), new UTF8Encoding(false));
                if (File.Exists(DataFile))
                {
                    string backup = Path.Combine(BackupDir, "auto_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json");
                    File.Replace(temp, DataFile, backup);
                }
                else File.Move(temp, DataFile);
                CleanBackups();
            }
        }

        static void AddSupplier(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<Supplier>(ReadBody(ctx.Request));
            Validate(item);
            var list = LoadSuppliers();
            if (list.Any(x => string.Equals(x.Company, item.Company, StringComparison.OrdinalIgnoreCase))) { WriteJson(ctx, new { error = "该供应商公司已经存在" }, 409); return; }
            item.Id = Guid.NewGuid().ToString("N"); item.Code = NextCode(SupplierSequenceFile, "GY", list.Select(x=>x.Code)); item.Status = "启用"; item.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); item.UpdatedBy = user.DisplayName;
            list.Insert(0, item); SaveSuppliers(list); Audit(user, "新增供应商", item.Company); WriteJson(ctx, item, 201);
        }

        static void UpdateSupplier(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<Supplier>(ReadBody(ctx.Request)); Validate(input);
            var list = LoadSuppliers(); var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "供应商不存在" }, 404); return; }
            if (list.Any(x => x.Id != id && string.Equals(x.Company, input.Company, StringComparison.OrdinalIgnoreCase))) { WriteJson(ctx, new { error = "该供应商公司已经存在" }, 409); return; }
            item.Company=input.Company; item.Contact=input.Contact; item.Phone=input.Phone; item.Goods=input.Goods; item.Address=input.Address; item.Bank=input.Bank; item.Account=input.Account; item.BankNo=input.BankNo; item.Payable=input.Payable; item.Status=string.IsNullOrEmpty(input.Status)?"启用":input.Status; item.UpdatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); item.UpdatedBy=user.DisplayName;
            SaveSuppliers(list); Audit(user, "修改供应商", item.Company); WriteJson(ctx, item);
        }

        static void DeleteSupplier(HttpListenerContext ctx, UserSession user, string id)
        {
            var list = LoadSuppliers(); var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "供应商不存在" }, 404); return; }
            if (IsSupplierUsedByMaterial(item.Company)) { WriteJson(ctx, new { error = "该供应商已被物料使用，不能删除。如需删除，请先从物料管理中移除或更换相关物料的供应商。" }, 409); return; }
            list.Remove(item); SaveSuppliers(list); Audit(user, "删除供应商", item.Company); WriteJson(ctx, new { ok = true });
        }

        static void BatchDeleteSuppliers(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchDeleteRequest>(ReadBody(ctx.Request));
            var ids = (req == null ? null : req.Ids) ?? new string[0];
            ids = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            if (ids.Length == 0) { WriteJson(ctx, new { error = "请先选择要删除的数据" }, 400); return; }
            var list = LoadSuppliers();
            var removed = list.Where(x => ids.Contains(x.Id)).ToList();
            if (removed.Count == 0) { WriteJson(ctx, new { error = "未找到可删除的供应商" }, 404); return; }
            if (removed.Any(item => IsSupplierUsedByMaterial(item.Company))) { WriteJson(ctx, new { error = "所选供应商中存在已被物料使用的记录，不能删除。如需删除，请先从物料管理中移除或更换相关物料的供应商。" }, 409); return; }
            foreach (var item in removed) list.Remove(item);
            SaveSuppliers(list);
            Audit(user, "批量删除供应商", "共" + removed.Count + "条");
            WriteJson(ctx, new { ok = true, deleted = removed.Count });
        }

        static void BatchAddSuppliers(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchSupplierRequest>(ReadBody(ctx.Request));
            var items = req == null ? null : req.Items;
            if (items == null || items.Count == 0) { WriteJson(ctx, new { error = "请至少填写一条供应商资料" }, 400); return; }
            var list = LoadSuppliers();
            int imported = 0, skipped = 0, rowNo = 0;
            var errors = new List<string>();
            var batchCompanies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new List<Supplier>();
            foreach (var input in items)
            {
                rowNo++;
                try
                {
                    Validate(input);
                    if (list.Any(x => string.Equals(x.Company, input.Company, StringComparison.OrdinalIgnoreCase)) || batchCompanies.Contains(input.Company))
                    {
                        skipped++; errors.Add("第" + rowNo + "行：供应商已存在"); continue;
                    }
                    var item = new Supplier
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Code = NextCode(SupplierSequenceFile, "GY", list.Select(x => x.Code).Concat(pending.Select(x => x.Code))),
                        Company = input.Company, Contact = input.Contact, Phone = input.Phone, Goods = input.Goods,
                        Address = input.Address, Bank = input.Bank, Account = input.Account, BankNo = input.BankNo,
                        Payable = input.Payable, Status = string.IsNullOrEmpty(input.Status) ? "启用" : input.Status,
                        UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), UpdatedBy = user.DisplayName
                    };
                    batchCompanies.Add(item.Company);
                    pending.Insert(0, item);
                    imported++;
                }
                catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
            }
            if (imported == 0) { WriteJson(ctx, new { error = "没有可保存的数据", imported = 0, skipped = skipped, errors = errors.Take(20).ToArray() }, 409); return; }
            foreach (var item in pending) list.Insert(0, item);
            SaveSuppliers(list);
            Audit(user, "批量添加供应商", "成功" + imported + "条，跳过" + skipped + "条");
            WriteJson(ctx, new { imported = imported, skipped = skipped, errors = errors.Take(20).ToArray() });
        }

        static List<Customer> LoadCustomers()
        {
            lock (DataLock)
            {
                string text = File.ReadAllText(CustomerFile, Encoding.UTF8);
                return Json.Deserialize<List<Customer>>(text) ?? new List<Customer>();
            }
        }

        static void SaveCustomers(List<Customer> items)
        {
            lock (DataLock)
            {
                string temp = CustomerFile + ".tmp";
                File.WriteAllText(temp, Json.Serialize(items), new UTF8Encoding(false));
                if (File.Exists(CustomerFile))
                {
                    string backup = Path.Combine(BackupDir, "customers_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json");
                    File.Replace(temp, CustomerFile, backup);
                }
                else File.Move(temp, CustomerFile);
                CleanBackups();
            }
        }

        static string NextCode(string sequenceFile, string prefix, IEnumerable<string> codes)
        {
            int max = 0;
            foreach (var code in codes.Select(x => x ?? ""))
            {
                int value;
                if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(code.Substring(prefix.Length), out value)) max = Math.Max(max, value);
            }
            lock (DataLock)
            {
                int sequence;
                if (!int.TryParse(File.ReadAllText(sequenceFile, Encoding.UTF8), out sequence)) sequence = 0;
                sequence = Math.Max(sequence, max) + 1;
                File.WriteAllText(sequenceFile, sequence.ToString(), new UTF8Encoding(false));
                return prefix + sequence.ToString("D2");
            }
        }

        static void EnsureLegacyCodes()
        {
            var suppliers = LoadSuppliers(); bool suppliersChanged = false;
            foreach (var item in suppliers.Where(x => string.IsNullOrWhiteSpace(x.Code)).Reverse()) { item.Code = NextCode(SupplierSequenceFile, "GY", suppliers.Select(x=>x.Code)); suppliersChanged = true; }
            if (suppliersChanged) SaveSuppliers(suppliers);
            var customers = LoadCustomers(); bool customersChanged = false;
            foreach (var item in customers.Where(x => string.IsNullOrWhiteSpace(x.Code)).Reverse()) { item.Code = NextCode(CustomerSequenceFile, "KH", customers.Select(x=>x.Code)); customersChanged = true; }
            if (customersChanged) SaveCustomers(customers);
            var materials = LoadMaterials(); bool materialsChanged = false;
            foreach (var item in materials.Where(x => string.IsNullOrWhiteSpace(x.Code)).Reverse()) { item.Code = NextCode(MaterialSequenceFile, "WL", materials.Select(x=>x.Code)); materialsChanged = true; }
            if (materialsChanged) SaveMaterials(materials);
        }

        static void AddCustomer(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<Customer>(ReadBody(ctx.Request)); ValidateCustomer(item);
            var list = LoadCustomers();
            if (list.Any(x => string.Equals(x.Company, item.Company, StringComparison.OrdinalIgnoreCase))) { WriteJson(ctx, new { error = "该客户公司已经存在" }, 409); return; }
            item.Id = Guid.NewGuid().ToString("N"); item.Code = NextCode(CustomerSequenceFile, "KH", list.Select(x=>x.Code)); item.Status = "启用"; item.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); item.UpdatedBy = user.DisplayName;
            list.Insert(0, item); SaveCustomers(list); Audit(user, "新增客户", item.Code + " " + item.Company); WriteJson(ctx, item, 201);
        }

        static void UpdateCustomer(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<Customer>(ReadBody(ctx.Request)); ValidateCustomer(input);
            var list = LoadCustomers(); var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "客户不存在" }, 404); return; }
            if (list.Any(x => x.Id != id && string.Equals(x.Company, input.Company, StringComparison.OrdinalIgnoreCase))) { WriteJson(ctx, new { error = "该客户公司已经存在" }, 409); return; }
            item.Company=input.Company; item.Contact=input.Contact; item.Phone=input.Phone; item.Bank=input.Bank; item.Account=input.Account; item.BankNo=input.BankNo; item.Address=input.Address; item.Receivable=input.Receivable; item.Status=string.IsNullOrEmpty(input.Status)?"启用":input.Status; item.UpdatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); item.UpdatedBy=user.DisplayName;
            SaveCustomers(list); Audit(user, "修改客户", item.Code + " " + item.Company); WriteJson(ctx, item);
        }

        static void DeleteCustomer(HttpListenerContext ctx, UserSession user, string id)
        {
            var list = LoadCustomers(); var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "客户不存在" }, 404); return; }
            list.Remove(item); SaveCustomers(list); Audit(user, "删除客户", item.Code + " " + item.Company); WriteJson(ctx, new { ok = true });
        }

        static void BatchDeleteCustomers(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchDeleteRequest>(ReadBody(ctx.Request));
            var ids = (req == null ? null : req.Ids) ?? new string[0];
            ids = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            if (ids.Length == 0) { WriteJson(ctx, new { error = "请先选择要删除的数据" }, 400); return; }
            var list = LoadCustomers();
            var removed = list.Where(x => ids.Contains(x.Id)).ToList();
            if (removed.Count == 0) { WriteJson(ctx, new { error = "未找到可删除的客户" }, 404); return; }
            foreach (var item in removed) list.Remove(item);
            SaveCustomers(list);
            Audit(user, "批量删除客户", "共" + removed.Count + "条");
            WriteJson(ctx, new { ok = true, deleted = removed.Count });
        }

        static void BatchAddCustomers(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchCustomerRequest>(ReadBody(ctx.Request));
            var items = req == null ? null : req.Items;
            if (items == null || items.Count == 0) { WriteJson(ctx, new { error = "请至少填写一条客户资料" }, 400); return; }
            var list = LoadCustomers();
            int imported = 0, skipped = 0, rowNo = 0;
            var errors = new List<string>();
            var batchCompanies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new List<Customer>();
            foreach (var input in items)
            {
                rowNo++;
                try
                {
                    ValidateCustomer(input);
                    if (list.Any(x => string.Equals(x.Company, input.Company, StringComparison.OrdinalIgnoreCase)) || batchCompanies.Contains(input.Company))
                    {
                        skipped++; errors.Add("第" + rowNo + "行：客户已存在"); continue;
                    }
                    var item = new Customer
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Code = NextCode(CustomerSequenceFile, "KH", list.Select(x => x.Code).Concat(pending.Select(x => x.Code))),
                        Company = input.Company, Contact = input.Contact, Phone = input.Phone, Bank = input.Bank,
                        Account = input.Account, BankNo = input.BankNo, Address = input.Address, Receivable = input.Receivable,
                        Status = string.IsNullOrEmpty(input.Status) ? "启用" : input.Status,
                        UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), UpdatedBy = user.DisplayName
                    };
                    batchCompanies.Add(item.Company);
                    pending.Insert(0, item);
                    imported++;
                }
                catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
            }
            if (imported == 0) { WriteJson(ctx, new { error = "没有可保存的数据", imported = 0, skipped = skipped, errors = errors.Take(20).ToArray() }, 409); return; }
            foreach (var item in pending) list.Insert(0, item);
            SaveCustomers(list);
            Audit(user, "批量添加客户", "成功" + imported + "条，跳过" + skipped + "条");
            WriteJson(ctx, new { imported = imported, skipped = skipped, errors = errors.Take(20).ToArray() });
        }

        static List<Material> LoadMaterials()
        {
            lock (DataLock)
            {
                string text = File.ReadAllText(MaterialFile, Encoding.UTF8);
                return Json.Deserialize<List<Material>>(text) ?? new List<Material>();
            }
        }

        static void SaveMaterials(List<Material> items)
        {
            lock (DataLock)
            {
                string temp = MaterialFile + ".tmp";
                File.WriteAllText(temp, Json.Serialize(items), new UTF8Encoding(false));
                if (File.Exists(MaterialFile)) File.Replace(temp, MaterialFile, Path.Combine(BackupDir, "materials_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json"));
                else File.Move(temp, MaterialFile);
                CleanBackups();
            }
        }

        static void AddMaterial(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<Material>(ReadBody(ctx.Request)); ValidateMaterial(item);
            var list = LoadMaterials();
            if (list.Any(x => string.Equals(x.Supplier, item.Supplier, StringComparison.OrdinalIgnoreCase) && string.Equals(x.NameSpec, item.NameSpec, StringComparison.OrdinalIgnoreCase))) { WriteJson(ctx, new { error = "该供应商的相同物料已经存在" }, 409); return; }
            item.Id=Guid.NewGuid().ToString("N"); item.Code=NextCode(MaterialSequenceFile,"WL",list.Select(x=>x.Code)); item.PriceType=NormalizePriceType(item.PriceType); item.Status="启用"; item.UpdatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); item.UpdatedBy=user.DisplayName;
            list.Insert(0,item); SaveMaterials(list); Audit(user,"新增物料",item.Code+" "+item.NameSpec); WriteJson(ctx,item,201);
        }

        static void UpdateMaterial(HttpListenerContext ctx, UserSession user, string id)
        {
            var input=Json.Deserialize<Material>(ReadBody(ctx.Request)); ValidateMaterial(input);
            var list=LoadMaterials(); var item=list.FirstOrDefault(x=>x.Id==id);
            if(item==null){WriteJson(ctx,new{error="物料不存在"},404);return;}
            if(list.Any(x=>x.Id!=id&&string.Equals(x.Supplier,input.Supplier,StringComparison.OrdinalIgnoreCase)&&string.Equals(x.NameSpec,input.NameSpec,StringComparison.OrdinalIgnoreCase))){WriteJson(ctx,new{error="该供应商的相同物料已经存在"},409);return;}
            item.Supplier=input.Supplier;item.NameSpec=input.NameSpec;item.QuantityUnit=input.QuantityUnit;item.TaxPrice=input.TaxPrice;item.NoTaxPrice=input.NoTaxPrice;item.PriceType=NormalizePriceType(input.PriceType);item.Note=input.Note;item.Status=string.IsNullOrEmpty(input.Status)?"启用":input.Status;item.UpdatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");item.UpdatedBy=user.DisplayName;
            SaveMaterials(list);Audit(user,"修改物料",item.Code+" "+item.NameSpec);WriteJson(ctx,item);
        }

        static void DeleteMaterial(HttpListenerContext ctx, UserSession user, string id)
        {
            var list=LoadMaterials();var item=list.FirstOrDefault(x=>x.Id==id);if(item==null){WriteJson(ctx,new{error="物料不存在"},404);return;}
            if(IsMaterialUsedByBom(item.Id,item.Code)){WriteJson(ctx,new{error=FormatMaterialDeleteBlockedMessage(GetBomsUsingMaterial(item.Id,item.Code))},409);return;}
            list.Remove(item);SaveMaterials(list);Audit(user,"删除物料",item.Code+" "+item.NameSpec);WriteJson(ctx,new{ok=true});
        }

        static void BatchDeleteMaterials(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchDeleteRequest>(ReadBody(ctx.Request));
            var ids = (req == null ? null : req.Ids) ?? new string[0];
            ids = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            if (ids.Length == 0) { WriteJson(ctx, new { error = "请先选择要删除的数据" }, 400); return; }
            var list = LoadMaterials();
            var removed = list.Where(x => ids.Contains(x.Id)).ToList();
            if (removed.Count == 0) { WriteJson(ctx, new { error = "未找到可删除的物料" }, 404); return; }
            var blocked = removed.Where(item => IsMaterialUsedByBom(item.Id, item.Code)).ToList();
            if (blocked.Count > 0)
            {
                var parts = new List<string>();
                foreach (var item in blocked.Take(10))
                {
                    var boms = GetBomsUsingMaterial(item.Id, item.Code);
                    var bomRefs = string.Join("、", boms.Take(3).Select(b => (b.Code ?? "") + " " + b.ModelName).Select(x => x.Trim()).Where(x => x.Length > 0));
                    parts.Add((item.Code ?? "") + " " + item.NameSpec + (bomRefs.Length > 0 ? "（BOM：" + bomRefs + "）" : ""));
                }
                string msg = "以下物料已被 BOM 使用，不能删除。请先删除相关 BOM 后再删除物料。 " + string.Join("；", parts);
                if (blocked.Count > 10) msg += " 等共" + blocked.Count + "条";
                WriteJson(ctx, new { error = msg }, 409); return;
            }
            foreach (var item in removed) list.Remove(item);
            SaveMaterials(list);
            Audit(user, "批量删除物料", "共" + removed.Count + "条");
            WriteJson(ctx, new { ok = true, deleted = removed.Count });
        }

        static void BatchAddMaterials(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchMaterialRequest>(ReadBody(ctx.Request));
            var items = req == null ? null : req.Items;
            if (items == null || items.Count == 0) { WriteJson(ctx, new { error = "请至少填写一条物料资料" }, 400); return; }
            var list = LoadMaterials();
            var suppliers = LoadSuppliers();
            int imported = 0, skipped = 0, rowNo = 0;
            var errors = new List<string>();
            var batchKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new List<Material>();
            foreach (var input in items)
            {
                rowNo++;
                try
                {
                    if (string.IsNullOrWhiteSpace(input.NameSpec)) { skipped++; errors.Add("第" + rowNo + "行：物料名称/规格不能为空"); continue; }
                    if (string.IsNullOrWhiteSpace(input.Supplier)) { skipped++; errors.Add("第" + rowNo + "行：请选择供应商"); continue; }
                    input.Supplier = input.Supplier.Trim(); input.NameSpec = input.NameSpec.Trim();
                    input.QuantityUnit = (input.QuantityUnit ?? "").Trim(); input.Note = (input.Note ?? "").Trim();
                    if (!suppliers.Any(x => string.Equals(x.Company, input.Supplier, StringComparison.OrdinalIgnoreCase)))
                    {
                        skipped++; errors.Add("第" + rowNo + "行：供应商未建档"); continue;
                    }
                    string key = input.Supplier + "\t" + input.NameSpec;
                    if (list.Any(x => string.Equals(x.Supplier, input.Supplier, StringComparison.OrdinalIgnoreCase) && string.Equals(x.NameSpec, input.NameSpec, StringComparison.OrdinalIgnoreCase)) || batchKeys.Contains(key))
                    {
                        skipped++; errors.Add("第" + rowNo + "行：物料已存在"); continue;
                    }
                    var item = new Material
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Code = NextCode(MaterialSequenceFile, "WL", list.Select(x => x.Code).Concat(pending.Select(x => x.Code))),
                        Supplier = input.Supplier, NameSpec = input.NameSpec, QuantityUnit = input.QuantityUnit,
                        TaxPrice = input.TaxPrice, NoTaxPrice = input.NoTaxPrice, PriceType = NormalizePriceType(input.PriceType), Note = input.Note,
                        Status = string.IsNullOrEmpty(input.Status) ? "启用" : input.Status,
                        UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), UpdatedBy = user.DisplayName
                    };
                    batchKeys.Add(key);
                    pending.Insert(0, item);
                    imported++;
                }
                catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
            }
            if (imported == 0) { WriteJson(ctx, new { error = "没有可保存的数据", imported = 0, skipped = skipped, errors = errors.Take(20).ToArray() }, 409); return; }
            foreach (var item in pending) list.Insert(0, item);
            SaveMaterials(list);
            Audit(user, "批量添加物料", "成功" + imported + "条，跳过" + skipped + "条");
            WriteJson(ctx, new { imported = imported, skipped = skipped, errors = errors.Take(20).ToArray() });
        }

        static List<FinanceTransaction> LoadFinance()
        {
            lock (DataLock)
            {
                string text = File.ReadAllText(FinanceFile, Encoding.UTF8);
                return Json.Deserialize<List<FinanceTransaction>>(text) ?? new List<FinanceTransaction>();
            }
        }

        static void SaveFinance(List<FinanceTransaction> items)
        {
            lock (DataLock)
            {
                string temp = FinanceFile + ".tmp";
                File.WriteAllText(temp, Json.Serialize(items), new UTF8Encoding(false));
                if (File.Exists(FinanceFile))
                {
                    string backup = Path.Combine(BackupDir, "finance_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json");
                    File.Replace(temp, FinanceFile, backup);
                }
                else File.Move(temp, FinanceFile);
                CleanBackups();
            }
        }

        static OpeningBalances LoadOpeningBalances()
        {
            lock (DataLock)
            {
                string text = File.ReadAllText(OpeningFile, Encoding.UTF8);
                return Json.Deserialize<OpeningBalances>(text) ?? new OpeningBalances();
            }
        }

        static void SaveOpeningBalances(HttpListenerContext ctx, UserSession user)
        {
            var value = Json.Deserialize<OpeningBalances>(ReadBody(ctx.Request)) ?? new OpeningBalances();
            lock (DataLock)
            {
                if (File.Exists(OpeningFile)) File.Copy(OpeningFile, Path.Combine(BackupDir, "opening_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json"), true);
                File.WriteAllText(OpeningFile, Json.Serialize(value), new UTF8Encoding(false));
                CleanBackups();
            }
            Audit(user, "修改期初余额", "公户=" + value.PublicAccount + ",公司私户=" + value.CompanyPrivate + ",个人私户=" + value.PersonalPrivate);
            WriteJson(ctx, value);
        }

        static void AddFinance(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<FinanceTransaction>(ReadBody(ctx.Request)); ValidateFinance(item);
            item.Id = Guid.NewGuid().ToString("N"); item.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); item.UpdatedBy = user.DisplayName;
            var list = LoadFinance(); list.Add(item); SaveFinance(list); Audit(user, "新增收支", item.Date + " " + item.AccountType + " " + item.Purpose); WriteJson(ctx, item, 201);
        }

        static void UpdateFinance(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<FinanceTransaction>(ReadBody(ctx.Request)); ValidateFinance(input);
            var list = LoadFinance(); var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "收支记录不存在" }, 404); return; }
            item.Date=input.Date; item.AccountType=input.AccountType; item.Receipt=input.Receipt; item.Payment=input.Payment; item.PaymentMethod=input.PaymentMethod; item.Purpose=input.Purpose; item.Counterparty=input.Counterparty; item.Note=input.Note; item.UpdatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); item.UpdatedBy=user.DisplayName;
            SaveFinance(list); Audit(user, "修改收支", item.Date + " " + item.AccountType + " " + item.Purpose); WriteJson(ctx, item);
        }

        static void DeleteFinance(HttpListenerContext ctx, UserSession user, string id)
        {
            var list = LoadFinance(); var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "收支记录不存在" }, 404); return; }
            list.Remove(item); SaveFinance(list); Audit(user, "删除收支", item.Date + " " + item.AccountType + " " + item.Purpose); WriteJson(ctx, new { ok = true });
        }

        static void ExportFinanceTemplateCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine("收支类型,日期,分类,金额,对方单位/客户/供应商,摘要/备注,经办人,付款方式/收款方式,账户类型");
            sb.AppendLine("收入,2026-01-01,整机销售,10000,示例客户,示例摘要,张三,公司公户（现金）,公户");
            WriteCsvDownload(ctx, "财务收支导入模板.csv", sb.ToString());
        }

        static bool TryParseFinanceDate(string value, out DateTime date)
        {
            value = (value ?? "").Trim();
            if (DateTime.TryParse(value, out date)) return true;
            double serial;
            if (double.TryParse(value, out serial) && serial > 0 && serial < 2958466)
            {
                try { date = DateTime.FromOADate(serial); return true; } catch { }
            }
            date = default(DateTime);
            return false;
        }

        static void ImportFinance(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<ImportRequest>(ReadBody(ctx.Request));
            if (req == null || string.IsNullOrWhiteSpace(req.Data)) { WriteJson(ctx, new { error = "请选择导入文件" }, 400); return; }
            List<Dictionary<string, string>> rows;
            if ((req.FileName ?? "").EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                rows = ReadImportRowsFromRequest(req);
            else if ((req.FileName ?? "").EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                rows = ReadCsvImportRowsFromRequest(new CsvImportRequest { FileName = req.FileName, Data = req.Data });
            else { WriteJson(ctx, new { error = "仅支持 .csv 或 .xlsx 格式文件" }, 400); return; }

            var list = LoadFinance();
            var errors = new List<string>();
            int imported = 0, rowNo = 1;
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    string type = Cell(row, "收支类型", "类型");
                    string dateText = Cell(row, "日期", "收支日期");
                    string category = Cell(row, "分类", "收付款用途", "用途");
                    string amountText = Cell(row, "金额", "收支金额");
                    if (type != "收入" && type != "支出") throw new Exception("收支类型必须是收入或支出");
                    DateTime date;
                    if (string.IsNullOrWhiteSpace(dateText)) throw new Exception("日期不能为空");
                    if (!TryParseFinanceDate(dateText, out date)) throw new Exception("日期格式不正确");
                    if (string.IsNullOrWhiteSpace(category)) throw new Exception("分类不能为空");
                    decimal amount;
                    if (string.IsNullOrWhiteSpace(amountText)) throw new Exception("金额不能为空");
                    if (!TryParseDecimalField(amountText.Replace("¥", "").Replace("￥", "").Replace(",", ""), out amount)) throw new Exception("金额必须为数字");
                    if (amount <= 0) throw new Exception("金额必须大于 0");
                    string account = Cell(row, "账户类型", "账户");
                    if (string.IsNullOrWhiteSpace(account)) account = "公户";
                    var item = new FinanceTransaction {
                        Id = Guid.NewGuid().ToString("N"), Date = date.ToString("yyyy-MM-dd"), AccountType = account,
                        Receipt = type == "收入" ? amount : 0, Payment = type == "支出" ? amount : 0,
                        Purpose = category, Counterparty = Cell(row, "对方单位/客户/供应商", "对方单位", "客户", "供应商", "对方账户主体"),
                        Note = Cell(row, "摘要/备注", "摘要", "备注"), PaymentMethod = Cell(row, "付款方式/收款方式", "付款方式", "收款方式", "收付款方式"),
                        UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), UpdatedBy = Cell(row, "经办人", "操作人")
                    };
                    if (string.IsNullOrWhiteSpace(item.UpdatedBy)) item.UpdatedBy = user.DisplayName;
                    ValidateFinance(item);
                    list.Add(item); imported++;
                }
                catch (Exception ex) { errors.Add("第" + rowNo + "行：" + ex.Message); }
            }
            if (imported > 0) SaveFinance(list);
            Audit(user, "导入财务收支", "成功" + imported + "条，失败" + errors.Count + "条");
            WriteJson(ctx, new { imported = imported, failed = errors.Count, errors = errors.ToArray() });
        }

        static void ValidateFinance(FinanceTransaction item)
        {
            if (item == null) throw new Exception("收支记录不能为空");
            DateTime date;
            if (string.IsNullOrWhiteSpace(item.Date) || !DateTime.TryParse(item.Date, out date)) throw new Exception("请选择正确的日期");
            string[] accounts = { "公户", "公司私户", "个人私户" };
            if (!accounts.Contains(item.AccountType)) throw new Exception("请选择正确的账户类型");
            if (item.Receipt < 0 || item.Payment < 0) throw new Exception("收付款金额不能为负数");
            if ((item.Receipt > 0 && item.Payment > 0) || (item.Receipt == 0 && item.Payment == 0)) throw new Exception("每笔记录只能填写收款或付款其中一项");
            item.Date = date.ToString("yyyy-MM-dd");
            item.PaymentMethod = (item.PaymentMethod ?? "").Trim(); item.Purpose = (item.Purpose ?? "").Trim(); item.Counterparty = (item.Counterparty ?? "").Trim(); item.Note = (item.Note ?? "").Trim();
        }

        static void Validate(Supplier item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Company)) throw new Exception("供应商公司名不能为空");
            item.Company = item.Company.Trim();
            if (item.Company.Length > 100) throw new Exception("供应商公司名过长");
            item.Contact=(item.Contact??"").Trim(); item.Phone=(item.Phone??"").Trim(); item.Goods=(item.Goods??"").Trim(); item.Address=(item.Address??"").Trim(); item.Bank=(item.Bank??"").Trim(); item.Account=(item.Account??"").Trim(); item.BankNo=(item.BankNo??"").Trim();
        }

        static void ValidateCustomer(Customer item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Company)) throw new Exception("客户公司名不能为空");
            item.Company = item.Company.Trim();
            if (item.Company.Length > 100) throw new Exception("客户公司名过长");
            item.Contact=(item.Contact??"").Trim(); item.Phone=(item.Phone??"").Trim(); item.Bank=(item.Bank??"").Trim(); item.Account=(item.Account??"").Trim(); item.BankNo=(item.BankNo??"").Trim(); item.Address=(item.Address??"").Trim();
        }

        static void ValidateMaterial(Material item)
        {
            if(item==null||string.IsNullOrWhiteSpace(item.NameSpec))throw new Exception("物料名称/规格不能为空");
            if(string.IsNullOrWhiteSpace(item.Supplier))throw new Exception("请选择供应商");
            item.Supplier=item.Supplier.Trim();item.NameSpec=item.NameSpec.Trim();item.QuantityUnit=(item.QuantityUnit??"").Trim();item.Note=(item.Note??"").Trim();
            if(!LoadSuppliers().Any(x=>string.Equals(x.Company,item.Supplier,StringComparison.OrdinalIgnoreCase)))throw new Exception("所选供应商不在供应商管理中，请先建立供应商档案");
        }

        static List<Dictionary<string,string>> ReadImportRows(HttpListenerContext ctx)
        {
            return ReadImportRowsFromRequest(Json.Deserialize<ImportRequest>(ReadBody(ctx.Request)));
        }

        static List<Dictionary<string,string>> ReadImportRowsFromRequest(ImportRequest req)
        {
            if(req==null||string.IsNullOrWhiteSpace(req.Data))throw new Exception("请选择要导入的 Excel 文件");
            if(!string.IsNullOrEmpty(req.FileName)&&!req.FileName.EndsWith(".xlsx",StringComparison.OrdinalIgnoreCase))throw new Exception("仅支持 .xlsx 格式文件");
            string encoded=req.Data;int comma=encoded.IndexOf(',');if(comma>=0)encoded=encoded.Substring(comma+1);
            byte[] bytes;try{bytes=Convert.FromBase64String(encoded);}catch{throw new Exception("Excel 文件内容无效");}
            if(bytes.Length>25*1024*1024)throw new Exception("Excel 文件不能超过 25MB");
            var table=new List<List<string>>();
            using(var ms=new MemoryStream(bytes))using(var zip=new ZipArchive(ms,ZipArchiveMode.Read))
            {
                var shared=new List<string>();var sharedEntry=zip.GetEntry("xl/sharedStrings.xml");
                if(sharedEntry!=null)using(var s=sharedEntry.Open()){var doc=XDocument.Load(s);XNamespace ns=doc.Root.Name.Namespace;foreach(var si in doc.Descendants(ns+"si"))shared.Add(string.Concat(si.Descendants(ns+"t").Select(x=>x.Value)));}
                var sheetEntry=zip.GetEntry("xl/worksheets/sheet1.xml");if(sheetEntry==null)throw new Exception("Excel 中没有可读取的工作表");
                using(var s=sheetEntry.Open())
                {
                    var doc=XDocument.Load(s);XNamespace ns=doc.Root.Name.Namespace;
                    foreach(var row in doc.Descendants(ns+"row"))
                    {
                        var values=new Dictionary<int,string>();int max=-1;
                        foreach(var c in row.Elements(ns+"c"))
                        {
                            string reference=(string)c.Attribute("r")??"A1";int col=0;foreach(char ch in reference){if(ch<'A'||ch>'Z')break;col=col*26+(ch-'A'+1);}col--;
                            string type=(string)c.Attribute("t")??"",value="";
                            if(type=="inlineStr")value=string.Concat(c.Descendants(ns+"t").Select(x=>x.Value));else{var v=c.Element(ns+"v");if(v!=null)value=v.Value;if(type=="s"){int index;if(int.TryParse(value,out index)&&index>=0&&index<shared.Count)value=shared[index];}}
                            values[col]=value;max=Math.Max(max,col);
                        }
                        if(max>=0){var cells=new List<string>();for(int i=0;i<=max;i++){string value;cells.Add(values.TryGetValue(i,out value)?value.Trim():"");}table.Add(cells);}
                    }
                }
            }
            if(table.Count<1)throw new Exception("Excel 表格没有表头");
            var headers=table[0];var result=new List<Dictionary<string,string>>();
            foreach(var cells in table.Skip(1)){var row=new Dictionary<string,string>();for(int i=0;i<headers.Count;i++)if(!string.IsNullOrWhiteSpace(headers[i]))row[headers[i]]=i<cells.Count?cells[i]:"";result.Add(row);}
            return result;
        }

        static string Cell(Dictionary<string,string> row, params string[] names){foreach(var name in names){string value;if(row.TryGetValue(name,out value))return(value??"").Trim();}return"";}
        static decimal Money(string value){decimal n;value=(value??"").Replace(",","").Replace("￥","").Replace("¥","").Trim();return decimal.TryParse(value,out n)?n:0;}
        static bool Placeholder(string value){return string.IsNullOrWhiteSpace(value)||value.Contains("必填选项")||value.Contains("要求自动生成");}

        static void ImportSuppliers(HttpListenerContext ctx,UserSession user)
        {
            var rows=ReadImportRows(ctx);var list=LoadSuppliers();int imported=0,skipped=0;var errors=new List<string>();int rowNo=1;
            foreach(var row in rows){rowNo++;string company=Cell(row,"供应商名称","供应商公司名");if(Placeholder(company)){skipped++;continue;}if(list.Any(x=>string.Equals(x.Company,company,StringComparison.OrdinalIgnoreCase))){skipped++;errors.Add("第"+rowNo+"行：供应商已存在");continue;}var item=new Supplier{Id=Guid.NewGuid().ToString("N"),Code=NextCode(SupplierSequenceFile,"GY",list.Select(x=>x.Code)),Company=company,Contact=Cell(row,"联系人"),Phone=Cell(row,"联系电话"),Goods=Cell(row,"供应商品"),Address=Cell(row,"单位地址"),Bank=Cell(row,"开户行"),Account=Cell(row,"银行账号"),BankNo=Cell(row,"开户行行号"),Payable=Money(Cell(row,"当前应付款")),Status=Cell(row,"状态"),UpdatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),UpdatedBy=user.DisplayName};if(string.IsNullOrEmpty(item.Status))item.Status="启用";list.Insert(0,item);imported++;}
            if(imported>0)SaveSuppliers(list);Audit(user,"导入供应商","成功"+imported+"条，跳过"+skipped+"条");WriteJson(ctx,new{imported=imported,skipped=skipped,errors=errors.Take(8).ToArray()});
        }

        static void ImportCustomers(HttpListenerContext ctx,UserSession user)
        {
            var rows=ReadImportRows(ctx);var list=LoadCustomers();int imported=0,skipped=0;var errors=new List<string>();int rowNo=1;
            foreach(var row in rows){rowNo++;string company=Cell(row,"客户名称","公司名");if(Placeholder(company)){skipped++;continue;}if(list.Any(x=>string.Equals(x.Company,company,StringComparison.OrdinalIgnoreCase))){skipped++;errors.Add("第"+rowNo+"行：客户已存在");continue;}var item=new Customer{Id=Guid.NewGuid().ToString("N"),Code=NextCode(CustomerSequenceFile,"KH",list.Select(x=>x.Code)),Company=company,Contact=Cell(row,"联系人"),Phone=Cell(row,"联系电话"),Bank=Cell(row,"开户行"),Account=Cell(row,"银行账号"),BankNo=Cell(row,"开户行行号"),Address=Cell(row,"地址"),Receivable=Money(Cell(row,"实时当前应收款")),Status=Cell(row,"状态"),UpdatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),UpdatedBy=user.DisplayName};if(string.IsNullOrEmpty(item.Status))item.Status="启用";list.Insert(0,item);imported++;}
            if(imported>0)SaveCustomers(list);Audit(user,"导入客户","成功"+imported+"条，跳过"+skipped+"条");WriteJson(ctx,new{imported=imported,skipped=skipped,errors=errors.Take(8).ToArray()});
        }

        static void ImportMaterials(HttpListenerContext ctx,UserSession user)
        {
            var rows=ReadImportRows(ctx);var list=LoadMaterials();var suppliers=LoadSuppliers();int imported=0,skipped=0;var errors=new List<string>();int rowNo=1;
            foreach(var row in rows){rowNo++;string supplier=Cell(row,"供应商"),name=Cell(row,"物料名称/规格");if(Placeholder(name)){skipped++;continue;}if(!suppliers.Any(x=>string.Equals(x.Company,supplier,StringComparison.OrdinalIgnoreCase))){skipped++;errors.Add("第"+rowNo+"行：供应商未建档");continue;}if(list.Any(x=>string.Equals(x.Supplier,supplier,StringComparison.OrdinalIgnoreCase)&&string.Equals(x.NameSpec,name,StringComparison.OrdinalIgnoreCase))){skipped++;errors.Add("第"+rowNo+"行：物料已存在");continue;}var item=new Material{Id=Guid.NewGuid().ToString("N"),Code=NextCode(MaterialSequenceFile,"WL",list.Select(x=>x.Code)),Supplier=supplier,NameSpec=name,QuantityUnit=Cell(row,"数量/单位"),TaxPrice=Money(Cell(row,"含税价")),NoTaxPrice=Money(Cell(row,"不含税价")),PriceType=NormalizePriceType(Cell(row,"价格类型")),Note=Cell(row,"备注"),Status=Cell(row,"状态"),UpdatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),UpdatedBy=user.DisplayName};if(string.IsNullOrEmpty(item.Status))item.Status="启用";list.Insert(0,item);imported++;}
            if(imported>0)SaveMaterials(list);Audit(user,"导入物料","成功"+imported+"条，跳过"+skipped+"条");WriteJson(ctx,new{imported=imported,skipped=skipped,errors=errors.Take(8).ToArray()});
        }

        static string ExportPriceTypeLabel(string priceType) { return NormalizePriceType(priceType) == "含税" ? "含税价" : "不含税价"; }

        static bool TryNormalizeImportPriceType(string input, out string normalized)
        {
            var s = (input ?? "").Trim();
            if (s == "含税价" || s == "含税") { normalized = "含税"; return true; }
            if (s == "不含税价" || s == "不含税") { normalized = "不含税"; return true; }
            normalized = "";
            return false;
        }

        static bool TryParseDecimalField(string value, out decimal result)
        {
            value = (value ?? "").Replace(",", "").Replace("￥", "").Replace("¥", "").Trim();
            return decimal.TryParse(value, out result);
        }

        static string BomConflictKey(string code, string version) { return (code ?? "").Trim() + "|" + (version ?? "").Trim(); }

        static string ModelCostConflictKey(string modelCode, string bomCode, string bomVersion)
        {
            return (modelCode ?? "").Trim() + "|" + (bomCode ?? "").Trim() + "|" + (bomVersion ?? "").Trim();
        }

        static void BumpSequenceIfNeeded(string sequenceFile, string prefix, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;
            int value;
            if (!int.TryParse(code.Substring(prefix.Length), out value)) return;
            lock (DataLock)
            {
                int sequence;
                if (!int.TryParse(File.ReadAllText(sequenceFile, Encoding.UTF8), out sequence)) sequence = 0;
                if (value > sequence) File.WriteAllText(sequenceFile, value.ToString(), new UTF8Encoding(false));
            }
        }

        static string DecodeCsvImportData(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) throw new Exception("请选择要导入的 CSV 文件");
            string encoded = data.Trim();
            if (encoded.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int comma = encoded.IndexOf(',');
                if (comma >= 0) encoded = encoded.Substring(comma + 1);
                try { return Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
                catch { throw new Exception("CSV 文件内容无效"); }
            }
            return encoded;
        }

        static List<string> ParseCsvLine(string line)
        {
            var cells = new List<string>();
            if (line == null) return cells;
            bool inQuotes = false;
            var current = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else current.Append(ch);
                }
                else
                {
                    if (ch == '"') inQuotes = true;
                    else if (ch == ',') { cells.Add(current.ToString()); current.Clear(); }
                    else current.Append(ch);
                }
            }
            cells.Add(current.ToString());
            return cells;
        }

        static List<Dictionary<string, string>> ParseCsvText(string csvText)
        {
            var table = new List<List<string>>();
            using (var reader = new StringReader(csvText ?? ""))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    table.Add(ParseCsvLine(line));
                }
            }
            if (table.Count < 1) throw new Exception("CSV 表格没有表头");
            var headers = table[0];
            var result = new List<Dictionary<string, string>>();
            for (int r = 1; r < table.Count; r++)
            {
                var cells = table[r];
                var row = new Dictionary<string, string>();
                for (int i = 0; i < headers.Count; i++)
                {
                    string header = (headers[i] ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(header)) continue;
                    row[header] = i < cells.Count ? (cells[i] ?? "").Trim() : "";
                }
                if (row.Values.Any(v => !string.IsNullOrWhiteSpace(v))) result.Add(row);
            }
            return result;
        }

        static List<Dictionary<string, string>> ReadCsvImportRowsFromRequest(CsvImportRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Data)) throw new Exception("请选择要导入的 CSV 文件");
            if (!string.IsNullOrEmpty(req.FileName) && !req.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                throw new Exception("仅支持 .csv 格式文件");
            string text = DecodeCsvImportData(req.Data);
            if (text.Length > 25 * 1024 * 1024) throw new Exception("CSV 文件不能超过 25MB");
            return ParseCsvText(text);
        }

        static List<Dictionary<string, string>> ReadCsvImportRows(HttpListenerContext ctx)
        {
            return ReadCsvImportRowsFromRequest(Json.Deserialize<CsvImportRequest>(ReadBody(ctx.Request)));
        }

        static void WriteCsvDownload(HttpListenerContext ctx, string filename, string csvContent)
        {
            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csvContent)).ToArray();
            ctx.Response.ContentType = "text/csv; charset=utf-8";
            ctx.Response.AddHeader("Content-Disposition", "attachment; filename=" + filename);
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        static readonly string[] BomCsvHeaders = new[] {
            "BOM编号","BOM版本","产品名称","机型编号","机型名称","总材料成本","BOM备注","BOM创建时间","BOM更新时间",
            "明细序号","物料编号","物料名称","规格","单位","用量","原始单价","价格类型","税率","不含税单价","金额","价格来源时间","明细备注"
        };

        static string BuildBomCsvRow(BomItem bom, BomDetail line, int lineNo)
        {
            var fields = new List<string>
            {
                bom.Code, bom.Version, bom.ProductName, bom.ModelCode, bom.ModelName,
                bom.TotalMaterialCost.ToString("0.00"), bom.Note, bom.CreatedAt, bom.UpdatedAt
            };
            if (line != null)
            {
                fields.Add(lineNo.ToString());
                fields.Add(line.MaterialCode);
                fields.Add(line.MaterialName);
                fields.Add(line.Spec);
                fields.Add(line.Unit);
                fields.Add(line.Quantity.ToString("0.####"));
                fields.Add(line.OriginalPrice.ToString("0.####"));
                fields.Add(ExportPriceTypeLabel(line.PriceType));
                fields.Add(line.TaxRate.ToString("0.##"));
                fields.Add(line.NoTaxPrice.ToString("0.####"));
                fields.Add(line.Amount.ToString("0.00"));
                fields.Add(line.PriceSourceTime);
                fields.Add(line.Note);
            }
            else
            {
                fields.Add("");
                fields.AddRange(new[] { "", "", "", "", "", "", "", "", "", "", "", "" });
            }
            return string.Join(",", fields.Select(Csv));
        }

        static void ExportBomCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", BomCsvHeaders.Select(Csv)));
            foreach (var bom in LoadBom())
            {
                var items = bom.Items ?? new List<BomDetail>();
                if (items.Count == 0) sb.AppendLine(BuildBomCsvRow(bom, null, 0));
                else for (int i = 0; i < items.Count; i++) sb.AppendLine(BuildBomCsvRow(bom, items[i], i + 1));
            }
            WriteCsvDownload(ctx, "BOM表_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv", sb.ToString());
        }

        static void ExportBomTemplateCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", BomCsvHeaders.Select(Csv)));
            sb.AppendLine(string.Join(",", new[] {
                "BOM-DEMO","V1","示例产品","MODEL-DEMO","示例机型","","示例备注","","",
                "1","WL-DEMO","示例物料","规格A","个","1","10","不含税价","10","10","10","","明细备注"
            }.Select(Csv)));
            WriteCsvDownload(ctx, "BOM导入模板.csv", sb.ToString());
        }

        static BomItem BuildBomFromImportGroup(List<Dictionary<string, string>> groupRows, decimal defaultTaxRate, List<Material> materials, List<string> warnings, List<string> errors, ref int failedRows, int firstRowNo)
        {
            var first = groupRows[0];
            string code = Cell(first, "BOM编号");
            string version = Cell(first, "BOM版本");
            if (string.IsNullOrWhiteSpace(code)) { errors.Add("第" + firstRowNo + "行：BOM编号不能为空"); failedRows++; return null; }
            if (string.IsNullOrWhiteSpace(version)) { errors.Add("第" + firstRowNo + "行：BOM版本不能为空"); failedRows++; return null; }
            var item = new BomItem
            {
                Code = code.Trim(),
                Version = version.Trim(),
                ProductName = Cell(first, "产品名称"),
                ModelCode = Cell(first, "机型编号"),
                ModelName = Cell(first, "机型名称"),
                Note = Cell(first, "BOM备注"),
                Status = "启用",
                Items = new List<BomDetail>()
            };
            if (string.IsNullOrWhiteSpace(item.ModelCode)) item.ModelCode = "-";
            if (string.IsNullOrWhiteSpace(item.ModelName)) item.ModelName = "-";
            if (string.IsNullOrWhiteSpace(item.ProductName)) item.ProductName = "-";
            int rowNo = firstRowNo;
            foreach (var row in groupRows)
            {
                string materialCode = Cell(row, "物料编号");
                if (string.IsNullOrWhiteSpace(materialCode))
                {
                    if (row != first)
                    {
                        string mc = Cell(row, "机型编号"), mn = Cell(row, "机型名称"), pn = Cell(row, "产品名称");
                        if (!string.IsNullOrWhiteSpace(mc)) item.ModelCode = mc;
                        if (!string.IsNullOrWhiteSpace(mn)) item.ModelName = mn;
                        if (!string.IsNullOrWhiteSpace(pn)) item.ProductName = pn;
                        if (!string.IsNullOrWhiteSpace(Cell(row, "BOM备注"))) item.Note = Cell(row, "BOM备注");
                    }
                    rowNo++;
                    continue;
                }
                string qtyText = Cell(row, "用量");
                decimal quantity;
                if (!TryParseDecimalField(qtyText, out quantity) || quantity <= 0)
                {
                    errors.Add("第" + rowNo + "行：用量必须是大于 0 的数字");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                string origText = Cell(row, "原始单价");
                string noTaxText = Cell(row, "不含税单价");
                string amountText = Cell(row, "金额");
                string taxText = Cell(row, "税率");
                decimal originalPrice, noTaxPrice, amount, taxRate;
                if (!string.IsNullOrWhiteSpace(origText) && !TryParseDecimalField(origText, out originalPrice))
                {
                    errors.Add("第" + rowNo + "行：原始单价必须是数字");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(noTaxText) && !TryParseDecimalField(noTaxText, out noTaxPrice))
                {
                    errors.Add("第" + rowNo + "行：不含税单价必须是数字");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(amountText) && !TryParseDecimalField(amountText, out amount))
                {
                    errors.Add("第" + rowNo + "行：金额必须是数字");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(taxText) && !TryParseDecimalField(taxText, out taxRate))
                {
                    errors.Add("第" + rowNo + "行：税率必须是数字");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                string priceTypeRaw = Cell(row, "价格类型");
                string priceType;
                if (string.IsNullOrWhiteSpace(priceTypeRaw)) priceType = "不含税";
                else if (!TryNormalizeImportPriceType(priceTypeRaw, out priceType))
                {
                    errors.Add("第" + rowNo + "行：价格类型只能是含税价或不含税价");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                taxRate = string.IsNullOrWhiteSpace(taxText) ? defaultTaxRate : Money(taxText);
                if (taxRate < 0) taxRate = 0;
                originalPrice = string.IsNullOrWhiteSpace(origText) ? 0 : Money(origText);
                noTaxPrice = string.IsNullOrWhiteSpace(noTaxText) ? 0 : Money(noTaxText);
                if (string.IsNullOrWhiteSpace(noTaxText) && !string.IsNullOrWhiteSpace(origText))
                    noTaxPrice = CalcNoTaxUnitPrice(originalPrice, priceType, taxRate);
                else if (string.IsNullOrWhiteSpace(origText) && !string.IsNullOrWhiteSpace(noTaxText))
                    originalPrice = noTaxPrice;
                amount = string.IsNullOrWhiteSpace(amountText) ? CalcLineAmount(quantity, noTaxPrice) : Money(amountText);
                var line = new BomDetail
                {
                    MaterialCode = materialCode.Trim(),
                    MaterialName = Cell(row, "物料名称"),
                    Spec = string.IsNullOrWhiteSpace(Cell(row, "规格")) ? "-" : Cell(row, "规格"),
                    Unit = string.IsNullOrWhiteSpace(Cell(row, "单位")) ? "-" : Cell(row, "单位"),
                    Quantity = quantity,
                    OriginalPrice = originalPrice,
                    PriceType = priceType,
                    TaxRate = taxRate,
                    NoTaxPrice = noTaxPrice,
                    Amount = amount,
                    PriceSourceTime = Cell(row, "价格来源时间"),
                    Note = Cell(row, "明细备注")
                };
                if (string.IsNullOrWhiteSpace(line.PriceSourceTime))
                    line.PriceSourceTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var mat = materials.FirstOrDefault(m => string.Equals(m.Code, line.MaterialCode, StringComparison.OrdinalIgnoreCase));
                if (mat != null)
                {
                    line.MaterialId = mat.Id;
                    if (string.IsNullOrWhiteSpace(line.MaterialName)) line.MaterialName = mat.NameSpec;
                    if (line.Unit == "-") line.Unit = string.IsNullOrWhiteSpace(mat.QuantityUnit) ? "-" : mat.QuantityUnit;
                }
                else
                {
                    line.MaterialId = "";
                    warnings.Add("第" + rowNo + "行：物料编号 " + line.MaterialCode + " 不存在，仅作为 BOM 快照导入");
                }
                item.Items.Add(line);
                rowNo++;
            }
            return item;
        }

        static void ImportBomCsv(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<CsvImportRequest>(ReadBody(ctx.Request));
            if (req == null || string.IsNullOrWhiteSpace(req.Data)) { WriteJson(ctx, new { error = "请选择要导入的 CSV 文件" }, 400); return; }
            var rows = ReadCsvImportRowsFromRequest(req);
            var list = LoadBom();
            var materials = LoadMaterials();
            var taxRate = LoadSystemSettings().TaxRate;
            var groups = new Dictionary<string, List<Dictionary<string, string>>>();
            var groupFirstRow = new Dictionary<string, int>();
            int rowNo = 1;
            var errors = new List<string>();
            var warnings = new List<string>();
            int failedRows = 0;
            foreach (var row in rows)
            {
                rowNo++;
                string code = Cell(row, "BOM编号"), version = Cell(row, "BOM版本");
                if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(Cell(row, "物料编号")))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：BOM编号和BOM版本不能为空");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(version))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：BOM编号和BOM版本不能为空");
                    continue;
                }
                string key = BomConflictKey(code, version);
                if (!groups.ContainsKey(key)) { groups[key] = new List<Dictionary<string, string>>(); groupFirstRow[key] = rowNo; }
                groups[key].Add(row);
            }
            var conflicts = groups.Keys.Where(k => list.Any(x => string.Equals(x.Code, k.Split('|')[0], StringComparison.OrdinalIgnoreCase) && string.Equals(x.Version ?? "", k.Split('|')[1], StringComparison.OrdinalIgnoreCase))).ToList();
            var actions = req.ConflictActions ?? new Dictionary<string, string>();
            if (conflicts.Count > 0)
            {
                var unresolved = conflicts.Where(k => !actions.ContainsKey(k) || string.IsNullOrWhiteSpace(actions[k])).ToList();
                if (unresolved.Count > 0)
                {
                    WriteJson(ctx, new TableImportResult { NeedsConflictDecision = true, Conflicts = conflicts.ToArray() });
                    return;
                }
            }
            int added = 0, updated = 0, skipped = 0;
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            foreach (var kv in groups)
            {
                string key = kv.Key;
                var existing = list.FirstOrDefault(x => string.Equals(x.Code, key.Split('|')[0], StringComparison.OrdinalIgnoreCase) && string.Equals(x.Version ?? "", key.Split('|')[1], StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    string action = (actions.ContainsKey(key) ? actions[key] : "").Trim().ToLowerInvariant();
                    if (action == "skip" || action == "跳过") { skipped++; continue; }
                    if (action != "overwrite" && action != "覆盖") { skipped++; continue; }
                }
                var item = BuildBomFromImportGroup(kv.Value, taxRate, materials, warnings, errors, ref failedRows, groupFirstRow[key]);
                if (item == null) continue;
                RecalcBomLines(item, taxRate);
                if (existing != null)
                {
                    item.Id = existing.Id;
                    item.Code = existing.Code;
                    item.CreatedAt = existing.CreatedAt;
                    item.UpdatedAt = now;
                    list[list.IndexOf(existing)] = item;
                    updated++;
                }
                else
                {
                    item.Id = Guid.NewGuid().ToString("N");
                    item.CreatedAt = string.IsNullOrWhiteSpace(Cell(kv.Value[0], "BOM创建时间")) ? now : Cell(kv.Value[0], "BOM创建时间");
                    item.UpdatedAt = string.IsNullOrWhiteSpace(Cell(kv.Value[0], "BOM更新时间")) ? now : Cell(kv.Value[0], "BOM更新时间");
                    BumpSequenceIfNeeded(BomSequenceFile, "BOM", item.Code);
                    list.Insert(0, item);
                    added++;
                }
            }
            if (added > 0 || updated > 0) SaveBom(list);
            Audit(user, "导入BOM", "新增" + added + "，更新" + updated + "，跳过" + skipped + "，失败" + failedRows + "行");
            WriteJson(ctx, new TableImportResult { Added = added, Updated = updated, Skipped = skipped, FailedRows = failedRows, Errors = errors.ToArray(), Warnings = warnings.ToArray() });
        }

        static readonly string[] ModelCostCsvHeaders = new[] {
            "机型编号","机型名称","产品名称","BOM编号","BOM版本","材料成本","总成本","备注","状态","创建时间","更新时间"
        };

        static readonly string[] ModelCostIgnoredHeaders = new[] { "人工成本", "制造费用", "其他费用", "运费" };

        static void ExportModelCostsCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", ModelCostCsvHeaders.Select(Csv)));
            foreach (var x in LoadModelCosts())
            {
                sb.AppendLine(string.Join(",", new[] {
                    x.ModelCode, x.ModelName, x.ProductName, x.BomCode, x.BomVersion,
                    x.MaterialCost.ToString("0.00"), x.TotalCost.ToString("0.00"),
                    x.Note, string.IsNullOrWhiteSpace(x.Status) ? "启用" : x.Status, x.CreatedAt, x.UpdatedAt
                }.Select(Csv)));
            }
            WriteCsvDownload(ctx, "机型成本_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv", sb.ToString());
        }

        static void ExportModelCostTemplateCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", ModelCostCsvHeaders.Select(Csv)));
            sb.AppendLine(string.Join(",", new[] {
                "MODEL-DEMO","示例机型","示例产品","BOM-DEMO","V1","100","100","示例备注","启用","",""
            }.Select(Csv)));
            WriteCsvDownload(ctx, "机型成本导入模板.csv", sb.ToString());
        }

        static void ImportModelCostsCsv(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<CsvImportRequest>(ReadBody(ctx.Request));
            if (req == null || string.IsNullOrWhiteSpace(req.Data)) { WriteJson(ctx, new { error = "请选择要导入的 CSV 文件" }, 400); return; }
            var rows = ReadCsvImportRowsFromRequest(req);
            var list = LoadModelCosts();
            var bomList = LoadBom();
            var errors = new List<string>();
            var warnings = new List<string>();
            int failedRows = 0, added = 0, updated = 0, skipped = 0;
            if (rows.Count > 0)
            {
                var allHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows) foreach (var k in row.Keys) allHeaders.Add(k);
                foreach (var h in ModelCostIgnoredHeaders)
                    if (allHeaders.Contains(h)) warnings.Add("导入文件包含已忽略字段：" + h);
            }
            var conflictKeys = new HashSet<string>();
            int previewRow = 1;
            foreach (var row in rows)
            {
                previewRow++;
                string modelCode = Cell(row, "机型编号"), bomCode = Cell(row, "BOM编号"), bomVersion = Cell(row, "BOM版本");
                if (string.IsNullOrWhiteSpace(modelCode) || string.IsNullOrWhiteSpace(bomCode) || string.IsNullOrWhiteSpace(bomVersion)) continue;
                string key = ModelCostConflictKey(modelCode, bomCode, bomVersion);
                if (list.Any(x => string.Equals(x.ModelCode, modelCode.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(x.BomCode, bomCode.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(x.BomVersion ?? "", bomVersion.Trim(), StringComparison.OrdinalIgnoreCase)))
                    conflictKeys.Add(key);
            }
            var actions = req.ConflictActions ?? new Dictionary<string, string>();
            if (conflictKeys.Count > 0)
            {
                var unresolved = conflictKeys.Where(k => !actions.ContainsKey(k) || string.IsNullOrWhiteSpace(actions[k])).ToList();
                if (unresolved.Count > 0)
                {
                    WriteJson(ctx, new TableImportResult { NeedsConflictDecision = true, Conflicts = conflictKeys.ToArray() });
                    return;
                }
            }
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            int rowNo = 1;
            foreach (var row in rows)
            {
                rowNo++;
                string modelCode = Cell(row, "机型编号");
                string modelName = Cell(row, "机型名称");
                string bomCode = Cell(row, "BOM编号");
                string bomVersion = Cell(row, "BOM版本");
                if (string.IsNullOrWhiteSpace(modelCode))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：机型编号不能为空");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：机型名称不能为空");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(bomCode))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：BOM编号不能为空");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(bomVersion))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：BOM版本不能为空");
                    continue;
                }
                string matText = Cell(row, "材料成本");
                decimal materialCost;
                if (string.IsNullOrWhiteSpace(matText) || !TryParseDecimalField(matText, out materialCost))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：材料成本必须是数字");
                    continue;
                }
                string totalText = Cell(row, "总成本");
                decimal totalCost = materialCost;
                if (!string.IsNullOrWhiteSpace(totalText))
                {
                    decimal parsedTotal;
                    if (!TryParseDecimalField(totalText, out parsedTotal))
                    {
                        failedRows++;
                        errors.Add("第" + rowNo + "行：总成本必须是数字");
                        continue;
                    }
                    if (parsedTotal != materialCost)
                        warnings.Add("第" + rowNo + "行：总成本已自动修正为材料成本");
                    totalCost = materialCost;
                }
                var bom = bomList.FirstOrDefault(b => string.Equals(b.Code, bomCode.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(b.Version ?? "", bomVersion.Trim(), StringComparison.OrdinalIgnoreCase));
                if (bom == null)
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：BOM " + bomCode + " " + bomVersion + " 不存在");
                    continue;
                }
                string key = ModelCostConflictKey(modelCode, bomCode, bomVersion);
                var existing = list.FirstOrDefault(x => string.Equals(x.ModelCode, modelCode.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(x.BomCode, bomCode.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(x.BomVersion ?? "", bomVersion.Trim(), StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    string action = (actions.ContainsKey(key) ? actions[key] : "").Trim().ToLowerInvariant();
                    if (action == "skip" || action == "跳过") { skipped++; continue; }
                    if (action != "overwrite" && action != "覆盖") { skipped++; continue; }
                }
                string status = Cell(row, "状态");
                if (string.IsNullOrWhiteSpace(status)) status = "启用";
                else status = status.Trim();
                if (status != "启用" && status != "停用")
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：状态只能是启用或停用");
                    continue;
                }
                var item = new ModelCost
                {
                    ModelCode = modelCode.Trim(),
                    ModelName = modelName.Trim(),
                    ProductName = string.IsNullOrWhiteSpace(Cell(row, "产品名称")) ? bom.ProductName : Cell(row, "产品名称"),
                    BomId = bom.Id,
                    BomCode = bom.Code,
                    BomVersion = bom.Version,
                    MaterialCost = Math.Round(materialCost, 2),
                    TotalCost = Math.Round(totalCost, 2),
                    Note = Cell(row, "备注"),
                    Status = status
                };
                if (existing != null)
                {
                    item.Id = existing.Id;
                    item.CreatedAt = existing.CreatedAt;
                    item.UpdatedAt = string.IsNullOrWhiteSpace(Cell(row, "更新时间")) ? now : Cell(row, "更新时间");
                    list[list.IndexOf(existing)] = item;
                    updated++;
                }
                else
                {
                    item.Id = Guid.NewGuid().ToString("N");
                    item.CreatedAt = string.IsNullOrWhiteSpace(Cell(row, "创建时间")) ? now : Cell(row, "创建时间");
                    item.UpdatedAt = string.IsNullOrWhiteSpace(Cell(row, "更新时间")) ? now : Cell(row, "更新时间");
                    list.Insert(0, item);
                    added++;
                }
            }
            if (added > 0 || updated > 0) SaveModelCosts(list);
            Audit(user, "导入机型成本", "新增" + added + "，更新" + updated + "，跳过" + skipped + "，失败" + failedRows + "行");
            WriteJson(ctx, new TableImportResult { Added = added, Updated = updated, Skipped = skipped, FailedRows = failedRows, Errors = errors.ToArray(), Warnings = warnings.ToArray() });
        }

        static void ExportCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder(); sb.AppendLine("供应商编号,供应商名称,联系人,联系电话,供应商品,单位地址,开户行,银行账号,开户行行号,当前应付款,状态,最后更新,操作人");
            foreach (var x in LoadSuppliers()) sb.AppendLine(string.Join(",", new[] { x.Code,x.Company,x.Contact,x.Phone,x.Goods,x.Address,x.Bank,x.Account,x.BankNo,x.Payable.ToString("0.00"),x.Status,x.UpdatedAt,x.UpdatedBy }.Select(Csv)));
            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            ctx.Response.ContentType = "text/csv; charset=utf-8"; ctx.Response.AddHeader("Content-Disposition", "attachment; filename=suppliers_" + DateTime.Now.ToString("yyyyMMdd") + ".csv"); ctx.Response.ContentLength64 = bytes.Length; ctx.Response.OutputStream.Write(bytes,0,bytes.Length); ctx.Response.Close();
        }

        static void ExportCustomersCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder(); sb.AppendLine("客户编号,客户名称,联系人,联系电话,开户行,银行账号,开户行行号,地址,实时当前应收款,状态,最后更新,操作人");
            foreach (var x in LoadCustomers()) sb.AppendLine(string.Join(",", new[] { x.Code,x.Company,x.Contact,x.Phone,x.Bank,x.Account,x.BankNo,x.Address,x.Receivable.ToString("0.00"),x.Status,x.UpdatedAt,x.UpdatedBy }.Select(Csv)));
            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            ctx.Response.ContentType = "text/csv; charset=utf-8"; ctx.Response.AddHeader("Content-Disposition", "attachment; filename=customers_" + DateTime.Now.ToString("yyyyMMdd") + ".csv"); ctx.Response.ContentLength64 = bytes.Length; ctx.Response.OutputStream.Write(bytes,0,bytes.Length); ctx.Response.Close();
        }

        static void ExportMaterialsCsv(HttpListenerContext ctx)
        {
            var sb=new StringBuilder();sb.AppendLine("物料编号,供应商,物料名称/规格,数量/单位,含税价,不含税价,价格类型,备注,状态,最后更新,操作人");
            foreach(var x in LoadMaterials())sb.AppendLine(string.Join(",",new[]{x.Code,x.Supplier,x.NameSpec,x.QuantityUnit,x.TaxPrice.ToString("0.00"),x.NoTaxPrice.ToString("0.00"),NormalizePriceType(x.PriceType),x.Note,x.Status,x.UpdatedAt,x.UpdatedBy}.Select(Csv)));
            byte[] bytes=Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();ctx.Response.ContentType="text/csv; charset=utf-8";ctx.Response.AddHeader("Content-Disposition","attachment; filename=materials_"+DateTime.Now.ToString("yyyyMMdd")+".csv");ctx.Response.ContentLength64=bytes.Length;ctx.Response.OutputStream.Write(bytes,0,bytes.Length);ctx.Response.Close();
        }

        static List<ContractSetting> LoadContractSettings()
        {
            lock (DataLock)
            {
                if (!File.Exists(ContractSettingsFile)) return new List<ContractSetting>();
                return Json.Deserialize<List<ContractSetting>>(File.ReadAllText(ContractSettingsFile, Encoding.UTF8)) ?? new List<ContractSetting>();
            }
        }

        static void SaveContractSettings(List<ContractSetting> items)
        {
            lock (DataLock)
            {
                File.WriteAllText(ContractSettingsFile, Json.Serialize(items), new UTF8Encoding(false));
            }
        }

        static List<ContractItem> LoadContracts()
        {
            lock (DataLock)
            {
                if (!File.Exists(ContractsFile)) return new List<ContractItem>();
                return Json.Deserialize<List<ContractItem>>(File.ReadAllText(ContractsFile, Encoding.UTF8)) ?? new List<ContractItem>();
            }
        }

        static void SaveContracts(List<ContractItem> items)
        {
            lock (DataLock)
            {
                File.WriteAllText(ContractsFile, Json.Serialize(items), new UTF8Encoding(false));
            }
        }

        static string NowTimeString() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); }

        static decimal RoundMoney(decimal v) { return Math.Round(v, 2, MidpointRounding.AwayFromZero); }

        static string ToChineseMoney(decimal amount)
        {
            if (amount == 0) return "零元整";
            string[] digits = { "零", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖" };
            string[] units = { "", "拾", "佰", "仟" };
            string[] bigUnits = { "", "万", "亿", "兆" };
            bool negative = amount < 0;
            amount = Math.Abs(amount);
            long cents = (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
            long intPart = cents / 100;
            int jiao = (int)(cents % 100 / 10);
            int fen = (int)(cents % 10);
            var sb = new StringBuilder();
            if (negative) sb.Append("负");
            if (intPart == 0) sb.Append("零");
            else
            {
                string intStr = intPart.ToString();
                int len = intStr.Length;
                for (int i = 0; i < len; i++)
                {
                    int digit = intStr[i] - '0';
                    int pos = len - i - 1;
                    int unitPos = pos % 4;
                    int bigUnitPos = pos / 4;
                    if (digit == 0)
                    {
                        if (unitPos == 0 && bigUnitPos > 0) sb.Append(bigUnits[bigUnitPos]);
                        else if (i < len - 1 && intStr[i + 1] != '0' && sb.Length > 0 && sb[sb.Length - 1] != '零') sb.Append("零");
                    }
                    else
                    {
                        sb.Append(digits[digit]).Append(units[unitPos]);
                        if (unitPos == 0 && bigUnitPos > 0) sb.Append(bigUnits[bigUnitPos]);
                    }
                }
            }
            sb.Append("元");
            if (jiao == 0 && fen == 0) sb.Append("整");
            else
            {
                if (jiao > 0) sb.Append(digits[jiao]).Append("角");
                else if (fen > 0) sb.Append("零");
                if (fen > 0) sb.Append(digits[fen]).Append("分");
            }
            return sb.ToString();
        }

        static void RecalcContractLine(ContractDetailLine line)
        {
            if (line.TaxIncluded && line.TaxRate >= 0)
                line.NoTaxUnitPrice = Math.Round(line.UnitPrice / (1 + line.TaxRate / 100m), 4, MidpointRounding.AwayFromZero);
            else
                line.NoTaxUnitPrice = line.UnitPrice;
            // 销售合同统一优先按不含税金额核算；旧合同的含税单价仍可兼容换算。
            line.TotalAmount = RoundMoney(line.Quantity * line.NoTaxUnitPrice);
        }

        static void RecalcContractAmounts(ContractItem item)
        {
            item.Items = item.Items ?? new List<ContractDetailLine>();
            decimal total = 0;
            int seq = 1;
            foreach (var line in item.Items)
            {
                line.Seq = seq++;
                RecalcContractLine(line);
                total += line.TotalAmount;
            }
            item.TotalAmount = RoundMoney(total);
            item.TotalAmountChinese = ToChineseMoney(item.TotalAmount);
            item.DepositAmount = RoundMoney(item.TotalAmount * item.DepositRatio / 100m);
            item.DepositAmountChinese = ToChineseMoney(item.DepositAmount);
            item.BalanceAmount = RoundMoney(item.TotalAmount - item.DepositAmount);
            item.BalanceAmountChinese = ToChineseMoney(item.BalanceAmount);
            if (item.InstallmentMonths > 0)
            {
                item.InstallmentAmount = RoundMoney(item.BalanceAmount / item.InstallmentMonths);
                item.InstallmentAmountChinese = ToChineseMoney(item.InstallmentAmount);
            }
            else
            {
                item.InstallmentAmount = 0;
                item.InstallmentAmountChinese = "";
            }
        }

        static void ApplyTemplateSnapshot(ContractItem item)
        {
            var settings = LoadContractSettings();
            ContractSetting tpl = null;
            if (!string.IsNullOrWhiteSpace(item.TemplateId))
                tpl = settings.FirstOrDefault(x => x.Id == item.TemplateId);
            if (tpl == null)
                tpl = settings.FirstOrDefault(x => x.Type == "合同范本" && x.IsDefault && x.Status == "启用");
            if (tpl == null)
                tpl = settings.FirstOrDefault(x => x.Type == "合同范本" && x.Status == "启用");
            if (tpl != null)
            {
                item.TemplateId = tpl.Id;
                item.TemplateCode = tpl.Code;
                item.TemplateName = tpl.Name;
                item.TemplateContent = tpl.Content;
            }
            else if (string.IsNullOrWhiteSpace(item.TemplateContent))
            {
                item.TemplateName = "冠誉公司自用销售合同默认范本";
                item.TemplateContent = GetDefaultContractTemplateHtml();
            }
        }

        static void NormalizeContract(ContractItem item)
        {
            item.Items = item.Items ?? new List<ContractDetailLine>();
            item.Accessories = item.Accessories ?? new List<ContractAccessoryLine>();
            item.ConfigItems = item.ConfigItems ?? new List<ContractConfigLine>();
            item.TechParams = item.TechParams ?? new List<ContractTechParamLine>();
            if (string.IsNullOrWhiteSpace(item.PartyBName)) item.PartyBName = "中山市冠誉数控设备有限公司";
            if (string.IsNullOrWhiteSpace(item.Status)) item.Status = "草稿";
            int aSeq = 1;
            foreach (var a in item.Accessories) a.Seq = aSeq++;
            int cSeq = 1;
            foreach (var c in item.ConfigItems) c.Seq = cSeq++;
            int tSeq = 1;
            foreach (var t in item.TechParams) t.Seq = tSeq++;
            RecalcContractAmounts(item);
            ApplyTemplateSnapshot(item);
        }

        static void ValidateContractSetting(ContractSetting item)
        {
            if (string.IsNullOrWhiteSpace(item.Type)) throw new Exception("资料类型不能为空");
            if (string.IsNullOrWhiteSpace(item.Name)) throw new Exception("资料名称不能为空");
            if (string.IsNullOrWhiteSpace(item.Status)) item.Status = "启用";
        }

        static void AddContractSetting(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<ContractSetting>(ReadBody(ctx.Request));
            ValidateContractSetting(item);
            var list = LoadContractSettings();
            string now = NowTimeString();
            item.Id = Guid.NewGuid().ToString("N");
            item.Code = NextCode(ContractSettingSequenceFile, "CS", list.Select(x => x.Code));
            item.CreatedAt = now;
            item.UpdatedAt = now;
            if (item.IsDefault)
                foreach (var x in list.Where(x => x.Type == item.Type)) x.IsDefault = false;
            list.Insert(0, item);
            SaveContractSettings(list);
            Audit(user, "新增合同资料", item.Code + " " + item.Name);
            WriteJson(ctx, item, 201);
        }

        static void UpdateContractSetting(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<ContractSetting>(ReadBody(ctx.Request));
            ValidateContractSetting(input);
            var list = LoadContractSettings();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "合同资料不存在" }, 404); return; }
            input.Id = item.Id;
            input.Code = item.Code;
            input.CreatedAt = item.CreatedAt;
            input.UpdatedAt = NowTimeString();
            if (input.IsDefault)
                foreach (var x in list.Where(x => x.Type == input.Type && x.Id != id)) x.IsDefault = false;
            list[list.IndexOf(item)] = input;
            SaveContractSettings(list);
            Audit(user, "修改合同资料", input.Code + " " + input.Name);
            WriteJson(ctx, input);
        }

        static void DeleteContractSetting(HttpListenerContext ctx, UserSession user, string id)
        {
            var list = LoadContractSettings();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "合同资料不存在" }, 404); return; }
            list.Remove(item);
            SaveContractSettings(list);
            Audit(user, "删除合同资料", item.Code + " " + item.Name);
            WriteJson(ctx, new { ok = true });
        }

        static void AddContract(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<ContractItem>(ReadBody(ctx.Request));
            if (string.IsNullOrWhiteSpace(item.Name)) throw new Exception("合同名称不能为空");
            if (string.IsNullOrWhiteSpace(item.PartyAName)) throw new Exception("甲方名称不能为空");
            if (item.Items == null || item.Items.Count == 0) throw new Exception("请至少添加一条设备明细");
            NormalizeContract(item);
            var list = LoadContracts();
            string now = NowTimeString();
            item.Id = Guid.NewGuid().ToString("N");
            item.Code = NextCode(ContractSequenceFile, "HT", list.Select(x => x.Code));
            item.CreatedAt = now;
            item.UpdatedAt = now;
            list.Insert(0, item);
            SaveContracts(list);
            Audit(user, "新增合同", item.Code + " " + item.Name);
            WriteJson(ctx, item, 201);
        }

        static void UpdateContract(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<ContractItem>(ReadBody(ctx.Request));
            if (string.IsNullOrWhiteSpace(input.Name)) throw new Exception("合同名称不能为空");
            if (string.IsNullOrWhiteSpace(input.PartyAName)) throw new Exception("甲方名称不能为空");
            if (input.Items == null || input.Items.Count == 0) throw new Exception("请至少添加一条设备明细");
            var list = LoadContracts();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "合同不存在" }, 404); return; }
            input.Id = item.Id;
            input.Code = item.Code;
            input.CreatedAt = item.CreatedAt;
            input.UpdatedAt = NowTimeString();
            if (string.IsNullOrWhiteSpace(input.TemplateContent)) input.TemplateContent = item.TemplateContent;
            NormalizeContract(input);
            list[list.IndexOf(item)] = input;
            SaveContracts(list);
            Audit(user, "修改合同", input.Code + " " + input.Name);
            WriteJson(ctx, input);
        }

        static void DeleteContract(HttpListenerContext ctx, UserSession user, string id)
        {
            var list = LoadContracts();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "合同不存在" }, 404); return; }
            list.Remove(item);
            SaveContracts(list);
            Audit(user, "删除合同", item.Code + " " + item.Name);
            WriteJson(ctx, new { ok = true });
        }

        static void VoidContract(HttpListenerContext ctx, UserSession user, string id)
        {
            var list = LoadContracts();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "合同不存在" }, 404); return; }
            item.Status = "已作废";
            item.UpdatedAt = NowTimeString();
            SaveContracts(list);
            Audit(user, "作废合同", item.Code + " " + item.Name);
            WriteJson(ctx, item);
        }

        static void PreviewContract(HttpListenerContext ctx, string id)
        {
            var item = LoadContracts().FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "合同不存在" }, 404); return; }
            // 预览始终重新按不含税口径计算，兼容历史合同中保存的含税合计。
            RecalcContractAmounts(item);
            string html = BuildContractPreviewHtml(item);
            WriteJson(ctx, new ContractPreviewResult { Html = html });
        }

        static string BuildContractPreviewHtml(ContractItem item)
        {
            string template = item.TemplateContent;
            if (string.IsNullOrWhiteSpace(template) || IsLegacyDefaultContractTemplate(item.TemplateName, template))
                template = GetDefaultContractTemplateHtml();
            var map = new Dictionary<string, string>
            {
                ["{{合同编号}}"] = item.Code ?? "",
                ["{{合同名称}}"] = item.Name ?? "",
                ["{{签订日期}}"] = item.SignDate ?? "",
                ["{{甲方名称}}"] = item.PartyAName ?? "",
                ["{{甲方联系人}}"] = item.PartyAContact ?? "",
                ["{{甲方电话}}"] = item.PartyAPhone ?? "",
                ["{{甲方地址}}"] = item.PartyAAddress ?? "",
                ["{{乙方名称}}"] = item.PartyBName ?? "",
                ["{{乙方联系人}}"] = item.PartyBContact ?? "",
                ["{{乙方电话}}"] = item.PartyBPhone ?? "",
                ["{{乙方地址}}"] = item.PartyBAddress ?? "",
                ["{{设备明细}}"] = BuildDeviceDetailTable(item.Items),
                ["{{随机配件}}"] = BuildAccessoryTable(item.Accessories),
                ["{{付款方式}}"] = (item.PaymentTerms ?? "").Replace("\n", "<br>"),
                ["{{定金金额}}"] = item.DepositAmount.ToString("0.00"),
                ["{{定金金额大写}}"] = item.DepositAmountChinese ?? "",
                ["{{余款金额}}"] = item.BalanceAmount.ToString("0.00"),
                ["{{余款金额大写}}"] = item.BalanceAmountChinese ?? "",
                ["{{分期说明}}"] = (item.InstallmentNote ?? "").Replace("\n", "<br>"),
                ["{{交货时间}}"] = item.DeliveryTime ?? "",
                ["{{交货地点}}"] = item.DeliveryPlace ?? "",
                ["{{包装方式}}"] = item.PackagingMethod ?? "",
                ["{{运输方式}}"] = item.TransportMethod ?? "",
                ["{{质量验收条款}}"] = (item.QualityAcceptanceTerms ?? "").Replace("\n", "<br>"),
                ["{{售后维修条款}}"] = (item.AfterSalesTerms ?? "").Replace("\n", "<br>"),
                ["{{不保修范围}}"] = (item.ExcludedWarranty ?? "").Replace("\n", "<br>"),
                ["{{违约责任}}"] = (item.BreachTerms ?? "").Replace("\n", "<br>"),
                ["{{合同总金额}}"] = item.TotalAmount.ToString("0.00"),
                ["{{合同总金额大写}}"] = item.TotalAmountChinese ?? "",
                ["{{是否含税}}"] = item.TaxIncluded ? "含税" : "不含税",
                ["{{税率}}"] = item.TaxRate.ToString("0.##") + "%",
                ["{{开票类型}}"] = item.InvoiceType ?? "",
                ["{{备注}}"] = (item.InternalNote ?? "").Replace("\n", "<br>"),
                ["{{开票名称}}"] = item.InvoiceTitle ?? "",
                ["{{纳税人识别号}}"] = item.TaxNumber ?? "",
                ["{{开户名称}}"] = item.AccountHolder ?? "",
                ["{{开户行}}"] = item.BankBranch ?? "",
                ["{{公司账号}}"] = item.CompanyAccount ?? "",
                ["{{私人账号}}"] = item.PersonalAccount ?? "",
                ["{{行号}}"] = item.BankRoutingNo ?? "",
                ["{{设备配置表}}"] = BuildConfigTable(item.ConfigItems),
                ["{{技术参数表}}"] = BuildTechParamTable(item.TechParams),
                ["{{甲方签字}}"] = "________________",
                ["{{乙方签字}}"] = "________________",
                ["{{甲方签约日期}}"] = item.SignDate ?? "",
                ["{{乙方签约日期}}"] = item.SignDate ?? ""
            };
            var html = template;
            foreach (var kv in map) html = html.Replace(kv.Key, kv.Value);
            if (!html.Contains("class=\"contract-document\"") && !html.Contains("class='contract-document'"))
                html = "<article class=\"contract-document\">" + html + "</article>";
            return html;
        }

        static string BuildDeviceDetailTable(List<ContractDetailLine> items)
        {
            if (items == null || items.Count == 0) return "<p>无</p>";
            var sb = new StringBuilder();
            sb.Append("<table class=\"device-detail-table\"><thead><tr><th>设备名称</th><th>型号规格</th><th>数量/单位</th><th>单价/不含税</th><th>合计金额/不含税</th><th>备注</th></tr></thead><tbody>");
            foreach (var x in items)
                sb.Append("<tr><td>").Append(HtmlEncode(x.DeviceName)).Append("</td><td>").Append(HtmlEncode(x.ModelSpec)).Append("</td><td>").Append(x.Quantity.ToString("0.##")).Append("/").Append(HtmlEncode(x.Unit)).Append("</td><td>").Append(x.NoTaxUnitPrice.ToString("0.00")).Append("</td><td>").Append(x.TotalAmount.ToString("0.00")).Append("</td><td>").Append(HtmlEncode(x.Note)).Append("</td></tr>");
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        static bool IsLegacyDefaultContractTemplate(string templateName, string content)
        {
            if (!string.Equals(templateName, "冠誉设备购销合同默认范本", StringComparison.OrdinalIgnoreCase)) return false;
            return string.IsNullOrWhiteSpace(content) || content.Contains("签订日期：{{签订日期}}。甲乙双方经友好协商");
        }

        static string BuildAccessoryTable(List<ContractAccessoryLine> items)
        {
            if (items == null || items.Count == 0) return "<p>无</p>";
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr><th>序号</th><th>配件名称</th><th>规格型号</th><th>数量</th><th>单位</th><th>备注</th></tr></thead><tbody>");
            foreach (var x in items)
                sb.Append("<tr><td>").Append(x.Seq).Append("</td><td>").Append(HtmlEncode(x.Name)).Append("</td><td>").Append(HtmlEncode(x.Spec)).Append("</td><td>").Append(HtmlEncode(x.Quantity)).Append("</td><td>").Append(HtmlEncode(x.Unit)).Append("</td><td>").Append(HtmlEncode(x.Note)).Append("</td></tr>");
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        static string BuildConfigTable(List<ContractConfigLine> items)
        {
            if (items == null || items.Count == 0) return "<p>无</p>";
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr><th>序号</th><th>分类</th><th>项目</th><th>产品/品牌</th><th>规格型号</th><th>数量</th><th>单位</th><th>备注</th></tr></thead><tbody>");
            foreach (var x in items)
                sb.Append("<tr><td>").Append(x.Seq).Append("</td><td>").Append(HtmlEncode(x.Category)).Append("</td><td>").Append(HtmlEncode(x.Item)).Append("</td><td>").Append(HtmlEncode(x.Brand)).Append("</td><td>").Append(HtmlEncode(x.Spec)).Append("</td><td>").Append(HtmlEncode(x.Quantity)).Append("</td><td>").Append(HtmlEncode(x.Unit)).Append("</td><td>").Append(HtmlEncode(x.Note)).Append("</td></tr>");
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        static string BuildTechParamTable(List<ContractTechParamLine> items)
        {
            if (items == null || items.Count == 0) return "<p>无</p>";
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr><th>序号</th><th>分类</th><th>名称</th><th>规格/型号/数值</th><th>备注</th></tr></thead><tbody>");
            foreach (var x in items)
                sb.Append("<tr><td>").Append(x.Seq).Append("</td><td>").Append(HtmlEncode(x.Category)).Append("</td><td>").Append(HtmlEncode(x.Name)).Append("</td><td>").Append(HtmlEncode(x.Value)).Append("</td><td>").Append(HtmlEncode(x.Note)).Append("</td></tr>");
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        static string HtmlEncode(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        static void EnsureDefaultContractSettings()
        {
            var list = LoadContractSettings();
            var legacyDefault = list.FirstOrDefault(x => x.Type == "合同范本" && x.Name == "冠誉设备购销合同默认范本");
            if (legacyDefault != null && IsLegacyDefaultContractTemplate(legacyDefault.Name, legacyDefault.Content))
            {
                legacyDefault.Name = "冠誉公司自用销售合同默认范本";
                legacyDefault.Content = GetDefaultContractTemplateHtml();
                legacyDefault.Note = "系统预置公司自用销售合同 HTML 范本（不含税）";
                legacyDefault.UpdatedAt = NowTimeString();
                SaveContractSettings(list);
                return;
            }
            if (list.Any(x => x.Type == "合同范本")) return;
            string now = NowTimeString();
            list.Add(new ContractSetting
            {
                Id = Guid.NewGuid().ToString("N"),
                Code = "CS01",
                Type = "合同范本",
                Name = "冠誉公司自用销售合同默认范本",
                Content = GetDefaultContractTemplateHtml(),
                IsDefault = true,
                Status = "启用",
                Sort = 1,
                Note = "系统预置公司自用销售合同 HTML 范本（不含税）",
                CreatedAt = now,
                UpdatedAt = now
            });
            list.Add(new ContractSetting
            {
                Id = Guid.NewGuid().ToString("N"),
                Code = "CS02",
                Type = "付款方式",
                Name = "标准付款方式（30%定金+5期余款）",
                Content = "1、定金：合同签订后甲方支付合同总金额定金30%。\n2、余款：合同余款分五个月等额支付。\n3、所有权保留：甲方付清全部货款前，设备所有权归乙方所有，甲方仅享有使用权。",
                IsDefault = true,
                Status = "启用",
                Sort = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            list.Add(new ContractSetting
            {
                Id = Guid.NewGuid().ToString("N"),
                Code = "CS03",
                Type = "公司资料",
                Name = "冠誉公司资料",
                Content = "中山市冠誉数控设备有限公司\n地址：广东省中山市\n联系人：王浪\n电话：18988541298",
                IsDefault = true,
                Status = "启用",
                Sort = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            if (!File.Exists(ContractSettingSequenceFile) || File.ReadAllText(ContractSettingSequenceFile).Trim() == "0")
                File.WriteAllText(ContractSettingSequenceFile, "3", new UTF8Encoding(false));
            SaveContractSettings(list);
        }

        static string GetDefaultContractTemplateHtml()
        {
            return @"<article class=""contract-document"">
<div class=""ct-header"">专业设计制造销售：数控车床、车铣复合车床、双主轴数控车床、自动化方案定制-王浪 18988541298</div>
<p class=""ct-contract-no"">合同编号：{{合同编号}}</p>
<h2>中山市冠誉数控设备有限公司销售合同</h2>
<div class=""ct-parties""><p>甲方（需方）：{{甲方名称}}</p><p>乙方（供方）：中山市冠誉数控设备有限公司</p></div>
<p class=""ct-intro"">甲乙双方本着平等互利、诚实信用的原则，就甲方向乙方购买设备事宜，经友好协商，达成如下协议</p>
<h3>一、合同主体与签订背景</h3>
<p>甲乙双方经友好协商，就设备购销事宜达成一致，旨在明确双方的权利、义务和责任，确保交易的顺利进行，特签订以下合同。</p>
<h3>二、设备名称、规格、数量、单价</h3>
{{设备明细}}
<div class=""ct-amount-lines""><p>小写（不含税）：¥{{合同总金额}}元</p><p>大写（不含税）：{{合同总金额大写}}</p></div>
<h3>三、随机配件</h3>
{{随机配件}}
<h3>四、付款方式与期限</h3>
<p>{{付款方式}}</p>
<p>定金：{{定金金额}} 元（大写：{{定金金额大写}}）；余款：{{余款金额}} 元（大写：{{余款金额大写}}）。{{分期说明}}</p>
<h3>五、交货时间、地点、包装及运输</h3>
<p>交货时间：{{交货时间}}；交货地点：{{交货地点}}；包装方式：{{包装方式}}；运输方式：{{运输方式}}。</p>
<h3>六、质量标准与检验验收</h3>
<p>{{质量验收条款}}</p>
<h3>七、售后维修</h3>
<p>{{售后维修条款}}</p>
<h3>八、不保修范围</h3>
<p>{{不保修范围}}</p>
<h3>九、违约责任</h3>
<p>{{违约责任}}</p>
<h3>十、合同生效</h3>
<p>本合同一式贰份，甲乙双方各执壹份，自双方签字盖章之日起生效。</p>
<table style=""width:100%;margin-top:30px""><tr><td>甲方签字/盖章：{{甲方签字}}<br>日期：{{甲方签约日期}}</td><td>乙方签字/盖章：{{乙方签字}}<br>日期：{{乙方签约日期}}</td></tr></table>
<h3>十一、开票信息及收款账户</h3>
<p>开票名称：{{开票名称}} &nbsp; 纳税人识别号：{{纳税人识别号}}<br>
开户名称：{{开户名称}} &nbsp; 开户行：{{开户行}}<br>
公司账号：{{公司账号}} &nbsp; 私人账号：{{私人账号}} &nbsp; 行号：{{行号}}</p>
<h3>十二、设备配置表</h3>
{{设备配置表}}
<h3>十三、技术参数表</h3>
{{技术参数表}}
<p style=""margin-top:20px"">备注：{{备注}}</p>
</article>";
        }

        static string ManualBackup()
        {
            lock (DataLock)
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string file = Path.Combine(BackupDir, "manual_suppliers_" + stamp + ".json");
                File.Copy(DataFile, file, true);
                File.Copy(SupplierSequenceFile, Path.Combine(BackupDir, "manual_supplier_sequence_" + stamp + ".json"), true);
                File.Copy(CustomerFile, Path.Combine(BackupDir, "manual_customers_" + stamp + ".json"), true);
                File.Copy(CustomerSequenceFile, Path.Combine(BackupDir, "manual_customer_sequence_" + stamp + ".json"), true);
                File.Copy(MaterialFile, Path.Combine(BackupDir, "manual_materials_" + stamp + ".json"), true);
                File.Copy(MaterialSequenceFile, Path.Combine(BackupDir, "manual_material_sequence_" + stamp + ".json"), true);
                File.Copy(FinanceFile, Path.Combine(BackupDir, "manual_finance_" + stamp + ".json"), true);
                File.Copy(OpeningFile, Path.Combine(BackupDir, "manual_opening_" + stamp + ".json"), true);
                CleanBackups(); return file;
            }
        }

        static void CleanBackups()
        {
            foreach (var f in new DirectoryInfo(BackupDir).GetFiles("*.json").OrderByDescending(x=>x.CreationTime).Skip(50)) try { f.Delete(); } catch { }
        }

        static void Audit(UserSession user, string action, string detail)
        {
            try { lock (DataLock) File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + (user == null ? "未登录" : user.Username + "/" + user.DisplayName) + "\t" + action + "\t" + detail.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine, Encoding.UTF8); } catch { }
        }

        static string ReadBody(HttpListenerRequest req) { using (var sr = new StreamReader(req.InputStream, Encoding.UTF8)) return sr.ReadToEnd(); }
        static void WriteJson(HttpListenerContext ctx, object obj, int status) { byte[] b=Encoding.UTF8.GetBytes(Json.Serialize(obj)); ctx.Response.StatusCode=status; ctx.Response.ContentType="application/json; charset=utf-8"; ctx.Response.ContentLength64=b.Length; ctx.Response.OutputStream.Write(b,0,b.Length); ctx.Response.Close(); }
        static void WriteJson(HttpListenerContext ctx, object obj) { WriteJson(ctx, obj, 200); }
        static void AddSecurityHeaders(HttpListenerResponse r) { r.AddHeader("X-Content-Type-Options","nosniff"); r.AddHeader("X-Frame-Options","DENY"); r.AddHeader("Cache-Control","no-store"); }
        static string Csv(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }
        static string Sha256(string value) { using (var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""))).Replace("-","").ToLowerInvariant(); }
        static bool FixedEquals(string a, string b) { if (a==null||b==null||a.Length!=b.Length) return false; int d=0; for(int i=0;i<a.Length;i++) d|=a[i]^b[i]; return d==0; }
        static string GetLanIp()
        {
            try { foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList) if (ip.AddressFamily==AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) return ip.ToString(); } catch { }
            return "127.0.0.1";
        }
    }
}
