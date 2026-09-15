// เทสต์ปุ่ม -1: ถอนเกมล่าสุด ย้อน +1 ตรงตัว
//
// สิ่งที่ต้องไม่พังมีสามอย่าง
//   1. ถอนแล้วทุกอย่างกลับเหมือนก่อนกด +1: แต้ม ผู้ชนะรายเกม เกมบนกระดาน ดราฟต์ และฝั่ง
//   2. คะแนน 0 ถอนไม่ได้ และถอนได้เฉพาะเกมล่าสุด ไม่งั้นเลขเกมเลื่อนแล้วดราฟต์เขียนทับเกมจริง
//   3. ถอนแต้มที่ปิดซีรีส์ ต้องถอนผู้ชนะออกจากคู่ถัดไปในสายด้วย

import test from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import os from 'os';
import path from 'path';

const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'rov-undo-test-'));
process.env.ROV_USER_DATA_DIR = path.join(TMP, 'data');
process.env.ROV_USER_MEDIA_DIR = path.join(TMP, 'media');
process.env.CONTROL_TOKEN = '';

const { getStores } = require('../server/store/index') as typeof import('../server/store/index');
const { closeDatabase } = require('../server/store/db') as typeof import('../server/store/db');
const live = require('../server/services/live-match') as typeof import('../server/services/live-match');
const liveState = require('../server/store/live-state') as typeof import('../server/store/live-state');
const { heroesData } = require('../server/domain/heroes') as typeof import('../server/domain/heroes');
import { sanitizeState } from '../server/domain/match';
import { must } from './helpers';

live.attachDraftCapture();

const [HERO_A] = heroesData.heroes;

test.after(() => {
  closeDatabase();
  try { fs.rmSync(TMP, { recursive: true, force: true }); } catch { /* ระบบเก็บเอง */ }
});

let cup = 0;

// สี่ทีม แพ้คัดออก สลับฝั่งทุกเกม (ค่าเริ่มต้น)
function setup(bestOf = 5) {
  const { teams, tournaments, matches } = getStores();
  cup += 1;
  const tournament = must(tournaments.create({ name: `Undo ${cup}`, format: 'single_elim', bestOf }).tournament);
  ['A', 'B', 'C', 'D'].forEach((name, i) => {
    tournaments.addTeam(tournament.id, must(teams.create({ name: `${name}${cup}` }).team).id, i);
  });
  const drawn = must(matches.generate(tournament.id).matches);
  const first = must(drawn.filter((m) => m.bracket === 'main' && m.round === 1 && !m.isBye).sort((a, b) => a.slot - b.slot)[0]);
  liveState.getState().swapSidesEachRound = true;
  return { tournament, first };
}

const matchOf = (id: string) => must(getStores().matches.get(id));
const gameOf = (matchId: string, gameNo: number) => must(getStores().games.forMatch(matchId).find((g) => g.gameNo === gameNo));

test('-1 takes back the last game and puts it back on the board, draft and sides included', () => {
  const { first } = setup();
  must(live.goLive(first.id).live);
  liveState.getState().teamBlue.picks[0] = must(HERO_A);   // ทีม A เลือกในเกมที่ 1
  liveState.emitState();

  must(live.finishGame('blue').result);                    // ทีม A ชนะ -> เกมที่ 2, ทีม A อยู่แดง
  assert.strictEqual(liveState.getState().round, 2);

  const out = live.undoGame('red');
  assert.ok(out.result, out.error ?? 'undo failed');
  assert.strictEqual(out.result.reopened, false);

  const m = matchOf(first.id);
  assert.deepStrictEqual([m.scoreA, m.scoreB], [0, 0], 'the point is gone');
  assert.strictEqual(gameOf(first.id, 1).winner, null, 'and game 1 has no winner again');

  const s = liveState.getState();
  assert.strictEqual(s.round, 1, 'game 1 is back on the board');
  assert.strictEqual(s.teamBlue.logo.src, first.teamAId, 'with game 1\'s sides');
  assert.strictEqual(s.teamBlue.picks[0], HERO_A, 'and game 1\'s draft');
  assert.strictEqual(s.teamBlue.score, 0);
});

