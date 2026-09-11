// จุดเริ่มของเซิร์ฟเวอร์
//
// ไฟล์นี้เป็น .js ธรรมดาโดยตั้งใจ ไม่ใช่ .ts
// electron-main.js เรียกด้วย require(path.join(__dirname, 'server.js'))
// หลังตั้ง ROV_USER_DATA_DIR / ROV_USER_MEDIA_DIR เสร็จ
// ถ้าย้ายไฟล์นี้หรือเปลี่ยนพฤติกรรม "require แล้วเซิร์ฟเวอร์ทำงานทันที" ตัวแอพจะพัง
//
// ซอร์สจริงเป็น TypeScript อยู่ใน server/ แล้วคอมไพล์ไปที่ build/server/
// ต้องรัน `npm run build` ก่อนถึงจะมีไฟล์ให้ require
// สคริปต์ start / app / dist ใน package.json สั่ง build ให้เองอยู่แล้ว
//
// อยากได้ app หรือ server ไปเขียนเทสต์ ให้ require('./build/server/index') แทน
// ตัวนั้นแยก createApp / createServer / start ออกจากกันไว้แล้ว

const path = require('path');
const fs = require('fs');

const COMPILED = path.join(__dirname, 'build', 'server', 'index.js');

if (!fs.existsSync(COMPILED)) {
  console.error('===========================================');
  console.error('Compiled server not found:');
  console.error(`  ${COMPILED}`);
  console.error('');
  console.error('Run this first:');
  console.error('  npm run build');
  console.error('===========================================');
  process.exit(1);
}

const { start } = require(COMPILED);

module.exports = start();

// v3: the desktop app runs this server as a child process and closes stdin to stop it.
// See server/lifecycle.ts. Off unless the desktop app sets the variable.
if (process.env.ROV_EXIT_WITH_PARENT === "1") {
  require(path.join(__dirname, "build", "server", "lifecycle.js")).exitWithParent();
}
