# 冠誉ERP管理系统

冠誉ERP管理系统是一套面向中小型制造企业的 Windows 局域网 ERP 管理程序。程序以 Windows 托盘应用运行，通过内置 HTTP 服务向同一局域网内的电脑提供浏览器管理界面。

- 当前版本：Ver1.4
- 运行平台：Windows
- 服务端口：`8787`
- 技术结构：C#/.NET Framework、WinForms、`HttpListener`、原生 HTML/CSS/JavaScript

## 已有功能

- 用户登录、退出及会话管理
- 供应商档案的新增、查询、修改、删除、Excel 导入和 CSV 导出
- 客户档案的新增、查询、修改、删除、Excel 导入和 CSV 导出
- 物料档案的新增、查询、修改、删除、Excel 导入和 CSV 导出
- 财务收支流水、期初余额、账户余额、年度/月度筛选和用途统计
- JSON 数据持久化、修改前自动备份、手动备份和操作日志
- 供应商、客户和物料编号自动生成
- Windows 托盘运行及局域网共享访问

采购管理、库存管理、应付账款和系统设置目前仅保留菜单入口，尚未实现业务功能。

## 项目文件

- `Program.cs`：程序入口、HTTP 服务、接口、数据存储和备份逻辑
- `App.html`：主界面、登录和供应商管理
- `Customer.html`：客户管理界面
- `Material.html`：物料管理界面
- `Finance.html`：财务收支界面
- `app.manifest`：Windows 管理员权限及系统兼容性声明

程序运行时会将四个 HTML 文件作为嵌入资源，由 `Program.cs` 组合后提供给浏览器。

## 编译方式

仓库当前未使用 Visual Studio 解决方案或项目文件，可使用 Windows 自带的 .NET Framework C# 编译器。在仓库根目录执行：

```powershell
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

& $csc /nologo /target:winexe /out:GuanYuERP.exe `
  /win32manifest:app.manifest `
  /resource:App.html,SupplierErpApp.App.html `
  /resource:Customer.html,SupplierErpApp.Customer.html `
  /resource:Material.html,SupplierErpApp.Material.html `
  /resource:Finance.html,SupplierErpApp.Finance.html `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Web.Extensions.dll `
  /reference:System.Windows.Forms.dll `
  /reference:System.Drawing.dll `
  /reference:System.Xml.Linq.dll `
  /reference:System.IO.Compression.dll `
  /reference:System.IO.Compression.FileSystem.dll `
  Program.cs
```

如果系统只有 32 位 .NET Framework，请将编译器路径中的 `Framework64` 改为 `Framework`。

## 运行方式

1. 编译生成 `GuanYuERP.exe`。
2. 以管理员身份运行该程序。
3. 程序启动后会驻留在 Windows 系统托盘，并自动打开默认浏览器。
4. 本机通过 `http://127.0.0.1:8787/` 访问。
5. 局域网内其他电脑使用界面中显示的局域网地址访问。

首次登录默认管理员账号为 `admin`，密码为 `admin123`。正式使用前应规划账号及密码管理方案。

## 数据目录

运行数据默认保存在：

```text
C:\ProgramData\智造ERP供应商管理
```

目录中包含供应商、客户、物料、财务和编号序列等 JSON 数据，以及：

- `backups`：自动备份和手动备份
- `operation.log`：操作审计日志

系统最多保留 50 个 JSON 备份文件。升级或迁移前，请完整备份该数据目录。

## 注意事项

- 程序清单要求管理员权限；启动失败时请确认是否以管理员身份运行。
- 服务监听局域网端口 `8787`，请按实际网络环境配置 Windows 防火墙。
- 当前使用 HTTP 明文通信，建议仅在可信局域网内使用，不要直接暴露到公网。
- 数据由运行程序统一读写，不建议在程序运行时手工编辑 JSON 文件。
- 数据文件、备份、日志和编译产物不应提交到 Git 仓库。
- 当前账号定义保存在源码中，且各角色尚未实施接口级权限隔离。

详细版本功能记录请参阅 [VERSION.md](VERSION.md)。
