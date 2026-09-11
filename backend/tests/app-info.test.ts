import test from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import os from 'os';
import path from 'path';
import http from 'http';
import type { AddressInfo } from 'net';

const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'rov-app-info-'));
process.env.ROV_USER_DATA_DIR = path.join(TMP, 'data');
process.env.ROV_USER_MEDIA_DIR = path.join(TMP, 'media');
process.env.CONTROL_TOKEN = '';

const { createApp } = require('../server/index') as typeof import('../server/index');
const { closeDatabase } = require('../server/store/db') as typeof import('../server/store/db');

// The desktop app attaches to whatever answers here, so this reply is what keeps it
// from attaching to a running v2 and editing v2's data.
test('/api/app-info names the v3 backend and where its data lives', async () => {
  const server = http.createServer(createApp());
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
  const { port } = server.address() as AddressInfo;

  try {
    const res = await fetch(`http://127.0.0.1:${port}/api/app-info`);
    assert.strictEqual(res.status, 200);

    const body = await res.json() as { app: string; version: string; dataDir: string; mediaDir: string };
    const pkg = JSON.parse(fs.readFileSync(path.join(__dirname, '..', '..', 'package.json'), 'utf8')) as { version: string };

    assert.strictEqual(body.app, 'rov-overlay-v3');
    assert.strictEqual(body.version, pkg.version);
    assert.strictEqual(body.dataDir, path.join(TMP, 'data'));
    assert.strictEqual(body.mediaDir, path.join(TMP, 'media'));
  } finally {
    await new Promise<void>((resolve) => server.close(() => resolve()));
    closeDatabase();
    fs.rmSync(TMP, { recursive: true, force: true });
  }
});
