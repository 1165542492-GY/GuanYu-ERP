# Ver2.9 全系统删除保护机制 + 取消按钮修复报告

**时间：** 2026-06-28 02:14:26  
**范围：** 删除 / 批量删除 / 影响预检删除的统一二次确认与取消行为  
**约束：** 未 commit / push / tag；未改库存/应收应付/财务核心逻辑；未改删除业务规则

---

## 1. 删除保护机制规则（全系统统一）

### 1.1 全局原则

| # | 规则 |
|---|------|
| 1 | 所有单条删除必须二次确认（`confirmDelete` 或影响预检弹窗） |
| 2 | 所有批量删除必须二次确认，并显示将删除条数（`confirmBatchDelete`） |
| 3 | 带影响范围的删除：先预检 → 弹窗展示影响 → 用户点「确认继续」后才 DELETE |
| 4 | 未点「确认删除 / 确认继续」前，禁止调用 DELETE 接口 |
| 5 | 点「取消」：关闭弹窗，不删除 |
| 6 | 点右上角 X：等同取消 |
| 7 | Esc：等同取消（`#erpConfirmModal` 专用处理） |
| 8 | 点击弹窗外遮罩：不关闭、不确认（全局拦截 backdrop click） |
| 9 | 「删除中…」仅在用户确认后、实际 DELETE 执行期间显示 |
| 10 | 删除过程中确认按钮禁用，防止重复点击 |

### 1.2 公共函数（`App.html`）

| 函数 | 用途 |
|------|------|
| `confirmDelete(opts)` | 单条删除确认；支持 `code` / `objectLabel` / `message`；按钮「确认删除」 |
| `confirmBatchDelete(opts)` | 批量删除确认；未选记录时提示「请先选择要删除的记录」；按钮「确认批量删除」 |
| `showImpactPreviewModal(preview, opts)` | 影响预检弹窗（`confirmOperationImpact` 别名） |
| `closeImpactPreviewModal()` | 强制关闭影响预检/确认弹窗 |
| `runDeleteWithImpactCheck(module, id, opts, deleteFn)` | 预检 → 确认 → `runBusyTask('删除中...')` → DELETE |
| `runBatchDeleteOnce(actionKey, opts, deleteFn)` | 批量：确认 → busy 锁 → DELETE |
| `runBusyTask(reason, fn)` | 仅在确认后加任务锁并执行 API |
| `deleteConfirm` | 兼容别名，指向 `confirmDelete` |

### 1.3 影响预检删除流程

```
点击删除
  → previewOperationImpact (API)
  → confirmOperationImpact（删除操作强制弹窗，禁止预检后自动删除）
  → 用户点「确认继续」
  → runBusyTask('删除中...')
  → DELETE API
  → 刷新列表
```

取消 / X / Esc 在任意确认阶段均 `return false`，记录 cancel 日志（影响预检路径），不调用 DELETE。

### 1.4 取消按钮修复原因（延续上一轮）

**根因：** `body.erp-ui-task-locked` 的 CSS 规则 `[id$="Cancel"] { pointer-events: none }` 误伤了 `#erpConfirmCancel`，在「删除中…」任务锁期间取消按钮不可点。

**修复：**

1. CSS 排除 `#erpConfirmModal`
2. `runActionOnce` 不再在整段流程外包 `runTask`；busy 锁推迟到确认之后
3. `confirmAction` 增加 X、Esc、事件清理、`type="button"`
4. 删除类操作统一走 `runDeleteWithImpactCheck` / `runBatchDeleteOnce`

---

## 2. 修改文件清单

| 文件 | 变更摘要 |
|------|----------|
| `App.html` | 公共删除保护 API；CSS/confirmAction 修复；供应商/字典/BOM/机型成本/子账号删除统一 |
| `Business.html` | `bizDelete`、批量删除、应收明细删除、生产工单 `pwoDelete` |
| `Customer.html` | 单条/批量删除 |
| `Material.html` | 单条/批量删除 |
| `Finance.html` | 财务收支删除（移除原生 `confirm` 回退） |
| `Contract.html` | 合同 / 合同资料删除 |

---

## 3. 已覆盖页面/模块

### 3.1 单条删除（影响预检或 confirmDelete）

