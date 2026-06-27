using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CodexPeek
{
    internal sealed class PeekConfig
    {
        public string IconPath { get; private set; }
        public string WatchPath { get; private set; }
        public string Position { get; private set; }
        public int Size { get; private set; }
        public int DurationMs { get; private set; }
        public int OffsetX { get; private set; }
        public int OffsetY { get; private set; }
        public bool SoundEnabled { get; private set; }
        public string SoundPath { get; private set; }
        public string AppDir { get; private set; }

        public static PeekConfig Load(string appDir)
        {
            var config = new PeekConfig
            {
                IconPath = Path.Combine(appDir, "assets", "eyes-emoji.png"),
                WatchPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".codex",
                    "sessions"
                ),
                Position = "bottom-right",
                Size = 72,
                DurationMs = 2400,
                OffsetX = 24,
                OffsetY = 48,
                SoundEnabled = true,
                SoundPath = Path.Combine(appDir, "assets", "complete.wav"),
                AppDir = appDir
            };

            var path = Path.Combine(appDir, "CodexPeek.ini");
            if (!File.Exists(path))
            {
                return config;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var index = line.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }

                values[line.Substring(0, index).Trim()] = Expand(line.Substring(index + 1).Trim());
            }

            string text;
            if (values.TryGetValue("iconPath", out text) && text.Length > 0)
            {
                config.IconPath = Path.IsPathRooted(text) ? text : Path.Combine(appDir, text);
            }
            if (values.TryGetValue("watchPath", out text) && text.Length > 0)
            {
                config.WatchPath = text;
            }
            if (values.TryGetValue("position", out text) && text.Length > 0)
            {
                config.Position = text;
            }
            if (values.TryGetValue("soundEnabled", out text) && text.Length > 0)
            {
                bool enabled;
                if (bool.TryParse(text, out enabled))
                {
                    config.SoundEnabled = enabled;
                }
            }
            if (values.TryGetValue("soundPath", out text) && text.Length > 0)
            {
                config.SoundPath = Path.IsPathRooted(text) ? text : Path.Combine(appDir, text);
            }

            config.Size = ReadInt(values, "size", config.Size);
            config.DurationMs = ReadInt(values, "durationMs", config.DurationMs);
            config.OffsetX = ReadInt(values, "offsetX", config.OffsetX);
            config.OffsetY = ReadInt(values, "offsetY", config.OffsetY);
            return config;
        }

        public void Update(
            string iconPath,
            string soundPath,
            bool soundEnabled,
            string position,
            int size,
            int durationMs,
            int offsetX,
            int offsetY)
        {
            IconPath = iconPath;
            SoundPath = soundPath;
            SoundEnabled = soundEnabled;
            Position = position;
            Size = size;
            DurationMs = durationMs;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        public void Save()
        {
            var path = Path.Combine(AppDir, "CodexPeek.ini");
            var lines = new[]
            {
                "# Path to a transparent PNG. Relative paths are resolved from this folder.",
                "iconPath=" + ToConfigPath(IconPath),
                "",
                "# Where Codex stores rollout JSONL session files.",
                "watchPath=" + WatchPath,
                "",
                "position=" + Position,
                "size=" + Size.ToString(CultureInfo.InvariantCulture),
                "durationMs=" + DurationMs.ToString(CultureInfo.InvariantCulture),
                "offsetX=" + OffsetX.ToString(CultureInfo.InvariantCulture),
                "offsetY=" + OffsetY.ToString(CultureInfo.InvariantCulture),
                "",
                "soundEnabled=" + SoundEnabled.ToString().ToLowerInvariant(),
                "soundPath=" + ToConfigPath(SoundPath)
            };
            File.WriteAllLines(path, lines);
        }

        private static int ReadInt(Dictionary<string, string> values, string key, int fallback)
        {
            string text;
            int value;
            if (values.TryGetValue(key, out text) &&
                int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                value > 0)
            {
                return value;
            }
            return fallback;
        }

        private static string Expand(string value)
        {
            return Environment.ExpandEnvironmentVariables(value);
        }

        private string ToConfigPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                return "";
            }

            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(AppDir);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return path;
        }
    }
}
