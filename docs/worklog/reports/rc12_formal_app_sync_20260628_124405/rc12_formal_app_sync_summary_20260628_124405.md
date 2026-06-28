# ERP Ver2.9-rc12 正式 App 同步报告

时间：20260628_124405

## 同步结论

ERP Ver2.9-rc12 已同步到正式 App。

## 当前版本

- 预期 HEAD：e61326a
- 实际 HEAD：e61326a
- 正式 App 目录：D:\冠誉制造ERP\App
- 正式 Data 目录：D:\冠誉制造ERP\Data

## 备份

- App 备份 zip：D:\冠誉制造ERP\Backups\App_backup_before_rc12_20260628_124405.zip
- Data 备份 zip：D:\冠誉制造ERP\Backups\Data_backup_before_rc12_20260628_124405.zip
- 旧 App 移动目录：D:\冠誉制造ERP\App_before_rc12_20260628_124405

## 已执行

1. 关闭旧 ERP 进程
2. git/head 状态检查
3. dotnet build
4. dotnet publish Release
5. 备份正式 App
6. 备份正式 Data
7. 移动旧正式 App
8. 复制 rc12 publish 到正式 App
9. 启动正式 App
10. 检查 http://127.0.0.1:8787
11. 检查 rc12 页面标记
12. 执行 run_ver29_regression.ps1

## 边界确认

- 未 tag
- 未执行 006
- 未接库存
- 未清空数据
- 未 commit
- 未 push

## 后续

请在浏览器访问：

http://127.0.0.1:8787

按 Ctrl + F5 强刷，再人工看首页、操作记录、售后维修工单、测试数据导入导出。
