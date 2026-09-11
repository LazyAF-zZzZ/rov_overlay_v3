using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RovOverlay.Desktop.ViewModels;

namespace RovOverlay.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) => MaxButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseDown += OnPreviewMouseDown;
    }

    // Esc goes back a page, as in v2, except while typing: there Esc belongs to the field
    // (and a dropdown that is open closes itself first).
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Keyboard.FocusedElement is TextBox) return;
        if (Keyboard.FocusedElement is ComboBox { IsDropDownOpen: true }) return;
        if (DataContext is ShellViewModel { CanGoBack: true } shell)
        {
            shell.Back();
            e.Handled = true;
        }
    }

    // The mouse's own back button does the same.
    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.XButton1) return;
        if (DataContext is ShellViewModel { CanGoBack: true } shell)
        {
            shell.Back();
            e.Handled = true;
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
