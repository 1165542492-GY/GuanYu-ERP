# Ver2.9 回归脚本说明

本目录包含 Ver2.9（rc1–rc6）回归检查 PowerShell 脚本，**只读、不修改业务数据**。

## 快速开始

```powershell
cd C:\Users\Administrator\Documents\ERP

# 一键回归（推荐）
powershell -ExecutionPolicy Bypass -File ".\tools\erp-agent\scripts\run_ver29_regression.ps1"

# 或分步执行
powershell -ExecutionPolicy Bypass -File ".\tools\erp-agent\scripts\check_ver29_git_guard.ps1"
powershell -ExecutionPolicy Bypass -File ".\tools\erp-agent\scripts\check_ver29_api_health.ps1"
powershell -ExecutionPolicy Bypass -File ".\tools\erp-agent\scripts\check_ver29_pages.ps1"
```

## 前置条件

1. 开发仓库路径：`C:\Users\Administrator\Documents\ERP`
2. API/Pages 检查需 ERP 已启动（默认 `http://127.0.0.1:8787`）
3. **Pages Check 须使用最新 build 的 ERP**：先从托盘退出旧版 ERP，再启动 `bin\Debug\net8.0-windows\冠誉制造ERP.exe`，否则可能读到 rc6 之前嵌入 HTML 导致标记 FAIL
4. 若 `bin\Debug` 被运行中的 ERP.exe 锁定，总入口会自动尝试 `bin\rc7-regression-build-check`

### API Health 未登录判定（rc8 起）

| 接口 | 200 | 401 | 403 |
|------|-----|-----|-----|
| `/api/info` | PASS | PASS（需登录） | PASS（权限正常） |
| `/api/dashboard/owner-summary` | PASS | PASS（需登录） | 按脚本配置 |

## 脚本清单

| 脚本 | 用途 |
|------|------|
| `_ver29_common.ps1` | 共享辅助函数（Git/HTTP/报告目录） |
| `check_ver29_git_guard.ps1` | 目录、分支、status、log 守卫 |
| `check_ver29_api_health.ps1` | 首页与 `/api/info`、owner-summary 健康检查 |
| `check_ver29_pages.ps1` | 首页嵌入 HTML 标记 + API 权限拦截抽样 |
| `run_ver29_regression.ps1` | 总入口：build → git → api → pages → 汇总报告 |

## 报告输出

所有报告写入 `docs/worklog/reports/`：

- `ver29_api_health_yyyyMMdd_HHmmss.md`
- `ver29_pages_check_yyyyMMdd_HHmmss.md`
- `ver29_regression_summary_yyyyMMdd_HHmmss.md`

## 安全约束（内置）

- 不 commit / push / tag
- 不同步 `D:\冠誉制造ERP\App`
- 不清空数据、不调用删除接口
- 不杀进程（API 未启动时仅提示）

## 与 run_safe_check.ps1 的关系

| 脚本 | 定位 |
|------|------|
| `run_safe_check.ps1` | 开发轮次安全检查：会停旧进程并启动 ERP |
| `run_ver29_regression.ps1` | 发布前回归：不启停进程，覆盖更多页面标记 |

建议：改功能前跑 `run_ver29_regression.ps1`，浏览器验收 P0 人工清单后再 commit。