| 模块 | 入口函数 | 保护方式 |
|------|----------|----------|
| 供应商 | `deleteItem` | `runDeleteWithImpactCheck` |
| 客户 | `deleteCustomer` | 同上 |
| 物料 | `deleteMaterial` | 同上 |
| 销售订单 | `bizDelete` | 同上 |
| 销售出库 | `bizDelete` | 同上 |
| 采购单 | `bizDelete` | 同上 |
| 采购入库 | `bizDelete` | 同上 |
| 生产领用 | `bizDelete` | 同上 |
| 成品入库 | `bizDelete` | 同上 |
| 应收款 / 应付款 | `bizDelete` | 同上 |
| 生产工单 | `pwoDelete` | `confirmDelete` + `runBusyTask`（原 `window.confirm` 已替换） |
| 应收明细 | `deleteArapDetail` | 收款：`runDeleteWithImpactCheck`；付款：`confirmDelete` |
| BOM | `deleteBom` | `runDeleteWithImpactCheck` |
| 机型成本 | `deleteModelCost` | 同上 |
| 合同 | `deleteCt` | 同上 |
| 合同资料 | `deleteCs` | 同上 |
| 财务收支 | `deleteFinance` | `confirmDelete` |
| 字典项 | `deleteDictItem` | `runDeleteWithImpactCheck` |
| 子账号 | `deleteUserAccount` | 同上 |

### 3.2 批量删除

| 模块 | 入口 | 保护方式 |
|------|------|----------|
| 供应商 | `supplierBatchDelBtn` | `runBatchDeleteOnce` |
| 客户 | `customerBatchDelBtn` | 同上 |
| 物料 | `materialBatchDelBtn` | 同上 |
| 销售订单等业务页 | `BatchDelBtn`（`wireBizBatchModule`） | 同上 |

未选记录时：`confirmBatchDelete` → `tip('请先选择要删除的记录')`。

### 3.3 高风险操作（本轮未重构，已有独立保护）

- 清空测试数据 / 清空全部业务数据 / 深度初始化：独立 modal + 二次密码 + 确认文字
- 合同作废 `voidCt`：影响预检（非 DELETE，保持现状）
- 数据恢复：影响预检 + 二次确认文字

---

## 4. 已实际验证

| 类型 | 状态 |
|------|------|
| `dotnet build ERP.csproj` | **0 错误 0 警告** |
| 代码路径审查 | 上述全部模块已接入公共函数 |
| UI 手工点击 | **需在本机 ERP 中执行**（见下表） |

建议手工验收（勿用重要正式数据，可用 AUTO_TEST 临时记录）：

| 页面 | 验收项 |
|------|--------|
| **销售出库** | 删除 → 影响预检弹窗 → 取消/X 不删 → 确认继续才删 |
| **客户或供应商** | 同上（单条 + 可选批量） |
| **生产工单** | 删除 → `#erpConfirmModal` → 取消/X 不删 → 确认删除才删 |
| **批量删除**（如客户） | 未选提示；选中后弹窗；取消不删；确认批量删除才删 |

---

## 5. 后续可继续统一的页面

| 项目 | 说明 |
|------|------|
| `pwoStartWo` / `pwoCancelWo` | 仍用 `window.confirm`，非删除操作，可后续改为 `confirmAction` |
| 保存为「已确认」时的 `window.confirm`（Business.html） | 非删除，可后续统一为影响预检弹窗 |
| 个性化设置「关闭删除确认」 | 保留开关；关闭后跳过弹窗（管理员自行承担风险） |
| 高风险清空/初始化 | 已有二次密码；本轮未强行重构 |

---

## 6. Build 结果

```
dotnet build ERP.csproj -o bin/delete-protection-check

已成功生成。
    0 个警告
    0 个错误
```

---

## 7. 约束确认

| 项 | 状态 |
|----|------|
| commit | **未执行** |
| push | **未执行** |
| tag | **未执行** |
| 同步 D:\冠誉制造ERP\App | **未执行** |
| 执行 006 | **未执行** |
| 清空数据 | **未执行** |
| 修改库存/应收应付/财务核心计算 | **未修改** |
| 改变删除业务规则 | **未改变** |

---

## 8. 相关报告

- 取消按钮专项修复：`docs/worklog/reports/ver29_delete_cancel_modal_fix_20260628_021125.md`
