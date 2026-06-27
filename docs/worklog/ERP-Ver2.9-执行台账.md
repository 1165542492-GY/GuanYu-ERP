# ERP Ver2.9 执行台账

> 自动任务工作台每轮执行后追加记录。时间格式：`yyyy-MM-dd HH:mm:ss`。

| 时间 | 阶段 | 任务文件 | 执行工具 | 修改文件 | 执行动作 | build结果 | 接口检查 | 浏览器验收 | 风险等级 | 是否提交 | 备注 |
|------|------|----------|----------|----------|----------|-----------|----------|------------|----------|----------|------|
| 2026-06-27 | Ver2.9-rc1 | — | 人工/Cursor | App.html、Program.cs、BusinessDashboard.cs | 开发老板首页看板 | 0 错误 0 警告 | 通过 | 已跑出新版首页 | R1 | 否 | 未 commit/push |
| 2026-06-27 | Ver2.9-rc2 | — | 人工/Cursor | Business.html、Program.cs、BusinessDocLinkage.cs | 业务单据自动关联与表单减负 | 0 错误 0 警告 | 待验 | 待验收 | R2 | 否 | 未 commit/push，待浏览器验收 |
| 2026-06-27 22:25:51 | Ver2.9-rc2 | 001_Ver29_rc2_browser_verify.md | run_task_loop | — | 任务循环 | 0 错误 | 通过 | 待人工 | R0/R1 — 只读检查与验收准备。 | 否 | CLI exit 0; 报告: ver29_safe_check_20260627_222551.md |
| 2026-06-27 22:41:47 | Ver2.9-rc1 | 002_Ver29_rc1_dashboard_verify_and_fix.md | run_task_loop | — | 任务拦截 | — | — | — | R1/R2 — 首页 UI 展示与轻微 JS 渲染修复；不涉及库存、应收应付等后端业务逻辑。 | 否 | 禁止模式拦截 → C:\Users\Administrator\Documents\ERP\docs\worklog\reports\task_failed_20260627_224147.md |
| 2026-06-27 22:44:40 | Ver2.9-rc1 | 002_Ver29_rc1_dashboard_verify_and_fix.md | run_task_loop | — | 任务循环 | 0 错误 | 通过 | 待人工 | R1/R2 — 首页 UI 展示与轻微 JS 渲染修复；不涉及库存、应收应付等后端业务逻辑。 | 否 | CLI exit 0; 报告: ver29_safe_check_20260627_224440.md |
| 2026-06-27 22:47:47 | Ver2.9-rc2 | 003_Ver29_rc2_forms_linkage_verify_and_fix.md | run_task_loop | — | 任务循环 | 0 错误 | 通过 | 待人工 | R2/R3 — 前端自动带出与中风险后端关联逻辑；涉及出库/入库数量校验时须谨慎，不擅自改库存扣减核心逻辑。 | 否 | CLI exit 0; 报告: ver29_safe_check_20260627_224747.md |
| 2026-06-27 23:10:09 | Ver2.9-rc3 | 004_Ver29_rc3_table_display_optimization.md | run_task_loop | — | 执行前安全检查 | 失败 | — | — | R1/R2 — 列表 UI 与前端展示逻辑；不改库存、应收应付、数据结构。 | 否 | 失败 → C:\Users\Administrator\Documents\ERP\docs\worklog\reports\task_failed_20260627_231009.md |
| 2026-06-27 23:10:23 | Ver2.9-rc4 | 005_Ver29_rc4_production_work_order_design.md | run_task_loop | — | 执行前安全检查 | 失败 | — | — | R3 — 涉及生产任务数据模型、领料与入库关联；须先设计方案再实现，不破坏现有数据。 | 否 | 失败 → C:\Users\Administrator\Documents\ERP\docs\worklog\reports\task_failed_20260627_231023.md |
| 2026-06-27 23:10:37 | Ver2.9-rc5 | 006_Ver29_rc5_stock_type_deepening.md | run_task_loop | — | 执行前安全检查 | 失败 | — | — | R3 — 涉及库存分类与出入库流向；必须先输出迁移兼容方案，不允许直接硬改旧数据。 | 否 | 失败 → C:\Users\Administrator\Documents\ERP\docs\worklog\reports\task_failed_20260627_231037.md |
| 2026-06-27 23:10:51 | Ver2.9-rc6 | 007_Ver29_rc6_next_action_buttons.md | run_task_loop | — | 执行前安全检查 | 失败 | — | — | R2 — 前端路由跳转与单据来源传递；如需轻量接口仅做参数预填，不改核心业务逻辑。 | 否 | 失败 → C:\Users\Administrator\Documents\ERP\docs\worklog\reports\task_failed_20260627_231051.md |
| 2026-06-27 23:11:05 | Ver2.9 | 008_Ver29_regression_plan_and_scripts.md | run_task_loop | — | 执行前安全检查 | 失败 | — | — | R0/R2 — 文档整理、只读检查脚本与测试数据方案；脚本只造测试数据，不清正式数据。 | 否 | 失败 → C:\Users\Administrator\Documents\ERP\docs\worklog\reports\task_failed_20260627_231105.md |
| 2026-06-27 23:17:33 | Ver2.9-rc3 | 004_Ver29_rc3_table_display_optimization.md | run_task_loop | — | 任务循环 | 0 错误 | 通过 | 待人工 | R1/R2 — 列表 UI 与前端展示逻辑；不改库存、应收应付、数据结构。 | 否 | CLI exit 0; 报告: ver29_safe_check_20260627_231733.md |
