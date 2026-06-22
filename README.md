# 冠誉ERP管理系统

冠誉ERP管理系统是一套面向中小型制造企业的 Windows 局域网 ERP 管理程序。程序以 Windows 托盘应用运行，通过内置 HTTP 服务向同一局域网内的电脑提供浏览器管理界面。

- 当前版本：Ver1.7
- 运行平台：Windows
- 服务端口：`8787`
- 技术结构：C#/.NET Framework、WinForms、`HttpListener`、原生 HTML/CSS/JavaScript

## 已有功能

- 用户登录、退出及会话管理
- 首页应收、应付、库存物料和财务收支综合看板
- 供应商、客户、物料档案的新增、查询、修改、删除、Excel 导入和 CSV 导出
- 供应商、客户、物料的批量添加与批量删除
- 显示大小切换（标准 / 大 / 超大），设置保存在浏览器本地
- 宽表格顶部横向滚动条，与底部滚动条同步
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
- `GuanYuERP.csproj`：MSBuild 项目文件
- `build.ps1`：一键编译并生成安装包
- `安装说明.txt`：随安装包分发的用户使用说明

程序运行时会将四个 HTML 文件作为嵌入资源，由 `Program.cs` 组合后提供给浏览器。

## 一键打包（推荐）

适合不会编程的使用场景。在源码目录 `01-源代码` 中执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

也可以右键 `build.ps1`，选择“使用 PowerShell 运行”。

脚本会自动完成以下步骤：

1. 使用 MSBuild 编译 `GuanYuERP.csproj`
2. 生成 `GuanYuERP.exe`
3. 打包 `GuanYuERP.exe`、`安装说明.txt`、`VERSION.md`
4. 输出安装包到：

```text
C:\Users\Administrator\Documents\冠誉ERP项目\02-安装包\冠誉ERP管理系统Ver1.7.zip
```

解压后即可使用。首次运行请右键 `GuanYuERP.exe`，选择“以管理员身份运行”。

## 手动编译方式

如需手工编译，也可使用 Windows 自带的 .NET Framework C# 编译器。在仓库根目录执行：

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

## 批量操作与显示设置

### 批量删除

在供应商、客户、物料管理页面：

1. 勾选表格左侧复选框，或使用表头全选
2. 点击「批量删除」
3. 确认本次将删除的数据条数

未选择数据时会提示「请先选择要删除的数据」。

### 批量添加

1. 点击「批量添加」打开批量录入窗口
2. 在表格中一次填写多行资料，可继续添加行或删除未保存的临时行
3. 点击「保存全部」统一校验并保存

批量添加采用部分成功策略：正确的数据会保存，重复或错误的数据会跳过，并在保存后显示跳过原因。若全部数据均有误，则不会保存任何数据。

Excel 导入功能保持不变。

### 显示大小

顶部栏提供「显示大小」切换：标准 / 大 / 超大。选择后会保存在浏览器 `localStorage` 中，下次打开仍保持该设置。

### 表格横向滚动

供应商、客户、物料的宽表格在顶部和底部均提供横向滚动条，拖动任一侧滚动条时，表格内容会同步左右移动。

## 注意事项

- 程序清单要求管理员权限；启动失败时请确认是否以管理员身份运行。
- 服务监听局域网端口 `8787`，请按实际网络环境配置 Windows 防火墙。
- 当前使用 HTTP 明文通信，建议仅在可信局域网内使用，不要直接暴露到公网。
- 数据由运行程序统一读写，不建议在程序运行时手工编辑 JSON 文件。
- 数据文件、备份、日志、编译产物和安装包 zip 不应提交到 Git 仓库。
- 安装包默认输出到仓库外的 `02-安装包` 目录，不会进入 GitHub。
- 当前账号定义保存在源码中，且各角色尚未实施接口级权限隔离。

详细版本功能记录请参阅 [VERSION.md](VERSION.md)。
