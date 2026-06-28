# 009 Ver2.9-rc9 售后维修工单

**状态**: 已完成（2026-06-28）

## 目标

新增售后维修工单基础模块：登记、派工、配件明细、费用汇总、手动生成应收；草稿不扣库存、不自动生成应收。

## 交付

- 模型 `AfterSalesServiceOrder` + 配件明细 `AfterSalesPartLine`
- API `/api/after-sales-service-orders` CRUD + 状态流转 + `generate-receivable`
- 菜单「售后管理 / 售后维修工单」
- 权限键 `after_sales.view/add/edit/delete`
- 报告 `docs/worklog/reports/ver29_rc9_after_sales_service_work_order_20260628_095706.md`

## 未做（后续）

- 售后配件出库 / 库存扣减
- 测试数据总表售后 Sheet
- 复杂导入
