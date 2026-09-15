// เทสต์คีย์ลัดระดับระบบ
//
// ตั้ง env ก่อน import อะไรก็ตาม เพราะ config อ่านตอน import
//
// เรื่องที่ต้องถูกที่สุดคือ "ปุ่มแบบไหนห้ามจอง"
// การจองคีย์ลัดระดับระบบคือการยึดปุ่มไปจากทุกโปรแกรมในเครื่อง
// พลาดตรงนี้ทีเดียวคือคนใช้พิมพ์ปุ่มนั้นไม่ได้ทั้งเครื่อง โดยไม่มีทางเดาถูกว่าเพราะอะไร

import test from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import os from 'os';
import path from 'path';
import http from 'http';

const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'rov-globalkeys-test-'));
process.env.ROV_USER_DATA_DIR = path.join(TMP, 'data');
process.env.ROV_USER_MEDIA_DIR = path.join(TMP, 'media');
process.env.CONTROL_TOKEN = '';

const { createApp } = require('../server/index') as typeof import('../server/index');
const { closeDatabase } = require('../server/store/db') as typeof import('../server/store/db');
const settings = require('../server/domain/settings') as typeof import('../server/domain/settings');
const liveState = require('../server/store/live-state') as typeof import('../server/store/live-state');
const draftEngine = require('../server/services/draft-engine') as typeof import('../server/services/draft-engine');

const live = require('../server/services/live-match') as typeof import('../server/services/live-match');
const { getStores } = require('../server/store/index') as typeof import('../server/store/index');
import { must } from './helpers';

const { toAccelerator, sanitizeGlobalHotkeys, GLOBAL_HOTKEY_DEFAULTS, GLOBAL_HOTKEY_ACTIONS, CARRIED_OVER_KEYS } = settings;

const app = createApp();
let server: http.Server;
let base = '';

interface Reply { status: number; body: any }

function request(method: string, url: string, payload?: unknown): Promise<Reply> {
  return new Promise((resolve, reject) => {
    const data = payload === undefined ? null : JSON.stringify(payload);
    const req = http.request(
      `${base}${url}`,
      {
        method,
        headers: data
          ? { 'content-type': 'application/json', 'content-length': Buffer.byteLength(data) }
          : {}
      },
      (res) => {
        const chunks: Buffer[] = [];
        res.on('data', (c: Buffer) => chunks.push(c));
        res.on('end', () => {
          const text = Buffer.concat(chunks).toString();
          let body: any = null;
          try { body = text ? JSON.parse(text) : null; } catch { body = text; }
          resolve({ status: res.statusCode || 0, body });
        });
      }
    );
    req.on('error', reject);
    if (data) req.write(data);
    req.end();
  });
}

test.before(async () => {
  await new Promise<void>((resolve) => {
    server = app.listen(0, '127.0.0.1', () => {
      const address = server.address();
      base = `http://127.0.0.1:${typeof address === 'object' && address ? address.port : 0}`;
      resolve();
    });
  });
});

test.after(async () => {
  // startDraftPhase เปิด setInterval ไว้ ถ้าไม่หยุด process ของเทสต์จะไม่ยอมจบ
  draftEngine.stopDraftTimer();
  closeDatabase();
  await new Promise<void>((resolve) => server.close(() => resolve()));
  try { fs.rmSync(TMP, { recursive: true, force: true }); } catch { /* ระบบเก็บเอง */ }
});

// ---- กฎที่ห้ามพัง ----

test('a system-wide hotkey without a modifier cannot be built at all', () => {
  const bare = { code: 'Space', ctrl: false, shift: false, alt: false, meta: false };
  assert.strictEqual(toAccelerator(bare), null, 'a bare Space would be taken from the whole machine');

  const withCtrl = { ...bare, ctrl: true };
  assert.strictEqual(toAccelerator(withCtrl), 'Control+Space');
});

test('modifiers come out in a fixed order, so the same binding is always the same string', () => {
  const all = { code: 'KeyH', ctrl: true, shift: true, alt: true, meta: true };
  assert.strictEqual(toAccelerator(all), 'Control+Alt+Shift+Super+H');
});

