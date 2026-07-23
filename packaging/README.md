# BabyToys 使用说明

BabyToys 是 Windows 儿童模式防误触工具，用于减少孩子拍打键盘、鼠标造成的误操作，并降低屏幕吸引力。它不是安全锁屏软件。

## 系统要求

- 64 位 Windows 10 或 Windows 11。
- 发布包已经包含运行环境，不需要另外安装 .NET。

## 开始使用

1. 将 ZIP 完整解压到一个固定文件夹。
2. 双击 `BabyToys.exe`。
3. 设置儿童模式图片、持续时间和是否显示倒计时。
4. 点击“进入儿童模式”后会立即显示低刺激黑色确认界面，在 3 秒倒计时结束前可点击取消或按 `Esc`。

关闭主窗口后，BabyToys 会继续在系统托盘运行。需要完全退出时，请右键托盘图标并选择“退出”。

默认全局快捷键为 `Ctrl + Alt + B`，可在设置中关闭。快捷键会直接显示低刺激确认界面，不会先闪现主窗口。

## 解锁与紧急恢复

- 正常解锁：长按 `Ctrl + Alt + U` 3 秒，看到“松开即可解锁”后松开按键。
- 紧急恢复：按 `Ctrl + Alt + Del`，打开并切换到任务管理器；BabyToys 检测到任务管理器后会释放输入拦截和遮罩。
- 倒计时结束后，BabyToys 会优先请求系统睡眠；唤醒后继续保持黑屏和输入拦截，直到家长正常解锁。请求失败时也会保留黑屏并允许解锁。

BabyToys 不能阻止 `Ctrl + Alt + Del`、UAC 安全桌面、硬件电源键或所有系统级快捷键。

## 配置、日志与卸载

- 配置文件：`%LocalAppData%\BabyToys\settings.json`
- 日志目录：`%LocalAppData%\BabyToys\logs`
- 日志不记录具体按键或输入内容。

卸载前先从托盘完全退出。如果启用了开机自启，请先在设置中关闭，然后删除程序文件夹。若也要清除个人配置，可删除 `%LocalAppData%\BabyToys`。

## 校验下载文件

Release 同时提供 `.sha256` 文件。可在 PowerShell 中运行：

```powershell
Get-FileHash .\BabyToys-v*-win-x64.zip -Algorithm SHA256
```

结果应与 `.sha256` 文件中的值一致。
