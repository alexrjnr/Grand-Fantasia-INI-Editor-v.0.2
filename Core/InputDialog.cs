using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GrandFantasiaINIEditor.Core
{
    public class InputDialog : Window
    {
        public string InputText { get; private set; }

        public InputDialog(string prompt, string title = "Clonar")
        {
            Title = title;
            Width = 320;
            Height = 140;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var sp = new StackPanel { Margin = new Thickness(16) };
            var lbl = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8), Foreground = Brushes.White };
            var tb = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x28)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                Height = 28,
                Padding = new Thickness(6, 4, 6, 4)
            };
            var btn = new Button
            {
                Content = "Confirmar",
                Height = 30,
                Margin = new Thickness(0, 10, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57)),
                Foreground = Brushes.White
            };
            btn.Click += (_, _) => { InputText = tb.Text; DialogResult = true; };

            Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x12));
            sp.Children.Add(lbl);
            sp.Children.Add(tb);
            sp.Children.Add(btn);
            Content = sp;
        }
    }
}
