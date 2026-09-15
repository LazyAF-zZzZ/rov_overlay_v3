using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.Views;

// "Version 3.0.5 is ready. Here is what changed. Do you want it now?"
//
// Asking is the point. Putting an update in takes the app down and back up, which means
// the overlays leave the air for a few seconds; doing that unannounced to someone who is
// mid-draft would be indefensible. Later is a real answer, and a permanent one until the
// operator changes their mind: nothing installs on close or on start any more, and the
// version number in the title bar brings this dialog back.
public partial class UpdateDialog : Window
{
    public UpdateDialog(string version, string? notes)
    {
        InitializeComponent();

        TitleText.Text = Loc.F("Update.DialogTitle", version);
        LaterButton.Content = Loc.T("Update.Later");
        NowButton.Content = Loc.T("Update.Now");
        LaterNote.Text = Loc.T("Update.LaterNote");

        foreach (var key in new[] { "Update.Warn.Restart", "Update.Warn.NotDuring", "Update.Warn.Obs", "Update.Warn.Data" })
        {
            WarnPanel.Children.Add(Bullet(Loc.T(key)));
        }

        RenderNotes(notes);

        // Enter is the safe answer here, not the destructive one: the dialog can appear
        // while somebody is typing, and Later loses nobody anything.
        LaterButton.IsDefault = true;
        LaterButton.IsCancel = true;
        Loaded += (_, _) => LaterButton.Focus();
    }

    private UIElement Bullet(string text)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new TextBlock
        {
            Text = "•",
            Foreground = (Brush)FindResource("GoldBrush"),
            VerticalAlignment = VerticalAlignment.Top
        };
        var body = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19,
            Foreground = (Brush)FindResource("TextBrush")
        };
        Grid.SetColumn(body, 1);
        row.Children.Add(dot);
        row.Children.Add(body);
        return row;
    }

    // The notes are the Markdown written for that release. This is not a Markdown
    // renderer and does not try to be: headings, bullets and plain lines, with the
    // punctuation that only matters in a text file taken out.
    private void RenderNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            NotesPanel.Children.Add(new TextBlock
            {
                Text = Loc.T("Update.NoNotes"),
                Foreground = (Brush)FindResource("MutedBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var raw in notes.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line is "" or "---") continue;

            var heading = line.StartsWith("##") ? 2 : line.StartsWith("#") ? 1 : 0;
            var bullet = line.StartsWith("- ") || line.StartsWith("* ");
            var quote = line.StartsWith("> ");

            var text = line.TrimStart('#', '-', '*', '>', ' ').Replace("**", "").Replace("`", "");
            if (text.Length == 0) continue;

            if (bullet)
            {
                NotesPanel.Children.Add(Bullet(text));
                continue;
            }

            NotesPanel.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 19,
                FontSize = heading == 1 ? 14 : heading == 2 ? 13 : 12.5,
                FontWeight = heading > 0 ? FontWeights.SemiBold : FontWeights.Normal,
                FontStyle = quote ? FontStyles.Italic : FontStyles.Normal,
                Foreground = (Brush)FindResource(quote ? "Muted2Brush" : "TextBrush"),
                Margin = new Thickness(0, heading > 0 ? 8 : 2, 0, 2)
            });
        }
    }

    private void Now_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Later_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
