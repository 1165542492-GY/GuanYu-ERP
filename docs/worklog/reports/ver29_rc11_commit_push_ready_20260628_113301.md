# ERP Ver2.9-rc11 commit/push ready report

时间：20260628_113301

## 本轮内容

- 售后维修操作记录、权限回归与审计收口
- 操作记录页面默认 8 列精简
- IP、UserAgent、长 ID、JSON 放入详情弹窗
- 售后维修 SR 记录模块名归一为“售后维修工单”
- 售后审计摘要改为人话
- 全系统 Toast、API 错误、空数据、权限、409、网络错误提示轻量统一
- ERP.csproj 排除 docs/**、tools/**，避免 review 副本 .cs 被误编译

## 验证

- dotnet build .\ERP.csproj：已通过
- http://127.0.0.1:8787：首页 200
- 页面标记：erpHumanizeError / erpEmptyHtml / ERP_UI_MSG 已加载
- run_ver29_regression.ps1：已通过
- 未同步正式 App
- 未 tag
- 未执行 006
- 未接库存
- 未清空数据

## 提交信息

Ver2.9-rc11 audit permission and UI dialog polish
