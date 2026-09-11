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

// ---- Teams ----------------------------------------------------------------

public sealed record TeamLogo(long V, string? Ext);

public sealed record TeamPlayer(int Slot, string? Name, string? Position, bool IsCaptain);

// One record for both a registry team and a team inside a tournament; Seed is only
// sent for the second (the server's SeededTeam) and is 0 otherwise.
public sealed record Team(
    string Id,
    string Name,
    string? Tag,
    TeamLogo? Logo,
    List<TeamPlayer>? Players,
    long CreatedAt,
    long UpdatedAt,
    int Seed);

public sealed record TeamList(List<Team>? Teams);
public sealed record TeamReply(bool Ok, Team Team);
public sealed record TeamSummary(string TeamId, int Tournaments, int Won, int Lost);
public sealed record TeamSummaries(List<TeamSummary>? Summaries);
public sealed record BulkDeleteReply(bool Ok, int Removed, int Missing);

public sealed record TeamRecord(int Played, int Won, int Lost, int GamesWon, int GamesLost, int Tournaments);
public sealed record TeamTournament(string Id, string Name, string Status, string Format, int Seed);

// outcome: win, loss, or null while not decided
public sealed record HistoryMatch(
    string MatchId,
    string TournamentId,
    string TournamentName,
    string Bracket,
    int Round,
    int BestOf,
    string Status,
    bool IsBye,
    string? OpponentId,
    string? OpponentName,
    bool OpponentGone,
    int Score,
    int OpponentScore,
    string? Outcome);

public sealed record TeamHistory(Team Team, TeamRecord Record, List<TeamTournament>? Tournaments, List<HistoryMatch>? Matches);

// ---- Tournament detail ----------------------------------------------------

public sealed record TournamentDetail(Tournament Tournament, List<Team>? Teams);
public sealed record RosterReply(bool Ok, int TeamCount, Tournament Tournament, List<Team>? Teams);
public sealed record TournamentUpdate(bool Ok, Tournament Tournament);
public sealed record RemovedCounts(int Teams, int Matches, int Games);
public sealed record DeleteTournamentReply(bool Ok, RemovedCounts? Removed, bool WasLive);

public sealed record MatchLite(string Id, string Status, bool IsBye);
public sealed record MatchList(List<MatchLite>? Matches);

public sealed record StandingRow(
    string TeamId,
    string? Name,
    int Played,
    int Won,
    int Lost,
    int GamesWon,
    int GamesLost,
    int GameDiff,
    int Points,
    int Rank,
    bool Tied);

public sealed record StandingsGroup(string Bracket, List<StandingRow>? Rows, int Remaining);
public sealed record StandingsReply(List<StandingsGroup>? Groups);
public sealed record PlayoffReply(bool Ok, int Promoted);

// ---- Backup ---------------------------------------------------------------

public sealed record BackupSummary(int Teams, int Tournaments, int Matches, int Drafts, int Logos, string? ExportedAt);
public sealed record AlreadyHere(int Teams, int Tournaments);
public sealed record BackupPreview(BackupSummary Summary, AlreadyHere AlreadyHere);
public sealed record RestoreReport(int TeamsAdded, int TournamentsAdded);
public sealed record RestoreReply(bool Ok, RestoreReport Report);

public sealed record OkReply(bool Ok);
