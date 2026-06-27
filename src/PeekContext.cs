using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;

namespace CodexPeek
{
    internal sealed class PeekContext : IDisposable
    {
        private readonly string appDir;
        private readonly string stateDir;
        private readonly NotifyIcon tray;
        private readonly FileSystemWatcher watcher;
        private readonly Debouncer debouncer;
        private readonly Dictionary<string, RolloutState> rolloutStates = new Dictionary<string, RolloutState>(StringComparer.OrdinalIgnoreCase);
        private PeekConfig config;
        private ToolStripMenuItem pauseItem;
        private SettingsWindow settingsWindow;
        private string lastFingerprint = "";
        private bool paused;
        private bool disposed;

        public PeekContext()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory;
            stateDir = Path.Combine(appDir, "state");
            Directory.CreateDirectory(stateDir);
            config = PeekConfig.Load(appDir);
            lastFingerprint = LoadLastFingerprint();
            InitializeKnownRollouts();

            tray = new NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Text = "Codex Peek",
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };
            tray.DoubleClick += delegate { ShowPeek(); };

            watcher = BuildWatcher(config.WatchPath);
            debouncer = new Debouncer(650, delegate(string path) { CheckRollout(path); });

            ShowBalloon("Codex Peek is watching Codex sessions.");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
            }
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Settings...", null, delegate { OpenSettings(); });
            menu.Items.Add("Test Peek", null, delegate { ShowPeek(); });
            pauseItem = new ToolStripMenuItem("Pause Notifications");
            pauseItem.Click += delegate { TogglePaused(); };
            menu.Items.Add(pauseItem);
            menu.Items.Add("Scan Latest", null, delegate { ScanLatest(); });
            menu.Items.Add("Reload Config", null, delegate { ReloadConfig(); });
            menu.Items.Add("Start with Windows", null, delegate { InstallStartupShortcut(); });
            menu.Items.Add("Remove Startup", null, delegate { RemoveStartupShortcut(); });
            menu.Items.Add("Open Folder", null, delegate { Process.Start(appDir); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate
            {
                Dispose();
                System.Windows.Application.Current.Shutdown();
            });
            return menu;
        }

        private Icon LoadTrayIcon()
        {
            var iconPath = Path.Combine(appDir, "assets", "codex-peek.ico");
            if (File.Exists(iconPath))
            {
                try
                {
                    return new Icon(iconPath);
                }
                catch
                {
                    return SystemIcons.Information;
                }
            }
            return SystemIcons.Information;
        }

        private FileSystemWatcher BuildWatcher(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var fsw = new FileSystemWatcher(path, "rollout-*.jsonl")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            fsw.Changed += delegate(object sender, FileSystemEventArgs e) { debouncer.Signal(e.FullPath); };
            fsw.Created += delegate(object sender, FileSystemEventArgs e) { debouncer.Signal(e.FullPath); };
            return fsw;
        }

        private void ScanLatest()
        {
            var root = config.WatchPath;
            if (!Directory.Exists(root))
            {
                ShowBalloon("Watch path does not exist.");
                return;
            }

            FileInfo newest = null;
            foreach (var file in Directory.EnumerateFiles(root, "rollout-*.jsonl", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                if (newest == null || info.LastWriteTimeUtc > newest.LastWriteTimeUtc)
                {
                    newest = info;
                }
            }

            if (newest == null)
            {
                ShowBalloon("No rollout files found.");
                return;
            }

            CheckRollout(newest.FullName, true);
        }

        private void CheckRollout(string path, bool force = false)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                if (paused && !force)
                {
                    return;
                }

                string fingerprint;
                if (force)
                {
                    if (!TryGetCompletionFingerprint(path, out fingerprint))
                    {
                        return;
                    }
                }
                else if (!TryGetNewCompletionFingerprint(path, out fingerprint))
                {
                    return;
                }

                if (!force && String.Equals(lastFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return;
                }

                lastFingerprint = fingerprint;
                SaveLastFingerprint(fingerprint);
                ShowPeek();
            }
            catch
            {
                // Stay quiet: this app should not interrupt the user's work.
            }
        }

        private void InitializeKnownRollouts()
        {
            var root = config.WatchPath;
            if (!Directory.Exists(root))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(root, "rollout-*.jsonl", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    rolloutStates[file] = new RolloutState { Position = info.Length };
                }
                catch
                {
                    // Ignore files that are briefly unavailable while Codex is writing them.
                }
            }
        }

        private bool TryGetNewCompletionFingerprint(string path, out string fingerprint)
        {
            fingerprint = "";

            RolloutState state;
            if (!rolloutStates.TryGetValue(path, out state))
            {
                state = new RolloutState();
                rolloutStates[path] = state;
            }

            string text;
            long endPosition;
            if (!TryReadNewText(path, state.Position, out text, out endPosition))
            {
                return false;
            }

            state.Position = endPosition;
            if (text.Length == 0)
            {
                return false;
            }

            var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var completedTurnId = "";
            foreach (var line in lines)
            {
                var payload = GetPayloadInfo(line);
                if (payload.Type.Length == 0)
                {
                    continue;
                }

                if (payload.Type == "task_started" ||
                    payload.Type == "user_message")
                {
                    state.FinalAnswerTurnId = "";
                    if (payload.TurnId.Length > 0)
                    {
                        state.ActiveTurnId = payload.TurnId;
                        state.IsArmed = true;
                    }
                    continue;
                }

                if (payload.Type == "turn_aborted" ||
                    payload.Type == "thread_rolled_back")
                {
                    state.ActiveTurnId = "";
                    state.FinalAnswerTurnId = "";
                    state.IsArmed = false;
                    continue;
                }

                if (payload.Type == "final_answer" &&
                    state.IsArmed &&
                    payload.TurnId.Length > 0 &&
                    String.Equals(payload.TurnId, state.ActiveTurnId, StringComparison.Ordinal))
                {
                    state.FinalAnswerTurnId = payload.TurnId;
                    continue;
                }

                if (payload.Type == "task_complete" &&
                    state.IsArmed &&
                    payload.TurnId.Length > 0 &&
                    String.Equals(payload.TurnId, state.FinalAnswerTurnId, StringComparison.Ordinal))
                {
                    completedTurnId = payload.TurnId;
                    state.ActiveTurnId = "";
                    state.FinalAnswerTurnId = "";
                    state.IsArmed = false;
                }
            }

            if (completedTurnId.Length == 0)
            {
                return false;
            }

            fingerprint = path + "|incremental_complete|" + completedTurnId + "|" + endPosition.ToString();
            return true;
        }

        private bool TryGetCompletionFingerprint(string path, out string fingerprint)
        {
            fingerprint = "";
            var tail = ReadTail(path, 220 * 1024);
            if (tail.Length == 0)
            {
                return false;
            }

            var lines = tail.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var lastTaskCompleteIndex = -1;
            var lastTaskCompleteTurnId = "";

            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];
                var payload = GetPayloadInfo(line);
                if (payload.Type.Length == 0)
                {
                    continue;
                }

                if (payload.Type == "task_complete" && payload.TurnId.Length > 0)
                {
                    lastTaskCompleteIndex = i;
                    lastTaskCompleteTurnId = payload.TurnId;
                    continue;
                }

                if (payload.Type == "final_answer" &&
                    String.Equals(payload.TurnId, lastTaskCompleteTurnId, StringComparison.Ordinal))
                {
                    break;
                }
            }

            if (lastTaskCompleteIndex < 0 || lastTaskCompleteTurnId.Length == 0)
            {
                return false;
            }

            var hasMatchingFinalAnswer = false;
            for (var i = lastTaskCompleteIndex - 1; i >= 0; i--)
            {
                var payload = GetPayloadInfo(lines[i]);
                if (payload.Type == "final_answer" &&
                    String.Equals(payload.TurnId, lastTaskCompleteTurnId, StringComparison.Ordinal))
                {
                    hasMatchingFinalAnswer = true;
                    break;
                }
            }

            if (!hasMatchingFinalAnswer)
            {
                return false;
            }

            fingerprint = path + "|final_answer_complete|" + lastTaskCompleteTurnId + "|" + CountLinesUpTo(lines, lastTaskCompleteIndex).ToString();
            return true;
        }

        private static PayloadInfo GetPayloadInfo(string line)
        {
            if (Regex.IsMatch(line, "\"type\"\\s*:\\s*\"response_item\"") &&
                Regex.IsMatch(line, "\"type\"\\s*:\\s*\"message\"") &&
                Regex.IsMatch(line, "\"phase\"\\s*:\\s*\"final_answer\"") &&
                Regex.IsMatch(line, "\"role\"\\s*:\\s*\"assistant\""))
            {
                return new PayloadInfo("final_answer", ExtractTurnId(line));
            }
            if (Regex.IsMatch(line, "\"type\"\\s*:\\s*\"response_item\"") &&
                Regex.IsMatch(line, "\"type\"\\s*:\\s*\"message\"") &&
                Regex.IsMatch(line, "\"phase\"\\s*:\\s*\"commentary\"") &&
                Regex.IsMatch(line, "\"role\"\\s*:\\s*\"assistant\""))
            {
                return new PayloadInfo("assistant_commentary", ExtractTurnId(line));
            }
            if (Regex.IsMatch(line, "\"type\"\\s*:\\s*\"response_item\"") &&
                Regex.IsMatch(line, "\"type\"\\s*:\\s*\"message\"") &&
                Regex.IsMatch(line, "\"phase\"\\s*:\\s*\"analysis\"") &&
                Regex.IsMatch(line, "\"role\"\\s*:\\s*\"assistant\""))
            {
                return new PayloadInfo("assistant_analysis", ExtractTurnId(line));
            }
            if (Regex.IsMatch(line, "\"type\"\\s*:\\s*\"event_msg\"") &&
                Regex.IsMatch(line, "\"payload\"\\s*:\\s*\\{\\s*\"type\"\\s*:\\s*\"task_complete\""))
            {
                return new PayloadInfo("task_complete", ExtractTurnId(line));
            }
            if (Regex.IsMatch(line, "\"type\"\\s*:\\s*\"event_msg\"") &&
                Regex.IsMatch(line, "\"payload\"\\s*:\\s*\\{\\s*\"type\"\\s*:\\s*\"task_started\""))
            {
                return new PayloadInfo("task_started", ExtractTurnId(line));
            }
            if (Regex.IsMatch(line, "\"type\"\\s*:\\s*\"event_msg\"") &&
                Regex.IsMatch(line, "\"payload\"\\s*:\\s*\\{\\s*\"type\"\\s*:\\s*\"user_message\""))
            {
                return new PayloadInfo("user_message", ExtractTurnId(line));
            }
            if (Regex.IsMatch(line, "\"type\"\\s*:\\s*\"event_msg\"") &&
                Regex.IsMatch(line, "\"payload\"\\s*:\\s*\\{\\s*\"type\"\\s*:\\s*\"turn_aborted\""))
            {
                return new PayloadInfo("turn_aborted", ExtractTurnId(line));
            }
            if (Regex.IsMatch(line, "\"type\"\\s*:\\s*\"event_msg\"") &&
                Regex.IsMatch(line, "\"payload\"\\s*:\\s*\\{\\s*\"type\"\\s*:\\s*\"thread_rolled_back\""))
            {
                return new PayloadInfo("thread_rolled_back", ExtractTurnId(line));
            }
            if (Regex.IsMatch(line, "\"type\"\\s*:\\s*\"event_msg\"") &&
                Regex.IsMatch(line, "\"payload\"\\s*:\\s*\\{\\s*\"type\"\\s*:\\s*\"token_count\""))
            {
                return new PayloadInfo("token_count", ExtractTurnId(line));
            }
            if (Regex.IsMatch(line, "\"type\"\\s*:\\s*\"turn_context\""))
            {
                return new PayloadInfo("turn_context", ExtractTurnId(line));
            }

            if (Regex.IsMatch(line, "\"type\"\\s*:\\s*\"response_item\"") &&
                Regex.IsMatch(line, "\"type\"\\s*:\\s*\"message\"") &&
                Regex.IsMatch(line, "\"role\"\\s*:\\s*\"user\""))
            {
                return new PayloadInfo("user_message", ExtractTurnId(line));
            }
            return new PayloadInfo("", "");
        }

        private static string ExtractTurnId(string line)
        {
            var match = Regex.Match(line, "\"turn_id\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : "";
        }

        private static int CountLinesUpTo(string[] lines, int index)
        {
            var count = 0;
            for (var i = 0; i <= index && i < lines.Length; i++)
            {
                if (lines[i].Length > 0)
                {
                    count++;
                }
            }
            return count;
        }

        private static string ReadTail(string path, int maxBytes)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var length = stream.Length;
                var bytes = Math.Min(length, maxBytes);
                stream.Seek(-bytes, SeekOrigin.End);
                var buffer = new byte[bytes];
                var read = stream.Read(buffer, 0, buffer.Length);
                return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
            }
        }

        private static bool TryReadNewText(string path, long startPosition, out string text, out long endPosition)
        {
            text = "";
            endPosition = startPosition;

            if (!File.Exists(path))
            {
                return false;
            }

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length < startPosition)
                {
                    startPosition = 0;
                }

                endPosition = stream.Length;
                var bytes = endPosition - startPosition;
                if (bytes <= 0)
                {
                    return false;
                }
                if (bytes > 512 * 1024)
                {
                    startPosition = endPosition - (512 * 1024);
                    bytes = endPosition - startPosition;
                }

                stream.Seek(startPosition, SeekOrigin.Begin);
                var buffer = new byte[(int)bytes];
                var read = stream.Read(buffer, 0, buffer.Length);
                text = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                return true;
            }
        }

        private void ShowPeek()
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(delegate
            {
                var overlay = new OverlayWindow(config);
                overlay.Show();
            }));
        }

        private void OpenSettings()
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (settingsWindow != null && settingsWindow.IsVisible)
                {
                    settingsWindow.Activate();
                    return;
                }

                settingsWindow = new SettingsWindow(config, delegate(PeekConfig saved)
                {
                    config = saved;
                    ShowBalloon("Settings saved.");
                });
                settingsWindow.Closed += delegate { settingsWindow = null; };
                settingsWindow.Show();
            }));
        }

        private void TogglePaused()
        {
            paused = !paused;
            if (pauseItem != null)
            {
                pauseItem.Text = paused ? "Resume Notifications" : "Pause Notifications";
            }
            ShowBalloon(paused ? "Notifications paused." : "Notifications resumed.");
        }

        private void ReloadConfig()
        {
            config = PeekConfig.Load(appDir);
            InitializeKnownRollouts();
            ShowBalloon("Config reloaded.");
        }

        private void InstallStartupShortcut()
        {
            try
            {
                var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var path = Path.Combine(startup, "Codex Peek.bat");
                File.WriteAllText(path, "@echo off\r\nstart \"\" \"" + Path.Combine(appDir, "CodexPeek.exe") + "\"\r\n");
                ShowBalloon("Startup shortcut installed.");
            }
            catch
            {
                ShowBalloon("Could not install startup shortcut.");
            }
        }

        private void RemoveStartupShortcut()
        {
            try
            {
                var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var path = Path.Combine(startup, "Codex Peek.bat");
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                ShowBalloon("Startup shortcut removed.");
            }
            catch
            {
                ShowBalloon("Could not remove startup shortcut.");
            }
        }

        private string LoadLastFingerprint()
        {
            var path = Path.Combine(stateDir, "last.txt");
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        private void SaveLastFingerprint(string fingerprint)
        {
            File.WriteAllText(Path.Combine(stateDir, "last.txt"), fingerprint);
        }

        private void ShowBalloon(string text)
        {
            tray.BalloonTipTitle = "Codex Peek";
            tray.BalloonTipText = text;
            tray.ShowBalloonTip(1200);
        }

        private sealed class RolloutState
        {
            public long Position;
            public string ActiveTurnId = "";
            public string FinalAnswerTurnId = "";
            public bool IsArmed;
        }

        private sealed class PayloadInfo
        {
            public readonly string Type;
            public readonly string TurnId;

            public PayloadInfo(string type, string turnId)
            {
                Type = type;
                TurnId = turnId;
            }
        }
    }
}
