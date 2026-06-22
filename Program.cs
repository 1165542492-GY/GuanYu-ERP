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
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using System.Windows.Forms;

namespace SupplierErpApp
{
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
        public string Note { get; set; }
        public string Status { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class ImportRequest { public string FileName { get; set; } public string Data { get; set; } }
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
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 50 * 1024 * 1024 };
        static readonly Dictionary<string, UserSession> Sessions = new Dictionary<string, UserSession>();
        static List<UserDef> Users = new List<UserDef>();
        static readonly string[] AllPermissionKeys = {
            "supplier.view","supplier.add","supplier.edit","supplier.delete","supplier.batch_delete",
            "customer.view","customer.add","customer.edit","customer.delete","customer.batch_delete",
            "material.view","material.add","material.edit","material.delete","material.batch_delete",
            "finance.view","finance.add","finance.edit","finance.delete",
            "settings.view","settings.account","settings.password"
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
            Process.Start("http://127.0.0.1:" + Port + "/");
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
                if (path == "/api/materials" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "material.view")) return; WriteJson(ctx, LoadMaterials()); return; }
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
                if (path.StartsWith("/api/finance/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "finance.edit")) return; UpdateFinance(ctx, user, path.Substring("/api/finance/".Length)); return; }
                if (path.StartsWith("/api/finance/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "finance.delete")) return; DeleteFinance(ctx, user, path.Substring("/api/finance/".Length)); return; }
                if (path == "/api/export") { if (!RequirePermission(ctx, user, "supplier.view")) return; ExportCsv(ctx); return; }
                if (path == "/api/customers/export") { if (!RequirePermission(ctx, user, "customer.view")) return; ExportCustomersCsv(ctx); return; }
                if (path == "/api/materials/export") { if (!RequirePermission(ctx, user, "material.view")) return; ExportMaterialsCsv(ctx); return; }
                if (path == "/api/backup" && ctx.Request.HttpMethod == "POST") { if (!IsAdminUser(user)) { if (!RequirePermission(ctx, user, "settings.view")) return; } string f = ManualBackup(); Audit(user, "手动备份", Path.GetFileName(f)); WriteJson(ctx, new { ok = true, file = f }); return; }
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
            return user.Permissions != null && user.Permissions.Contains(permission);
        }

        static bool RequirePermission(HttpListenerContext ctx, UserSession user, string permission)
        {
            if (HasPermission(user, permission)) return true;
            WriteJson(ctx, new { error = "无权限操作" }, 403);
            return false;
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
                    new PermissionItem { Key = "finance.delete", Label = "删除" }
                }},
                new PermissionGroup { Module = "系统设置", Items = new[] {
                    new PermissionItem { Key = "settings.view", Label = "查看系统设置" },
                    new PermissionItem { Key = "settings.account", Label = "账号管理" },
                    new PermissionItem { Key = "settings.password", Label = "修改密码" }
                }}
            };
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
            item.Id=Guid.NewGuid().ToString("N"); item.Code=NextCode(MaterialSequenceFile,"WL",list.Select(x=>x.Code)); item.Status="启用"; item.UpdatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); item.UpdatedBy=user.DisplayName;
            list.Insert(0,item); SaveMaterials(list); Audit(user,"新增物料",item.Code+" "+item.NameSpec); WriteJson(ctx,item,201);
        }

        static void UpdateMaterial(HttpListenerContext ctx, UserSession user, string id)
        {
            var input=Json.Deserialize<Material>(ReadBody(ctx.Request)); ValidateMaterial(input);
            var list=LoadMaterials(); var item=list.FirstOrDefault(x=>x.Id==id);
            if(item==null){WriteJson(ctx,new{error="物料不存在"},404);return;}
            if(list.Any(x=>x.Id!=id&&string.Equals(x.Supplier,input.Supplier,StringComparison.OrdinalIgnoreCase)&&string.Equals(x.NameSpec,input.NameSpec,StringComparison.OrdinalIgnoreCase))){WriteJson(ctx,new{error="该供应商的相同物料已经存在"},409);return;}
            item.Supplier=input.Supplier;item.NameSpec=input.NameSpec;item.QuantityUnit=input.QuantityUnit;item.TaxPrice=input.TaxPrice;item.NoTaxPrice=input.NoTaxPrice;item.Note=input.Note;item.Status=string.IsNullOrEmpty(input.Status)?"启用":input.Status;item.UpdatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");item.UpdatedBy=user.DisplayName;
            SaveMaterials(list);Audit(user,"修改物料",item.Code+" "+item.NameSpec);WriteJson(ctx,item);
        }

        static void DeleteMaterial(HttpListenerContext ctx, UserSession user, string id)
        {
            var list=LoadMaterials();var item=list.FirstOrDefault(x=>x.Id==id);if(item==null){WriteJson(ctx,new{error="物料不存在"},404);return;}list.Remove(item);SaveMaterials(list);Audit(user,"删除物料",item.Code+" "+item.NameSpec);WriteJson(ctx,new{ok=true});
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
                        TaxPrice = input.TaxPrice, NoTaxPrice = input.NoTaxPrice, Note = input.Note,
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
            var req=Json.Deserialize<ImportRequest>(ReadBody(ctx.Request));
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
            foreach(var row in rows){rowNo++;string supplier=Cell(row,"供应商"),name=Cell(row,"物料名称/规格");if(Placeholder(name)){skipped++;continue;}if(!suppliers.Any(x=>string.Equals(x.Company,supplier,StringComparison.OrdinalIgnoreCase))){skipped++;errors.Add("第"+rowNo+"行：供应商未建档");continue;}if(list.Any(x=>string.Equals(x.Supplier,supplier,StringComparison.OrdinalIgnoreCase)&&string.Equals(x.NameSpec,name,StringComparison.OrdinalIgnoreCase))){skipped++;errors.Add("第"+rowNo+"行：物料已存在");continue;}var item=new Material{Id=Guid.NewGuid().ToString("N"),Code=NextCode(MaterialSequenceFile,"WL",list.Select(x=>x.Code)),Supplier=supplier,NameSpec=name,QuantityUnit=Cell(row,"数量/单位"),TaxPrice=Money(Cell(row,"含税价")),NoTaxPrice=Money(Cell(row,"不含税价")),Note=Cell(row,"备注"),Status=Cell(row,"状态"),UpdatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),UpdatedBy=user.DisplayName};if(string.IsNullOrEmpty(item.Status))item.Status="启用";list.Insert(0,item);imported++;}
            if(imported>0)SaveMaterials(list);Audit(user,"导入物料","成功"+imported+"条，跳过"+skipped+"条");WriteJson(ctx,new{imported=imported,skipped=skipped,errors=errors.Take(8).ToArray()});
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
            var sb=new StringBuilder();sb.AppendLine("物料编号,供应商,物料名称/规格,数量/单位,含税价,不含税价,备注,状态,最后更新,操作人");
            foreach(var x in LoadMaterials())sb.AppendLine(string.Join(",",new[]{x.Code,x.Supplier,x.NameSpec,x.QuantityUnit,x.TaxPrice.ToString("0.00"),x.NoTaxPrice.ToString("0.00"),x.Note,x.Status,x.UpdatedAt,x.UpdatedBy}.Select(Csv)));
            byte[] bytes=Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();ctx.Response.ContentType="text/csv; charset=utf-8";ctx.Response.AddHeader("Content-Disposition","attachment; filename=materials_"+DateTime.Now.ToString("yyyyMMdd")+".csv");ctx.Response.ContentLength64=bytes.Length;ctx.Response.OutputStream.Write(bytes,0,bytes.Length);ctx.Response.Close();
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
