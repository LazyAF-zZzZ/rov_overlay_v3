// เทสต์ปุ่มจบเกม และรายการคู่ที่พร้อมเล่น
//
// สิ่งที่ต้องไม่พังคือ "นับครั้งเดียว": ปุ่มนี้แตะคะแนนซีรีส์ ผู้ชนะรายเกม และตัวเดินรอบ
// พร้อมกัน นับซ้ำหนึ่งครั้งคือสายการแข่งผิด และทีมที่ไม่ควรเข้ารอบได้เข้ารอบ

import test from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import os from 'os';
import path from 'path';

const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'rov-finish-test-'));
process.env.ROV_USER_DATA_DIR = path.join(TMP, 'data');
process.env.ROV_USER_MEDIA_DIR = path.join(TMP, 'media');
process.env.CONTROL_TOKEN = '';

const { getStores } = require('../server/store/index') as typeof import('../server/store/index');
const { closeDatabase } = require('../server/store/db') as typeof import('../server/store/db');
const live = require('../server/services/live-match') as typeof import('../server/services/live-match');
const liveState = require('../server/store/live-state') as typeof import('../server/store/live-state');
const { recordSeriesResult } = require('../server/services/series') as typeof import('../server/services/series');
const { heroesData } = require('../server/domain/heroes') as typeof import('../server/domain/heroes');
const { deepClone } = require('../server/lib/json') as typeof import('../server/lib/json');
import { must } from './helpers';

live.attachDraftCapture();

const [HERO_A, HERO_B] = heroesData.heroes;

test.after(() => {
  closeDatabase();
  try { fs.rmSync(TMP, { recursive: true, force: true }); } catch { /* ระบบเก็บเอง */ }
});

let cup = 0;

// สี่ทีม แพ้คัดออก: รอบรองสองคู่ที่เล่นได้ทันที กับรอบชิงและชิงที่ 3 ที่ยังรอผล
function setup(bestOf = 3) {
  const { teams, tournaments, matches } = getStores();
  cup += 1;
  const tournament = must(tournaments.create({ name: `Finish ${cup}`, format: 'single_elim', bestOf }).tournament);
  ['A', 'B', 'C', 'D'].forEach((name, i) => {
    const team = must(teams.create({ name: `${name}${cup}` }).team);
    tournaments.addTeam(tournament.id, team.id, i);
  });
  const drawn = must(matches.generate(tournament.id).matches);
  const semis = drawn
    .filter((m) => m.bracket === 'main' && m.round === 1 && !m.isBye)
    .sort((a, b) => a.slot - b.slot);
  assert.strictEqual(semis.length, 2, 'four teams draw two semifinals');
  return { tournament, first: must(semis[0]), second: must(semis[1]) };
}

test('finishing a game records the point and the winner, keeps the draft, and puts the next game on air', () => {
  const { first } = setup();
  const firstGameId = must(must(live.goLive(first.id).live).gameId);

  const state = liveState.getState();
  state.teamBlue.picks[0] = must(HERO_A);
  liveState.emitState();

  const out = live.finishGame('blue');
  assert.ok(out.result, out.error ?? 'finishGame failed');

  const { matches, games } = getStores();
  const after = must(matches.get(first.id));
  assert.deepStrictEqual([after.scoreA, after.scoreB], [1, 0]);

  const played = must(games.get(firstGameId));
  assert.strictEqual(played.winner, 'blue', 'the game just played has its winner');
  assert.ok(played.slots.some((s) => s.hero === HERO_A), 'its draft is still recorded against it');

  assert.strictEqual(out.result.round, 2);
  assert.strictEqual(out.result.seriesOver, false);
  assert.strictEqual(out.result.nextMatch, null);
  assert.strictEqual(out.result.live.gameNo, 2, 'game 2 is on air');

  const now = liveState.getState();
  assert.ok(!now.teamBlue.picks[0], 'the board is clean for game 2');
  assert.strictEqual(now.teamBlue.score, 1, 'and the series score is on screen');
});

test('a game whose result was already typed in is not counted a second time', () => {
  const { first } = setup();
  live.goLive(first.id);
  must(recordSeriesResult(first.id, 1, 0).match);

  const out = live.finishGame('red');
  assert.strictEqual(out.code, 'game-decided');

  const after = must(getStores().matches.get(first.id));
  assert.deepStrictEqual([after.scoreA, after.scoreB], [1, 0], 'the score did not move');
  assert.strictEqual(liveState.getState().round, 1, 'and nothing moved on');
});

