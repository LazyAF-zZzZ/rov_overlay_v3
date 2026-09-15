using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using RovOverlay.Desktop.Services;
using RovOverlay.Desktop.ViewModels;

namespace RovOverlay.Desktop.Views;

// Keyboard handling for the Control Panel, ported from v2's control.js.
//
// Two rules decide whether a key is a shortcut or typing:
//   * a key pressed while a text field has focus is typing, unless the field is an
//     empty hero box (so the operator can drive the draft from there);
//   * a bare modifier held and released on its own is a "tap", which is how the banner
//     is toggled. Any other key in between cancels it.
public partial class ControlView : UserControl
{
    private ControlViewModel? _vm;
    private Window? _window;
    private string? _armedModifier;

    public ControlView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as ControlViewModel;
        if (_vm is not null) _vm.IsEditing = IsEditing;

        _window = Window.GetWindow(this);
        if (_window is null) return;
        _window.PreviewKeyDown += OnKeyDown;
        _window.PreviewKeyUp += OnKeyUp;
        _window.Deactivated += OnDeactivated;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.IsEditing = _ => false;
        if (_window is null) return;
        _window.PreviewKeyDown -= OnKeyDown;
        _window.PreviewKeyUp -= OnKeyUp;
        _window.Deactivated -= OnDeactivated;
        _window = null;
    }

    // True when the keyboard is inside a field bound to this view model object, so a push
    // from the server leaves that field alone.
    //
    // Only fields someone types or chooses in count. It used to be any focused element, and
    // every button on this panel shares the panel's DataContext: after pressing "Put on air",
    // the button kept focus, the panel counted as being edited, and the match title field
    // went on showing the previous match while the new one was on air.
    private static bool IsEditing(object target) =>
        Keyboard.FocusedElement is TextBoxBase or ComboBox
        && Keyboard.FocusedElement is FrameworkElement element
        && ReferenceEquals(element.DataContext, target);

    private static bool TypingHere()
    {
        if (Keyboard.FocusedElement is not FrameworkElement element) return false;
        if (element is ComboBox) return true;
        if (element is not TextBox box) return false;
        // An empty hero box still takes shortcuts: that is where the hands are during a draft.
        return box.DataContext is not HeroSlot || box.Text.Length > 0;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm is null) return;

        // Track a lone modifier for the "tap" shortcut; any other key cancels it. A modifier
        // pressed while another is held is a combination, not a tap: system-wide keys are
        // Ctrl+Alt+something, and Windows swallows their letter but not the Alt, so releasing
        // it here used to flip the banner as well whenever this window had focus.
        var modifier = Hotkeys.ModifierOf(e);
        if (modifier is not null)
        {
            if (!e.IsRepeat) _armedModifier = OtherModifiersHeld(modifier) ? null : modifier;
            return;
        }
        _armedModifier = null;

        // Undo works even while typing, as long as the field is empty.
        if (_vm.Binding("undo")?.Matches(e) == true)
        {
            if (Keyboard.FocusedElement is TextBox { Text.Length: > 0 }) return;
            _vm.UndoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (TypingHere()) return;

        // Space on a focused button belongs to the button.
        if (e.Key == Key.Space && Keyboard.FocusedElement is ButtonBase) return;

        if (_vm.Binding("pauseResume")?.Matches(e) == true)
        {
            _vm.TogglePause();
            e.Handled = true;
        }
        else if (_vm.Binding("nextPhase")?.Matches(e) == true)
        {
            _vm.NextCommand.Execute(null);
            e.Handled = true;
        }
        else if (_vm.Binding("prevPhase")?.Matches(e) == true)
        {
            _vm.PrevCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (_vm is null) return;

        // Releasing an ordinary key means the modifier was part of a combination. The key
        // press of a system-wide hotkey never arrives here, but its release can.
        var modifier = Hotkeys.ModifierOf(e);
        if (modifier is null)
        {
            _armedModifier = null;
            return;
        }

        var binding = _vm.Binding("toggleBanner");
        if (binding is null || !binding.IsModifierOnly) return;
        if (modifier != binding.Code || _armedModifier != binding.Code) return;

        _armedModifier = null;
        if (OtherModifiersHeld(modifier) || TypingHere()) return;

        // Handled, or Alt would open the window menu instead.
        e.Handled = true;
        _vm.ToggleBanner();
    }

    private void OnDeactivated(object? sender, EventArgs e) => _armedModifier = null;

    private static bool OtherModifiersHeld(string modifier)
    {
        var held = Keyboard.Modifiers;
        return (modifier != "Control" && held.HasFlag(ModifierKeys.Control))
               || (modifier != "Alt" && held.HasFlag(ModifierKeys.Alt))
               || (modifier != "Shift" && held.HasFlag(ModifierKeys.Shift))
               || (modifier != "Meta" && held.HasFlag(ModifierKeys.Windows));
    }
}
