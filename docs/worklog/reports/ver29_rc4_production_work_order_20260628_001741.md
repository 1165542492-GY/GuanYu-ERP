# Ver2.9-rc4 生产工单基础模块报告

> **全系统设计原则**（Ver2.9 后续各 rc 统一遵守）：见 [`ERP-Ver2.9-系统设计原则.md`](../ERP-Ver2.9-系统设计原则.md)

| 项目 | 值 |
|------|-----|
| **时间** | 2026-06-28 00:17:41 |
| **阶段** | Ver2.9-rc4 生产任务/工单设计（005） |
| **build** | 0 错误 0 警告（输出到 `bin/rc4-build-check/`） |
| **commit / push / tag** | 均未执行 |

## 修改的源码文件

| 文件 | 改动摘要 |
|------|----------|
| `Program.cs` | 新增 `ProductionWorkOrder` 模型、JSON 存储、CRUD 与 start/finish/cancel 接口 |
| `Business.html` | 新增生产工单页面、列表/弹窗/统计卡片、前端状态操作 |
| `App.html` | 生产管理菜单新增「生产工单」、路由与权限映射 |
| `docs/worklog/ERP-Ver2.9-执行台账.md` | 追加 rc4 005 执行记录 |
| `tools/erp-agent/tasks/done/005_Ver29_rc4_production_work_order_design.md` | 任务完成归档 |

## 新增模型：ProductionWorkOrder

| 字段 | 说明 |
|------|------|
| Id | 主键 |
| WorkOrderNo | 工单号，自动生成 `WOyyyyMMdd001` |
| WorkOrderDate | 工单日期 |
| SourceType | 销售订单 / 手工 |
| SalesOrderId / SalesOrderNo | 关联销售订单 |
| CustomerName | 客户名称 |
| ProductName / Spec | 产品/机型、规格 |
| Quantity / Unit | 计划数量、单位（默认台） |
| BomId / BomName | 关联 BOM |
| ModelCostId / UnitCost | 机型成本、单台成本 |
| PlannedStartDate / PlannedFinishDate | 计划日期 |
| ActualStartDate / ActualFinishDate | 实际日期 |
| ProducedQuantity / UnproducedQuantity | 已完工 / 未完工 |
| PickedMaterialAmount / FinishedInboundAmount | 领料/入库金额（本轮仅字段占位，不自动写入） |
| Status | 草稿/待生产/生产中/部分完工/已完成/已取消 |
| Remark | 备注 |
| CreatedAt / UpdatedAt / CreatedBy / UpdatedBy | 审计字段 |

数据文件：`D:\冠誉制造ERP\Data\production-work-orders.json`（首次自动创建空数组 `[]`）

## 新增接口

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/production-work-orders` | 列表 |
| GET | `/api/production-work-orders/{id}` | 单条详情 |
| POST | `/api/production-work-orders` | 新增（自动生成 WorkOrderNo） |
| PUT | `/api/production-work-orders/{id}` | 编辑 |
| DELETE | `/api/production-work-orders/{id}` | 删除（仅草稿/已取消） |
| POST | `/api/production-work-orders/{id}/start` | 开始生产 |
| POST | `/api/production-work-orders/{id}/finish` | 登记本次完工数量 |
| POST | `/api/production-work-orders/{id}/cancel` | 取消 |

权限复用现有 `production_pick.view/add/edit/delete`（未改 `Permissions.cs`，避免扩大允许修改范围）。

## 新增页面与菜单

- **菜单位置**：生产管理 → **生产工单**（位于 BOM/机型成本之后，生产领用之前）
- **页面**：`productionWorkOrderView` / 路由 `production-work-order`
- **功能**：顶部说明、5 项统计卡片、搜索+状态筛选、列表、新增/编辑/详情/完工弹窗
- **详情弹窗**：完整字段 +「前往生产领用」「前往成品入库」入口按钮（仅跳转，不自动联动）

## 状态流转规则

```
草稿 ──start──► 生产中 ──finish(部分)──► 部分完工 ──finish(满额)──► 已完成
  │                │                           │
  │                └──── finish(一次满额) ──────┘
  │
