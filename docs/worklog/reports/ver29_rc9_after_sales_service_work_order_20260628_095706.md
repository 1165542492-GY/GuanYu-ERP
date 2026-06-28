# Ver2.9-rc9 售后维修工单基础模块

- **执行时间**: 2026-06-28 09:57:06
- **分支**: codex/ver2.8-basic-business-framework（未 commit / push / tag）

---

## 1. 新增/修改文件

### 新增

| 文件 | 说明 |
|------|------|
| `AfterSalesServiceOrder.cs` | 售后维修工单 CRUD、金额计算、状态流转、生成应收、CSV 导出 |
| `tools/erp-agent/tasks/done/009_Ver29_after_sales_service_work_order.md` | 任务归档 |

### 修改

| 文件 | 说明 |
|------|------|
| `Program.cs` | 模型、Receivable 扩展字段、数据文件、路由 |
| `Permissions.cs` | `after_sales.*` 权限键与权限组 |
| `OperationImpactService.cs` | 删除影响预检 `afterSalesServiceOrder`；应收关联维修单删除拦截 |
| `Business.html` | 列表/详情/编辑弹窗、配件明细、金额自动计算、dual-scroll |
| `App.html` | 售后管理菜单、页面注册、dual-scroll 映射 |
| `tools/erp-agent/scripts/check_ver29_pages.ps1` | 页面标记与 API 抽样 |
| `docs/worklog/ERP-Ver2.9-任务总表.md` | rc9 状态 |
| `docs/worklog/ERP-Ver2.9-执行台账.md` | 本轮记录 |

---

## 2. 模型与接口

### 主模型 `AfterSalesServiceOrder`

维修单号 `ServiceNo`（SRyyyyMMdd001）、客户/联系人/设备/故障、维修类型、派工、上门日期、维修结果、状态、费用字段、关联应收 `ReceivableId/ReceivableNo`、配件明细 `Parts`。

### 配件明细 `AfterSalesPartLine`

MaterialId/Code/Name/Spec/Unit/Quantity/UnitPrice/Amount/Remark。

### Receivable 扩展

新增 `ServiceOrderId`、`ServiceOrderNo`；`SourceType = "售后维修"` 用于手工生成应收。

### API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/after-sales-service-orders` | 列表 |
| GET | `/api/after-sales-service-orders/{id}` | 详情 |
| POST | `/api/after-sales-service-orders` | 新增 |
| PUT | `/api/after-sales-service-orders/{id}` | 修改 |
| DELETE | `/api/after-sales-service-orders/{id}` | 删除（草稿/已取消，无应收） |
| POST | `/api/after-sales-service-orders/{id}/confirm-dispatch` | 草稿→待派工 |
| POST | `/api/after-sales-service-orders/{id}/start` | 待派工→维修中 |
| POST | `/api/after-sales-service-orders/{id}/finish` | 维修中→已完成 |
| POST | `/api/after-sales-service-orders/{id}/settle` | 已完成→已结算 |
| POST | `/api/after-sales-service-orders/{id}/cancel` | 取消 |
| POST | `/api/after-sales-service-orders/{id}/generate-receivable` | 生成应收（不可重复） |
| GET | `/api/after-sales-service-orders/export` | 导出 CSV |

权限：`after_sales.view/add/edit/delete`。

---

## 3. 菜单

左侧新增一级分组 **售后管理**：

- 售后维修工单（页面键 `after-sales-service-order`）

---

## 4. 业务流程

1. **草稿**：登记客户、故障、派工、配件与费用；不扣库存、不生成应收。
2. **确认派工**：草稿 → 待派工（需派工人员）。
3. **开始维修**：待派工 → 维修中（可填上门日期）。
4. **完成维修**：维修中 → 已完成。
5. **生成应收**（可选）：已完成/已结算且应收合计 > 0 且无关联应收时，按钮生成应收款。
6. **结算**：已完成 → 已结算（已收=应收，未收=0）。

维修类型：保内免费 / 保外收费 / 只收人工 / 友情减免（通过优惠减免实现差异化收费）。

---

## 5. 金额计算

- 配件行金额 = 数量 × 单价（2 位小数）
- 配件费 `PartsAmount` = 配件明细合计
- 费用合计 = 配件费 + 人工费 + 其他费用
- 应收合计 `ReceivableAmount` = max(0, 费用合计 − 优惠减免)
- 未收 `UnreceivedAmount` = max(0, 应收合计 − 已收)
- 约束：优惠 ≤ 费用合计；已收 ≤ 应收；已结算时未收为 0

