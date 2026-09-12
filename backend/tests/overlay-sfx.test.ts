import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'fs';
import path from 'path';

// เสียงพิค/แบนต้องไม่ดังรัวทั้งกระดานตอนสลับรอบ
//
// เกิดจริงกับผู้ใช้: สลับจากรอบ 1 ไปรอบ 2 แล้วกลับมารอบ 1 กระดานที่ดราฟต์ไว้แล้ว
// เด้งมาครบในอัปเดตเดียว overlay จึงเล่นเสียงทีละช่องรวมสิบแปดเสียงพร้อมกัน
//
// สคริปต์ฝั่งเบราว์เซอร์ทดสอบตรง ๆ ไม่ได้ (ไม่มี DOM ใน node) เทสต์นี้จึงคุมที่ตัวกลไก
// แบบเดียวกับเทสต์ลำดับแท็ก <script> ใน tournament-api.test.ts

const js = (name: string) =>
  fs.readFileSync(path.join(__dirname, '..', '..', 'public', 'js', name), 'utf8');

test('the sound module can be silenced for one update', () => {
  const sfx = js('overlay-sfx.js');

  assert.match(sfx, /function disarm\(\)/, 'overlay-sfx.js must have disarm()');
  assert.match(sfx, /global\.RovSfx = \{[^}]*\bdisarm\b/, 'disarm must be exported on RovSfx');

  // play() ต้องยังถูกคุมด้วย armed ไม่งั้น disarm() ไม่มีผลอะไรเลย
  const play = /function play\(name\) \{([\s\S]*?)\n  \}/.exec(sfx);
  assert.ok(play, 'overlay-sfx.js must still have play()');
  assert.match(play![1], /!armed/, 'play() must stay gated on armed');
});

test('the overlay silences a whole board arriving at once', () => {
  const overlay = js('overlay.js');

  assert.match(overlay, /function isBoardSwap\(state\)/, 'overlay.js must decide what a board swap is');
  assert.match(overlay, /RovSfx\.disarm\(\)/, 'overlay.js must silence the update');
  assert.match(overlay, /RovSfx\.arm\(\)/, 'and arm again at the end of the update');

  // ทั้งเลขรอบและจำนวนช่องที่เพิ่งมีฮีโร่ ต้องยังถูกใช้ตัดสิน
  const swap = /function isBoardSwap\(state\) \{([\s\S]*?)\n\}/.exec(overlay);
  assert.ok(swap, 'isBoardSwap must be a plain function');
  assert.match(swap![1], /round/, 'a changed round means the board was swapped');
  assert.match(swap![1], /countIncomingHeroes\(state\)/, 'so does a pile of heroes arriving together');

  // ต้อง disarm ก่อนจะวาด ban/pick ไม่งั้นเสียงดังไปแล้วก่อนถูกปิด
  const disarmAt = overlay.indexOf('RovSfx.disarm()');
  const bansAt = overlay.indexOf("updateBans('teamBlue'");
  const picksAt = overlay.indexOf("updatePicks('teamBlue'");
  assert.ok(disarmAt > -1 && bansAt > -1 && picksAt > -1, 'all three must be present');
  assert.ok(disarmAt < bansAt && disarmAt < picksAt,
    'disarm must run before the board is drawn, or the sounds fire before it takes effect');
});
