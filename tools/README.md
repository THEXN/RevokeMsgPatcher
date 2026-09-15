# 维护工具

`PatchSmokeTest.cs` 是独立的二进制补丁冒烟测试。它不修改传入的原文件，而是把文件复制到指定工作目录后验证：

- 3.0 补丁数据、程序集版本和维护者元数据。
- 在线补丁地址仅指向 `yzguo/RevokeMsgPatcher`。
- 主程序不包含旧 Google Analytics 端点。
- QQNT 群聊/私聊功能开关。
- 原始特征唯一命中、安装后状态、重复安装拦截和混合状态补齐。
- 用原文件覆盖测试副本后的字节一致性。

编译时引用 Release 目录中的 `RevokeMsgPatcher.exe`、`Newtonsoft.Json.dll` 和 `System.Windows.Forms.dll`。运行参数：

```text
PatchSmokeTest.exe <patch.json> <原始 Weixin.dll> <原始 wrapper.node> <测试工作目录>
PatchSmokeTest.exe --metadata-only <patch.json>
```

请只使用合法取得的本机安装文件，勿把应用二进制提交到仓库。