test('keys the accelerator table does not know are refused, not guessed at', () => {
  assert.strictEqual(toAccelerator({ code: 'Alt', ctrl: true, shift: false, alt: false, meta: false }), null);
  assert.strictEqual(toAccelerator({ code: 'Nonsense', ctrl: true, shift: false, alt: false, meta: false }), null);
  assert.strictEqual(toAccelerator(null), null);
  assert.strictEqual(
    toAccelerator({ code: 'ArrowLeft', ctrl: true, shift: false, alt: true, meta: false }),
    'Control+Alt+Left',
    'DOM code names are translated, not passed through'
  );
});

test('a binding that cannot be registered falls back to the default rather than being kept', () => {
  const cleaned = sanitizeGlobalHotkeys({
    enabled: true,
    bindings: { undo: { code: 'KeyZ', ctrl: false, shift: false, alt: false, meta: false } }
  });
  assert.deepStrictEqual(cleaned.bindings.undo, GLOBAL_HOTKEY_DEFAULTS.bindings.undo);
  assert.strictEqual(cleaned.enabled, true);
});

// เปิดเป็นค่าเริ่มต้นตั้งแต่ 3.1.0 (ผู้ใช้ขอ) รวมถึงคนที่อัปเดตมาจากเวอร์ชันที่ค่าเริ่มต้นคือปิด
// แต่หลังจากนั้น ปิดเองต้องปิดอยู่ และค่ามั่วๆ ต้องไม่นับเป็นเปิด
test('hotkeys are on by default, including after an update, and switching them off sticks', () => {
  const layout = settings.GLOBAL_HOTKEY_LAYOUT;
  assert.strictEqual(GLOBAL_HOTKEY_DEFAULTS.enabled, true, 'a new install has them on');
  assert.strictEqual(sanitizeGlobalHotkeys(null).enabled, true);
  assert.strictEqual(sanitizeGlobalHotkeys({ enabled: false }).enabled, true, 'saved as off by an older version, where off was the default');
  assert.strictEqual(sanitizeGlobalHotkeys({ layout, enabled: false }).enabled, false, 'switched off on this version stays off');
  assert.strictEqual(sanitizeGlobalHotkeys({ layout, enabled: 'yes' }).enabled, false, 'junk never switches them on');
  assert.deepStrictEqual(sanitizeGlobalHotkeys(null).bindings, GLOBAL_HOTKEY_DEFAULTS.bindings);
});

test('the setting survives a reset, the way theme and hotkeys do', () => {
  assert.ok(
    (CARRIED_OVER_KEYS as readonly string[]).includes('globalHotkeys'),
    'switching a match must not silently drop the keys a caster is holding'
  );
});

// ---- ทางที่ Electron ใช้ ----

test('the app can ask what to register, and gets accelerators rather than raw bindings', async () => {
  const before = await request('GET', '/api/global-hotkeys');
  assert.strictEqual(before.status, 200);
  assert.strictEqual(before.body.enabled, true, 'on from the start');
  // ปิดอยู่ก็ยังบอกได้ว่าถ้าเปิดจะจองอะไร ตัวจองเป็นคนตัดสินใจว่าจะจองหรือไม่
  assert.strictEqual(before.body.accelerators.undo, 'Control+Alt+Z');
});

test('firing an action does nothing while the feature is switched off', async () => {
  const state = liveState.getState();
  state.globalHotkeys = { ...state.globalHotkeys, enabled: false };

  const refused = await request('POST', '/api/global-hotkeys/fire', { action: 'toggleBanner' });
  assert.strictEqual(refused.status, 409, 'a stray request cannot drive the broadcast');
});

test('an unknown action is refused rather than ignored quietly', async () => {
  const state = liveState.getState();
  state.globalHotkeys = { ...state.globalHotkeys, enabled: true };

  assert.strictEqual((await request('POST', '/api/global-hotkeys/fire', { action: 'rm -rf' })).status, 400);
  assert.strictEqual((await request('POST', '/api/global-hotkeys/fire', {})).status, 400);
});

