// เทสต์สถิติเต็มรายทีม
//
// สิ่งที่ต้องไม่พังคือ "นับให้ทีมที่ถูก": ช่องในฐานนับฝั่งตามสำเนาแช่แข็ง (ทีม A = น้ำเงิน)
// ส่วนจอสลับฝั่งได้ นับตามสีเมื่อไหร่ ฮีโร่ แบน ฝั่ง และผู้เล่นของสองทีมจะสลับกันเงียบๆ

import test from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import os from 'os';
import path from 'path';

const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'rov-team-stats-test-'));
process.env.ROV_USER_DATA_DIR = path.join(TMP, 'data');
process.env.ROV_USER_MEDIA_DIR = path.join(TMP, 'media');
process.env.CONTROL_TOKEN = '';

const { getStores } = require('../server/store/index') as typeof import('../server/store/index');
const { closeDatabase } = require('../server/store/db') as typeof import('../server/store/db');
const { recordSeriesResult } = require('../server/services/series') as typeof import('../server/services/series');
const { heroesData } = require('../server/domain/heroes') as typeof import('../server/domain/heroes');
import { sanitizeState } from '../server/domain/match';
import { must } from './helpers';

const H = heroesData.heroes;

test.after(() => {
  closeDatabase();
  try { fs.rmSync(TMP, { recursive: true, force: true }); } catch { /* ระบบเก็บเอง */ }
});

let cup = 0;

function newTeams(names: string[]): string[] {
  cup += 1;
  return names.map((n) => must(getStores().teams.create({ name: `${n}${cup}` }).team).id);
}

function tournamentWith(teamIds: string[], bestOf = 3) {
  const { tournaments, matches } = getStores();
  const t = must(tournaments.create({ name: `Stats ${Math.random()}`, format: 'single_elim', bestOf }).tournament);
  teamIds.forEach((id, i) => tournaments.addTeam(t.id, id, i));
  must(matches.generate(t.id).matches);
  return t;
}

const matchWith = (tournamentId: string, teamId: string) =>
  must(getStores().matches.list(tournamentId).find((m) => !m.isBye && (m.teamAId === teamId || m.teamBId === teamId)));

// บันทึกผลจากมุมของทีมหนึ่ง: ours คือแต้มของ teamId ไม่ว่ามันจะเป็นฝั่ง A หรือ B
function result(matchId: string, teamId: string, ours: number, theirs: number) {
  const m = must(getStores().matches.get(matchId));
  const weAreA = m.teamAId === teamId;
  must(recordSeriesResult(matchId, weAreA ? ours : theirs, weAreA ? theirs : ours).match);
}

// ดราฟต์ครบหนึ่งเกม วางตามทีม (A = น้ำเงินในสำเนาแช่แข็ง) แล้ววางลงจอตามว่าสลับฝั่งหรือไม่
function play(gameId: string, opts: {
  swapped: boolean;
  a: { picks: string[]; bans: string[]; players?: string[] };
  b: { picks: string[]; bans: string[]; players?: string[] };
}) {
  const { games } = getStores();
  const g = must(games.get(gameId));
  const a = { name: g.blueName, logo: { v: 0, ext: '', src: g.blueTeamId }, ...opts.a, players: opts.a.players ?? [] };
  const b = { name: g.redName, logo: { v: 0, ext: '', src: g.redTeamId }, ...opts.b, players: opts.b.players ?? [] };
  games.captureDraft(gameId, sanitizeState({ teamBlue: opts.swapped ? b : a, teamRed: opts.swapped ? a : b }));
  return must(games.get(gameId));
}

const picksA = H.slice(0, 5);
const picksB = H.slice(5, 10);
const bansA = H.slice(10, 14);
const bansB = H.slice(14, 18);

test('series and games records, form and opponents, across tournaments and filtered to one', () => {
  const [alpha, bravo, charlie, delta] = newTeams(['ALPHA', 'BRAVO', 'CHARLIE', 'DELTA']) as [string, string, string, string];

  const t1 = tournamentWith([alpha, bravo, charlie, delta], 3);
  const semi = matchWith(t1.id, alpha);
  result(semi.id, alpha, 2, 1);                                   // ALPHA ชนะรอบรอง 2-1
  const otherSemi = must(getStores().matches.list(t1.id).find((m) => m.round === 1 && !m.isBye && m.id !== semi.id));
  must(recordSeriesResult(otherSemi.id, 2, 0).match);
  // ALPHA ถูกดันเข้ารอบชิงแล้ว หาจากรอบที่ 2 ตรงๆ รอบรองก็ยังมีชื่อ ALPHA อยู่
  const final = must(getStores().matches.list(t1.id)
    .find((m) => m.round === 2 && (m.teamAId === alpha || m.teamBId === alpha)), 'ALPHA reached the final');
  result(final.id, alpha, 0, 2);                                   // แพ้รอบชิง 0-2

  const t2 = tournamentWith([alpha, bravo, charlie, delta], 1);
  result(matchWith(t2.id, alpha).id, alpha, 1, 0);                 // ชนะอีกรายการ 1-0

  const all = getStores().teamStats.forTeam(alpha);
  assert.deepStrictEqual(
    [all.record.seriesWon, all.record.seriesLost, all.record.gamesWon, all.record.gamesLost, all.record.tournaments],
    [2, 1, 3, 3, 2]
  );
  assert.deepStrictEqual([...all.record.form].sort(), ['loss', 'win', 'win'], 'form holds every finished series');

  const sum = (key: 'seriesWon' | 'seriesLost' | 'gamesWon' | 'gamesLost') =>
    all.opponents.reduce((n, o) => n + o[key], 0);
  assert.deepStrictEqual([sum('seriesWon'), sum('seriesLost'), sum('gamesWon'), sum('gamesLost')], [2, 1, 3, 3],
    'the opponents table adds up to the record');

  const one = getStores().teamStats.forTeam(alpha, t1.id);
  assert.deepStrictEqual(
    [one.record.seriesWon, one.record.seriesLost, one.record.gamesWon, one.record.gamesLost, one.record.tournaments],
    [1, 1, 2, 3, 1],
    'the tournament filter narrows every number'
  );
  assert.strictEqual(one.opponents.length, 2);
});