前后端均在保存时重算（`SyncAfterSalesServiceAmounts` / 前端 `asoRecalcSummaryFromForm`）。

---

## 6. 应收生成与关联

- 草稿/流转过程 **不** 自动生成应收。
- `generate-receivable`：写入应收，`SourceType=售后维修`，`ServiceOrderId/No` 双向关联。
- 已有 `ReceivableId` 时拒绝重复生成。
- 删除应收：关联维修单或 `SourceType=售后维修` 时禁止删除（与销售订单自动应收规则并列）。

未改动销售订单 `SyncAutoReceivableInMemory` 逻辑。

---

## 7. 库存

**本轮未接入。** 页面与报告均说明：售后配件出库/扣库存后续单独接入；确认维修单、完成维修均不调用库存核心逻辑。

---

## 8. 删除保护

- 前端：`runDeleteWithImpactCheck('afterSalesServiceOrder')` + `confirmDelete`
- 后端：仅草稿/已取消可删；已生成应收不可删；已完成/已结算不可删
- 影响预检：`ImpactAfterSalesServiceOrder`

---

## 9. build 结果

```
dotnet build ERP.csproj → 0 错误，0 警告
```

---

## 10. run_ver29_regression.ps1

```
Done: PASS
Summary: docs/worklog/reports/ver29_regression_summary_20260628_095705.md
```

售后维修页面标记与 API 抽样均 PASS。

---

## 11. Git 状态

- **未 commit**
- **未 push**
- **未 tag**
- 未同步 `D:\冠誉制造ERP\App`
- 未清空数据

---

## 12. 后续建议

1. 浏览器验收：新增工单、客户/物料下拉、金额联动、弹窗关闭、删除确认、生成应收防重复。
2. 打包 zip 给 ChatGPT 复核后再 commit/push。
3. 后续 rc：售后配件出库单、测试数据 Sheet、007 下一步按钮（维修→应收）。

---

## 13. 新增维修工单按钮无法打开修复

- **执行时间**: 2026-06-28 10:05
- **状态**: 已修复并验收

### 根因

售后维修模块（ASO）被放在 `Business.html` 第二个独立 IIFE 中，而 `asoToday()`、`asoFillForm()` 等依赖同文件第一个 IIFE 内的 `bizToday()`、`bizLoadRefs()`、`bizStr()` 等函数。点击「＋ 新增维修工单」触发 `asoOpenAdd` → `asoFillForm(null)` → `asoToday()` → `bizToday()` 时抛出 `ReferenceError: bizToday is not defined`，错误在 onclick 中被吞掉，表现为按钮无反应。列表仍能加载，因 `asoRender` 主要使用 `App.html` 全局的 `money`、`formatDateTime` 等。

### 修改文件

| 文件 | 说明 |
|------|------|
| `Business.html` | 将 ASO 代码并入第一个 IIFE；`loadAfterSalesServiceOrderPage` 增加 `wireAfterSalesServiceOrderModule()`；暴露 `window.asoOpenAdd`；`asoAdd` 按钮加 `type="button"` |

### 修复的函数/事件绑定

- **IIFE 作用域合并**：ASO 与生产工单共用同一闭包，可正常调用 `bizToday` / `bizLoadRefs` 等。
- **`wireAfterSalesServiceOrderModule()`**：在 `loadAfterSalesServiceOrderPage` 入口调用，确保进入页面时绑定 `#asoAdd.onclick = asoOpenAdd`。
- **`window.asoOpenAdd = asoOpenAdd`**：与 PWO 模块一致，便于调试与外部调用。

### 验收结果

| 项 | 结果 |
|----|------|
| 售后维修工单页面打开 | PASS |
| 点击「＋ 新增维修工单」打开弹窗 | PASS（标题「新增维修工单」） |
| X / 取消关闭弹窗 | PASS |
| 客户下拉、维修类型、费用字段、配件明细 | PASS |
| 保存草稿维修工单 | PASS（UI 保存 SR20260628001；API 冒烟亦 PASS） |
| 保存后列表刷新 | PASS |
| 删除草稿弹确认 | PASS（`erpConfirmModal` 弹出） |
| `dotnet build ERP.csproj` | 0 错误，0 警告 |
| `run_ver29_regression.ps1` | PASS（`ver29_regression_summary_20260628_100449.md`） |

### Git 状态

- **未 commit**
- **未 push**
- **未 tag**
- 测试记录 `AUTO_TEST` 已清理
