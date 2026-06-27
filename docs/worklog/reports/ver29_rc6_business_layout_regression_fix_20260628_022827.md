# Ver2.9-rc6 Business 布局回归修复报告

| 项目 | 值 |
|------|-----|
| **时间** | 2026-06-28 02:28:27 |
| **阶段** | Ver2.9-rc6 007 / 布局补丁 — 业务页工具栏回归 |
| **build** | 0 错误 0 警告（`-o bin/rc6-build-check/`）；默认 `bin/Debug` 因运行中 exe 文件锁未能覆盖 |
| **commit / push / tag** | 均未执行 |

## 回归现象

007 期间 `Business.html` 的 `buildBizView()` 工具栏 HTML 结构错误，导致 8 个业务页（销售订单、销售出库、采购单、采购入库、生产领用、成品入库、应收款、应付款）出现：

- 搜索框被挤成竖条
- 重置按钮变窄
- 批量/新增/导入/导出按钮堆叠在左侧
- 表格区域被横向 dual-scroll 块挤占
- 顶部横向滚动条位置异常或不可见

## 根因

`buildBizView()` 在 `.tools` 内多嵌套了一层 `<div>`，且 **未在 dual-scroll 之前闭合 `.tools`**。`.tools` 使用 `display:flex; justify-content:space-between`，导致 `#xxxDualScroll` 成为 flex 第三子项，与左侧搜索区、右侧按钮区并排，破坏整页布局。

错误结构（简化）：

```html
<div class="tools">
  <div class="lefttools">…</div>
  <div>
    <div><button id="xxxAdd">…</button></div>
  </div>
  <div class="dual-scroll" id="xxxDualScroll">…</div>  <!-- 仍在 .tools 内 -->
</div>
```

## 修复内容

### 修改文件

| 文件 | 改动 |
|------|------|
| `Business.html` | 修复 `buildBizView()` DOM：`.tools` 正确闭合后再输出 `dual-scroll`；右侧按钮区使用 `righttools`；补充工具栏 flex 样式防止搜索框/按钮区变形 |

### DOM 修正后结构

```html
<div class="panel">
  <div class="tools">
    <div class="lefttools">搜索 + 重置</div>
    <div class="righttools">批量/导入/导出 + 新增 + 007 下一步按钮</div>
  </div>
  <div class="dual-scroll" id="xxxDualScroll">…</div>
  <div class="foot">…</div>
</div>
```

与 `App.html` 供应商页、`buildProductionWorkOrderView()`、`buildStockView()` 结构一致。

### 新增 CSS（Business.html）

- `.panel > .tools` 对齐与换行
- `.righttools` 右对齐 flex 容器
- `.lefttools` / `.search` 最小/最大宽度，避免超大模式下搜索框被压扁

## 保留项确认

| 项 | 状态 |
|----|------|
| 007 下一步按钮（`NextFin` / `NextOut` / 行级 `bizNextBtn`） | 保留 |
| 全系统删除保护（`confirmDelete` / `runDeleteWithImpactCheck` 等） | 未改动 |
| rc3 表格紧凑显示（`biz-table-compact`） | 未改动 |
| 库存 / 应收应付 / 业务计算逻辑 | 未改动 |

## 验收说明（待人工浏览器确认）

在 **标准 / 大 / 超大** 显示模式下，逐页检查：

1. 销售订单、销售出库、采购单、采购入库、生产领用、成品入库、应收款、应付款
2. 搜索框宽度正常；重置按钮正常
3. 操作按钮在工具栏右侧；表格在工具栏下方
4. 顶部横向滚动条在表格上方（与 dual-scroll 修复联动）
5. 007 下一步按钮仍可见且可点击

**操作建议**：关闭运行中的 `冠誉制造ERP.exe` 后重新 build，浏览器 **Ctrl+F5** 强刷。

## 未执行项

- 未 commit / push / tag
- 未同步 `D:\冠誉制造ERP\App`
- 未执行 006 库存类型深化
- 未清空数据
