using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RovOverlay.Desktop.ViewModels;

namespace RovOverlay.Desktop.Views.Controls;

// Keyboard rules, kept the same as v2's hero box so the operator's habits still work:
//   type          filter the list
//   Down / Up     move through it
//   Enter         take the highlighted hero (or whatever resolves from the text)
//   Esc           close the list, then put the committed hero back
//   leaving       commit what is in the box
public partial class HeroPickerView : UserControl
{
    private HeroSlot? _slot;

    public HeroPickerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _slot = e.NewValue as HeroSlot;
        if (_slot is not null) _slot.FocusRequested += OnFocusRequested;
    }

    private void Detach()
    {
        if (_slot is not null) _slot.FocusRequested -= OnFocusRequested;
        _slot = null;
    }

    private void OnFocusRequested()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Box.Focus();
            Box.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void Box_GotFocus(object sender, KeyboardFocusChangedEventArgs e) => Box.SelectAll();

    private void Box_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_slot is null) return;
        _slot.Close();
        _slot.Commit();
    }

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_slot is null) return;
        switch (e.Key)
        {
            case Key.Down:
                if (_slot.IsOpen) _slot.Move(1);
                else _slot.OpenSuggestions();
                e.Handled = true;
                break;
            case Key.Up:
                if (_slot.IsOpen) _slot.Move(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                _slot.ChooseHighlighted();
                e.Handled = true;
                break;
            case Key.Escape:
                if (_slot.IsOpen) _slot.Close();
                else
                {
                    _slot.Cancel();
                    Keyboard.ClearFocus();
                }
                e.Handled = true;
                break;
            default:
                // Anything that changes the text reopens the list, on the next tick so
                // the typed character is already in it.
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (Box.IsKeyboardFocusWithin && _slot is not null) _slot.OpenSuggestions();
                }), System.Windows.Threading.DispatcherPriority.Background);
                break;
        }
    }

    private void List_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_slot is null) return;
        if ((e.OriginalSource as DependencyObject)?.FindAncestor<ListBoxItem>() is not { DataContext: HeroSuggestion suggestion }) return;
        e.Handled = true;
        _slot.Choose(suggestion);
    }
}

internal static class VisualTreeExtensions
{
    public static T? FindAncestor<T>(this DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match) return match;
            node = System.Windows.Media.VisualTreeHelper.GetParent(node);
        }
        return null;
    }
}
