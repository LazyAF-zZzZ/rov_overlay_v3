// การ์ดทีมสำหรับขึ้นจอ (/api/team-card)
//
// เรื่องที่ต้องถูกที่สุดคือ "ฝั่ง" ทีมสลับฝั่งทุกเกม การ์ดฝั่งน้ำเงินต้องขึ้นทีมที่อยู่ฝั่งน้ำเงิน
// ตอนนี้ ไม่ใช่ทีม A ของแมตช์ ไม่งั้นหลังเกมแรกการ์ดสองใบจะขึ้นสลับกับสีบนจอ
// ส่วนตัวเลขข้างใน teamStats.forTeam มีเทสต์ของตัวเองแล้ว (team-stats.test.ts)
//
// ตั้ง env ก่อน import อะไรก็ตาม เหตุผลเดียวกับ tournament-api.test.ts

import test from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import os from 'os';
import path from 'path';
import http from 'http';

const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'rov-team-card-'));
process.env.ROV_USER_DATA_DIR = path.join(TMP, 'data');
process.env.ROV_USER_MEDIA_DIR = path.join(TMP, 'media');
process.env.ROV_USER_SOUND_DIR = path.join(TMP, 'sounds');
process.env.CONTROL_TOKEN = '';

const { createApp } = require('../server/index') as typeof import('../server/index');
const { getStores } = require('../server/store/index') as typeof import('../server/store/index');
const { closeDatabase } = require('../server/store/db') as typeof import('../server/store/db');
const live = require('../server/services/live-match') as typeof import('../server/services/live-match');
const liveState = require('../server/store/live-state') as typeof import('../server/store/live-state');
import { must } from './helpers';

const app = createApp();
let server: http.Server;
let base = '';

interface Reply { status: number; body: any }

function request(method: string, url: string): Promise<Reply> {
  return new Promise((resolve, reject) => {
    const req = http.request(`${base}${url}`, { method }, (res) => {
      const chunks: Buffer[] = [];
      res.on('data', (c: Buffer) => chunks.push(c));
      res.on('end', () => {
        const text = Buffer.concat(chunks).toString();
        let body: any = null;
        try { body = JSON.parse(text); } catch { body = text; }
        resolve({ status: res.statusCode || 0, body });
      });
    });
    req.on('error', reject);
    req.end();
  });
}

test.before(async () => {
  await new Promise<void>((resolve) => {
    server = app.listen(0, '127.0.0.1', () => {
      const addr = server.address() as { port: number };
      base = `http://127.0.0.1:${addr.port}`;
      resolve();
    });
  });
});

test.after(() => {
  server?.close();
  closeDatabase();
  try { fs.rmSync(TMP, { recursive: true, force: true }); } catch { /* ระบบเก็บเอง */ }
});

let cup = 0;

function setup() {
  const { teams, tournaments, matches } = getStores();
  cup += 1;
  const tournament = must(tournaments.create({ name: `Card Cup ${cup}`, format: 'single_elim', bestOf: 3 }).tournament);
  ['North', 'South', 'East', 'West'].forEach((name, i) => {
    tournaments.addTeam(tournament.id, must(teams.create({ name: `${name} ${cup}`, tag: `T${i}` }).team).id, i);
  });
  const drawn = must(matches.generate(tournament.id).matches);
  const first = must(drawn.filter((m) => m.bracket === 'main' && m.round === 1 && !m.isBye).sort((a, b) => a.slot - b.slot)[0]);
  liveState.getState().swapSidesEachRound = true;
  return { tournament, first };
}

