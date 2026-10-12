# 维护工具

`PatchSmokeTest.cs` 是独立的二进制补丁冒烟测试。它不修改传入的原文件，而是把文件复制到指定工作目录后验证：

- 3.1 补丁数据、程序集版本和维护者元数据。
- 在线补丁地址仅指向 `yzguo/RevokeMsgPatcher`。
- 主程序不包含旧 Google Analytics 端点。
- QQNT 群聊/私聊功能开关。
- 私聊布局变体在每个构建上唯一命中，且没有变体残留补丁痕迹。
- 原始特征唯一命中、安装后状态、重复安装拦截和混合状态补齐。
- 用原文件覆盖测试副本后的字节一致性。

编译时引用 Release 目录中的 `RevokeMsgPatcher.exe`、`Newtonsoft.Json.dll` 和 `System.Windows.Forms.dll`。运行参数：

```text
PatchSmokeTest.exe --metadata-only <patch.json>
PatchSmokeTest.exe <patch.json> <测试工作目录> [weixin=<原始 Weixin.dll>] [qqnt=<原始 wrapper.node>]...
```

`weixin=` 与 `qqnt=` 都可选，至少要给一个；只带 `qqnt=` 即可在没有微信安装文件的机器上单独验证 QQNT 防撤回。QQNT 私聊分支的指令宽度在构建之间来回变化，`qqnt=` 可以重复传入多个不同文件版本的 `wrapper.node` 副本，程序会按各自的文件版本选取特征组并逐一验证互斥变体解析（标签为 `QQNT1-<版本>`、`QQNT2-<版本>`……）。

请只使用合法取得的本机安装文件，勿把应用二进制提交到仓库。
