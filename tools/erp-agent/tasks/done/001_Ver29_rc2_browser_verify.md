# 任务标题

Ver2.9-rc2 浏览器验收准备

# 允许修改文件

不允许修改业务代码。

# 禁止动作

- 禁止清数据
- 禁止 commit
- 禁止 push
- 禁止同步正式 App（`D:\冠誉制造ERP\App`）
- 禁止修改稳定仓库（`C:\Users\Administrator\Documents\GuanYu-ERP-Stable`）
- 禁止删除、移动旧 tag

# 风险等级

R0/R1 — 只读检查与验收准备。

# 执行步骤

1. 运行 `dotnet build ERP.csproj`，确认 0 错误。
2. 停止旧 ERP 进程（如有）。
3. 启动开发版 ERP：`bin\Debug\net8.0-windows\冠誉制造ERP.exe`。
4. 检查 `http://127.0.0.1:8787` 是否返回登录页（HTTP 200）。
5. 检查 `http://127.0.0.1:8787/api/dashboard/owner-summary?range=month` 接口。
6. 检查 `http://127.0.0.1:8787/api/dashboard/owner-summary?range=all` 接口。
7. 输出验收报告。

# 验收要求

- build 成功，0 错误。
- ERP 正常启动，8787 端口可访问。
- owner-summary 接口返回 JSON（未登录可能 401，属正常；已登录环境应 200）。
- **人工浏览器验收**以下弹窗的自动带出与字段减负：
  - 销售订单
  - 销售出库
  - 采购单
  - 采购入库
  - 生产领用
  - 成品入库

# 输出报告要求

报告写入 `docs/worklog/reports/`，包含：

- 执行时间
- build 结果
- ERP 启动状态
- 接口检查结果
- 提醒用户逐项人工验收上述 6 类业务弹窗
- 风险等级与是否建议继续开发/提交
