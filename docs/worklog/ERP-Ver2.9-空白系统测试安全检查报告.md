# ERP Ver2.9 空白系统测试安全检查报告

**检查时间**：2026-06-28  
**检查阶段**：空白新系统全流程测试 — 阶段 0（仅安全检查）  
**项目路径**：`C:\Users\Administrator\Documents\ERP`  
**当前分支**：`codex/ver2.8-basic-business-framework`  
**当前 commit**：`45bec18`（Ver2.9-rc18-rc19 table display and document flow polish）

---

## 一、核心结论（必须先读）

| 问题 | 结论 |
|------|------|
| **当前能不能安全做空白新系统测试？** | **不能** |
| **是否支持测试沙盒数据目录？** | **不支持**（代码层无切换机制） |
| **实际正式 Data 路径** | `D:\冠誉制造ERP\Data` |
| **是否会触碰正式 Data？** | 若启动现有 ERP（开发版或正式 App）并造数/跑流程，**必然写入正式 Data** |
| **是否建议继续进行完整空白系统测试？** | **不建议** — 须先开发或准备测试沙盒机制 |

> **明确声明**：当前系统未提供安全的空白数据沙盒，**不能在正式 Data 上做空白新系统全流程测试**。若强行在 `D:\冠誉制造ERP\Data` 大量造 `E2E_RC19_MFG_` 测试数据，将污染封版前正式业务数据，且现有「清理测试数据」机制无法按该前缀安全回收。

---

## 二、Git 状态（检查项 1）

```text
git status --short
?? docs/worklog/ERP-Ver2.9-现阶段系统整体评估报告.md
?? docs/worklog/reports/ver29_api_health_20260628_153755.md
?? docs/worklog/reports/ver29_api_health_20260628_153846.md
?? docs/worklog/reports/ver29_pages_check_20260628_153752.md
?? docs/worklog/reports/ver29_pages_check_20260628_153756.md
?? docs/worklog/reports/ver29_pages_check_20260628_153846.md
?? docs/worklog/reports/ver29_pages_check_20260628_153848.md
?? docs/worklog/reports/ver29_regression_summary_20260628_153752.md
?? docs/worklog/reports/ver29_regression_summary_20260628_153843.md
?? tools/erp-agent/scripts/pack_ver29_system_assessment_after_rc18_rc19.ps1

git status -sb
## codex/ver2.8-basic-business-framework...origin/codex/ver2.8-basic-business-framework
（同上 untracked 文件）

git log -3 --oneline --decorate
45bec18 (HEAD -> codex/ver2.8-basic-business-framework, origin/codex/ver2.8-basic-business-framework) Ver2.9-rc18-rc19 table display and document flow polish
cd3d003 (tag: v2.9.0-rc17) Ver2.9-rc17 inventory type stage 2 polish
7114cbd (tag: v2.9.0-rc16) Ver2.9-rc16 add inventory packaging scripts
```

本阶段**未修改业务代码**，**未 commit / push / tag**。

---

## 三、Program.cs 数据目录逻辑（检查项 2）

### 3.1 硬编码路径（唯一数据源）

```772:777:Program.cs
        static readonly string AppRoot = @"D:\冠誉制造ERP";
        static readonly string LegacyDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "智造ERP供应商管理");
        static readonly string DataDir = Path.Combine(AppRoot, "Data");
        static readonly string BackupDir = Path.Combine(AppRoot, "Backups");
        static readonly string ExportsDir = Path.Combine(AppRoot, "Exports");
        static readonly string ImportsDir = Path.Combine(AppRoot, "Imports");
```

- `AppRoot`、`DataDir` 为 **`static readonly` 编译期常量**，运行时不可切换。
- 全部业务 JSON（供应商、客户、物料、单据、应收应付、财务、BOM、合同、售后等）均指向 `DataDir` 下固定文件名。
- `/api/info` 返回的 `dataDir` 即为该硬编码路径。

### 3.2 启动入口

```823:823:Program.cs
        public static void Main()
```

- `Main()` **无命令行参数**，不读取 `args`。
- **未使用** `AppContext.BaseDirectory` 作为数据根目录。
- 仓库内**无** `appsettings.json` 或同类配置文件用于指定 Data 路径。

### 3.3 环境变量

```1000:1009:Program.cs
        static string ResolveListenUrl()
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            ...
            return DefaultListenUrl;
        }
```

- 唯一读取的环境变量为 **`ASPNETCORE_URLS`**，仅影响 **HTTP 监听地址/端口**，**不影响数据目录**。
- 未发现 `ERP_DATA`、`DATA_DIR`、`冠誉制造ERP` 等相关环境变量支持。

