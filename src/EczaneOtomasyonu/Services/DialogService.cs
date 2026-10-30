using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// Kullanıcıya modal uyarı ve hata bildirim pencereleri göstermek için yardımcı servis.
/// </summary>
public static class DialogService
{
    public static async Task UyariGosterAsync(string baslik, string mesaj)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var parent = desktop?.MainWindow;

            var dialog = new Window
            {
                Title = baslik,
                Width = 440,
                Height = 210,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = Brush.Parse("#141720"),
                SystemDecorations = SystemDecorations.BorderOnly
            };

            var mainBorder = new Border
            {
                BorderBrush = Brush.Parse("#DC2626"),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Background = Brush.Parse("#181B26")
            };

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*,Auto")
            };

            // Başlık alanı
            var headerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Thickness(0, 0, 0, 12)
            };
            var icon = new TextBlock
            {
                Text = "⚠️",
                FontSize = 20,
                VerticalAlignment = VerticalAlignment.Center
            };
            var titleText = new TextBlock
            {
                Text = baslik,
                FontSize = 15,
                FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#F87171"),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerStack.Children.Add(icon);
            headerStack.Children.Add(titleText);
            Grid.SetRow(headerStack, 0);

            // Mesaj içeriği
            var messageText = new TextBlock
            {
                Text = mesaj,
                FontSize = 13,
                Foreground = Brush.Parse("#E2E8F0"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(messageText, 1);

            // Kapat / Tamam Butonu
            var button = new Button
            {
                Content = "Tamam",
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(24, 7),
                CornerRadius = new CornerRadius(6),
                Background = Brush.Parse("#DC2626"),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            button.Click += (_, _) => dialog.Close();
            Grid.SetRow(button, 2);

            grid.Children.Add(headerStack);
            grid.Children.Add(messageText);
            grid.Children.Add(button);

            mainBorder.Child = grid;
            dialog.Content = mainBorder;

            if (parent != null)
            {
                await dialog.ShowDialog(parent);
            }
            else
            {
                dialog.Show();
            }
        });
    }
}
