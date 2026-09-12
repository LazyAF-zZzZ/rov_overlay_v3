using System.IO;
using System.Windows;
using Microsoft.Win32;
using RovOverlay.Desktop.Views;

namespace RovOverlay.Desktop.Services;

// Modal questions and file pickers, owned by the main window.
public static class Dialogs
{
    public static bool Confirm(string title, IEnumerable<string> body, string confirmLabel, bool danger = false)
    {
        var dialog = new ConfirmDialog(title, body, confirmLabel, Loc.T("Common.Cancel"), danger)
        {
            Owner = Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true;
    }

    public static string? PickImage() =>
        PickOpen($"{Loc.T("Dialog.Images")} (*.png;*.jpg;*.webp)|*.png;*.jpg;*.jpeg;*.webp");

    public static string? PickOpen(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog(Application.Current?.MainWindow) == true ? dialog.FileName : null;
    }

    // Used to point at a v2 installation folder.
    public static string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        return dialog.ShowDialog(Application.Current?.MainWindow) == true ? dialog.FolderName : null;
    }

    public static string? PickSave(string fileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = fileName,
            Filter = filter,
            DefaultExt = Path.GetExtension(fileName),
            OverwritePrompt = true
        };
        return dialog.ShowDialog(Application.Current?.MainWindow) == true ? dialog.FileName : null;
    }
}
