using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Poe2Crafter.Core.Games;

namespace Poe2Crafter;

// Minimal first-run / switch-game chooser. Returns the picked profile, or null
// if the window was closed without a choice.
public static class GamePicker
{
    public static GameProfile? Choose()
    {
        GameProfile? picked = null;

        var win = new Window
        {
            Title = "Choose game",
            Width = 300,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e)),
            Topmost = true,
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = "Which game?",
            Foreground = Brushes.White,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12),
        });

        foreach (var profile in GameProfiles.All)
        {
            var btn = new Button
            {
                Content = profile.Name,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 13,
            };
            btn.Click += (_, _) => { picked = profile; win.DialogResult = true; };
            panel.Children.Add(btn);
        }

        win.Content = panel;
        win.ShowDialog();
        return picked;
    }
}
