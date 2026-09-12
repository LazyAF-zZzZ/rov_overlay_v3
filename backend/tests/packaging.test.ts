import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'fs';
import path from 'path';
import { PAGES } from '../server/http/pages';

// ตัวติดตั้งไม่เอาหน้า HTML ของคนคุมงานไปด้วย เพราะทุกหน้ามีหน้าจอในแอพแทนแล้ว
// การส่งไปด้วยเท่ากับให้คนใช้สองทางที่ไม่เหมือนกัน และทางหนึ่งไม่มีใครดูแล
//
// อันตรายคือรายชื่อนี้กินหน้า overlay เข้าไปด้วย ซึ่งจะทำให้ OBS ของทุกคนจอดำ
// โดยที่แอพยังดูปกติดี เทสต์นี้กันเรื่องนั้น

const packScript = fs.readFileSync(
  path.join(__dirname, '..', '..', '..', 'scripts', 'pack.ps1'), 'utf8'
);

const publicDir = path.join(__dirname, '..', '..', 'public');

function operatorPages(): string[] {
  const block = /\$operatorPages\s*=\s*@\(([\s\S]*?)\)/.exec(packScript);
  assert.ok(block, 'pack.ps1 must list the operator pages it drops');
  return [...block[1].matchAll(/'([^']+)'/g)].map((m) => m[1]);
}

// overlay ทุกหน้าที่ OBS โหลด ดึงจากตารางเส้นทางจริง ไม่ได้พิมพ์ซ้ำไว้ที่นี่
// หน้า overlay ใหม่ที่เพิ่มทีหลังจึงถูกคุ้มครองอัตโนมัติ
const overlayFiles = Object.values(PAGES)
  .filter((file) => file.startsWith('overlay') || file === 'result.html');

test('the installer drops the operator pages the app replaced', () => {
  const dropped = operatorPages();

  ['home.html', 'control.html', 'teams.html', 'team.html', 'tournament.html',
    'tournament-drafts.html', 'bracket.html', 'analytics.html', 'design.html',
    'hotkeys.html'].forEach((page) => {
    assert.ok(dropped.includes(page), `${page} has a native screen and must not ship`);
  });

  // ประกาศไว้เฉยๆ ไม่พอ ต้องลบจริง
  assert.match(
    packScript,
    /foreach \(\$page in \$operatorPages\)[\s\S]*Remove-Item/,
    'pack.ps1 must delete the operator pages, not just name them'
  );
});

test('the installer never drops an overlay OBS loads', () => {
  const dropped = operatorPages();

  assert.ok(overlayFiles.length >= 9, 'the overlay list came out suspiciously short');
  overlayFiles.forEach((file) => {
    assert.ok(!dropped.includes(file), `${file} is an OBS browser source and must ship`);
  });

  // หน้าตรวจเสียงเป็นเครื่องมือแก้ปัญหา ไม่มีหน้าจอในแอพมาแทน
  assert.ok(!dropped.includes('sfx-test.html'), 'the sound check page must keep shipping');

  // คู่มือก็ต้องอยู่: หน้าคู่มือในแอพมีปุ่มเปิด /guide ในเบราว์เซอร์
  // (Guide.OpenWebTip "เปิดคู่มือชุดเดียวกันแบบหน้าเว็บ") ไว้เปิดอ่านบนจออีกตัว
  // ถ้าตัดไฟล์ทิ้ง ปุ่มในแอพจะพาไปหน้า 410 ของตัวเอง
  assert.ok(!dropped.includes('guide.html'), 'the manual must keep shipping: the app links to it');
});

test('every page named for dropping actually exists', () => {
  operatorPages().forEach((page) => {
    assert.ok(
      fs.existsSync(path.join(publicDir, page)),
      `pack.ps1 names ${page}, which is not in public/ - a typo would silently ship it`
    );
  });
});

// เส้นทางยังอยู่หลังไฟล์ถูกเอาออก คนที่ bookmark /control ไว้ต้องได้คำอธิบาย
// ไม่ใช่ 500 จาก sendFile ที่หาไฟล์ไม่เจอ
test('a page whose file is not installed explains itself', () => {
  const source = fs.readFileSync(
    path.join(__dirname, '..', '..', 'server', 'http', 'pages.ts'), 'utf8'
  );
  assert.match(source, /fs\.existsSync/, 'pages.ts must check the file is there');
  assert.match(source, /status\(410\)/, 'a replaced page should answer 410, not 500');
  assert.doesNotMatch(
    source,
    /router\.get\([^)]*res\.sendFile/,
    'every page route must go through sendPage, or it will 500 once the file is dropped'
  );
});
