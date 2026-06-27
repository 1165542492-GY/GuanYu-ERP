# Ver2.9-rc4 生产工单来源字段锁定补丁报告

| 项目 | 值 |
|------|-----|
| **时间** | 2026-06-28 00:48:31 |
| **阶段** | Ver2.9-rc4 005 来源数据一致性修复 |
| **build** | 0 错误 0 警告（`bin/rc4-build-check/`） |
| **commit / push / tag** | 均未执行 |

## 问题

新增/编辑弹窗选择「来源 BOM」后，系统已带出 BOM 名称，但「产品/机型名称」「规格型号」仍可手改，导致工单与 BOM 不一致，影响后续生产、成本、库存理解。

## 修改文件

| 文件 | 改动 |
|------|------|
| `Business.html` | 来源字段锁定逻辑、`pwoRefreshSourceLocks()`、带出函数强制覆盖、弹窗提示、清空来源时恢复可编辑 |
| `Program.cs` | 保存时按 SalesOrderId / BomId / ModelCostId 重新解析并覆盖来源字段，不信任前端手填 |

## 字段锁定规则（前端）

| 条件 | 锁定字段 |
|------|----------|
| 来源类型=销售订单 **且** 已选 SalesOrderId | 客户、产品/机型名称、规格型号、计划数量、单位 |
| 已选 BomId | BOM 名称、产品/机型名称、规格型号（BOM 优先覆盖产品/规格） |
| 已选 ModelCostId | 单台成本；若无关联 BOM，还锁定产品/机型名称、规格型号 |
| 清空 BomId / ModelCostId / 销售订单 | 在无更高优先级来源时，恢复对应字段可编辑 |

弹窗提示（`pwoSourceLockHint`）：

> 选择销售订单 / BOM / 机型成本后，相关字段由系统带出并锁定，避免数据不一致。

## 手工工单何时允许手填

同时满足以下条件时，**产品/机型名称、规格型号、数量、单位** 可手填：

- `SourceType = 手工`
- 未选 `SalesOrderId`
- 未选 `BomId`
- 未选 `ModelCostId`

客户仍必须从客户下拉选择（不允许手输不存在客户）。

## 后端兜底（保存时）

解析顺序：**销售订单 → 客户 → 机型成本 → BOM**（BOM 最后覆盖产品/规格）。

| 来源 ID | 后端行为 |
|---------|----------|
| `SalesOrderId` | 查销售订单，覆盖 CustomerId/Name、ProductName、Quantity、Unit(台)、BomId、ModelCostId |
| `ModelCostId` | 查机型成本，覆盖 UnitCost；可带出 BomId；无 BOM 时覆盖 ProductName/Spec |
| `BomId` | 查 BOM，覆盖 BomName、ProductName、Spec；不存在则报错 |

**不信任**前端提交的 ProductName / Spec / UnitCost 等与来源冲突的值。

**旧数据兼容**：仅 GET 列表/详情不受影响；编辑保存时若 BOM/机型成本 ID 已失效会提示不存在，不会因字段缺失导致列表无法打开。

## 未做事项

- 未执行 006、未自动扣库存、未自动生成领用/入库
- 未实现售后维修模块
- 未 commit / push / tag
- 未同步 `D:\冠誉制造ERP\App`
- 未清空数据

## build 结果

```
dotnet build ERP.csproj -o bin\rc4-build-check
已成功生成。0 个警告，0 个错误
```

## 人工验收建议

1. 手工新增：只选 BOM → 产品/规格只读且与 BOM 一致，手改无效（保存后仍为 BOM 值）。
2. 选销售订单 → 客户/产品/数量/单位锁定；若订单带 BOM，产品/规格以 BOM 为准。
3. 选机型成本 → 单台成本锁定；有关联 BOM 时产品/规格随 BOM。
4. 清空 BOM（手工工单）→ 产品/规格恢复可编辑。
5. 用开发者工具篡改 POST  body 中的 ProductName，保存后后端仍按 BOM/订单纠正。