待生产 ──start──► 生产中

草稿 / 待生产 / 生产中 / 部分完工 ──cancel──► 已取消
已完成 ──不可 cancel──►
草稿 / 已取消 ──delete──► 物理删除
其他状态 ──不可 delete──►
```

- **start**：草稿或待生产 → 生产中，写入 `ActualStartDate`
- **finish**：生产中或部分完工，累加 `ProducedQuantity`，重算 `UnproducedQuantity`；未满为「部分完工」，达到计划数量为「已完成」并写 `ActualFinishDate`
- **cancel**：除已完成外均可取消，数据保留
- **edit**：草稿/待生产/生产中/部分完工可编辑；生产中及之后不可改核心数量/BOM/成本

## 本轮未做的高风险联动

- 不自动扣减库存
- 不自动生成生产领用单
- 不自动生成成品入库单
- 不修改销售订单、BOM、机型成本、生产领用、成品入库、库存汇总、应收应付核心逻辑
- 不做 MRP / 排产 / 006 库存类型深化
- 未改 `Permissions.cs`（独立权限项留待后续 rc）

## build 结果

```
dotnet build ERP.csproj -o bin\rc4-build-check
已成功生成。0 个警告，0 个错误
```

## 人工验收建议

1. 重启开发版 ERP，浏览器 Ctrl+F5 刷新。
2. 打开 **生产管理 → 生产工单**，确认菜单与统计卡片显示正常。
3. **手工新增** 1 条工单：填写产品、数量，保存后检查工单号格式 `WOyyyyMMdd001`。
4. **从销售订单带出**：来源类型选销售订单，选单后客户/产品/数量/BOM/成本应自动填充。
5. **开始生产**：草稿/待生产工单点击「开始生产」，状态变为生产中，实际开始日期有值。
6. **完工登记**：输入部分数量，状态为部分完工；再登记剩余数量，状态为已完成。
7. **取消 / 删除**：非已完成可取消；仅草稿/已取消可删除。
8. 详情弹窗点击「前往生产领用」「前往成品入库」能跳转对应页面。
9. 确认 **生产领用、成品入库、库存汇总** 等旧模块仍可正常使用。

## 005 任务状态

- 已移动至 `tools/erp-agent/tasks/done/005_Ver29_rc4_production_work_order_design.md`
- **未 commit、未 push、未 tag**

---

## 补充轮（2026-06-28 00:37:10）

详见 `docs/worklog/reports/ver29_rc4_production_work_order_fix_20260628_003710.md`。

### 简化业务流程设计原则

| # | 原则 |
|---|------|
| 1 | 工人少填字段，能自动带出的尽量自动带出 |
| 2 | 生产工单只负责需要车间生产/装配的产品 |
| 3 | 只卖配件/物料/现货：**销售订单 → 销售出库 → 应收款**（不走生产工单） |
| 4 | 车间生产整机：**销售订单 → 生产工单 → 生产领用/成品入库 → 销售出库 → 应收款** |
| 5 | 上门维修（后续）：**维修工单 → 维修配件出库 → 收费 → 应收款**（005 不实现） |
| 6 | 不做复杂 MRP、排产、多级工序 |
| 7 | 只记录关键状态与关键金额 |
| 8 | 页面提示用通俗语言 |
| 9 | 005 不执行 006、不自动扣库存/领用/入库 |
| 10 | 生产、销售、维修分模块，避免混成一个复杂流程 |

### 补充改动摘要

- `CustomerId` 字段 + 客户下拉选择
- 页面顶部四条业务边界与工人操作说明
- 销售订单下拉区分成品/物料；按钮与弹窗 id 修复

### 全系统原则文档

用户后续补充的 Ver2.9 全系统设计原则（不限于 005）已写入：

**[`docs/worklog/ERP-Ver2.9-系统设计原则.md`](../ERP-Ver2.9-系统设计原则.md)**
