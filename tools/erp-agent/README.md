# ERP Agent 自动任务工作台

Ver2.9 开发阶段的任务队列、安全检查与执行框架。

## 目录结构

```
tools/erp-agent/
├── README.md              # 本文件：任务格式说明
├── run_safe_check.ps1     # 一键安全检查
├── run_task_loop.ps1      # 任务循环执行
├── tasks/
│   ├── inbox/             # 待执行任务（按文件名排序）
│   ├── running/           # 正在执行
│   ├── done/              # 已完成
│   └── failed/            # 失败
└── logs/                  # 脚本运行日志
```

## 任务文件格式

每个任务放在 `tools/erp-agent/tasks/inbox/`，使用 Markdown 格式。

### 文件名示例

```
001_Ver29_rc2_verify.md
002_Ver29_rc2_fix.md
003_Ver29_rc3_table_ui.md
```

- 建议使用三位数字前缀保证排序。
- 文件名应简短描述任务内容。

### 必须包含的章节

任务文件**必须**包含以下一级标题（`#`）：

```markdown
# 任务标题

简要描述本任务要做什么。

# 允许修改文件

列出允许改动的文件路径，每行一个。若不允许改代码，写「无」或「不允许修改业务代码」。

# 禁止动作

列出本任务额外禁止的操作，例如：禁止 commit、禁止清数据。

# 风险等级

R0 / R1 / R2 / R3 / R4 之一或组合。参见 docs/worklog/ERP-Ver2.9-风险清单.md。

# 执行步骤

1. 第一步
2. 第二步
...

# 验收要求

描述怎样算任务完成，包括 build、接口、浏览器验收等。

# 输出报告要求

描述报告应包含哪些内容，输出到哪里。
```

### 完整示例

参见 `tasks/inbox/001_Ver29_rc2_browser_verify.md`。

## 脚本说明

| 脚本 | 用途 |
|------|------|
| `run_safe_check.ps1` | git 状态、build、启动 ERP、接口检查、生成报告 |
| `run_task_loop.ps1` | 从 inbox 取任务 → 安全检查 → 生成执行提示 → 可选 Cursor CLI → 再检查 → 更新台账 |

## 风险拦截

两个脚本均内置危险命令黑名单，禁止：

- git add / commit / push / reset / clean / push --force
- 删除或操作 `D:\冠誉制造ERP\Data`、`App`、`Backups`
- robocopy 到正式 App
- 清空数据、删除 tag、数据迁移等

`run_task_loop.ps1` 扫描任务时会**自动忽略** `# 禁止动作` 章节，以及含「禁止 / 不允许 / 不要 / 不得」的说明行，避免把任务约束误判为可执行命令。仅当任务正文（执行步骤等）明确要求危险操作时才会拦截。

详见 `docs/worklog/ERP-Ver2.9-风险清单.md`。

## 快速开始

```powershell
cd C:\Users\Administrator\Documents\ERP

# 仅安全检查
powershell -ExecutionPolicy Bypass -File ".\tools\erp-agent\run_safe_check.ps1"

# 处理 inbox 中第一个任务
powershell -ExecutionPolicy Bypass -File ".\tools\erp-agent\run_task_loop.ps1"
```
