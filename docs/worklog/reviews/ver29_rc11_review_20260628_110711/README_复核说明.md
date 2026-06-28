# Ver2.9-rc11 ChatGPT 复核包

- 未 commit / push / tag
- 含：售后维修操作记录、权限审计、操作记录页列表精简
- build: bin\rc11-regression-build-check 0 错误 0 警告
- 回归: run_ver29_regression.ps1 PASS (20260628_110630)

## 本轮操作记录页优化（rc11 追加）

1. 列表默认 6 列：时间 / 操作人 / 模块 / 操作 / 摘要 / 详情
2. SR 单号记录模块显示「售后维修工单」（含历史记录读取时归一化）
3. 摘要人话化，如「新增维修单 SR...，客户 XX，草稿，应收 100 元」

请重启 ERP 后验收操作记录页。
