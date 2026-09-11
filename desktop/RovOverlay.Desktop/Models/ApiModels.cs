namespace RovOverlay.Desktop.Models;

// Shapes of the backend's JSON replies. Property names match the server's camelCase
// through JsonSerializerDefaults.Web; see backend/server/http/*.ts for the source.

public sealed record AppInfo(string App, string Version, string? DataDir, string? MediaDir, int Pid);

public sealed record Tournament(
    string Id,
    string Name,
    string Status,
    string Format,
    int BestOf,
    string? Note,
    int TeamCount,
    int MaxTeams,
    long CreatedAt,
    long UpdatedAt);

public sealed record TournamentList(List<Tournament>? Tournaments);

public sealed record CreatedTournament(bool Ok, Tournament Tournament);

public sealed record FormatOption(string Id, string Label, int MinTeams, int MaxTeams);

public sealed record TournamentOptions(List<FormatOption> Formats, List<int> BestOf, List<string> Statuses, int MaxTeams);

public sealed record LiveInfo(
    string? MatchId,
    string? GameId,
    int? GameNo,
    string? TournamentId,
    string? TournamentName,
    string? MatchLabel,
    string? Winner,
    bool DraftLocked);

public sealed record LiveResponse(LiveInfo Live);

// Pushed on the socket's "dataChanged" event to everyone in the data room.
// Topic is one of: teams, tournaments, roster, matches, games, live.
public sealed record DataChange(string Topic, string? TournamentId, string? TeamId);
