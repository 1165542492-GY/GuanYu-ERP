# ERP Ver2.9 任务总表

> 本表用于跟踪 Ver2.9 大版本各阶段开发进度，供自动任务工作台与人工验收参考。  
> **全系统统一设计原则**：见 [`ERP-Ver2.9-系统设计原则.md`](ERP-Ver2.9-系统设计原则.md)（业务主线、模块边界、计算模式、操作方式、开发优先级与禁止事项）。

---

## 1. Ver2.9-rc1 老板首页看板

| 项目 | 内容 |
|------|------|
| **状态** | 已开发，待最终验收/提交 |
| **涉及文件** | `App.html`、`Program.cs`、`BusinessDashboard.cs` |
| **说明** | 老板经营驾驶舱、统计口径、首页数据汇总 |

---

## 2. Ver2.9-rc2 业务单据自动关联与表单减负

| 项目 | 内容 |
|------|------|
| **状态** | 已开发，待浏览器验收 |
| **涉及文件** | `Business.html`、`Program.cs`、`BusinessDocLinkage.cs` |
| **内容** | 销售订单、销售出库、采购单、采购入库、生产领用、成品入库自动带出与字段减负 |

---

## 3. Ver2.9-rc3 表格显示优化

| 项目 | 内容 |
|------|------|
| **状态** | 待开发 |
| **内容** | |
| | 常用列默认显示 |
| | 详细列进详情 |
| | 日期格式统一 |
| | 长名称省略显示 |
| | 减少横向滚动 |

---

## 4. Ver2.9-rc4 生产任务/工单

| 项目 | 内容 |
|------|------|
| **状态** | 待开发 |
| **内容** | |
| | 生产任务 |
| | 分批领用 |
| | 已领/未领/缺料 |
| | 成品/半成品入库关联生产任务 |

---

## 5. Ver2.9-rc5 仓库类型深化

| 项目 | 内容 |
|------|------|
| **状态** | 待开发 |
| **内容** | |
| | 原材料 |
| | 半成品 |
| | 成品 |
| | 库存汇总按类型展示 |

---

## 6. Ver2.9-rc6 业务下一步按钮（007）

| 项目 | 内容 |
|------|------|
| **状态** | 已完成（2026-06-28），待浏览器验收 |
| **报告** | `docs/worklog/reports/ver29_rc6_next_action_buttons_20260628_015759.md` |
| **内容** | |
| | 销售订单 → 销售出库 / 生产工单（跳转+预填，不自动生成） |
| | 生产工单 → 生产领用 / 成品入库 / 销售出库 |
| | 采购单 → 采购入库 |
| | 应收/应付 → 登记收/付款 |
| | 各页顶部通俗流程提示 |

---

## 7. Ver2.9-rc7 回归计划与自动化脚本（008）

| 项目 | 内容 |
|------|------|
| **状态** | 已完成（2026-06-28） |
| **报告** | `docs/worklog/reports/ver29_rc7_regression_plan_20260628_024104.md` |
| **脚本入口** | `tools/erp-agent/scripts/run_ver29_regression.ps1` |
| **内容** | |
| | P0/P1/P2 回归分级与人工验收清单 |
| | `check_ver29_git_guard.ps1` — Git 工作区守卫 |
| | `check_ver29_api_health.ps1` — API 健康检查 |
| | `check_ver29_pages.ps1` — 核心页面嵌入标记检查 |
| | `run_ver29_regression.ps1` — build + 汇总报告 |

---

## 8. Ver2.9-rc8 表单简化与自动关联

| 项目 | 内容 |
|------|------|
| **状态** | 已开发（2026-06-28），待浏览器验收 |
| **报告** | `docs/worklog/reports/ver29_rc8_form_simplification_auto_link_20260628_092217.md` |
| **涉及文件** | `App.html`、`Business.html`、`OperationLog.html`、`Program.cs`、`TestData.html`、`check_ver29_api_health.ps1` |
| **内容** | |
| | 采购单/批量、采购入库（Path A/B）、BOM、机型成本、生产领用、成品入库表单减负 |
| | 应收/应付关联只读、库存汇总引导与筛选 |
| | 测试数据导入结果分级展示 |
| | 操作记录详情弹窗关闭修复 |
| | `/api/info` 401/403 回归脚本误判修复 |

---

## 9. Ver2.9-rc9 售后维修工单（009）

