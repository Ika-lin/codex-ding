# Codex Peek

Codex Peek is a lightweight Windows tray app that shows a tiny desktop peek when a Codex conversation finishes.

It runs locally, watches Codex session files, and only reacts after an assistant final answer is completed.

## What It Does

- Shows a floating PNG emoji/image when Codex finishes replying.
- Plays a short sound effect with the visual cue.
- Uses `FileSystemWatcher`, so it does not poll every minute.
- Runs as a small tray app.
- Keeps everything local; it does not upload or send Codex content anywhere.

## Run

Double-click:

```text
CodexPeek.exe
```

Or:

```text
Run Codex Peek.bat
```

The app will appear in the Windows tray.

## Tray Menu

- `Settings...`: change image, sound, size, position, duration, and offsets.
- `Test Peek`: show the notification once.
- `Pause Notifications`: stop automatic notifications temporarily.
- `Scan Latest`: manually inspect the newest Codex session.
- `Reload Config`: reload `CodexPeek.ini`.
- `Start with Windows`: add a startup shortcut.
- `Remove Startup`: remove the startup shortcut.
- `Open Folder`: open this app folder.
- `Exit`: quit Codex Peek.

## Configure

You can use the Settings window or edit `CodexPeek.ini`:

```ini
iconPath=assets/distorted-face.png
watchPath=%USERPROFILE%\.codex\sessions
position=bottom-right
size=44
durationMs=1800
offsetX=28
offsetY=56
soundEnabled=true
soundPath=assets/pop.mp3
```

Use transparent PNG files for images. WAV sounds start fastest; MP3 sounds work but may have a tiny startup delay.

## Test

Double-click:

```text
Test Peek.bat
```

This only tests the visual and sound. It does not test the automatic Codex completion detector.

## Privacy

Codex Peek reads local Codex session JSONL files only to detect completion events. It does not send data to a server.

## Known Limitation

Codex Peek relies on Codex's local session log format. If Codex changes that format in a future update, the detector may need an update.