test('firing toggleBanner really moves the overlay on and off air', async () => {
  const state = liveState.getState();
  state.globalHotkeys = { ...state.globalHotkeys, enabled: true };
  state.overlayVisible = true;

  const off = await request('POST', '/api/global-hotkeys/fire', { action: 'toggleBanner' });
  assert.strictEqual(off.status, 200);
  assert.strictEqual(liveState.getState().overlayVisible, false, 'the banner went off air');

  await request('POST', '/api/global-hotkeys/fire', { action: 'toggleBanner' });
  assert.strictEqual(liveState.getState().overlayVisible, true, 'and back on');
});

test('firing nextPhase and prevPhase walks the draft, and prev stops at the first phase', async () => {
  const state = liveState.getState();
  state.globalHotkeys = { ...state.globalHotkeys, enabled: true };
  state.draftPhaseIndex = 0;

  await request('POST', '/api/global-hotkeys/fire', { action: 'nextPhase' });
  assert.strictEqual(liveState.getState().draftPhaseIndex, 1);

  await request('POST', '/api/global-hotkeys/fire', { action: 'prevPhase' });
  assert.strictEqual(liveState.getState().draftPhaseIndex, 0);

  await request('POST', '/api/global-hotkeys/fire', { action: 'prevPhase' });
  assert.strictEqual(liveState.getState().draftPhaseIndex, 0, 'never below the first phase');
});

test('undo reports that nothing changed instead of failing when there is nothing to undo', async () => {
  const state = liveState.getState();
  state.globalHotkeys = { ...state.globalHotkeys, enabled: true };

  const reply = await request('POST', '/api/global-hotkeys/fire', { action: 'undo' });
  assert.strictEqual(reply.status, 200, 'a caster pressing undo at OBS gets no dialog either way');
  assert.strictEqual(typeof reply.body.changed, 'boolean');
});

// ---- คะแนนกับรอบ (3.1.0) ----
//
// จังหวะที่ต้องกดคีย์ลัดมากที่สุดคือตอนเกมจบ ซึ่งคนคุมงานอยู่ที่ OBS
// ต้องทำงานเหมือนปุ่มข้างคะแนนทุกอย่าง และส่งผลก้อนเดียวกันกลับไปให้หน้า Control ขึ้นแถบซีรีส์จบ

test('every system-wide default can be registered, and no two actions share a key', () => {
  const accelerators = GLOBAL_HOTKEY_ACTIONS.map((action) => toAccelerator(GLOBAL_HOTKEY_DEFAULTS.bindings[action]));
  accelerators.forEach((accelerator, i) => {
    assert.ok(accelerator, `${GLOBAL_HOTKEY_ACTIONS[i]} has a default Windows will accept`);
  });
  assert.strictEqual(new Set(accelerators).size, accelerators.length, 'one key, one action');
});

test('settings saved before the score keys existed get them at their defaults, and keep the rest', () => {
  const old = sanitizeGlobalHotkeys({
    enabled: true,
    bindings: { undo: { code: 'KeyU', ctrl: true, shift: false, alt: true, meta: false } }
  });
  assert.strictEqual(old.enabled, true, 'the switch stays on');
  assert.strictEqual(old.bindings.undo.code, 'KeyU', 'a key someone chose stays chosen');
  assert.deepStrictEqual(old.bindings.bluePlus, GLOBAL_HOTKEY_DEFAULTS.bindings.bluePlus);
  assert.deepStrictEqual(old.bindings.nextRound, GLOBAL_HOTKEY_DEFAULTS.bindings.nextRound);
});

test('keys still on the old two-handed defaults move to the new block once, chosen keys stay', () => {
  const saved = sanitizeGlobalHotkeys({
    enabled: true,
    bindings: {
      toggleBanner: { code: 'KeyH', ctrl: true, shift: false, alt: true, meta: false },   // old default
      pauseResume: { code: 'KeyP', ctrl: true, shift: false, alt: true, meta: false },    // chosen
      prevPhase: { code: 'ArrowLeft', ctrl: true, shift: false, alt: true, meta: false }  // old default
    }
  });
  assert.strictEqual(saved.layout, settings.GLOBAL_HOTKEY_LAYOUT);
  assert.deepStrictEqual(saved.bindings.toggleBanner, GLOBAL_HOTKEY_DEFAULTS.bindings.toggleBanner, 'moved to the block');
  assert.deepStrictEqual(saved.bindings.prevPhase, GLOBAL_HOTKEY_DEFAULTS.bindings.prevPhase, 'moved to the block');
  assert.strictEqual(saved.bindings.pauseResume.code, 'KeyP', 'a key someone chose is not touched');

  // ตั้ง Ctrl+Alt+H กลับมาเองหลังย้ายแล้ว ต้องไม่โดนย้ายซ้ำ
  const chosenAgain = sanitizeGlobalHotkeys({
    ...saved,
    bindings: { ...saved.bindings, toggleBanner: { code: 'KeyH', ctrl: true, shift: false, alt: true, meta: false } }
  });
  assert.strictEqual(chosenAgain.bindings.toggleBanner.code, 'KeyH', 'moved once, never again');
});