### 3.4 旧目录迁移（非沙盒）

- `LegacyDataDir` = `C:\ProgramData\智造ERP供应商管理`
- 仅在 **新 Data 目录尚无 JSON** 时，一次性复制旧 JSON 到 `D:\冠誉制造ERP\Data`。
- 这是历史迁移逻辑，**不是**测试沙盒切换。

### 3.5 「TestData」相关（易混淆，非独立目录）

| 名称 | 实际含义 | 是否沙盒 |
|------|----------|----------|
| `TestData.html` / `TestDataService.cs` | 系统设置中的「测试数据导入导出」功能页 | 否，读写仍走 `DataDir` |
| `/api/test-data/export-all` 等 | 从正式 Data 导出 / 导入 Excel | 否 |
| `ClearTestData` / `AUTO_TEST` | 仅删除名称/编号/备注含 **`AUTO_TEST`** 的记录 | 否，操作对象仍是 `D:\冠誉制造ERP\Data` |
| `ClearAllBusinessData` | 清空全部业务 JSON（保留账号/设置等） | 否，**高危**，直接作用于正式 Data |

`ClearDataService.cs` 中清理标记为：

```13:14:ClearDataService.cs
        const string AutoTestMarker = "AUTO_TEST";
        const string ClearTestDataConfirmText = "确认清理测试数据";
```

计划使用的 `E2E_RC19_MFG_` 前缀**不在**自动清理范围内。

### 3.6 关键词检索汇总

| 关键词 | 检索结果 |
|--------|----------|
| `Data` / `DataDir` | 全部指向 `D:\冠誉制造ERP\Data` |
| `DataDirectory` | 无独立配置类；`EnsureDataDirectories()` 仅创建固定目录 |
| `冠誉制造ERP` | 硬编码于 `AppRoot` |
| `D:\冠誉制造ERP\Data` | 等价于 `Path.Combine(AppRoot, "Data")` |
| `AppContext.BaseDirectory` | **未使用** |
| `Environment` | 仅 `CommonApplicationData`（旧迁移）与 `ASPNETCORE_URLS`（监听） |
| `appsettings` | **不存在** |
| `TestData` | 功能模块名，非磁盘沙盒目录 |
| `ClearData` | 清理逻辑均针对 `DataDir` 内 JSON |

---

## 四、tools/erp-agent/scripts 脚本检查（检查项 3）

已检查脚本（16 个 `.ps1`，含 `_ver29_common.ps1`）：

| 脚本 | 数据目录相关行为 |
|------|------------------|
| `run_ver29_regression.ps1` | 仅 `dotnet build` + HTTP 检查，**只读** |
| `check_ver29_api_health.ps1` | 访问 `http://127.0.0.1:8787`，**只读** |
| `check_ver29_pages.ps1` | 访问 8787 首页/API，**只读** |
| `check_ver29_ui_dialogs.ps1` | 静态 HTML 检查，**只读** |
| `check_ver29_git_guard.ps1` | Git 守卫，**只读** |
| `pack_*.ps1` | 打包日志/报告，**只读** |

**结论**：现有 erp-agent 脚本**均未**实现临时 Data 目录切换、沙盒启动或空白库初始化。全部假设 ERP 已在 8787 运行，且使用的是程序内置的正式 Data 路径。

---

## 五、空白测试环境可行性（检查项 4）

| 方式 | 是否可行 | 说明 |
|------|----------|------|
| **环境变量切换 Data** | ❌ 不支持 | 无相关实现 |
| **启动参数** | ❌ 不支持 | `Main()` 无参数 |
| **配置文件** | ❌ 不支持 | 无 appsettings 等 |
| **测试专用目录（代码内置）** | ❌ 不支持 | `D:\冠誉制造ERP\TestData` 磁盘上**不存在**，代码也未引用 |
| **复制 Data 后临时切换（程序内）** | ❌ 不支持 | 无切换 API；改路径需改 `Program.cs`（本轮禁止） |
| **OS 目录联接（junction）手工切换** | ⚠️ 未官方支持、高风险 | 需停 ERP、动 `Data` 目录结构，仍触碰正式 Data 路径；无回滚脚本；**不建议作为封版前方案** |
| **「仅清理 AUTO_TEST」** | ⚠️ 非沙盒 | 只删 `AUTO_TEST` 标记；不能用于 `E2E_RC19_MFG_`；且操作对象仍是正式 Data |
| **「清空全部业务数据」** | ❌ 禁止 | 等同清空正式业务库，违反本轮约束 |

### 开发版 vs 正式 App

