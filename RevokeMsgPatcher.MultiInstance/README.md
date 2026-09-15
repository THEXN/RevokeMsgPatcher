# RevokeMsgPatcher 微信多开工具

这是 RevokeMsgPatcher 3.0 附带的通用微信多开工具，由 Yanzhen Guo（[@yzguo](https://github.com/yzguo)）维护。

项目主页：https://github.com/yzguo/RevokeMsgPatcher

## 使用方法

自动模式适合一般用户：点击“启动多开”，然后依次启动微信。启动频率过快时可能失败。

手动模式提供两个操作：

- “关闭所有微信进程”：清理启动失败后残留的无界面进程。
- “清理所有微信互斥句柄”：清除微信用于限制多开的互斥标志；每启动一个新实例后可能需要再次执行。

如果已经通过主程序给微信安装了多开补丁，请勿重复使用本工具。

## 致谢

- 原项目作者：[huiyadanli](https://github.com/huiyadanli)
- [微信电脑端多开分析与原理](https://mp.weixin.qq.com/s/bb7XMxop7e8rd7YqQ88nyA)

## 许可证

本工具随主项目按 [GPL-3.0](../LICENSE) 发布。