test('every default sits under the left hand holding Ctrl+Alt', () => {
  const leftHand = new Set(['Digit1', 'Digit2', 'KeyQ', 'KeyW', 'KeyE', 'KeyR', 'KeyA', 'KeyS', 'KeyD', 'KeyF', 'KeyZ']);
  GLOBAL_HOTKEY_ACTIONS.forEach((action) => {
    const binding = GLOBAL_HOTKEY_DEFAULTS.bindings[action];
    assert.ok(leftHand.has(binding.code), `${action} is ${binding.code}, outside the block`);
    assert.deepStrictEqual([binding.ctrl, binding.alt, binding.shift, binding.meta], [true, true, false, false], `${action} is Ctrl+Alt only`);
  });
});

test('the score and round keys play a quick match the way the buttons beside the score do', async () => {
  live.clearLive();
  const state = liveState.getState();
  state.globalHotkeys = { ...state.globalHotkeys, enabled: true };
  state.swapSidesEachRound = true;
  state.round = 1;
  state.teamBlue.name = 'Alpha';
  state.teamBlue.score = 0;
  state.teamRed.name = 'Bravo';
  state.teamRed.score = 0;

  const fire = (action: string) => request('POST', '/api/global-hotkeys/fire', { action });
  const team = (name: string) => {
    const s = liveState.getState();
    return s.teamBlue.name === name ? s.teamBlue : s.teamRed;
  };

  const plus = await fire('bluePlus');
  assert.strictEqual(plus.status, 200);
  assert.strictEqual(plus.body.changed, true);
  assert.strictEqual(plus.body.finish.teamName, 'Alpha', 'named before the sides swapped');
  assert.strictEqual(liveState.getState().round, 2, 'the next game is on the board');
  assert.strictEqual(team('Alpha').score, 1);
  assert.strictEqual(liveState.getState().teamRed.name, 'Alpha', 'and the winner has moved to red');

  const minus = await fire('redMinus');
  assert.strictEqual(minus.body.changed, true);
  assert.strictEqual(minus.body.undo.teamName, 'Alpha');
  assert.strictEqual(team('Alpha').score, 0, 'the point is back off');
  assert.strictEqual(liveState.getState().round, 1);

  const nothing = await fire('blueMinus');
  assert.strictEqual(nothing.status, 200, 'refused, but not an error for someone pressing a key in OBS');
  assert.deepStrictEqual([nothing.body.changed, nothing.body.code], [false, 'no-points']);

  assert.strictEqual((await fire('nextRound')).body.changed, true);
  assert.strictEqual(liveState.getState().round, 2);
  assert.strictEqual((await fire('prevRound')).body.changed, true);
  assert.strictEqual(liveState.getState().round, 1);
  const first = await fire('prevRound');
  assert.deepStrictEqual([first.body.changed, first.body.code], [false, 'round-first']);
});

