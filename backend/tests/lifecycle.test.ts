import test from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import os from 'os';
import path from 'path';
import { spawn } from 'child_process';

// The desktop app stops the backend by closing its stdin. If the server ignored that,
// the app would have to kill it, and the last change the operator made would be lost.
test('the server saves and exits when its parent closes stdin', async () => {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'rov-lifecycle-'));
  const port = 39000 + Math.floor(Math.random() * 1000);
  const root = path.join(__dirname, '..', '..');

  const child = spawn(process.execPath, ['server.js'], {
    cwd: root,
    stdio: ['pipe', 'pipe', 'pipe'],
    env: {
      ...process.env,
      PORT: String(port),
      HOST: '127.0.0.1',
      CONTROL_TOKEN: '',
      ROV_USER_DATA_DIR: path.join(tmp, 'data'),
      ROV_USER_MEDIA_DIR: path.join(tmp, 'media'),
      ROV_USER_SOUND_DIR: path.join(tmp, 'sounds'),
      ROV_EXIT_WITH_PARENT: '1'
    }
  });

  try {
    await new Promise<void>((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('server did not start within 10 s')), 10_000);
      child.stdout.on('data', (chunk: Buffer) => {
        if (String(chunk).includes('Home:')) {
          clearTimeout(timer);
          resolve();
        }
      });
      child.once('exit', (code) => reject(new Error(`server exited early with code ${code}`)));
    });

    const exited = new Promise<number | null>((resolve) => child.once('exit', (code) => resolve(code)));
    child.stdin.end();
    const code = await Promise.race([
      exited,
      new Promise<string>((resolve) => setTimeout(() => resolve('still running after 5 s'), 5_000))
    ]);

    assert.strictEqual(code, 0);
    assert.ok(fs.existsSync(path.join(tmp, 'data', 'state.json')), 'state.json is written on the way out');
  } finally {
    if (child.exitCode === null) child.kill();
    fs.rmSync(tmp, { recursive: true, force: true });
  }
});
