# ERP Ver2.9 当前基线健康快照

时间：20260628_131217

## 一、当前基线

- 分支：codex/ver2.8-basic-business-framework
- HEAD：bd59179 Ver2.9-rc13 safe observe archive
- 正式 App：D:\冠誉制造ERP\App\冠誉制造ERP.exe
- 本机访问：http://127.0.0.1:8787

## 二、检查结果

- Git 分支正确
- HEAD 正确
- 工作区干净
- 无 ahead
- 正式 App 存在
- 正式 App 本机 HTTP 200
- rc12 / rc13 页面标记已加载
- dotnet build 已执行
- run_ver29_regression.ps1 已执行

## 三、当前稳定状态

当前 Ver2.9 基线稳定。

当前节点可作为后续开发起点。

## 四、边界确认

- 未改代码
- 未同步正式 App
- 未 tag
- 未执行 006
- 未接库存
- 未清空数据
- 未 commit
- 未 push
- 未管第二台电脑

## 五、后续建议

下一步可进入 Ver2.9 后续规划，但以下事项仍需用户单独确认：

1. tag
2. 006 库存类型深化
3. 库存核心计算深化
4. 应收 / 应付 / 财务核心计算调整
5. 同步稳定仓库
6. 清空数据 / 恢复备份
