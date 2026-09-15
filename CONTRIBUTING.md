# 参与维护

感谢帮助维护 RevokeMsgPatcher。

## 问题反馈

请在 https://github.com/yzguo/RevokeMsgPatcher/issues 提交问题，并提供：

- 微信、QQ 或 TIM 的完整版本号。
- 报错中显示的目标文件名、文件版本和特征命中数。
- 使用的 RevokeMsgPatcher 版本。
- 是否安装过其他补丁，以及还原备份后能否复现。

请勿公开上传包含账号数据、聊天记录或其他隐私信息的文件。大型二进制文件请先与维护者联系。

## 提交补丁特征

1. 从最新 `master` 创建分支。
2. 在 `RevokeMsgPatcher.Assistant/JsonData.cs` 中维护补丁数据。
3. 同步生成对应版本的 `Data/<版本>/patch.json` 和主程序内嵌资源。
4. 在原始文件副本上验证 Search 唯一命中。
5. 验证安装后 Replace 唯一命中、重复安装被拦截、混合功能状态可补齐。
6. 验证 `.h.bak` 还原后与原文件字节一致。
7. 在 Pull Request 中写明应用版本、目标文件版本和测试结果。

## 联系方式

维护者：Yanzhen Guo（[@yzguo](https://github.com/yzguo)）

邮箱：yzhguo@mail.ustc.edu.cn
