# ERP Ver2.9 自动工作台说明

## 1. 这个工作台是干啥的？

ERP 自动任务工作台是一套**辅助开发、验收、检查**的框架，专门为 Ver2.9 大版本的多阶段任务服务。

它帮你做这些事：

- **排队**：把待办任务放进 `tools/erp-agent/tasks/inbox/`，按文件名顺序自动处理。
- **检查**：每轮任务前后自动跑 build、启动 ERP、检查接口，生成报告。
- **拦截风险**：内置黑名单，禁止自动清空数据、commit、push、同步正式 App 等危险操作。
- **留痕**：执行结果写入 `docs/worklog/ERP-Ver2.9-执行台账.md` 和 `docs/worklog/reports/`。

**它不会代替你在 Cursor 里写代码**，而是帮你把「检查 → 执行提示 → 再检查 → 记录」这套流程标准化。

---

## 2. 用户以后怎么放任务？

1. 在 `tools/erp-agent/tasks/inbox/` 新建 `.md` 任务文件。
2. 文件名建议带序号前缀，例如：`001_Ver29_rc2_verify.md`、`002_Ver29_rc3_table_ui.md`。
3. 任务文件必须包含以下章节（详见 `tools/erp-agent/README.md`）：
   - `# 任务标题`
   - `# 允许修改文件`
   - `# 禁止动作`
   - `# 风险等级`
   - `# 执行步骤`
   - `# 验收要求`
   - `# 输出报告要求`

任务会按**文件名排序**依次处理，处理完移到 `done/`，失败移到 `failed/`。

---

## 3. 怎么一键运行？

### 仅做安全检查（推荐日常使用）

```powershell
cd C:\Users\Administrator\Documents\ERP
powershell -ExecutionPolicy Bypass -File ".\tools\erp-agent\run_safe_check.ps1"
```

### 跑任务循环（从 inbox 取一个任务）

```powershell
cd C:\Users\Administrator\Documents\ERP
powershell -ExecutionPolicy Bypass -File ".\tools\erp-agent\run_task_loop.ps1"
```

可选参数：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `-MaxRounds` | 1 | 最多处理几个任务 |
| `-LoopDelaySeconds` | 10 | 每轮之间的等待秒数 |

示例：连续处理 3 个任务，间隔 15 秒：

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\erp-agent\run_task_loop.ps1" -MaxRounds 3 -LoopDelaySeconds 15
```

---

## 4. 报告在哪里看？

| 类型 | 路径 |
|------|------|
| 安全检查报告 | `docs/worklog/reports/ver29_safe_check_yyyyMMdd_HHmmss.md` |
| 当前待执行任务提示 | `docs/worklog/reports/current_task_prompt.md` |
| 任务失败报告 | `docs/worklog/reports/task_failed_yyyyMMdd_HHmmss.md` |
| 执行台账 | `docs/worklog/ERP-Ver2.9-执行台账.md` |
| 任务总表 | `docs/worklog/ERP-Ver2.9-任务总表.md` |
| 风险清单 | `docs/worklog/ERP-Ver2.9-风险清单.md` |
| 脚本日志 | `tools/erp-agent/logs/` |

---

## 5. 哪些事情它绝对不会自动做？

以下操作被**硬编码禁止**，脚本和任务循环都不会执行：

- 清空数据、删除 `D:\冠誉制造ERP\Data` / `App` / `Backups`
- `git add`、`git commit`、`git push`、`git reset`、`git clean`、`git push --force`
- 删除或移动 git tag
- 用 robocopy 等方式同步到 `D:\冠誉制造ERP\App`
- 恢复备份、数据迁移、深度初始化
- 自动点击鼠标、自动点 Cursor Run 按钮

**提交代码、发布正式版、清数据——这些永远需要你本人决定并手动操作。**

---

## 6. 出错后怎么停？

1. **build 失败**：`run_safe_check.ps1` 会立即停止并写报告，不会启动 ERP。
2. **ERP 启动失败**：写报告后标记失败，任务循环会把任务移到 `failed/`。
3. **接口检查失败**：报告标记「需修复」，建议先修再跑下一任务。
4. **手动停止任务循环**：在 PowerShell 窗口按 `Ctrl+C`。
5. **清理卡住的任务**：若任务留在 `running/`，可手动移回 `inbox/` 或移到 `failed/`。

---

## 7. 高风险任务为什么必须人工确认？

Ver2.9 涉及库存、应收应付、财务流水等**真实业务数据**。一旦自动执行出错，可能造成：

- 库存数量错误
- 财务账目不一致
- 正式环境被误覆盖

因此：

- **R0/R1**：可以自动检查和小改 UI。
- **R2**：自动准备 + 你必须在浏览器里验收。
- **R3**：改后端业务逻辑前，必须你确认方案并验收。
- **R4**：任何 destructive 操作，只允许你本人手动执行。

**误拦截说明：** 任务文件 `# 禁止动作` 章节里写的「禁止 git push」等约束，不会被当成要执行的危险命令；脚本只扫描执行步骤等正文，真正要求执行危险操作时才会拦截。

工作台的设计原则是：**能自动检查的自动检查，能自动报告的自动报告，涉及数据和发布的永远人工把关。**
