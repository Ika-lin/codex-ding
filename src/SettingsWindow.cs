using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;

namespace CodexPeek
{
    internal sealed class SettingsWindow : Window
    {
        private readonly PeekConfig config;
        private readonly Action<PeekConfig> onSaved;
        private readonly TextBox iconPathBox;
        private readonly TextBox soundPathBox;
        private readonly CheckBox soundEnabledBox;
        private readonly ComboBox positionBox;
        private readonly TextBox sizeBox;
        private readonly TextBox durationBox;
        private readonly TextBox offsetXBox;
        private readonly TextBox offsetYBox;

        public SettingsWindow(PeekConfig config, Action<PeekConfig> onSaved)
        {
            this.config = config;
            this.onSaved = onSaved;

            Title = "Codex Peek Settings";
            Width = 460;
            Height = 420;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;

            var root = new Grid
            {
                Margin = new Thickness(16)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            iconPathBox = AddPathRow(root, 0, "Image", config.IconPath, "PNG", BrowseIcon);
            soundPathBox = AddPathRow(root, 1, "Sound", config.SoundPath, "Audio", BrowseSound);

            soundEnabledBox = new CheckBox
            {
                Content = "Play sound",
                IsChecked = config.SoundEnabled,
                Margin = new Thickness(0, 8, 0, 8)
            };
            Grid.SetRow(soundEnabledBox, 2);
            Grid.SetColumn(soundEnabledBox, 1);
            root.Children.Add(soundEnabledBox);

            positionBox = new ComboBox
            {
                Margin = new Thickness(0, 6, 0, 6)
            };
            positionBox.Items.Add("bottom-right");
            positionBox.Items.Add("bottom-left");
            positionBox.Items.Add("top-right");
            positionBox.Items.Add("top-left");
            positionBox.SelectedItem = config.Position;
            if (positionBox.SelectedIndex < 0)
            {
                positionBox.SelectedIndex = 0;
            }
            AddLabeledControl(root, 3, "Position", positionBox);

            var sizing = new StackPanel { Orientation = Orientation.Horizontal };
            sizeBox = AddSmallBox(sizing, "Size", config.Size);
            durationBox = AddSmallBox(sizing, "Duration", config.DurationMs);
            AddLabeledControl(root, 4, "Display", sizing);

            var offsets = new StackPanel { Orientation = Orientation.Horizontal };
            offsetXBox = AddSmallBox(offsets, "Offset X", config.OffsetX);
            offsetYBox = AddSmallBox(offsets, "Offset Y", config.OffsetY);
            AddLabeledControl(root, 5, "Offset", offsets);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var test = new Button { Content = "Test", Width = 76, Margin = new Thickness(0, 0, 8, 0) };
            test.Click += delegate { SaveAndApply(false); new OverlayWindow(this.config).Show(); };
            var save = new Button { Content = "Save", Width = 76, Margin = new Thickness(0, 0, 8, 0) };
            save.Click += delegate { SaveAndApply(true); };
            var cancel = new Button { Content = "Cancel", Width = 76 };
            cancel.Click += delegate { Close(); };
            buttons.Children.Add(test);
            buttons.Children.Add(save);
            buttons.Children.Add(cancel);
            Grid.SetRow(buttons, 7);
            Grid.SetColumnSpan(buttons, 3);
            root.Children.Add(buttons);

            Content = root;
        }

        private static TextBox AddPathRow(Grid root, int row, string label, string value, string buttonText, RoutedEventHandler browse)
        {
            var textBox = new TextBox
            {
                Text = value,
                Margin = new Thickness(0, 6, 8, 6)
            };
            AddLabel(root, row, label);
            Grid.SetRow(textBox, row);
            Grid.SetColumn(textBox, 1);
            root.Children.Add(textBox);

            var button = new Button
            {
                Content = buttonText,
                Width = 64,
                Margin = new Thickness(0, 6, 0, 6)
            };
            button.Click += browse;
            Grid.SetRow(button, row);
            Grid.SetColumn(button, 2);
            root.Children.Add(button);
            return textBox;
        }

        private static void AddLabeledControl(Grid root, int row, string label, FrameworkElement control)
        {
            AddLabel(root, row, label);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            Grid.SetColumnSpan(control, 2);
            root.Children.Add(control);
        }

        private static void AddLabel(Grid root, int row, string text)
        {
            var label = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 6, 12, 6)
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            root.Children.Add(label);
        }

        private static TextBox AddSmallBox(Panel parent, string label, int value)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 0, 12, 0)
            };
            panel.Children.Add(new TextBlock { Text = label });
            var box = new TextBox
            {
                Text = value.ToString(),
                Width = 72
            };
            panel.Children.Add(box);
            parent.Children.Add(panel);
            return box;
        }

        private void BrowseIcon(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.OpenFileDialog
            {
                Title = "Choose image",
                Filter = "PNG images (*.png)|*.png|All files (*.*)|*.*",
                InitialDirectory = ChooseInitialDirectory("emojis")
            };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                iconPathBox.Text = dialog.FileName;
            }
        }

        private void BrowseSound(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.OpenFileDialog
            {
                Title = "Choose sound",
                Filter = "Audio files (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*",
                InitialDirectory = ChooseInitialDirectory("sounds")
            };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                soundPathBox.Text = dialog.FileName;
            }
        }

        private void SaveAndApply(bool closeAfterSave)
        {
            int size;
            int duration;
            int offsetX;
            int offsetY;
            if (!ReadInt(sizeBox.Text, 8, 256, "Size", out size) ||
                !ReadInt(durationBox.Text, 300, 10000, "Duration", out duration) ||
                !ReadInt(offsetXBox.Text, 0, 2000, "Offset X", out offsetX) ||
                !ReadInt(offsetYBox.Text, 0, 2000, "Offset Y", out offsetY))
            {
                return;
            }

            var iconPath = ResolvePath(iconPathBox.Text);
            var soundPath = ResolvePath(soundPathBox.Text);
            if (!File.Exists(iconPath))
            {
                MessageBox.Show("Image file does not exist.", "Codex Peek", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (soundEnabledBox.IsChecked == true && !File.Exists(soundPath))
            {
                MessageBox.Show("Sound file does not exist.", "Codex Peek", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            config.Update(
                iconPath,
                soundPath,
                soundEnabledBox.IsChecked == true,
                Convert.ToString(positionBox.SelectedItem),
                size,
                duration,
                offsetX,
                offsetY);
            config.Save();
            if (onSaved != null)
            {
                onSaved(config);
            }

            if (closeAfterSave)
            {
                Close();
            }
        }

        private bool ReadInt(string text, int min, int max, string label, out int value)
        {
            if (!Int32.TryParse(text, out value) || value < min || value > max)
            {
                MessageBox.Show(label + " must be between " + min + " and " + max + ".", "Codex Peek", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private string ResolvePath(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                return "";
            }
            path = Environment.ExpandEnvironmentVariables(path.Trim());
            return Path.IsPathRooted(path) ? path : Path.Combine(config.AppDir, path);
        }

        private string ChooseInitialDirectory(string folderName)
        {
            var folder = Path.Combine(config.AppDir, folderName);
            return Directory.Exists(folder) ? folder : config.AppDir;
        }
    }
}
