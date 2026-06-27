# Ver2.9-rc6 顶部横向滚动条（dual-scroll）回归修复报告

| 项目 | 值 |
|------|-----|
| **时间** | 2026-06-28 02:28:27 |
| **阶段** | Ver2.9-rc6 — dual-scroll 超大模式回归 |
| **build** | 0 错误 0 警告（`-o bin/rc6-build-check/`）；默认 `bin/Debug` 因运行中 exe 文件锁未能覆盖 |
| **commit / push / tag** | 均未执行 |

## 回归现象

在 **显示大小 = 超大（xlarge）** 模式下，多个宽表格页面顶部横向滚动条消失或不可用，用户需滚到表格底部才能左右移动。用户点名页面：

1. 供应商管理
2. 客户管理
3. 物料管理
4. BOM 表
5. 机型成本
6. 生产工单

业务页（销售订单等）因 `buildBizView()` 布局错误，dual-scroll 被挤入 `.tools` flex 行，加剧顶部滚动条异常。

## 根因分析

| # | 原因 | 影响 |
|---|------|------|
| 1 | **xlarge CSS 仅修复首页 `#dashReceivableScrollTop`**，未覆盖全局 `.dual-scroll .table-scroll-top` | 超大模式下 flex 子项高度被压为 0，顶部滚动条不可见 |
| 2 | **`PAGE_DUAL_SCROLL` 缺少 `bom`、`model-cost` 映射** | 切页/刷新时 `refreshPageDualScroll` 跳过 BOM / 机型成本 |
| 3 | **`DUAL_SCROLL_IDS` 缺少 `pwoDualScroll`** | `refreshAllDualScroll` 可能漏绑生产工单 |
| 4 | **`buildBizView()` DOM 错误**（见 layout 报告） | 业务页 dual-scroll 容器布局异常 |
| 5 | 部分 render 仅用 `refreshAllDualScroll` 而非按页 `refreshPageDualScroll` | 数据刷新后绑定时机不够精确 |

## 修复内容

### 修改文件

| 文件 | 改动 |
|------|------|
| `App.html` | 全局 xlarge `.dual-scroll .table-scroll-top` CSS；`PAGE_DUAL_SCROLL` 补 `bom` / `model-cost`；`DUAL_SCROLL_IDS` 补 `pwoDualScroll`；`#bomDualScroll` 等 overflow 约束；supplier/bom/model-cost render 后调用 `refreshPageDualScroll` |
| `Business.html` | `buildBizView()` DOM 修复（dual-scroll 移出 `.tools`）；工具栏 CSS（与 layout 报告共用） |

未改 `Customer.html` / `Material.html`（已有正确容器与 `refreshPageDualScroll` 调用）。

### PAGE_DUAL_SCROLL 补充映射

| 页面 key | dual-scroll id |
|----------|----------------|
| `bom` | `bomDualScroll` |
| `model-cost` | `modelCostDualScroll` |

已有映射确认保留：`supplier` → `supplierDualScroll`，`customer` → `customerDualScroll`，`material` → `materialDualScroll`，`production-work-order` → `pwoDualScroll`，以及全部业务页（`soDualScroll` … `stockDualScroll` 等）。

### DUAL_SCROLL_IDS 补充

- 新增：`pwoDualScroll`（生产工单）

### xlarge CSS（关键）

将原先仅针对 `#dashReceivableScrollTop` 的规则，扩展为：

```css
html[data-size="xlarge"] .dual-scroll .table-scroll-top {
  flex: 0 0 var(--hscroll-track) !important;
  height / min-height / max-height: var(--hscroll-track) !important;
  display: block !important;
  overflow-x: scroll !important;
  …
}
```

确保所有页面在超大模式下顶部滚动轨道保留固定高度且可横向滚动。

### 生产工单 dual-scroll

- HTML：`buildProductionWorkOrderView()` 已有 `#pwoDualScroll` 结构（正确）
- 映射：`PAGE_DUAL_SCROLL['production-work-order']` + `DUAL_SCROLL_IDS` 均已包含 `pwoDualScroll`
- 渲染：`pwoRender()` 末尾已调用 `refreshPageDualScroll('production-work-order')`

## 统一机制说明（未重写，仅完善）

沿用 Ver2.7/2.8 机制：

- `bindDualScroll(id)` — 顶部/底部 scrollLeft 双向同步
- `refreshPageDualScroll(page)` — 按 `PAGE_DUAL_SCROLL` 重绑
- `scheduleDualScrollRefresh()` — 切页、密度、显示大小变更时调度
- `applyDisplaySize()` — xlarge 时额外延迟多次 refresh

## 保留项确认

| 项 | 状态 |
|----|------|
| 007 下一步按钮 | 保留 |
| 删除保护确认弹窗 | 未改动 |
| rc3 表格显示优化 | 未改动 |
| 业务 / 库存 / 应收应付逻辑 | 未改动 |

## 验收清单（待人工浏览器 — 超大模式）

### 重点 6 页

| 页面 | 顶部滚动条 | 顶↔底同步 | 标准→大→超大 |
|------|-----------|-----------|--------------|
| 供应商管理 | 待验 | 待验 | 待验 |
| 客户管理 | 待验 | 待验 | 待验 |
| 物料管理 | 待验 | 待验 | 待验 |
| BOM 表 | 待验 | 待验 | 待验 |
| 机型成本 | 待验 | 待验 | 待验 |
| 生产工单 | 待验 | 待验 | 待验 |

### 快速抽查 8 页

销售订单、销售出库、采购单、采购入库、生产领用、成品入库、应收款、应付款 — 同上五项。

### 每页确认项

1. 顶部横向滚动条存在且不遮挡搜索框/按钮/下一步按钮
2. 拖动顶部滚动条，表格左右移动
3. 拖动表格底部横向滚动条，顶部同步
4. 切换显示大小后仍正常
5. 筛选区、按钮区布局不变形

**操作建议**：关闭运行中的 ERP.exe → `dotnet build ERP.csproj` → 启动后 **Ctrl+F5** 强刷 → 个性化设置选「超大」逐页验证。

## build 结果

```
dotnet build ERP.csproj -o bin/rc6-build-check
→ 0 错误 0 警告
```

说明：直接 build 到 `bin/Debug/net8.0-windows/` 时因 `冠誉制造ERP.exe (PID 1992)` 占用导致 MSB3027 文件复制失败；编译本身无代码错误。

## 未执行项

- 未 commit / push / tag
- 未同步 `D:\冠誉制造ERP\App`
- 未执行 006
- 未清空数据
