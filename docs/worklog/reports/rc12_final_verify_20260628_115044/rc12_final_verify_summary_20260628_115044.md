# ERP Ver2.9-rc12 最终复核摘要

时间：20260628_115044

## 基线

- rc11 基线：ca1f4be
- 当前 HEAD：ca1f4be
- 最新日志：ca1f4be Ver2.9-rc11 audit permission and UI dialog polish

## 本轮边界

- 未同步正式 App
- 未 tag
- 未执行 006
- 未接库存
- 未清空数据
- 未 commit
- 未 push

## 已执行

1. 关闭旧 ERP 进程
2. dotnet build .\ERP.csproj
3. 启动新 Debug exe
4. 检查 http://127.0.0.1:8787
5. 检查页面标记：
   - erpHumanizeError
   - erpEmptyHtml
   - ERP_UI_MSG
   - erpTableEmptyHtml
   - erpShowDetailMessage
6. 执行 check_ver29_ui_dialogs.ps1
7. 执行 run_ver29_regression.ps1
8. 收集 git status / diff
9. 打包日志给 ChatGPT

## 日志包

C:\Users\Administrator\Desktop\ERP_Ver29_rc12最终复核日志给ChatGPT_20260628_115044.zip
