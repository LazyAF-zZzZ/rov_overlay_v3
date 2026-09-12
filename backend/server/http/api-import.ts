// Importing a v2 installation's data.
//
// Three steps on purpose: say where v2 might be, say what a folder holds, then bring it
// in. Nothing is written until the last one, and the last one is a merge, so importing
// twice adds nothing the second time.

import express, { Router } from 'express';
import { readV2, findSources } from '../domain/import-v2';
import { readBackup, summarise } from '../domain/backup';
import { getStores } from '../store/index';
import { requireControl } from './auth';
import { notifyData } from '../services/sync';

export function importRoutes(): Router {
  const router = express.Router();

  // Where a v2 install would be on this machine, and which of those actually hold data.
  router.get('/api/import/v2/candidates', requireControl, (_req, res) => {
    res.json({
      sources: findSources().map((source) => ({
        root: source.root,
        label: source.label,
        dbPath: source.dbPath,
        mediaDir: source.mediaDir
      }))
    });
  });

  router.post('/api/import/v2/preview', requireControl, (req, res) => {
    const body = (req.body || {}) as { path?: unknown };
    const target = typeof body.path === 'string' ? body.path : '';
    const found = readV2(target);
    if (found.error !== undefined) {
      res.status(400).json({ error: found.error });
      return;
    }

    const checked = readBackup(found.file);
    if (checked.error !== undefined) {
      res.status(400).json({ error: checked.error });
      return;
    }

    const { teamIds, tournamentIds } = getStores().backup.existing();
    res.json({
      source: found.source,
      summary: summarise(checked.file),
      alreadyHere: {
        teams: checked.file.data.teams.filter((t) => teamIds.has(t.id)).length,
        tournaments: checked.file.data.tournaments.filter((t) => tournamentIds.has(t.id)).length
      }
    });
  });

  router.post('/api/import/v2', requireControl, (req, res) => {
    const body = (req.body || {}) as { path?: unknown };
    const target = typeof body.path === 'string' ? body.path : '';
    const found = readV2(target);
    if (found.error !== undefined) {
      res.status(400).json({ error: found.error });
      return;
    }

    const checked = readBackup(found.file);
    if (checked.error !== undefined) {
      res.status(400).json({ error: checked.error });
      return;
    }

    let report;
    try {
      // Merge only. A v2 import must never be able to wipe what is already here.
      report = getStores().backup.restore(checked.file, 'merge');
    } catch (error) {
      res.status(500).json({ error: `Import failed: ${(error as Error).message}` });
      return;
    }

    notifyData({ topic: 'teams' });
    notifyData({ topic: 'tournaments' });
    res.json({ ok: true, source: found.source, summary: summarise(checked.file), report });
  });

  return router;
}
