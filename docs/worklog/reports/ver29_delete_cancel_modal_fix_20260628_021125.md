# Ver2.9 删除/影响预检弹窗「取消按钮无效」修复报告

**时间：** 2026-06-28 02:11:25  
**任务：** 修复全局删除/影响预检弹窗取消按钮无效问题  
**优先级：** 高（危险操作交互）

---

## 1. 问题原因

根因有两层，叠加导致用户只能点「确认继续」：

### 1.1 CSS 任务锁禁用取消按钮（主因）

删除流程调用 `runActionOnce(..., '删除中...')`，内部通过 `erpUiLock.runTask()` 给 `body` 加上 `erp-ui-task-locked`。

`App.html` 原有 CSS：

```css
body.erp-ui-task-locked .modal.show [id$="Cancel"] {
  pointer-events: none !important;
  opacity: .45;
}
```

影响预检弹窗 `#erpConfirmModal` 的取消按钮 id 为 `#erpConfirmCancel`，匹配 `[id$="Cancel"]`，在「删除中…」任务锁期间 **pointer-events 被禁用**，用户无法点击取消。

典型链路：

1. 用户点「删除」
2. `runActionOnce` → `runTask('删除中...')` → 显示底部「删除中…」横幅
3. 调用 `/api/operation-impact/preview` 预检
4. `confirmAction()` 弹出 `#erpConfirmModal`
5. **取消按钮已被 CSS 禁用** → 只能点「确认继续」

### 1.2 任务锁时机过早（次因）

`runActionOnce` 在整段异步流程（含预检 API + 确认弹窗）外层就加任务锁，导致：

- 预检阶段就显示「删除中…」
- 确认弹窗处于 task-locked 上下文

### 1.3 其他小问题

- `#erpConfirmModal` 无右上角关闭 X
- 全局 Esc 监听器拦截所有 `.modal.show` 的 Esc，但未关闭弹窗；与确认弹窗 Esc 取消需求冲突
- `confirmOperationImpact` 在 `needConfirm=false` 且无影响明细时直接 `return true`，可能跳过用户确认（已收紧：有影响明细/警告时仍弹窗）

---

## 2. 修改了哪些文件

| 文件 | 变更类型 |
|------|----------|
| `App.html` | 核心修复：CSS、公共函数、确认弹窗 HTML |
| `Business.html` | `bizDelete` 非预检路径、`deleteArapDetail` 使用 `runBusyTask` |
| `Finance.html` | `deleteFinance` 使用 `runBusyTask` |

未修改：`Program.cs`、库存/应收应付/财务核心逻辑、删除业务规则。

---

## 3. 修复了哪些公共函数或页面函数

### 3.1 公共函数（`App.html`）

| 函数 | 修复内容 |
|------|----------|
| **CSS** | `erp-ui-task-locked` 规则排除 `#erpConfirmModal`；确认弹窗 `z-index:70` |
| **`confirmAction`** | 重写：X 关闭、Esc 取消、事件 listener 清理、防重复 settle、`type="button"` |
| **`runActionOnce`** | 移除自动 `runTask(busyText)` 包裹；busy 锁改由调用方/`runWithOperationImpactCheck` 在确认后执行 |
| **`runBusyTask`** | 新增：仅在确认后执行 DELETE 时显示「删除中…」 |
| **`runWithOperationImpactCheck`** | 用户确认后才 `runTask('删除中...')` 执行 `runFn` |
| **`confirmOperationImpact`** | 有影响明细或 warnings 时强制弹窗，不因 `needConfirm=false` 自动继续 |
| **全局 Esc 监听** | `#erpConfirmModal.show` 时不拦截，交由 `confirmAction` 处理 Esc=取消 |

### 3.2 页面函数

| 函数 | 文件 | 修复内容 |
|------|------|----------|
| `bizDelete` | Business.html | 非 impact 模块路径：确认后用 `runBusyTask` 执行 DELETE |
| `deleteArapDetail` | Business.html | 预检通过后 `runBusyTask('删除中...')` 再 DELETE |
| `deleteFinance` | Finance.html | 确认后用 `runBusyTask` 执行 DELETE |

### 3.3 自动覆盖的模块（通过公共函数，无需逐页改代码）

以下删除/预检均走 `runWithOperationImpactCheck` + `confirmAction`，已全局生效：

- 销售订单 / 销售出库 / 采购单 / 采购入库
- 生产领用 / 成品入库 / 应收款 / 应付款
- 供应商 / 客户 / 物料 / BOM / 机型成本 / 合同 / 合同资料
- 字典、子账号、备份恢复等同类流程

批量删除（`runSubmitOnce` + `deleteConfirm`）在 CSS 排除 `#erpConfirmModal` 后，取消按钮同样可点击。

---

## 4. 取消按钮如何保证不触发删除

1. **CSS**：`#erpConfirmModal` 不受 `erp-ui-task-locked` 取消禁用规则影响，取消/X 始终可点。
2. **`confirmAction`**：`onCancel` / X / Esc 均调用 `done(false)`，仅 `resolve(false)`，不调用 `onConfirm`、不触发 DELETE。
3. **`runWithOperationImpactCheck`**：`confirmOperationImpact` 返回 `false` 时记录 cancel 日志并 `return false`，**不执行** `runFn`（DELETE API）。
4. **`runActionOnce`**：预检与弹窗阶段无 task lock；「删除中…」仅在用户点「确认继续」后的 `runTask` 内出现。
5. **按钮类型**：`#erpConfirmCancel`、`#erpConfirmOk`、`#erpConfirmClose` 均为 `type="button"`，避免 form 默认提交。

---

## 5. 哪些页面已验证

### 5.1 代码路径审查（全模块）

已确认上述公共函数被所有列出模块引用；销售出库走 `bizDelete` → `runWithOperationImpactCheck('salesOutbound', ...)` → `confirmAction`。

### 5.2 建议人工验收（按任务要求）

请在以下页面各测一遍：**删除 → 取消 / X / 确认继续**（勿用重要正式数据，可建 AUTO_TEST 临时记录）：

| 页面 | 验证点 |
|------|--------|
| **销售出库** | 影响预检弹窗：取消关闭、不 DELETE；X 等同取消；确认后才删除 |
| **客户或供应商** | 同上（供应商/客户 impact 预检 + 批量删除 cancel） |
| **生产工单** | 原生 `window.confirm` 删除（无 impact 弹窗）；其他生产模块（领用/入库）走 impact 预检 |

> 注：本次会话完成代码修复与 build；UI 点击验收需在本机运行 ERP 后按上表执行。

---

## 6. Build 结果

```
dotnet build ERP.csproj -o bin/delete-cancel-fix-check

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
| 清空数据 | **未执行** |
| 同步 D:\冠誉制造ERP\App | **未执行** |
| 执行 006 | **未执行** |
| 修改库存/应收应付/财务核心计算 | **未修改** |
| 改变删除业务规则 | **未改变**（仅弹窗/锁时机/交互） |

---

## 8. 修复摘要

删除/影响预检弹窗取消无效，是因为「删除中…」任务锁 CSS 误伤了 `#erpConfirmCancel`。通过排除确认弹窗、将 busy 锁推迟到用户确认之后、并完善 `confirmAction`（X / Esc / 事件清理），取消与关闭行为现已与「不执行 DELETE」一致。