- 开发版：`bin\Debug\net8.0-windows\冠誉制造ERP.exe` — 与正式 App **共用同一 `AppRoot`/`DataDir` 硬编码**。
- 正式 App：`D:\冠誉制造ERP\App\冠誉制造ERP.exe` — 同上。

**无论启动哪一个，数据均写入 `D:\冠誉制造ERP\Data`。**

---

## 六、正式目录现场确认（检查项 5、6）

| 路径 | 状态 | 备注 |
|------|------|------|
| `D:\冠誉制造ERP\Data` | ✅ 存在 | 当前含 **39 个** `.json` 文件（正式业务数据） |
| `D:\冠誉制造ERP\App\冠誉制造ERP.exe` | ✅ 存在 | 正式 App 已部署 |
| `D:\冠誉制造ERP\TestData` | ❌ 不存在 | 无预置测试沙盒目录 |
| `D:\冠誉制造ERP\Backups` | （未逐项列举） | 备份与清空前备份均相对正式 Data |

本阶段**未读取、未修改、未清空、未删除**正式 Data 内任何 JSON 文件。

---

## 七、六问直答

### 1. 当前能不能安全做空白新系统测试？

**不能。** 现有 ERP 实例启动后只能使用 `D:\冠誉制造ERP\Data`；无隔离沙盒，造数即污染正式库。

### 2. 是否支持测试沙盒数据目录？

**不支持。** 代码、配置、脚本均无「切换 Data 根目录」能力。

### 3. 实际正式 Data 路径是什么？

**`D:\冠誉制造ERP\Data`**

### 4. 是否会触碰正式 Data？

- **若继续按原计划跑空白全流程（API/浏览器造数）**：**会**，且不可避免。
- **本安全检查阶段**：**未触碰**（仅读代码、Git、目录存在性）。

### 5. 下一阶段应该怎么做？

建议按优先级：

1. **（推荐）先开发测试沙盒机制（封版后或 rc20+ 专项，非本轮）**
   - 例如：支持环境变量 `ERP_APP_ROOT` 或 `ERP_DATA_DIR`，或启动参数 `--data-dir`。
   - 空白库初始化仅作用于沙盒目录；`/api/info` 返回实际 dataDir 供脚本校验。
   - 提供「E2E 专用清理」或按前缀（如 `E2E_RC19_MFG_`）回收，且**限定在沙盒路径**。

2. **沙盒就绪后的推荐测试目录（示例）**
   ```text
   D:\冠誉制造ERP\TestData\E2E_BlankMachineFactory_20260628_HHmmss\
   ```
   由启动脚本指定为 Data 根（需程序支持后生效）。

3. **沙盒机制就绪后，再执行**
   - 阶段 1：空白库初始化 + 机床厂基础资料造数（`E2E_RC19_MFG_` 前缀）
   - 阶段 2：采购→生产→销售→收付款→合同→售后 分流程 API/浏览器验证
   - 阶段 3：库存/应收应付校验 + 报告 + 打包脚本

4. **封版前若必须验证业务**：可在**已有正式数据**上做**只读回归**（build / 8787 / pages / API health），**不要**模拟「从 0 空白造全量机床厂数据」。

### 6. 是否建议继续进行完整空白系统测试？

**不建议。** 在沙盒机制落地前强行测试，要么污染正式 Data，要么依赖清空/恢复等高危操作，均不符合封版前安全要求。

---

## 八、安全边界确认（本阶段）

| 项 | 状态 |
|----|------|
| 修改业务代码 | ❌ 未修改 |
| commit / push / tag | ❌ 未执行 |
| 同步正式 App | ❌ 未执行 |
| 清空 / 删除 `D:\冠誉制造ERP\Data` | ❌ 未执行 |
| 执行 006 | ❌ 未执行 |
| 直接写 JSON 数据 | ❌ 未执行 |
| 造测试数据 / 跑业务流程 | ❌ 未执行（本阶段仅检查） |

---

## 九、附录：相关文档与代码位置

- 数据目录说明：`README.md` →「数据目录」章节（明确正式路径为 `D:\冠誉制造ERP\Data`）
- 数据路径定义：`Program.cs` 第 772–815 行
- 清理测试数据：`ClearDataService.cs`（`AUTO_TEST` 标记）
- 备份恢复（含 `ClearDataDirectory`）：`BackupRestore.cs`
- 回归脚本（只读）：`tools/erp-agent/scripts/run_ver29_regression.ps1`

---

**报告路径**：`docs/worklog/ERP-Ver2.9-空白系统测试安全检查报告.md`  
**下一阶段**：待测试沙盒机制方案确认后，再启动「空白新系统机床厂全流程测试」阶段 1。
