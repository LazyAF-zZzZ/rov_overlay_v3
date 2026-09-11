// Who is answering on this port.
//
// The v3 desktop app starts this server itself, but the port can already be taken,
// most likely by ROV Overlay Tool v2, which answers every other /api route exactly
// like this server does. Without an identity check the desktop app would happily
// attach to v2 and start editing v2's data. This route exists only in v3, so a reply
// from it is the proof the desktop app needs before it trusts the port.

import fs from 'fs';
import path from 'path';
import express, { Router } from 'express';
import { ROOT_DIR, DATA_DIR, USER_MEDIA_DIR } from '../config';

export const APP_ID = 'rov-overlay-v3';

function readVersion(): string {
  try {
    const pkg = JSON.parse(fs.readFileSync(path.join(ROOT_DIR, 'package.json'), 'utf8')) as { version?: string };
    return pkg.version || '0.0.0';
  } catch {
    return '0.0.0';
  }
}

const VERSION = readVersion();

export function appInfoRoutes(): Router {
  const router = express.Router();

  router.get('/api/app-info', (_req, res) => {
    res.json({
      app: APP_ID,
      version: VERSION,
      dataDir: DATA_DIR,
      mediaDir: USER_MEDIA_DIR,
      pid: process.pid
    });
  });

  return router;
}
