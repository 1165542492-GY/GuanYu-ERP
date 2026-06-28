# ERP Ver2.9-rc11 防闪退最终复核摘要

时间：20260628_112234

## 边界确认

- 未同步正式 App
- 未 tag
- 未执行 006
- 未接库存
- 未 commit
- 未 push

## 执行内容

1. 关闭旧 ERP 进程
2. dotnet build .\ERP.csproj
3. 如 Debug 构建失败，尝试 alt 输出目录构建
4. 启动 Debug exe
5. 检查 http://127.0.0.1:8787
6. 检查 erpHumanizeError / erpEmptyHtml 标记
7. 执行 run_ver29_regression.ps1
8. 收集 git status

## 日志包

C:\Users\Administrator\Documents\ERP\docs\worklog\reviews\ERP_Ver29_rc11防闪退最终复核日志给ChatGPT_20260628_112234.zip