test('the card follows the team on each side of the match on air, and keeps up when sides swap', async () => {
  const { tournament, first } = setup();
  must(live.goLive(first.id).live);

  const blue = await request('GET', '/api/team-card?side=blue');
  assert.strictEqual(blue.status, 200);
  assert.strictEqual(blue.body.team.id, first.teamAId, 'team A is on blue in game 1');
  assert.strictEqual(blue.body.side, 'blue');
  assert.strictEqual(blue.body.tournament.id, tournament.id, 'counts the tournament on air unless told otherwise');
  assert.strictEqual(typeof blue.body.stats.record.gamesWon, 'number', 'with the same stats as the team page');

  const red = await request('GET', '/api/team-card?side=red');
  assert.strictEqual(red.body.team.id, first.teamBId);

  const noSide = await request('GET', '/api/team-card');
  assert.strictEqual(noSide.body.team.id, first.teamAId, 'no side means blue');

  must(live.finishGame('blue').result);   // game 2: the teams swap sides
  const swapped = await request('GET', '/api/team-card?side=blue');
  assert.strictEqual(swapped.body.team.id, first.teamBId, 'team B is on blue in game 2, so the blue card is team B now');
  assert.strictEqual(swapped.body.stats.record.gamesLost, 1, 'and it already counts the game just played');
});

// ทุกกราฟิกที่ตามคู่ออกอากาศต้องระบายสีตรงกับ overlay หลัก
// เจอบนจอจริง: เกมที่ 4 overlay หลักมีทีม B ฝั่งน้ำเงิน แต่หัวต่อหัวกับตัวที่แต่ละทีมหยิบ
// ยังระบายทีม A เป็นน้ำเงิน เพราะเคยเอาทีม A ของสายเป็นน้ำเงินเสมอ
test('every graphic that follows the match on air colours the teams the way the screen does', async () => {
  const { first } = setup();
  must(live.goLive(first.id).live);

  const game1 = {
    card: (await request('GET', '/api/team-card?side=blue')).body.team.id,
    matchup: (await request('GET', '/api/matchup')).body.matchup.a.teamId,
    drafts: (await request('GET', '/api/team-drafts')).body.a.teamId
  };
  assert.deepStrictEqual(game1, { card: first.teamAId, matchup: first.teamAId, drafts: first.teamAId }, 'game 1: team A is blue everywhere');

  must(live.finishGame('blue').result);   // game 2: team B moves to blue
  assert.strictEqual(liveState.getState().teamBlue.logo.src, first.teamBId, 'the main overlay has team B on blue');

  const game2 = {
    card: (await request('GET', '/api/team-card?side=blue')).body.team.id,
    matchup: (await request('GET', '/api/matchup')).body.matchup.a.teamId,
    drafts: (await request('GET', '/api/team-drafts')).body.a.teamId
  };
  assert.deepStrictEqual(game2, { card: first.teamBId, matchup: first.teamBId, drafts: first.teamBId }, 'game 2: team B is blue everywhere');
});

test('a named team, one tournament or every tournament', async () => {
  const { tournament, first } = setup();

  const fixed = await request('GET', `/api/team-card?team=${first.teamBId}&tournament=all`);
  assert.strictEqual(fixed.status, 200);
  assert.strictEqual(fixed.body.team.id, first.teamBId);
  assert.strictEqual(fixed.body.side, null, 'a named team does not belong to a side');
  assert.strictEqual(fixed.body.tournament, null, 'all = no tournament filter');

  const one = await request('GET', `/api/team-card?team=${first.teamBId}&tournament=${tournament.id}`);
  assert.strictEqual(one.body.tournament.name, tournament.name);

  const badTournament = await request('GET', `/api/team-card?team=${first.teamBId}&tournament=nope1234`);
  assert.strictEqual(badTournament.status, 404);
});

test('an unknown team, or a side typed in by hand, says so instead of drawing somebody else', async () => {
  setup();
  const unknown = await request('GET', '/api/team-card?team=nope1234');
  assert.strictEqual(unknown.status, 404, 'never falls back to the team on a side');

  const bad = await request('GET', '/api/team-card?team=../../etc');
  assert.strictEqual(bad.status, 404);

  live.clearLive();
  const state = liveState.getState();
  state.teamBlue.name = 'Typed In';
  state.teamBlue.logo = { ...state.teamBlue.logo, src: '' };
  const typed = await request('GET', '/api/team-card?side=blue');
  assert.strictEqual(typed.status, 404);
  assert.match(String(typed.body.error), /blue side/);
});
