// เทสต์การสลับฝั่งทุกเกม
//
// ผู้ใช้ขอมา: เปลี่ยนรอบแล้วให้ทีมสลับฝั่งเอง
//
// ส่วนที่ต้องไม่พังคือ "สลับแค่บนจอ แต่ข้อมูลยังเป็นของทีมเดิม"
// แต้มซีรีส์ ดราฟต์ที่บันทึกลงฐาน และกระดานรอบก่อนหน้า ต้องตามทีมไป ไม่ใช่ตามสี
// ถ้าตามสี ดราฟต์ของทีมหนึ่งจะไปนับเป็นของอีกทีมในสถิติแบบเงียบๆ

import test from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import os from 'os';
import path from 'path';

const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'rov-swap-test-'));
process.env.ROV_USER_DATA_DIR = path.join(TMP, 'data');
process.env.ROV_USER_MEDIA_DIR = path.join(TMP, 'media');
process.env.CONTROL_TOKEN = '';

const { getStores } = require('../server/store/index') as typeof import('../server/store/index');
const { closeDatabase } = require('../server/store/db') as typeof import('../server/store/db');
const live = require('../server/services/live-match') as typeof import('../server/services/live-match');
const liveState = require('../server/store/live-state') as typeof import('../server/store/live-state');
const { heroesData } = require('../server/domain/heroes') as typeof import('../server/domain/heroes');
import { sanitizeState, defaultState } from '../server/domain/match';
import { carryOverSettings, CARRIED_OVER_KEYS } from '../server/domain/settings';
import { must } from './helpers';

live.attachDraftCapture();

const [HERO_A, HERO_B] = heroesData.heroes;

test.after(() => {
  closeDatabase();
  try { fs.rmSync(TMP, { recursive: true, force: true }); } catch { /* ระบบเก็บเอง */ }
});

// FW เป็นทีม A ของแมตช์ (น้ำเงินในเกมที่ 1) EA เป็นทีม B
function setupMatch(swap = true) {
  const { teams, tournaments, matches } = getStores();
  const tournament = must(tournaments.create({ name: `Swap ${Math.random()}`, format: 'single_elim', bestOf: 5 }).tournament);
  const fw = must(teams.create({ name: 'FW' }).team);
  const ea = must(teams.create({ name: 'EA' }).team);
  tournaments.addTeam(tournament.id, fw.id, 0);
  tournaments.addTeam(tournament.id, ea.id, 1);
  const match = must(must(matches.generate(tournament.id).matches)[0]);
  liveState.getState().swapSidesEachRound = swap;
  return { match, fw, ea };
}

const screen = () => {
  const s = liveState.getState();
  return { blue: s.teamBlue.name, red: s.teamRed.name, blueScore: s.teamBlue.score, redScore: s.teamRed.score };
};

test('the teams swap sides on even games and swap back on odd ones', () => {
  const { match } = setupMatch();
  must(live.goLive(match.id).live);
  assert.deepStrictEqual([screen().blue, screen().red], ['FW', 'EA'], 'game 1 is the draw order');

  must(live.stepRound(1).result);
  assert.deepStrictEqual([screen().blue, screen().red], ['EA', 'FW'], 'game 2 swaps');
  assert.match(liveState.getState().matchInfo.title, /^EA VS FW : GAME 2/, 'the title follows the sides');

  must(live.stepRound(1).result);
  assert.deepStrictEqual([screen().blue, screen().red], ['FW', 'EA'], 'game 3 swaps back');

  must(live.stepRound(-1).result);
  assert.deepStrictEqual([screen().blue, screen().red], ['EA', 'FW'], 'going back to game 2 gives game 2 its own sides');
});

test('each team keeps its own series score across the swap', () => {
  const { match } = setupMatch();
  must(live.goLive(match.id).live);

  must(live.finishGame('blue').result);           // FW ชนะเกมที่ 1
  assert.deepStrictEqual(screen(), { blue: 'EA', red: 'FW', blueScore: 0, redScore: 1 });

  must(live.finishGame('blue').result);           // EA (น้ำเงินในเกมที่ 2) ชนะเกมที่ 2
  const m = must(getStores().matches.get(match.id));
  assert.deepStrictEqual([m.scoreA, m.scoreB], [1, 1], 'the point went to EA, not to "blue"');
  assert.deepStrictEqual(screen(), { blue: 'FW', red: 'EA', blueScore: 1, redScore: 1 }, 'game 3, back in draw order');
});

