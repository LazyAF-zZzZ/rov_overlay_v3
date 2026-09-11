using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RovOverlay.Desktop.Views;

// The one confirmation box, for anything that cannot be undone.
//
// For dangerous actions Enter means Cancel, not Confirm: a stray Enter left over from
// typing in a field must never be what deletes a tournament.
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, IEnumerable<string> body, string confirmLabel, string cancelLabel, bool danger)
    {
        InitializeComponent();
        TitleText.Text = title;

        foreach (var line in body)
        {
            BodyPanel.Children.Add(new TextBlock
            {
                Text = line,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 7),
                LineHeight = 19,
                Foreground = (System.Windows.Media.Brush)FindResource("Muted2Brush")
            });
        }

        ConfirmButton.Content = confirmLabel;
        CancelButton.Content = cancelLabel;
        ConfirmButton.Style = (Style)FindResource(danger ? "DangerButton" : "PrimaryButton");
        CancelButton.IsCancel = true;
        if (danger) CancelButton.IsDefault = true;
        else ConfirmButton.IsDefault = true;

        Loaded += (_, _) => (danger ? CancelButton : ConfirmButton).Focus();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
