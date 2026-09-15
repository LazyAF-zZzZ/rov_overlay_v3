namespace RovOverlay.Desktop.Models;

// GET /api/teams/:id/stats[?tournamentId=]. Shapes match backend/server/store/team-stats.ts.

public sealed record TeamStatsReply(TeamStatsData Stats);

public sealed record TeamStatsData(
    string? TournamentId,
    TeamStatsRecord Record,
    int DraftedGames,
    int DecidedGames,
    List<TeamStatsPick>? Picks,
    List<TeamStatsBan>? Bans,
    List<TeamStatsBan>? BansAgainst,
    List<TeamStatsOpponent>? Opponents,
    TeamStatsSides Sides,
    List<TeamStatsPlayer>? Players);

// form: the last five finished series, newest first, "win" or "loss".
public sealed record TeamStatsRecord(int SeriesWon, int SeriesLost, int GamesWon, int GamesLost, int Tournaments, List<string>? Form);

public sealed record TeamStatsPick(string Hero, int Picked, int Wins, int Decided);
public sealed record TeamStatsBan(string Hero, int Banned);

// OpponentId is null when that team has been deleted from the registry.
public sealed record TeamStatsOpponent(
    string? OpponentId, string Name, int SeriesWon, int SeriesLost, int GamesWon, int GamesLost,
    string LastTournament, int LastScore, int LastOpponentScore);

public sealed record TeamStatsSide(int Played, int Won);

// Unknown: decided games played before the side a team played was recorded.
public sealed record TeamStatsSides(TeamStatsSide Blue, TeamStatsSide Red, int Unknown);

public sealed record TeamStatsPlayerHero(string Hero, int Picked, int Wins, int Decided);
public sealed record TeamStatsPlayer(string Name, int Games, List<TeamStatsPlayerHero>? Heroes);
