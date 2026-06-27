using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CodexPeek
{
    internal sealed class OverlayWindow : Window
    {
        private static readonly object AudioLock = new object();
        private static readonly List<MediaPlayer> ActivePlayers = new List<MediaPlayer>();
        private readonly PeekConfig config;
        private readonly ScaleTransform scaleTransform;
        private readonly TranslateTransform translateTransform;

        public OverlayWindow(PeekConfig config)
        {
            this.config = config;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            Width = config.Size;
            Height = config.Size;
            Opacity = 0;
            Focusable = false;
            IsHitTestVisible = false;

            var image = new Image
            {
                Stretch = Stretch.Uniform,
                Source = LoadImage(config.IconPath),
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            scaleTransform = new ScaleTransform(0.94, 0.94);
            translateTransform = new TranslateTransform(0, 5);
            var transforms = new TransformGroup();
            transforms.Children.Add(scaleTransform);
            transforms.Children.Add(translateTransform);

            var host = new Grid
            {
                RenderTransform = transforms,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            host.Children.Add(image);

            Content = host;
            Loaded += delegate
            {
                PlaceWindow();
                PlaySound();
                StartAnimation();
            };
        }

        private static ImageSource LoadImage(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private void PlaySound()
        {
            if (!config.SoundEnabled || String.IsNullOrWhiteSpace(config.SoundPath) || !File.Exists(config.SoundPath))
            {
                return;
            }

            var path = config.SoundPath;
            if (!String.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
            {
                PlayMediaFile(path);
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    using (var player = new SoundPlayer(path))
                    {
                        player.PlaySync();
                    }
                }
                catch
                {
                    // The visual cue is the important part; never interrupt the user for audio failures.
                }
            });
        }

        private static void PlayMediaFile(string path)
        {
            try
            {
                var player = new MediaPlayer
                {
                    Volume = 0.8
                };

                EventHandler endedHandler = null;
                EventHandler<ExceptionEventArgs> failedHandler = null;
                Action cleanup = delegate
                {
                    player.MediaEnded -= endedHandler;
                    player.MediaFailed -= failedHandler;
                    player.Close();
                    lock (AudioLock)
                    {
                        ActivePlayers.Remove(player);
                    }
                };

                endedHandler = delegate { cleanup(); };
                failedHandler = delegate { cleanup(); };

                player.MediaEnded += endedHandler;
                player.MediaFailed += failedHandler;

                lock (AudioLock)
                {
                    ActivePlayers.Add(player);
                }
                player.Open(new Uri(path, UriKind.Absolute));
                player.Play();
            }
            catch
            {
                // The visual cue is the important part; never interrupt the user for audio failures.
            }
        }

        private void PlaceWindow()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - config.OffsetX;
            Top = area.Bottom - Height - config.OffsetY;

            if (String.Equals(config.Position, "top-right", StringComparison.OrdinalIgnoreCase))
            {
                Top = area.Top + config.OffsetY;
            }
            else if (String.Equals(config.Position, "top-left", StringComparison.OrdinalIgnoreCase))
            {
                Left = area.Left + config.OffsetX;
                Top = area.Top + config.OffsetY;
            }
            else if (String.Equals(config.Position, "bottom-left", StringComparison.OrdinalIgnoreCase))
            {
                Left = area.Left + config.OffsetX;
            }
        }

        private void StartAnimation()
        {
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = easeOut
            };
            BeginAnimation(OpacityProperty, fadeIn);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = easeOut
            });
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = easeOut
            });
            translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(5, 0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = easeOut
            });

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(config.DurationMs)
            };
            timer.Tick += delegate
            {
                timer.Stop();
                var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(420))
                {
                    EasingFunction = easeIn
                };
                fadeOut.Completed += delegate { Close(); };
                BeginAnimation(OpacityProperty, fadeOut);
                translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -3, TimeSpan.FromMilliseconds(420))
                {
                    EasingFunction = easeIn
                });
            };
            timer.Start();
        }
    }
}
