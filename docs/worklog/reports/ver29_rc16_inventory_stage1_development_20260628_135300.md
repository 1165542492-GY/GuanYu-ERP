# ERP Ver2.9-rc16：006 / 库存类型深化阶段 1 开发报告

生成时间：2026-06-28 13:53:00  
任务：`017_Ver29_rc16_006_inventory_stage1_development.md`  
基线：`dc91549` Ver2.9 baseline health snapshot  
用户规则确认：`ver29_rc16_inventory_rules_confirmed_20260628_133303.md`

---

## 1. 修改文件清单

| 文件 | 变更摘要 |
|------|----------|
| `Program.cs` | Material/StockItem 新字段；读取默认值；导入导出；BuildStockItems 展示 enrich |
| `Material.html` | 库存属性表单、列表列、筛选、详情 |
| `Business.html` | 库存汇总列、出入库库存类型带出、不足提醒确认、采购成品提示 |
| `TestDataService.cs` | 测试数据总表物料 Sheet 新列导入导出 |
| `check_ver29_pages.ps1` | rc16 页面标记 |
| `VERSION.md` / `安装说明.txt` | rc16 说明 |
| `docs/worklog/ERP-Ver2.9-任务总表.md` | §13 rc16 |
| `docs/worklog/ERP-Ver2.9-执行台账.md` | rc16 执行行 |

---

## 2. 新增字段（Material）

| 字段 | 默认值 |
|------|--------|
| StockType | 外购配件 |
| IsInventoryItem | true（JSON 缺省按 true） |
| IsFinishedGood | false |
| IsServicePart | false |
| SafetyStock | 0 |
| DefaultWarehouse | 默认仓库 |
| CostMethod | 固定成本价 |

库存类型枚举：原材料、外购配件、标准件、成品设备、维修备件、低值易耗品、其他  
成本方式枚举：固定成本价、最近采购价、机型成本快照、手动指定

---

## 3. 页面展示

| 页面 | 能力 |
|------|------|
| 物料管理 | 新增/编辑/详情/列表/库存类型与纳入库存筛选 |
| 库存汇总 | 库存类型、安全库存、库存状态（缺货/偏低/正常） |
| 采购入库 | 只读库存类型 + 成品设备提示（不拦截） |
| 销售出库 | 只读库存类型；不足提醒确认后仍可保存 |
| 生产领用 | 只读库存类型；不足提醒确认后仍可保存 |
| 成品入库 | 只读库存类型「成品设备」 |

---

## 4. 导入导出

- 物料 CSV 导出含 7 个新列；老模板缺列时使用默认值，不报错
- 测试数据总表「物料管理」Sheet 同步新列

---

## 5. 测试

| 项 | 结果 |
|----|------|
| dotnet build | 0 错误 0 警告（`bin/rc16-inventory-stage1-check`） |
| run_ver29_regression | **PASS** |
| API Health | PASS |
| Pages Check | PASS（含 rc16 标记） |
| 浏览器人工 | 待人工：8787 登录 admin 验收物料/库存/出入库表单 |

---

## 6. 未做 / 遗留（阶段 2+）

- BuildStockMap 聚合键仍按 ItemType+ItemId，**未**按类型/仓库拆分汇总
- 未做库存不足硬拦截、多仓库、批次、移动加权平均
- 未做售后维修扣库存
- 未改应收/应付/财务核心

---

## 7. 边界遵守

| 项 | 状态 |
|----|------|
| 同步正式 App | 否 |
| tag | 否 |
| commit/push | 否 |
| 清空数据 | 否 |

---

## 8. 人工验收建议

1. 物料管理 → 新增物料，选「原材料」，保存后列表可见
2. 编辑旧物料，缺字段应显示默认「外购配件」
3. 导出 CSV 含库存类型列；用旧模板导入应成功
4. 库存汇总查看库存状态列
5. 采购入库选手动物料且类型为成品设备 → 见橙色提示，仍可保存
