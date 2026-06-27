# 任务标题

Ver2.9 回归计划与自动化脚本方案

# 阶段

Ver2.9 全版本回归（rc1–rc6 汇总）

# 风险等级

R0/R2 — 文档整理、只读检查脚本与测试数据方案；脚本只造测试数据，不清正式数据。

# 当前目标

- 整理 Ver2.9 完整回归清单。
- 编写自动化测试脚本方案（可落地为 PowerShell / 接口检查脚本）。
- 覆盖范围：首页看板、销售订单、销售出库、采购单、采购入库、生产任务/领用、成品入库、库存、应收应付、财务、权限、大/超大显示。
- 脚本只造测试数据，不清正式数据。
- 输出回归计划与脚本说明报告。

# 允许修改文件

- `tools/`（含 `tools/erp-agent/`、`tools/regression/` 等）
- `docs/worklog/`
- `docs/worklog/reports/`
- 可新增 `tests/` 或 `tools/regression/` 目录及脚本

# 禁止动作

- 禁止清数据
- 禁止 commit
- 禁止 push
- 禁止同步正式 App（`D:\冠誉制造ERP\App`）
- 禁止修改稳定仓库（`C:\Users\Administrator\Documents\GuanYu-ERP-Stable`）
- 禁止删除、移动、重建 tag
- 禁止 `git reset`、`git clean`、`git push --force`
- 禁止修改业务代码（`*.cs`、`*.html` 等业务源文件）
- 禁止脚本调用清空数据接口或删除 `D:\冠誉制造ERP\Data`

# 执行步骤

1. 阅读 `docs/worklog/ERP-Ver2.9-任务总表.md` 与各 rc 阶段报告，汇总功能点。
2. 编写回归清单文档 `docs/worklog/ERP-Ver2.9-回归清单.md`，按模块列出：
   - 首页看板（rc1）
   - 单据自动关联（rc2）
   - 表格显示（rc3）
   - 生产任务/工单（rc4）
   - 仓库类型（rc5）
   - 业务下一步按钮（rc6）
   - 权限、显示模式、build、接口检查
3. 设计自动化脚本方案，建议目录 `tools/regression/`：
   - `run_regression_check.ps1` — 串联 build、接口 GET、safe_check
   - 各模块检查项清单（Markdown 或 JSON）
   - 测试数据构造说明（仅开发环境，不碰正式 Data）
4. 实现或完善可运行的只读检查脚本（build + HTTP GET + 报告生成）。
5. 明确哪些项必须人工浏览器验收，哪些可脚本自动检查。
6. 运行现有 `run_safe_check.ps1` 作为基线，记录结果。
7. 输出回归计划与脚本方案报告。

# 验收要求

- `docs/worklog/ERP-Ver2.9-回归清单.md` 完整，覆盖上述全部模块。
- 自动化脚本方案文档清晰，可交给后续轮次执行。
- 至少有一个可运行的回归检查入口脚本（如 `tools/regression/run_regression_check.ps1` 或扩展现有 safe_check）。
- 脚本内置风险拦截：不清数据、不 commit、不 push、不同步正式 App。
- 未修改任何业务源代码（`*.cs`、业务 `*.html`）。
- build 与 safe_check 基线可跑通。

# 输出报告要求

报告写入 `docs/worklog/reports/`，文件名建议 `ver29_regression_plan_yyyyMMdd_HHmmss.md`，包含：

- 执行时间与阶段（Ver2.9 回归）
- 回归清单文档路径
- 自动化脚本方案与目录结构
- 可自动检查 vs 必须人工验收的分工表
- 测试数据构造策略（不碰正式 Data）
- 基线 safe_check 结果摘要
- 新增文件列表
- Ver2.9 发布前建议检查顺序
- 是否建议提交（仅记录，不自动执行）

# 完成后是否允许自动提交

否
