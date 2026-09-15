// สถิติเต็มของทีมหนึ่ง สำหรับหน้าโปรไฟล์ทีม อ่านอย่างเดียว
//
// ต่างจากไฟล์อื่นที่ตอบคำถามคล้ายกันตรงมุมมอง
//   history.ts    รายการแมตช์ของทีม (ประวัติทีละคู่ ไม่ใช่ตัวเลขรวม)
//   analytics.ts  ฮีโร่ของทั้งทัวร์นาเมนต์ หรือของทีมเดียว แต่ไม่มีแบนที่โดน ฝั่ง หรือผู้เล่น
//   matchup.ts    เฉพาะสองทีมที่ระบุ
//
// กติกาเดียวกับ analytics ทุกข้อ ไม่คิดใหม่:
// - ดราฟต์นับเฉพาะเกมที่ล็อกแล้ว (draft_locked = 1)
// - ช่องของทีมนี้ = ช่องที่ side ตรงกับฝั่งของทีมในสำเนาแช่แข็ง ไม่ใช่ฝั่งบนจอ
// - อัตราชนะนับเฉพาะเกมที่รู้ผู้ชนะ
// - บายไม่นับเป็นแมตช์ที่ลงเล่น
//
// ฝั่งและผู้เล่นเริ่มเก็บในขั้นที่ 6 ของ migrations.ts เกมก่อนหน้านั้นนับเป็น "ไม่รู้ฝั่ง"
// และไม่มีรายผู้เล่น ไม่เดาย้อนหลัง

import type { DatabaseSync } from 'node:sqlite';

export type SeriesOutcome = 'win' | 'loss';

export interface TeamStatsRecord {
  seriesWon: number;
  seriesLost: number;
  gamesWon: number;
  gamesLost: number;
  tournaments: number;
  // ซีรีส์ที่จบแล้วห้าคู่ล่าสุด ใหม่สุดก่อน
  form: SeriesOutcome[];
}

export interface TeamHeroPick { hero: string; picked: number; wins: number; decided: number }
export interface TeamHeroBan { hero: string; banned: number }

export interface TeamOpponent {
  // null = ทีมนั้นถูกลบออกจากทะเบียนไปแล้ว ชื่อมาจากสำเนาแช่แข็ง
  opponentId: string | null;
  name: string;
  seriesWon: number;
  seriesLost: number;
  gamesWon: number;
  gamesLost: number;
  lastTournament: string;
  lastScore: number;
  lastOpponentScore: number;
}

export interface TeamSideRecord { played: number; won: number }

export interface TeamPlayerHero { hero: string; picked: number; wins: number; decided: number }
export interface TeamPlayer { name: string; games: number; heroes: TeamPlayerHero[] }

export interface TeamStats {
  tournamentId: string | null;
  record: TeamStatsRecord;
  // เกมที่ดราฟต์ล็อกแล้ว = ตัวหารของอัตราพิคและแบน
  draftedGames: number;
  decidedGames: number;
  picks: TeamHeroPick[];
  bans: TeamHeroBan[];
  bansAgainst: TeamHeroBan[];
  opponents: TeamOpponent[];
  // unknown = เกมที่รู้ผลแต่เล่นก่อนเริ่มเก็บฝั่ง
  sides: { blue: TeamSideRecord; red: TeamSideRecord; unknown: number };
  players: TeamPlayer[];
}

export interface TeamStatsStore {
  forTeam(teamId: string, tournamentId?: string | null): TeamStats;
}

const FORM_LENGTH = 5;

interface MatchRow {
  id: string;
  tournament_name: string;
  team_a_id: string | null;
  team_b_id: string | null;
  team_a_name: string | null;
  team_b_name: string | null;
  status: string;
  score_a: number;
  score_b: number;
  winner_id: string | null;
  is_bye: number;
}

