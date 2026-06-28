# ERP Ver2.9-rc12 commit/push ready report

时间：20260628_115246

## 本轮内容

- 全系统人机对话界面浏览器验收与补漏
- 新增 erpTableEmptyHtml：区分无数据与筛选无结果
- 新增 erpShowDetailMessage：用统一弹窗替代导入/批量明细 alert
- 供应商、客户、物料、合同、业务、财务页面空态和提示补漏
- 新增 check_ver29_ui_dialogs.ps1
- check_ver29_pages.ps1 增加 rc12 标记

## 验证

- dotnet build .\ERP.csproj：已通过
- http://127.0.0.1:8787：首页 200
- 页面标记已加载：
  - erpHumanizeError
  - erpEmptyHtml
  - ERP_UI_MSG
  - erpTableEmptyHtml
  - erpShowDetailMessage
- check_ver29_ui_dialogs.ps1：PASS/WARN，仅兜底 window.confirm
- run_ver29_regression.ps1：PASS
- 未同步正式 App
- 未 tag
- 未执行 006
- 未接库存
- 未清空数据

## 提交信息

Ver2.9-rc12 UI dialog browser polish
