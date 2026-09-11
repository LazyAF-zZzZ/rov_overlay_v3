using System.Windows.Media;

namespace RovOverlay.Desktop.Services;

// Plays a sound effect here, in the operator's own window, so a level can be judged
// without putting it on air. The overlay pages do the real playing; this never touches
// what viewers hear.
public static class SoundTest
{
    private static readonly MediaPlayer Player = new();

    public static void Play(Uri url, double volume)
    {
        try
        {
            Player.Stop();
            Player.Open(url);
            Player.Volume = Math.Clamp(volume, 0, 1);
            Player.Play();
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
        }
    }
}