// เกมของทีมนี้ในขอบเขตที่ขอ พร้อมฝั่งของทีมตามสำเนาแช่แข็ง
// ใช้เป็นตารางตั้งต้นของทุกคิวรีดราฟต์ ขอบเขตกับกติกาการนับจึงอยู่ที่เดียว
// พารามิเตอร์: teamId, teamId, teamId, tournamentId, tournamentId
const TEAM_GAMES = `
  WITH team_games AS (
    SELECT g.id, g.winner, g.sides_swapped,
           CASE WHEN g.blue_team_id = ? THEN 'blue' ELSE 'red' END AS own_side
      FROM games g
      JOIN matches m ON m.id = g.match_id
     WHERE g.draft_locked = 1
       AND (g.blue_team_id = ? OR g.red_team_id = ?)
       AND (? IS NULL OR m.tournament_id = ?)
  )`;

export function createTeamStatsStore(db: DatabaseSync): TeamStatsStore {
  const q = {
    // เรียงตามเวลาที่เล่นจริงล่าสุด (เกมล่าสุดของแมตช์) ไม่ใช่ตามเวลาจับสาย
    // ฟอร์มห้าคู่ล่าสุดต้องหมายถึงคู่ที่เพิ่งแข่ง
    matches: db.prepare(
      `SELECT m.id, t.name AS tournament_name,
              m.team_a_id, m.team_b_id, ta.name AS team_a_name, tb.name AS team_b_name,
              m.status, m.score_a, m.score_b, m.winner_id, m.is_bye,
              COALESCE((SELECT MAX(g.updated_at) FROM games g WHERE g.match_id = m.id), m.created_at) AS played_at
         FROM matches m
         JOIN tournaments t ON t.id = m.tournament_id
         LEFT JOIN teams ta ON ta.id = m.team_a_id
         LEFT JOIN teams tb ON tb.id = m.team_b_id
        WHERE (m.team_a_id = ? OR m.team_b_id = ?)
          AND (? IS NULL OR m.tournament_id = ?)
        ORDER BY played_at DESC, m.round DESC`
    ),

    entries: db.prepare(
      `SELECT COUNT(*) AS n FROM tournament_teams
        WHERE team_id = ? AND (? IS NULL OR tournament_id = ?)`
    ),

    // ชื่อคู่แข่งที่ถูกลบออกจากทะเบียนไปแล้ว อ่านจากสำเนาแช่แข็งของเกมแรก
    snapshot: db.prepare(
      `SELECT blue_team_id, red_team_id, blue_name, red_name
         FROM games WHERE match_id = ? ORDER BY game_no LIMIT 1`
    ),

    totals: db.prepare(
      `${TEAM_GAMES}
       SELECT COUNT(*) AS games,
              SUM(CASE WHEN winner IS NOT NULL THEN 1 ELSE 0 END) AS decided
         FROM team_games`
    ),

    heroes: db.prepare(
      `${TEAM_GAMES}
       SELECT s.hero AS hero,
              COUNT(DISTINCT CASE WHEN s.kind = 'pick' AND s.side = tg.own_side THEN s.game_id END) AS picked,
              COUNT(DISTINCT CASE WHEN s.kind = 'pick' AND s.side = tg.own_side AND tg.winner = tg.own_side
                                  THEN s.game_id END) AS wins,
              COUNT(DISTINCT CASE WHEN s.kind = 'pick' AND s.side = tg.own_side AND tg.winner IS NOT NULL
                                  THEN s.game_id END) AS decided,
              COUNT(DISTINCT CASE WHEN s.kind = 'ban' AND s.side = tg.own_side THEN s.game_id END) AS banned,
              COUNT(DISTINCT CASE WHEN s.kind = 'ban' AND s.side <> tg.own_side THEN s.game_id END) AS banned_against
         FROM game_slots s
         JOIN team_games tg ON tg.id = s.game_id
        GROUP BY s.hero`
    ),

    // ฝั่งที่ลงเล่นจริงบนจอ = ฝั่งในสำเนาแช่แข็ง พลิกถ้าจอสลับอยู่
    sides: db.prepare(
      `${TEAM_GAMES}
       SELECT CASE WHEN sides_swapped IS NULL THEN 'unknown'
                   WHEN (own_side = 'blue') = (sides_swapped = 0) THEN 'blue'
                   ELSE 'red' END AS played_side,
              COUNT(*) AS played,
              SUM(CASE WHEN winner = own_side THEN 1 ELSE 0 END) AS won
         FROM team_games
        WHERE winner IS NOT NULL
        GROUP BY played_side`
    ),

    // ผู้เล่นแถว idx คู่กับช่องพิค idx ของฝั่งเดียวกัน ในเกมเดียวกัน
    players: db.prepare(
      `${TEAM_GAMES}
       SELECT p.name AS name, p.idx AS idx, s.hero AS hero, tg.id AS game_id, tg.winner AS winner, tg.own_side AS own_side
         FROM game_players p
         JOIN team_games tg ON tg.id = p.game_id AND p.side = tg.own_side
         LEFT JOIN game_slots s ON s.game_id = p.game_id AND s.side = p.side AND s.kind = 'pick' AND s.idx = p.idx`
    )
  };

  function frozenOpponent(matchId: string, teamId: string): string | null {
    const row = q.snapshot.get(matchId) as
      { blue_team_id: string | null; red_team_id: string | null; blue_name: string; red_name: string } | undefined;
    if (!row) return null;
    if (row.blue_team_id === teamId) return row.red_name || null;
    if (row.red_team_id === teamId) return row.blue_name || null;
    return null;
  }

  return {
    forTeam(teamId, tournamentId = null) {
      const scope = tournamentId || null;
      const draftParams = [teamId, teamId, teamId, scope, scope];

      // ---- ผลแมตช์ และคู่แข่ง ----
      const record: TeamStatsRecord = {
        seriesWon: 0, seriesLost: 0, gamesWon: 0, gamesLost: 0,
        tournaments: (q.entries.get(teamId, scope, scope) as { n: number }).n,
        form: []
      };
      const opponents = new Map<string, TeamOpponent>();

      (q.matches.all(teamId, teamId, scope, scope) as unknown as MatchRow[]).forEach((row) => {
        if (row.is_bye === 1) return;
        const weAreA = row.team_a_id === teamId;
        const score = weAreA ? row.score_a : row.score_b;
        const opponentScore = weAreA ? row.score_b : row.score_a;
        const complete = row.status === 'complete' && row.winner_id !== null;
        const outcome: SeriesOutcome | null = complete ? (row.winner_id === teamId ? 'win' : 'loss') : null;

        record.gamesWon += score;
        record.gamesLost += opponentScore;
        if (outcome === 'win') record.seriesWon += 1;
        if (outcome === 'loss') record.seriesLost += 1;
        if (outcome && record.form.length < FORM_LENGTH) record.form.push(outcome);

        // คู่ที่ยังไม่รู้ว่าเจอใคร หรือยังไม่ได้แข่งเลย ไม่ใช่ประวัติการเจอกัน
        const opponentId = weAreA ? row.team_b_id : row.team_a_id;
        const registryName = weAreA ? row.team_b_name : row.team_a_name;
        const name = opponentId ? (registryName || '') : frozenOpponent(row.id, teamId);
        if (!name || (!complete && score + opponentScore === 0)) return;

        const key = opponentId ?? `gone:${name}`;
        let entry = opponents.get(key);
        if (!entry) {
          // แถวแรกที่เจอคือนัดล่าสุด เพราะคิวรีเรียงใหม่สุดก่อน
          entry = {
            opponentId, name, seriesWon: 0, seriesLost: 0, gamesWon: 0, gamesLost: 0,
            lastTournament: row.tournament_name, lastScore: score, lastOpponentScore: opponentScore
          };
          opponents.set(key, entry);
        }
        entry.gamesWon += score;
        entry.gamesLost += opponentScore;
        if (outcome === 'win') entry.seriesWon += 1;
        if (outcome === 'loss') entry.seriesLost += 1;
      });

      // ---- ดราฟต์ ----
      const totals = q.totals.get(...draftParams) as { games: number; decided: number | null };
      const heroRows = q.heroes.all(...draftParams) as unknown as
        { hero: string; picked: number; wins: number; decided: number; banned: number; banned_against: number }[];

      const byCount = <T extends { hero: string }>(count: (row: T) => number) =>
        (a: T, b: T) => count(b) - count(a) || a.hero.localeCompare(b.hero);

      const picks = heroRows.filter((r) => r.picked > 0)
        .map((r) => ({ hero: r.hero, picked: r.picked, wins: r.wins, decided: r.decided }))
        .sort(byCount((r) => r.picked));
      const bans = heroRows.filter((r) => r.banned > 0)
        .map((r) => ({ hero: r.hero, banned: r.banned }))
        .sort(byCount((r) => r.banned));
      const bansAgainst = heroRows.filter((r) => r.banned_against > 0)
        .map((r) => ({ hero: r.hero, banned: r.banned_against }))
        .sort(byCount((r) => r.banned));

      // ---- ฝั่ง ----
      const sides = { blue: { played: 0, won: 0 }, red: { played: 0, won: 0 }, unknown: 0 };
      (q.sides.all(...draftParams) as unknown as { played_side: string; played: number; won: number }[])
        .forEach((row) => {
          if (row.played_side === 'blue') sides.blue = { played: row.played, won: row.won };
          else if (row.played_side === 'red') sides.red = { played: row.played, won: row.won };
          else sides.unknown = row.played;
        });

      // ---- ผู้เล่น ----
      // row = แถวบนสุดที่ผู้เล่นคนนี้เคยนั่ง ใช้เรียงตามลำดับรายชื่อ (ป่า แครี่ กลาง ...) แทนตัวอักษร
      const players = new Map<string, { games: Set<string>; row: number; heroes: Map<string, TeamPlayerHero> }>();
      (q.players.all(...draftParams) as unknown as
        { name: string; idx: number; hero: string | null; game_id: string; winner: string | null; own_side: string }[])
        .forEach((row) => {
          let player = players.get(row.name);
          if (!player) {
            player = { games: new Set(), row: row.idx, heroes: new Map() };
            players.set(row.name, player);
          }
          player.row = Math.min(player.row, row.idx);
          player.games.add(row.game_id);
          if (!row.hero) return;
          let hero = player.heroes.get(row.hero);
          if (!hero) {
            hero = { hero: row.hero, picked: 0, wins: 0, decided: 0 };
            player.heroes.set(row.hero, hero);
          }
          hero.picked += 1;
          if (row.winner !== null) {
            hero.decided += 1;
            if (row.winner === row.own_side) hero.wins += 1;
          }
        });

      return {
        tournamentId: scope,
        record,
        draftedGames: totals.games || 0,
        decidedGames: totals.decided || 0,
        picks,
        bans,
        bansAgainst,
        opponents: [...opponents.values()]
          .sort((a, b) => (b.seriesWon + b.seriesLost) - (a.seriesWon + a.seriesLost)
            || (b.gamesWon + b.gamesLost) - (a.gamesWon + a.gamesLost)
            || a.name.localeCompare(b.name)),
        sides,
        players: [...players.entries()]
          .sort(([aName, a], [bName, b]) => b.games.size - a.games.size || a.row - b.row || aName.localeCompare(bName))
          .map(([name, p]) => ({
            name,
            games: p.games.size,
            heroes: [...p.heroes.values()].sort((a, b) => b.picked - a.picked || b.wins - a.wins || a.hero.localeCompare(b.hero))
          }))
      };
    }
  };
}
