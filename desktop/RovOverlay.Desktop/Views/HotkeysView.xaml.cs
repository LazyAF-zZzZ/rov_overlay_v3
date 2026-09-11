using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RovOverlay.Desktop.Services;
using RovOverlay.Desktop.ViewModels;

namespace RovOverlay.Desktop.Views;

// Recording a shortcut: while a row is waiting, the next key press becomes its binding.
// A modifier held on its own counts too (that is how the banner toggle is bound), so it
// is taken on release, when nothing else came with it.
public partial class HotkeysView : UserControl
{
    private HotkeysViewModel? _vm;
    private Window? _window;

    public HotkeysView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as HotkeysViewModel;
        _window = Window.GetWindow(this);
        if (_window is null) return;
        _window.PreviewKeyDown += OnKeyDown;
        _window.PreviewKeyUp += OnKeyUp;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _vm?.CancelRecording();
        if (_window is null) return;
        _window.PreviewKeyDown -= OnKeyDown;
        _window.PreviewKeyUp -= OnKeyUp;
        _window = null;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm is not { IsRecording: true }) return;
        e.Handled = true;

        if ((e.Key == Key.Escape || e.SystemKey == Key.Escape) && Keyboard.Modifiers == ModifierKeys.None)
        {
            _vm.CancelRecording();
            return;
        }

        // A modifier on its own is decided on release, not now.
        if (Hotkeys.ModifierOf(e) is not null) return;

        if (Hotkeys.CodeOf(e) is not { } code) return;
        var keyboard = Keyboard.Modifiers;
        _vm.Record(new HotkeyBinding(code,
            keyboard.HasFlag(ModifierKeys.Control),
            keyboard.HasFlag(ModifierKeys.Shift),
            keyboard.HasFlag(ModifierKeys.Alt),
            keyboard.HasFlag(ModifierKeys.Windows)));
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (_vm is not { IsRecording: true }) return;
        if (Hotkeys.ModifierOf(e) is not { } modifier) return;
        if (Keyboard.Modifiers != ModifierKeys.None) return;   // still holding another one

        e.Handled = true;
        _vm.Record(new HotkeyBinding(modifier, false, false, false, false));
    }
}
