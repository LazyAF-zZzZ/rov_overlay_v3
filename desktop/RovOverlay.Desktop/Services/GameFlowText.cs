using RovOverlay.Desktop.Models;

namespace RovOverlay.Desktop.Services;

// What +1, -1 and the round arrows say, in one place, so a point added with a system-wide
// key from OBS reads exactly like one added with the button beside the score.
public static class GameFlowText
{
    public static string Finished(FinishGameReply reply, string fallbackName)
    {
        var name = string.IsNullOrWhiteSpace(reply.TeamName) ? fallbackName : reply.TeamName;
        if (!reply.SeriesOver) return Loc.F("Flow.NextGame", name, reply.Round);

        // "PSG Esports win the series 2–1": the winner's score first, whichever side they are on.
        var score = reply.Score ?? new SideScores(0, 0);
        return Loc.F("Flow.SeriesWon", reply.SeriesWinner ?? name, Math.Max(score.Blue, score.Red), Math.Min(score.Blue, score.Red));
    }

    public static string Undone(UndoGameReply reply, string fallbackName)
    {
        var name = string.IsNullOrWhiteSpace(reply.TeamName) ? fallbackName : reply.TeamName;
        return Loc.F(reply.Reopened ? "Flow.SeriesReopened" : "Flow.GameUndone", name, reply.Round);
    }

    public static string FinishError(string? code, string message) => code switch
    {
        "game-decided" => Loc.T("Flow.Err.GameDecided"),
        "series-over" => Loc.T("Flow.Err.SeriesOver"),
        "round-limit" => Loc.T("Flow.Err.RoundLimit"),
        "not-recorded" => Loc.T("Flow.Err.NotRecorded"),
        _ => message
    };

    public static string UndoError(string? code, string message) => code switch
    {
        "no-points" => Loc.T("Flow.Err.NoPoints"),
        "not-last" => Loc.T("Flow.Err.NotLast"),
        "not-recorded" => Loc.T("Flow.Err.NotRecorded"),
        _ => message
    };

    public static string RoundError(string? code, string message) => code switch
    {
        "round-first" => Loc.T("Flow.Err.RoundFirst"),
        "round-limit" => Loc.T("Flow.Err.RoundLimit"),
        _ => message
    };
}