test('a draft made after the swap is recorded against the team that made it', () => {
  const { match } = setupMatch();
  must(live.goLive(match.id).live);
  must(live.stepRound(1).result);

  liveState.getState().teamBlue.picks[0] = must(HERO_A);   // EA อยู่น้ำเงินในเกมที่ 2
  liveState.emitState();

  const game2 = must(getStores().games.forMatch(match.id).find((g) => g.gameNo === 2));
  assert.strictEqual(game2.redName, 'EA', 'the frozen copy still has team B on red');
  assert.deepStrictEqual(game2.slots, [{ side: 'red', kind: 'pick', idx: 0, hero: HERO_A }],
    'stored as EA\'s pick, so analytics credits EA');
});

test('walking back and forth puts every draft under the team that made it', () => {
  const { match } = setupMatch();
  must(live.goLive(match.id).live);
  liveState.getState().teamBlue.picks[0] = must(HERO_A);   // FW, เกมที่ 1
  liveState.emitState();

  must(live.stepRound(1).result);
  liveState.getState().teamBlue.picks[0] = must(HERO_B);   // EA, เกมที่ 2
  liveState.emitState();

  must(live.stepRound(-1).result);
  let s = liveState.getState();
  assert.deepStrictEqual([s.teamBlue.name, s.teamBlue.picks[0]], ['FW', HERO_A]);

  must(live.stepRound(1).result);
  s = liveState.getState();
  assert.deepStrictEqual([s.teamBlue.name, s.teamBlue.picks[0]], ['EA', HERO_B]);
  assert.strictEqual(s.teamRed.picks[0], null, 'FW\'s game 1 pick did not leak into game 2');
});

test('the previous rounds line up with the swapped screen, names and all', () => {
  const { match } = setupMatch();
  must(live.goLive(match.id).live);
  liveState.getState().teamBlue.picks[0] = must(HERO_A);   // FW, เกมที่ 1
  liveState.emitState();

  must(live.stepRound(1).result);
  const round1 = must(liveState.getState().rounds[0]);
  assert.strictEqual(round1.red.name, 'FW', 'FW is on red now, in the old round too');
  assert.strictEqual(round1.red.picks[0], HERO_A);
  assert.strictEqual(round1.blue.name, 'EA');
});

test('switched off, the sides stay where the draw put them', () => {
  const { match } = setupMatch(false);
  must(live.goLive(match.id).live);
  must(live.stepRound(1).result);
  assert.deepStrictEqual([screen().blue, screen().red], ['FW', 'EA']);
});

test('the setting is on by default and survives a reset and a match change', () => {
  assert.strictEqual(defaultState.swapSidesEachRound, true);
  assert.strictEqual(sanitizeState({}).swapSidesEachRound, true, 'old save files get it switched on');
  assert.strictEqual(sanitizeState({ swapSidesEachRound: false }).swapSidesEachRound, false);
  assert.ok((CARRIED_OVER_KEYS as readonly string[]).includes('swapSidesEachRound'));

  const reset = carryOverSettings(sanitizeState(defaultState), sanitizeState({ swapSidesEachRound: false }));
  assert.strictEqual(reset.swapSidesEachRound, false, 'RESET MATCH keeps it');

  const { match } = setupMatch(false);
  must(live.goLive(match.id).live);
  assert.strictEqual(liveState.getState().swapSidesEachRound, false, 'putting a match on air keeps it');
});

test('a quick match swaps on every round step, and each round keeps its draft by team', () => {
  getStores().liveMatch.clear();
  liveState.setState(sanitizeState({
    teamBlue: { name: 'ALPHA', score: 1 },
    teamRed: { name: 'BRAVO' },
    swapSidesEachRound: true
  }));
  liveState.getState().teamBlue.picks[0] = must(HERO_A);   // ALPHA, รอบ 1

  must(live.stepRound(1).result);
  let s = liveState.getState();
  assert.deepStrictEqual([s.teamBlue.name, s.teamRed.name], ['BRAVO', 'ALPHA']);
  assert.strictEqual(s.teamRed.score, 1, 'ALPHA\'s score moves with it');
  assert.strictEqual(must(s.rounds[0]).blue.name, 'ALPHA', 'round 1 was filed as it was played');
  s.teamBlue.picks[0] = must(HERO_B);                       // BRAVO, รอบ 2

  must(live.stepRound(-1).result);
  s = liveState.getState();
  assert.deepStrictEqual([s.teamBlue.name, s.teamBlue.picks[0], s.teamRed.picks[0]], ['ALPHA', HERO_A, null]);

  must(live.stepRound(1).result);
  s = liveState.getState();
  assert.deepStrictEqual([s.teamBlue.name, s.teamBlue.picks[0], s.teamRed.picks[0]], ['BRAVO', HERO_B, null]);
});
