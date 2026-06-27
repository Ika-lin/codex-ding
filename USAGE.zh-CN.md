# Codex Peek 使用说明

Codex Peek 是一个轻量 Windows 托盘软件。它会在 Codex 某个对话回复完成时，在桌面角落弹出一张 emoji 图片并播放一声提示音。

## 1. 启动

双击：

```text
Run Codex Peek.bat
```

或者直接双击：

```text
CodexPeek.exe
```

启动后，软件会出现在 Windows 右下角托盘区域。

## 2. 测试提示

双击：

```text
Test Peek.bat
```

这只会测试图片和声音，不代表自动监听逻辑。

也可以右键托盘图标，选择：

```text
Test Peek
```

## 3. 更换 emoji 图片

把 PNG 图片放进：

```text
emojis
```

然后右键托盘图标：

```text
Settings... -> Image -> PNG
```

选择你想用的图片，点 `Save`。

推荐使用透明背景 PNG，显示效果最好。

## 4. 更换提示音

把音效文件放进：

```text
sounds
```

然后右键托盘图标：

```text
Settings... -> Sound -> Audio
```

选择你想用的音效，点 `Save`。

推荐使用 WAV。MP3 也能播放，但可能有轻微启动延迟。

## 5. 调整位置和大小

右键托盘图标：

```text
Settings...
```

可以调整：

- `Position`: 弹出位置
- `Size`: 图片大小
- `Duration`: 显示时长
- `Offset X`: 距离屏幕左右边缘
- `Offset Y`: 距离屏幕上下边缘
- `Play sound`: 是否播放声音

点 `Test` 可以预览，点 `Save` 保存。

## 6. 暂停提醒

右键托盘图标：

```text
Pause Notifications
```

暂停后不会自动弹出。再次点击：

```text
Resume Notifications
```

即可恢复。

## 7. 开机自启

右键托盘图标：

```text
Start with Windows
```

会添加开机自启。取消自启：

```text
Remove Startup
```

## 8. 软件文件夹结构

```text
CodexPeek.exe        主程序
CodexPeek.ini        配置文件
emojis/              你可以放 emoji 图片
sounds/              你可以放提示音
assets/              软件默认资源
tools/               构建和打包脚本
README.md            英文说明
USAGE.zh-CN.md       中文使用说明
```

## 9. 隐私说明

Codex Peek 只读取本地 Codex 会话日志，用来判断回复是否完成。它不会上传聊天内容，也不会在运行时访问网络。

## 10. 已知限制

它依赖 Codex 本地日志格式。如果未来 Codex 更新日志格式，可能需要更新 Codex Peek 的检测逻辑。
