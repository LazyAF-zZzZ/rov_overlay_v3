namespace RovOverlay.Desktop.Models;

// The bracket, as the server keeps it. bracket is "main", "losers", "grand",
// "playoff", or a group letter; round and slot place the match inside it.
public sealed record BracketMatch(
    string Id,
    string TournamentId,
    string Bracket,
    int Round,
    int Slot,
    string? TeamAId,
    string? TeamBId,
    int BestOf,
    string Status,
    int ScoreA,
    int ScoreB,
    string? WinnerId,
    bool IsBye);

public sealed record BracketMatchList(List<BracketMatch>? Matches);
public sealed record MatchResultReply(bool Ok, BracketMatch Match, List<BracketMatch>? Matches);
public sealed record GoLiveReply(bool Ok, LiveInfo Live);

// A match that can go on air right now: both teams known, not a bye, series not over.
public sealed record ReadyMatchInfo(string MatchId, string TournamentId, string TournamentName, string MatchLabel, string BlueName, string RedName);
public sealed record ReadyMatchesReply(List<ReadyMatchInfo>? Matches);

// A game finished from the Control Panel. NextMatch is set only when that game ended the
// series, and is the next playable match in the same tournament.
public sealed record FinishGameReply(bool Ok, LiveInfo Live, int Round, bool SeriesOver, string? SeriesWinner,
    SideScores? Score, ReadyMatchInfo? NextMatch);
public sealed record SideScores(int Blue, int Red);

// One side of a recorded draft: who played, what they picked and banned, and whether
// they won that game. Names are the frozen snapshot, not today's registry.
public sealed record DraftSide(string? TeamId, string Name, List<string?>? Picks, List<string?>? Bans, bool Won);

public sealed record DraftGame(
    string GameId,
    string MatchId,
    int GameNo,
    string Bracket,
    int Round,
    int BestOf,
    bool Decided,
    bool DraftLocked,
    DraftSide Blue,
    DraftSide Red);

public sealed record DraftsReply(TournamentRef Tournament, List<DraftGame>? Games);
public sealed record TournamentRef(string Id, string Name);

// ---- Pick and ban statistics ----------------------------------------------

// Rates are per game, not per draft slot: a hero picked in 8 of 100 games is 8%.
// winRate is null when no game this hero was picked in has a recorded winner;
// banPriority is its rank by first-phase bans, or null if never banned early.
public sealed record HeroStat(
    string Hero,
    int Picked,
    int Banned,
    int Present,
    int EarlyBans,
    int Wins,
    int Decided,
    double PickRate,
    double BanRate,
    double Presence,
    double EarlyBanRate,
    double? WinRate,
    int? BanPriority);

public sealed record AnalyticsSummary(int Games, int DecidedGames, int HeroesSeen, int SlotsPerGame);
public sealed record AnalyticsScope(string? TournamentId, string? TeamId);
public sealed record AnalyticsReply(AnalyticsScope Scope, AnalyticsSummary Summary, List<HeroStat>? Heroes);

// ---- System-wide hotkeys ---------------------------------------------------

// accelerators: action -> the string Windows is asked for ("Control+Alt+H").
// held: what the desktop app reported it actually holds, or null before it has said.
public sealed record GlobalHotkeysReply(bool Enabled, Dictionary<string, string>? Accelerators, List<string>? Held);