test('a +1 key that ends a series brings back what the Control Panel needs for SERIES OVER', async () => {
  const { teams, tournaments, matches } = getStores();
  const tournament = must(tournaments.create({ name: 'Hotkey Cup', format: 'single_elim', bestOf: 1 }).tournament);
  ['North', 'South', 'East', 'West'].forEach((name, i) => {
    tournaments.addTeam(tournament.id, must(teams.create({ name }).team).id, i);
  });
  const drawn = must(matches.generate(tournament.id).matches);
  const first = must(drawn.filter((m) => m.bracket === 'main' && m.round === 1 && !m.isBye).sort((a, b) => a.slot - b.slot)[0]);
  must(live.goLive(first.id).live);

  const state = liveState.getState();
  state.globalHotkeys = { ...state.globalHotkeys, enabled: true };
  const blueName = state.teamBlue.name;

  const won = await request('POST', '/api/global-hotkeys/fire', { action: 'bluePlus' });
  assert.strictEqual(won.body.changed, true);
  assert.strictEqual(won.body.finish.seriesOver, true, 'a Bo1 is over after one point');
  assert.strictEqual(won.body.finish.seriesWinner, blueName);
  assert.ok(won.body.finish.nextMatch, 'with the next match to put on air');
  assert.strictEqual(must(matches.get(first.id)).winnerId, first.teamAId, 'and the bracket has the result');

  const again = await request('POST', '/api/global-hotkeys/fire', { action: 'bluePlus' });
  assert.deepStrictEqual([again.status, again.body.changed, again.body.code], [200, false, 'series-over']);
  live.clearLive();
});

// ---- หน้าตั้งค่า ----

test('the hotkeys page offers the system-wide panel and no longer says it is impossible', async () => {
  const html = String((await request('GET', '/hotkeys')).body);
  assert.ok(html.includes('id="globalPanel"'), 'the panel is there to switch on');
  assert.ok(html.includes('id="globalEnabled"'), 'with a switch of its own');
  assert.ok(
    !/cannot register system-wide shortcuts/.test(html),
    'the page must not still claim this is impossible'
  );
});

// รีเซ็ตปุ่ม ไม่ใช่ปิดฟีเจอร์
//
// เจอตอนต่อปุ่ม RESET ALL ของแผงนี้เข้ากับคำสั่ง resetGlobalHotkeys ที่มีอยู่แล้ว
// แต่ไม่เคยมีใครเรียกถึงได้: มันเขียนทับทั้งก้อนด้วยค่าเริ่มต้น ซึ่งรวม enabled: false
// อาการคือคีย์ลัดระดับระบบดับทั้งชุดกลางรายการเพราะคนกดปุ่มที่คิดว่าแค่คืนค่าปุ่ม
// และ "กดคีย์แล้วไม่มีอะไรเกิดขึ้น" อ่านเหมือนของเสีย ไม่ใช่เหมือนสวิตช์ถูกปิด
test('resetting the system-wide bindings leaves the switch where it was', () => {
  const { registerHandlers } = require('../server/sockets/handlers') as
    typeof import('../server/sockets/handlers');

  // socket ปลอมเท่าที่ registerHandlers ต้องใช้ เก็บ handler ไว้เรียกเอง
  const handlers = new Map<string, (payload: unknown) => void>();
  const socket = {
    id: 'test',
    handshake: { auth: {}, query: {} },
    on(event: string, fn: (payload: unknown) => void) { handlers.set(event, fn); },
    emit() { /* ไม่สนใจสิ่งที่ส่งกลับ */ },
    join() { /* ไม่ใช้ห้องในเทสต์นี้ */ },
    leave() { /* เช่นกัน */ }
  };
  registerHandlers(socket as never);

  const reset = handlers.get('resetGlobalHotkeys');
  assert.ok(reset, 'the command is registered');

  const state = liveState.getState();
  state.globalHotkeys = {
    enabled: true,
    layout: GLOBAL_HOTKEY_DEFAULTS.layout,
    bindings: {
      ...GLOBAL_HOTKEY_DEFAULTS.bindings,
      undo: { code: 'F9', ctrl: true, shift: true, alt: false, meta: false }
    }
  };

  reset!({});

  const after = liveState.getState().globalHotkeys;
  assert.strictEqual(after.enabled, true, 'the switch is a separate control and stays put');
  assert.deepStrictEqual(
    after.bindings.undo,
    GLOBAL_HOTKEY_DEFAULTS.bindings.undo,
    'but the binding really did go back to default'
  );

  // และปิดอยู่ก็ต้องยังปิดอยู่ ไม่ใช่ถูกเปิดขึ้นมาเอง
  liveState.getState().globalHotkeys = { ...after, enabled: false };
  reset!({});
  assert.strictEqual(liveState.getState().globalHotkeys.enabled, false);
});