test('heroes, bans, bans against, sides and players are credited to the right team across a side swap', () => {
  const [fw, ea] = newTeams(['FW', 'EA']) as [string, string];
  const t = tournamentWith([fw, ea], 3);
  const match = matchWith(t.id, fw);
  const { games } = getStores();
  const game1 = must(games.forMatch(match.id).find((g) => g.gameNo === 1));
  assert.strictEqual(game1.blueTeamId, match.teamAId);
  const fwIsA = match.teamAId === fw;
  const side = (fwSide: typeof picksA, eaSide: typeof picksA) => (fwIsA ? [fwSide, eaSide] : [eaSide, fwSide]);

  // เกมที่ 1: ไม่สลับฝั่ง FW ชนะ
  const [a1, b1] = side(picksA, picksB);
  const [ab1, bb1] = side(bansA, bansB);
  const fwPlayers = ['FW.Kai', 'FW.Nut', 'Player 3', 'FW.Tee', 'FW.Bank'];
  const eaPlayers = ['EA.One', 'EA.Two', 'EA.Three', 'EA.Four', 'EA.Five'];
  const [ap, bp] = fwIsA ? [fwPlayers, eaPlayers] : [eaPlayers, fwPlayers];
  play(game1.id, { swapped: false, a: { picks: must(a1), bans: must(ab1), players: ap }, b: { picks: must(b1), bans: must(bb1), players: bp } });
  result(match.id, fw, 1, 0);

  // เกมที่ 2: สลับฝั่งบนจอ EA ชนะ FW เลือกฮีโร่ชุดเดิม
  const game2 = games.ensure(match.id, 2, {
    blueTeamId: game1.blueTeamId, redTeamId: game1.redTeamId, blueName: game1.blueName, redName: game1.redName
  });
  const stored = play(game2.id, { swapped: true, a: { picks: must(a1), bans: must(ab1), players: ap }, b: { picks: must(b1), bans: must(bb1), players: bp } });
  assert.strictEqual(stored.sidesSwapped, true, 'the swap was recorded with the draft');
  result(match.id, fw, 1, 1);

  const s = getStores().teamStats.forTeam(fw);
  assert.deepStrictEqual([s.draftedGames, s.decidedGames], [2, 2]);

  const first = must(s.picks.find((p) => p.hero === picksA[0]));
  assert.deepStrictEqual([first.picked, first.wins, first.decided], [2, 1, 2], 'FW\'s own pick, won once in two');
  assert.ok(!s.picks.some((p) => p.hero === picksB[0]), 'EA\'s picks are not FW\'s');
  assert.strictEqual(must(s.bans.find((b) => b.hero === bansA[0])).banned, 2);
  assert.strictEqual(must(s.bansAgainst.find((b) => b.hero === bansB[0])).banned, 2, 'EA\'s bans count as against FW');
  assert.ok(!s.bans.some((b) => b.hero === bansB[0]));

  assert.deepStrictEqual(s.sides, { blue: { played: 1, won: 1 }, red: { played: 1, won: 0 }, unknown: 0 },
    'game 1 on blue and won; game 2 swapped to red and lost');

  const kai = must(s.players.find((p) => p.name === 'FW.Kai'));
  assert.strictEqual(kai.games, 2);
  assert.deepStrictEqual(kai.heroes, [{ hero: picksA[0], picked: 2, wins: 1, decided: 2 }], 'row 1 picked hero 1 both games');
  assert.ok(!s.players.some((p) => p.name === 'Player 3'), 'placeholder names are not players');
  assert.ok(!s.players.some((p) => p.name.startsWith('EA.')), 'EA\'s players are not FW\'s');

  const e = getStores().teamStats.forTeam(ea);
  assert.deepStrictEqual(e.sides, { blue: { played: 1, won: 1 }, red: { played: 1, won: 0 }, unknown: 0 },
    'EA was red and lost game 1, then blue and won game 2');

  // เกมที่เล่นก่อนเริ่มเก็บฝั่ง: รู้ผลแต่ไม่รู้ฝั่ง ต้องไม่ถูกเดา
  getStores().db.prepare('UPDATE games SET sides_swapped = NULL WHERE id = ?').run(game1.id);
  assert.deepStrictEqual(getStores().teamStats.forTeam(fw).sides,
    { blue: { played: 0, won: 0 }, red: { played: 1, won: 0 }, unknown: 1 });
});

test('a team that has played nothing gets zeros, not an error', () => {
  const [lonely] = newTeams(['LONELY']) as [string];
  const s = getStores().teamStats.forTeam(lonely);
  assert.deepStrictEqual(s.record, { seriesWon: 0, seriesLost: 0, gamesWon: 0, gamesLost: 0, tournaments: 0, form: [] });
  assert.deepStrictEqual([s.picks, s.bans, s.bansAgainst, s.opponents, s.players], [[], [], [], [], []]);
  assert.deepStrictEqual(s.sides, { blue: { played: 0, won: 0 }, red: { played: 0, won: 0 }, unknown: 0 });
});
