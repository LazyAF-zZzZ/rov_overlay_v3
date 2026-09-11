namespace RovOverlay.Desktop.Core;

// A page opened on top of a sidebar screen: a tournament, a team profile. The shell
// calls OnClosed when the page leaves the back stack, so it can drop its event
// subscriptions. Without that, every page ever opened would keep reloading itself
// on every data change for the rest of the session.
public interface IClosablePage
{
    void OnClosed();
}
