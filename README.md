<p align="center">
  <img width="100" src="Images/logo.png" alt="RevokeMsgPatcher logo">
</p>

# RevokeMsgPatcher 3.0

[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)
[![Release](https://img.shields.io/github/v/release/yzguo/RevokeMsgPatcher)](https://github.com/yzguo/RevokeMsgPatcher/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/yzguo/RevokeMsgPatcher/total.svg)](https://github.com/yzguo/RevokeMsgPatcher/releases)
[![License](https://img.shields.io/github/license/yzguo/RevokeMsgPatcher)](LICENSE)

Windows 下的微信、QQ、TIM 防撤回与微信多开工具。3.0 由
[Yanzhen Guo (@yzguo)](https://github.com/yzguo) 继续维护。

- 项目主页：https://github.com/yzguo/RevokeMsgPatcher
- 问题反馈：https://github.com/yzguo/RevokeMsgPatcher/issues
- 联系邮箱：yzhguo@mail.ustc.edu.cn
- 最新版本：https://github.com/yzguo/RevokeMsgPatcher/releases/latest

> 本项目会修改应用程序安装目录中的二进制文件。使用前请完全退出目标应用，确认备份可用，并自行承担兼容性风险。软件仅供学习与技术交流，请遵守所在地法律法规及相关软件服务条款。

## 主要功能

| 应用 | 目标文件 | 功能 |
| --- | --- | --- |
| 微信 3.x | `WeChatWin.dll` | 防撤回、多开 |
| 微信 4.x | `Weixin.dll` | 防撤回、多开，可独立选择 |
| QQ / TIM | `IM.dll` | 防撤回 |
| QQNT | `wrapper.node` | 群聊防撤回、私聊防撤回，可独立选择 |

3.0 同时提供：

- 每条特征独立校验，支持部分功能已安装时补齐其他功能。
- 特征命中异常时拒绝写入，并显示具体命中数。
- 安装前自动创建 `.h.bak` 备份，支持界面还原。
- 从本仓库获取 `3.0/patch.json` 补丁数据。
- 不收集或发送使用统计数据。

## 支持范围

3.0 已针对以下文件完成二进制副本测试：

- Weixin `4.1.13.12`。
- QQ `9.9.33-52230`，`wrapper.node` 文件版本 `9.8.0.23961`。

补丁使用版本范围和通配特征兼容后续小版本，但无法保证任意未来版本。出现“特征比对失败”时请停止操作，并在
[Issues](https://github.com/yzguo/RevokeMsgPatcher/issues) 中附上应用版本、目标文件版本和完整错误信息；不要强制套用旧特征。

## 使用方法

1. 从 [Releases](https://github.com/yzguo/RevokeMsgPatcher/releases/latest) 下载完整 ZIP 并解压，勿只复制 EXE。
2. 安装 .NET Framework 4.5.2 或更高版本。
3. 完全退出微信、QQ 或 TIM。
4. 以管理员身份运行 `RevokeMsgPatcher.exe`。
5. 选择应用和安装路径，勾选需要的功能，点击“安装所选功能”。
6. 如需恢复，使用“备份还原”按钮恢复 `.h.bak` 文件。

应用升级后通常需要重新安装补丁。杀毒软件可能因二进制修改行为报警，请只从本仓库 Release 下载，并使用发布页提供的 SHA-256 校验文件。

## 工作原理

程序优先使用文件版本和 SHA-1 进行精确匹配；没有精确记录时，使用支持通配字节的特征模式查找修改位置。每条模式必须处于以下两种唯一状态之一：

- 原始特征命中一次、替换特征未命中：功能尚未安装。
- 原始特征未命中、替换特征命中一次：功能已经安装。

任何重复命中、同时命中或完全不命中都会中止写入。详细实现见 `Matcher/FuzzyMatcher.cs`、`Matcher/ModifyFinder.cs` 和 `Modifier/AppModifier.cs`。

## 开发与补丁数据

- 技术栈：C#、WinForms、.NET Framework 4.5.2。
- 主解决方案：`RevokeMsgPatcher.sln`。
- 补丁数据源：`RevokeMsgPatcher.Assistant/JsonData.cs`。
- 3.0 在线补丁：`RevokeMsgPatcher.Assistant/Data/3.0/patch.json`。
- 依赖：Newtonsoft.Json 13.0.3。

提交兼容性修复前，请至少验证原始特征唯一命中、替换后特征唯一命中、重复安装拦截及备份还原。参与方式见 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 维护者

当前维护者：**Yanzhen Guo**（[@yzguo](https://github.com/yzguo)）

联系邮箱：**yzhguo@mail.ustc.edu.cn**

## 致谢与项目来源

RevokeMsgPatcher 3.0 基于 [huiyadanli/RevokeMsgPatcher](https://github.com/huiyadanli/RevokeMsgPatcher) 继续开发。感谢原作者 **huiyadanli** 创建并长期维护本项目，也感谢历代贡献者留下的代码、文档与补丁数据。历史版权和贡献归各自作者所有，详见 [NOTICE.md](NOTICE.md) 与 Git 提交历史。

还要感谢以下项目和贡献来源：

- [wechat_anti_revoke](https://github.com/36huo/wechat_anti_revoke)
- [LiteLoaderQQNT](https://github.com/LiteLoaderQQNT/LiteLoaderQQNT)
- [QQNTFileVerifyPatch](https://github.com/LiteLoaderQQNT/QQNTFileVerifyPatch)
- [LiteLoaderQQNT-Anti-Recall](https://github.com/xh321/LiteLoaderQQNT-Anti-Recall)
- [NTQQAntiRecall](https://github.com/NapNeko/NTQQAntiRecall)
- [BetterWX](https://github.com/zetaloop/BetterWX)

## 许可证

本项目继续使用 [GNU General Public License v3.0](LICENSE)。分发修改版时必须保留许可证、版权声明、项目来源和修改说明。