test('the deciding game ends the series: no game 3, and the other semifinal is offered next', () => {
  const { first, second } = setup(3);
  live.goLive(first.id);

  must(live.finishGame('blue').result);
  const out = live.finishGame('blue');
  assert.ok(out.result, out.error ?? 'second finish failed');

  assert.strictEqual(out.result.seriesOver, true);
  assert.strictEqual(out.result.round, 2, 'still showing the last game played');
  assert.strictEqual(getStores().games.forMatch(first.id).length, 2, 'no empty game 3 row');
  assert.strictEqual(must(getStores().matches.get(first.id)).status, 'complete');
  assert.strictEqual(must(out.result.nextMatch).matchId, second.id);

  const again = live.finishGame('red');
  assert.strictEqual(again.code, 'series-over', 'a finished series cannot be scored again');
});

test('after Switch Teams, the win goes to the team shown on the blue side', () => {
  const { first } = setup();
  live.goLive(first.id);

  // สลับแบบเดียวกับคำสั่ง switchTeams ใน sockets/handlers.ts
  const state = liveState.getState();
  const blue = deepClone(state.teamBlue);
  state.teamBlue = deepClone(state.teamRed);
  state.teamRed = blue;
  liveState.emitState();

  must(live.finishGame('blue').result);
  const after = must(getStores().matches.get(first.id));
  assert.deepStrictEqual([after.scoreA, after.scoreB], [0, 1], 'team B was on the blue side, so team B scored');
});

test('a quick match adds the point and files the draft as a previous round', () => {
  live.clearLive();
  const state = liveState.getState();
  state.round = 1;
  state.rounds = [];
  state.teamRed.score = 0;
  state.teamRed.bans[0] = must(HERO_B);
  liveState.emitState();

  const out = live.finishGame('red');
  assert.ok(out.result, out.error ?? 'quick finish failed');

  const now = liveState.getState();
  assert.strictEqual(now.teamRed.score, 1);
  assert.strictEqual(now.round, 2);
  assert.strictEqual(now.rounds.length, 1, 'the draft was filed as round 1');
  assert.ok(!now.teamRed.bans[0], 'and the board is clean');
  assert.strictEqual(out.result.live.matchId, null);
});

test('ready matches leave out byes, finished series, half-known matches and finished tournaments', () => {
  const { teams, tournaments, matches } = getStores();
  cup += 1;
  const odd = must(tournaments.create({ name: `Odd ${cup}`, format: 'single_elim', bestOf: 1 }).tournament);
  for (let i = 0; i < 3; i += 1) {
    tournaments.addTeam(odd.id, must(teams.create({ name: `O${cup}-${i}` }).team).id, i);
  }
  const drawn = must(matches.generate(odd.id).matches);
  const playable = must(drawn.find((m) => !m.isBye && m.round === 1 && m.teamAId !== null && m.teamBId !== null));

  const before = live.readyMatches({ tournamentId: odd.id });
  assert.deepStrictEqual(before.map((m) => m.matchId), [playable.id], 'the bye and the half-known final are not ready');
  assert.ok(must(before[0]).blueName.length > 0 && must(before[0]).redName.length > 0, 'names come with it');

  must(recordSeriesResult(playable.id, 1, 0).match);
  const after = live.readyMatches({ tournamentId: odd.id });
  assert.ok(!after.some((m) => m.matchId === playable.id), 'a finished series drops out');
  assert.strictEqual(after.length, 1, 'and the final, now fully known, takes its place');

  tournaments.setStatus(odd.id, 'finished');
  assert.ok(!live.readyMatches().some((m) => m.tournamentId === odd.id), 'finished tournaments are not offered');
});

// เห็นจากภาพหน้าจอจริง: เรียงตามลำดับรายการ หกช่องเต็มไปด้วยรายการอื่น
// และรอบรองอีกคู่ของงานที่กำลังถ่ายทอดไม่ได้แสดงเลย
test('the tournament on air comes first among ready matches, and the limit cannot push it out', () => {
  const { tournament: onAir } = setup();
  for (let i = 0; i < 3; i += 1) setup();   // รายการอื่นที่มีคู่พร้อมเล่นมากพอจะเต็มหกช่อง

  const plain = live.readyMatches();
  const preferred = live.readyMatches({ preferTournamentId: onAir.id });
  assert.strictEqual(must(preferred[0]).tournamentId, onAir.id);
  assert.strictEqual(preferred.length, plain.length, 'nothing is dropped, only reordered');

  const capped = live.readyMatches({ preferTournamentId: onAir.id, limit: 6 });
  assert.strictEqual(capped.filter((m) => m.tournamentId === onAir.id).length, 2, 'both of its semifinals make the cut');
});
