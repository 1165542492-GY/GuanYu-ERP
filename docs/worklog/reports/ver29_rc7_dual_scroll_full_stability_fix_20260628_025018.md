# Ver2.9-rc7 dual-scroll 全页面稳定性修复报告

| 项目 | 值 |
|------|-----|
| **时间** | 2026-06-28 02:50:18 |
| **阶段** | UI 回归 — 超大/大显示模式顶部横向滚动条 |
| **build** | 0 错误 0 警告（`-o bin/rc7-dual-scroll-fix-check/`）；`bin/Debug` 因运行中 ERP.exe (PID 292) 锁定未能覆盖 |
| **commit / push / tag** | 均未执行 |

---

## 回归现象

用户在继续验收时发现：部分宽表格页面在 **显示大小 = 超大** 模式下，顶部横向滚动条再次消失或不可用。上轮 rc6 已修复一部分，但覆盖不全、刷新时机不稳定。

---

## 根因分析

| # | 原因 | 影响 |
|---|------|------|
| 1 | **`isDualScrollVisible` 过严**：要求 root `height>0`，页面刚切换/数据未渲染时判为不可见，绑定被跳过 | 部分页面 `data-dual-pending` 后未重绑 |
| 2 | **仅依赖 `PAGE_DUAL_SCROLL` 映射 ID**，未扫描当前可见页面内全部 `.dual-scroll` | 映射遗漏或 DOM 重建后漏绑 |
| 3 | **`openPage` / 异步 `bizLoadPage` 刷新过早** | 表格隐藏或宽度为 0 时计算 scrollWidth，后续不再刷新 |
| 4 | **xlarge CSS 仅覆盖 `.table-scroll-top`**，未约束 `.dual-scroll` 容器本身；**large 模式无同等兜底** | flex 子项在超大/大模式下高度被压为 0 |
| 5 | **`BIZ_SCROLL_PAGES` 缺少 `production-work-order`** | 生产工单页刷新路径与其他业务页不一致 |
| 6 | **`bindDualScroll` 只认 `.tablewrap`** | BOM 明细等 `.batch-tablewrap` 容器无法绑定 |

---

## 修复内容

### 修改文件

| 文件 | 改动摘要 |
|------|----------|
| `App.html` | 统一 dual-scroll 机制增强；xlarge/large CSS 兜底；`openPage` 延迟刷新 |
| `Business.html` | `BIZ_SCROLL_PAGES` 补生产工单；`bizLoadPage`/渲染后刷新；业务页 CSS |
| `Contract.html` | 列表渲染后 large/xlarge 延迟二次刷新 |
| `tools/erp-agent/scripts/check_ver29_pages.ps1` | 标记更新为 `refreshAllVisibleDualScrolls` |

未改：`Customer.html`、`Material.html`（已有 `refreshPageDualScroll` 调用，由增强后的统一机制覆盖）。

### 新增/增强 JS 机制（App.html）

| 函数 | 作用 |
|------|------|
| `refreshAllVisibleDualScrolls(scope)` | 扫描当前可见页面内所有 `.dual-scroll` 并重绑 |
| `isDualScrollShown(el)` | 沿 DOM 链检查祖先是否隐藏 |
| `findDualScrollWrap(root)` | 支持 `.tablewrap` / `.batch-tablewrap` / `.table-wrap` |
| `initPageDualScroll(page)` | 对外别名，等同 `refreshPageDualScroll` |
| `DUAL_SCROLL_REFRESH_DELAYS` | 统一延迟刷新序列 `[0,50,120,150,350,650,900,1200]` |

**增强点：**

- `refreshPageDualScroll`：映射 ID + 可见页面全扫描
- `bindDualScroll`：放宽可见性判断；ResizeObserver + 多段 setTimeout 重算宽度
- `applyDisplaySize`：large 与 xlarge 均触发延迟全量刷新
- `openPage`：`requestAnimationFrame` + 延迟队列，等 DOM 显示后再绑

### 映射补齐

