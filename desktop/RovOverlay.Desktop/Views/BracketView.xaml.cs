using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RovOverlay.Desktop.ViewModels;

namespace RovOverlay.Desktop.Views;

public partial class BracketView : UserControl
{
    public BracketView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is BracketViewModel vm) vm.IsEditing = IsEditing;
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is BracketViewModel vm) vm.IsEditing = _ => false;
        };
    }

    // A score being typed is never overwritten by a push from the server.
    private static bool IsEditing(object target) =>
        Keyboard.FocusedElement is FrameworkElement element && ReferenceEquals(element.DataContext, target);
}