| 项目 | 内容 |
|------|------|
| **状态** | 已完成（2026-06-28），待浏览器验收 |
| **报告** | `docs/worklog/reports/ver29_rc9_after_sales_service_work_order_20260628_095706.md` |
| **涉及文件** | `AfterSalesServiceOrder.cs`、`Program.cs`、`Permissions.cs`、`OperationImpactService.cs`、`Business.html`、`App.html`、`check_ver29_pages.ps1` |
| **内容** | |
| | 售后管理菜单、维修工单 CRUD、配件明细与金额自动计算 |
| | 状态流转（派工/维修/完成/结算）、手动生成应收 |
| | 草稿不扣库存、不自动应收；删除与应收关联保护 |

---

## 10. Ver2.9-rc10 售后维修收口增强（010）

| 项目 | 内容 |
|------|------|
| **状态** | 已完成（2026-06-28），待浏览器验收 |
| **报告** | `docs/worklog/reports/ver29_rc10_after_sales_closure_enhancement_20260628_102905.md` |
| **涉及文件** | `Business.html`、`TestDataService.cs`、`TestData.html`、`check_ver29_pages.ps1` |
| **内容** | |
| | 售后维修工单状态「下一步」按钮（复用 007 `bizNextBtn`） |
| | 测试数据总表 Sheet「售后维修工单」导出/预检查/导入（按单号更新） |
| | 页面通俗提示；回归脚本补充标记 |
| | 不扣库存、不自动应收 |

---

## 11. Ver2.9-rc11 售后维修操作记录与权限审计（011）

| 项目 | 内容 |
|------|------|
| **状态** | 已完成（2026-06-28），待浏览器验收 |
| **报告** | `docs/worklog/reports/ver29_rc11_after_sales_audit_permission_closure_20260628_105208.md` |
| **涉及文件** | `AfterSalesServiceOrder.cs`、`OperationLog.cs`、`OperationLog.html`、`Business.html`、`check_ver29_pages.ps1` |
| **内容** | |
| | 售后维修 CRUD/状态流转/生成应收 结构化操作记录 |
| | `ParseAuditAction` 识别售后维修模块与操作类型 |
| | 权限键回归（前后端已有，本轮验证） |
| | 操作记录页筛选提示；回归脚本源码标记 |

---

## 12. Ver2.9-rc15 006 / 库存类型深化设计（015）

| 项目 | 内容 |
|------|------|
| **状态** | 设计已完成（2026-06-28），**未执行开发**，等待用户确认库存规则 |
| **报告** | `docs/worklog/reports/ver29_rc15_006_inventory_design.md` |
| **评审任务** | `tools/erp-agent/tasks/todo/016_Ver29_rc15_006_inventory_design_user_review.md` |
| **前置清单** | `docs/worklog/reports/ver29_006_inventory_precheck_questions_20260628_132014.md` |
| **说明** | |
| | 7 类库存类型、物料字段、汇总/出入库/负库存/成本价规则设计 |
| | 分 7 阶段开发计划；不改业务代码、不执行 inbox 006 |
| | 用户确认后 → 迁移方案 + 分阶段开发任务 |

---

## 13. Ver2.9-rc16 006 / 库存类型深化阶段 1（017）

| 项目 | 内容 |
|------|------|
| **状态** | 已开发（2026-06-28），待浏览器验收 |
| **报告** | `docs/worklog/reports/ver29_rc16_inventory_stage1_development_20260628_135300.md` |
| **涉及文件** | `Program.cs`、`Material.html`、`Business.html`、`TestDataService.cs`、`check_ver29_pages.ps1` |
| **说明** | |
| | 物料 StockType 等 7 字段 + 物料页展示/筛选/导入导出 |
| | 库存汇总展示库存类型、安全库存、库存状态 |
| | 出入库轻量带出库存类型；库存不足提醒不拦截 |
| | **未**重构 BuildStockMap；**未**改应收/应付/财务核心 |

---

## 阶段依赖关系

```
rc1 首页看板 ──► rc2 单据关联 ──► rc3 表格优化
                      │
                      ├──► rc4 生产工单（005）
                      ├──► rc6 下一步按钮（007，优先于库存深化）
                      ├──► rc7 回归计划（008）◄── 006/009 前必跑
                      ├──► 售后维修（009，独立模块）
                      └──► rc5 仓库类型（006，最后做）
```

> 优先级详见 `ERP-Ver2.9-系统设计原则.md` 第十节。