| 页面 key | dual-scroll id | 说明 |
|----------|----------------|------|
| （已有全部保留） | supplier … ctDualScroll 等 | 无 key 变更 |
| `production-work-order` | `pwoDualScroll` | 加入 `BIZ_SCROLL_PAGES` |
| `warehouse-stock` | `stockDualScroll` | 异步 load 完成后显式 refresh |
| `backupListDualScroll` | 数据备份列表 | 加入 `DUAL_SCROLL_IDS` |

### CSS 兜底（xlarge + large）

- `.dual-scroll` 容器：`display:flex; width:100%; overflow:hidden; flex-shrink:0`
- `.table-scroll-top`：固定 `--hscroll-track` 高度，`overflow-x:auto`，`opacity:1`
- `.table-scroll-inner`：`min-width:100%`
- `.panel > .dual-scroll`：在 large/xlarge 下保证宽度与 flex 布局

### 刷新时机

1. `openPage()` — rAF + 延迟队列
2. `applyDisplaySize()` — large/xlarge 多段延迟
3. `bizLoadPage()` / `bizRender()` / `pwoRender()` / `renderStockList()` — 数据加载后
4. `renderCsList()` / `renderCtList()` — 合同页 large/xlarge 二次延迟
5. 各列表 render（supplier/bom/model-cost/customer/material）— 沿用 `refreshPageDualScroll`

---

## 重点验收页面（待人工浏览器 — 超大模式）

| # | 页面 | 自动标记检查 | 人工待验 |
|---|------|-------------|----------|
| 1 | 供应商管理 | supplierDualScroll | 顶↔底同步 |
| 2 | 客户管理 | customerDualScroll | 同上 |
| 3 | 物料管理 | materialDualScroll | 同上 |
| 4 | BOM 表 | bomDualScroll | 同上 |
| 5 | 机型成本 | modelCostDualScroll | 同上 |
| 6 | 生产工单 | pwoDualScroll | 同上 |
| 7 | 销售订单 | soDualScroll | 同上 |
| 8 | 销售出库 | soutDualScroll | 同上 |
| 9 | 采购单 | poDualScroll | 同上 |
| 10 | 采购入库 | pinDualScroll | 同上 |
| 11 | 生产领用 | ppDualScroll | 同上 |
| 12 | 成品入库 | finDualScroll | 同上 |
| 13 | 库存汇总 | stockDualScroll | 同上 |
| 14 | 应收款 | recDualScroll | 同上 |
| 15 | 应付款 | payDualScroll | 同上 |
| 16 | 合同 | ctDualScroll | 同上 |
| 17 | 合同资料 | csDualScroll | 同上 |

每页确认：超大模式下顶部横滚条存在 → 拖动顶栏/表体双向同步 → 标准/大/超大切换正常 → 切页再回来正常 → 搜索框/下一步按钮不变形。

---

## 008 回归脚本

- **保留**：`run_ver29_regression.ps1` 及全部 008 脚本未删除
- **微调**：`check_ver29_pages.ps1` 将 dual-scroll 标记改为 `refreshAllVisibleDualScrolls`（新统一函数名）

---

## 保留项确认

| 项 | 状态 |
|----|------|
| 007 下一步按钮 | 未改动 |
| 删除保护 / 影响预检 | 未改动 |
| rc3 表格显示优化 | 未改动 |
| 业务 / 库存 / 财务计算逻辑 | 未改动 |

---

## 安全声明

- 未 commit / 未 push / 未 tag
- 未同步 `D:\冠誉制造ERP\App`
- 未清空数据

---

## 操作建议

1. 关闭运行中的开发版 `冠誉制造ERP.exe`
2. `dotnet build ERP.csproj`（或继续用 `bin/rc7-dual-scroll-fix-check` 验证）
3. 启动 ERP → 浏览器 **Ctrl+F5** 强刷
4. 显示大小选「超大」，按上表 17 页逐页验收
5. 验收通过后再与 008 回归脚本一并 commit