test('at zero there is nothing to take back, and nothing moves', () => {
  const { first } = setup();
  must(live.goLive(first.id).live);

  const out = live.undoGame('blue');
  assert.strictEqual(out.code, 'no-points');
  assert.strictEqual(liveState.getState().round, 1);
  const m = matchOf(first.id);
  assert.deepStrictEqual([m.scoreA, m.scoreB], [0, 0]);
});

test('only the team that won the last game can have it taken back', () => {
  const { first } = setup();
  must(live.goLive(first.id).live);
  must(live.finishGame('blue').result);   // เกมที่ 1: ทีม A (น้ำเงิน) ชนะ
  must(live.finishGame('blue').result);   // เกมที่ 2: ทีม B (น้ำเงินหลังสลับ) ชนะ -> 1-1, เกมที่ 3 ทีม A น้ำเงิน

  const refused = live.undoGame('blue');  // ทีม A ไม่ได้ชนะเกมล่าสุด
  assert.strictEqual(refused.code, 'not-last');
  assert.deepStrictEqual([matchOf(first.id).scoreA, matchOf(first.id).scoreB], [1, 1], 'nothing changed');
  assert.strictEqual(liveState.getState().round, 3);

  const out = live.undoGame('red');       // ทีม B ชนะเกมที่ 2 ถอนได้
  assert.ok(out.result, out.error ?? 'undo failed');
  assert.deepStrictEqual([matchOf(first.id).scoreA, matchOf(first.id).scoreB], [1, 0]);
  assert.strictEqual(gameOf(first.id, 2).winner, null, 'game 2 is unplayed again');
  assert.strictEqual(gameOf(first.id, 1).winner, 'blue', 'game 1 keeps team A\'s win');
  assert.strictEqual(liveState.getState().round, 2);
  assert.strictEqual(liveState.getState().teamBlue.logo.src, first.teamBId, 'game 2 has its swapped sides back');
});

test('taking back the deciding point reopens the series and pulls the winner out of the next round', () => {
  const { tournament, first } = setup(3);
  must(live.goLive(first.id).live);
  must(live.finishGame('blue').result);   // ทีม A ชนะเกมที่ 1
  must(live.finishGame('red').result);    // ทีม A (แดงในเกมที่ 2) ชนะ -> 2-0 จบซีรีส์

  const finalBefore = must(getStores().matches.list(tournament.id).find((m) => m.bracket === 'main' && m.round === 2));
  assert.ok([finalBefore.teamAId, finalBefore.teamBId].includes(first.teamAId), 'team A went through');

  const out = live.undoGame('red');
  assert.ok(out.result, out.error ?? 'undo failed');
  assert.strictEqual(out.result.reopened, true);

  const m = matchOf(first.id);
  assert.deepStrictEqual([m.scoreA, m.scoreB], [1, 0]);
  assert.strictEqual(m.winnerId, null, 'the series has no winner any more');
  assert.notStrictEqual(m.status, 'complete');
  assert.strictEqual(gameOf(first.id, 2).winner, null);

  const finalAfter = must(getStores().matches.list(tournament.id).find((m) => m.bracket === 'main' && m.round === 2));
  assert.ok(![finalAfter.teamAId, finalAfter.teamBId].includes(first.teamAId), 'team A is out of the final again');
  assert.strictEqual(liveState.getState().round, 2, 'game 2 stays on the board to be played again');
});

test('a quick match takes the point back and steps back a round, sides and draft included', () => {
  live.clearLive();
  liveState.setState(sanitizeState({ teamBlue: { name: 'ALPHA' }, teamRed: { name: 'BRAVO' }, swapSidesEachRound: true }));
  liveState.getState().teamBlue.picks[0] = must(HERO_A);

  must(live.finishGame('blue').result);   // ALPHA ชนะ -> รอบ 2, ALPHA อยู่แดง
  assert.deepStrictEqual([liveState.getState().teamRed.name, liveState.getState().teamRed.score], ['ALPHA', 1]);

  const out = live.undoGame('red');
  assert.ok(out.result, out.error ?? 'undo failed');
  const s = liveState.getState();
  assert.deepStrictEqual([s.round, s.teamBlue.name, s.teamBlue.score, s.teamBlue.picks[0]], [1, 'ALPHA', 0, HERO_A]);

  assert.strictEqual(live.undoGame('blue').code, 'no-points', 'and at zero it refuses');
});
