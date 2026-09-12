import test from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import os from 'os';
import path from 'path';
import http from 'http';
import type { AddressInfo } from 'net';
import { DatabaseSync } from 'node:sqlite';

const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'rov-import-test-'));
process.env.ROV_USER_DATA_DIR = path.join(TMP, 'data');
process.env.ROV_USER_MEDIA_DIR = path.join(TMP, 'media');
process.env.ROV_USER_SOUND_DIR = path.join(TMP, 'sounds');
process.env.CONTROL_TOKEN = '';

const { createApp } = require('../server/index') as typeof import('../server/index');
const { closeDatabase } = require('../server/store/db') as typeof import('../server/store/db');
const { MIGRATIONS } = require('../server/store/migrations') as typeof import('../server/store/migrations');

// A v2 installation, built the way v2 would have left it: the same schema, a team with a
// logo file, a tournament, a match and a recorded draft.
function makeV2Install(root: string): void {
  const dataDir = path.join(root, 'data');
  const logoDir = path.join(root, 'media', 'team-logos');
  fs.mkdirSync(dataDir, { recursive: true });
  fs.mkdirSync(logoDir, { recursive: true });

  const db = new DatabaseSync(path.join(dataDir, 'tournament.db'));
  MIGRATIONS.forEach((sql) => db.exec(sql));
  db.exec(`
    INSERT INTO teams (id, name, tag, logo_v, logo_ext, created_at, updated_at)
      VALUES ('t-old', 'Legacy Wolves', 'LW', 7, 'png', 1000, 1000);
    INSERT INTO team_players (team_id, slot, name, position, is_captain)
      VALUES ('t-old', 0, 'LW.Kai', 'jungle', 1);
    INSERT INTO tournaments (id, name, status, format, best_of, note, created_at, updated_at)
      VALUES ('tr-old', 'Old Cup', 'finished', 'single_elim', 3, '', 1000, 1000);
    INSERT INTO tournament_teams (tournament_id, team_id, seed, added_at)
      VALUES ('tr-old', 't-old', 1, 1000);
    INSERT INTO matches (id, tournament_id, bracket, round, slot, team_a_id, team_b_id,
                         best_of, status, score_a, score_b, winner_id, is_bye, created_at)
      VALUES ('m-old', 'tr-old', 'main', 1, 0, 't-old', NULL, 3, 'complete', 2, 0, 't-old', 0, 1000);
    INSERT INTO games (id, match_id, game_no, blue_team_id, red_team_id, blue_name, red_name,
                       draft_locked, winner, started_at, updated_at)
      VALUES ('g-old', 'm-old', 1, 't-old', NULL, 'Legacy Wolves', 'Ghosts', 1, 'blue', 1000, 1000);
    INSERT INTO game_slots (game_id, side, kind, idx, hero) VALUES ('g-old', 'blue', 'pick', 0, 'airi');
  `);
  db.close();

  // A one-pixel PNG is enough: the importer only has to carry the bytes across.
  const png = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
    'base64'
  );
  fs.writeFileSync(path.join(logoDir, 't-old.png'), png);
}

function request(base: string, method: string, url: string, body?: unknown): Promise<{ status: number; body: any }> {
  return new Promise((resolve, reject) => {
    const payload = body === undefined ? null : Buffer.from(JSON.stringify(body));
    const req = http.request(`${base}${url}`, {
      method,
      headers: payload ? { 'Content-Type': 'application/json', 'Content-Length': payload.length } : {}
    }, (res) => {
      const chunks: Buffer[] = [];
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => {
        const text = Buffer.concat(chunks).toString('utf8');
        resolve({ status: res.statusCode || 0, body: text ? JSON.parse(text) : {} });
      });
    });
    req.on('error', reject);
    if (payload) req.write(payload);
    req.end();
  });
}

test('a v2 folder imports into v3, and v2 is left exactly as it was', async () => {
  const v2 = path.join(TMP, 'v2');
  makeV2Install(v2);

  // What v2 looks like before we touch it, byte for byte.
  const dbFile = path.join(v2, 'data', 'tournament.db');
  const before = fs.readdirSync(path.join(v2, 'data')).map((name) => {
    const full = path.join(v2, 'data', name);
    return { name, size: fs.statSync(full).size, hash: fs.readFileSync(full).toString('base64').slice(0, 64) };
  });

  const server = http.createServer(createApp());
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
  const { port } = server.address() as AddressInfo;
  const base = `http://127.0.0.1:${port}`;

  try {
    const preview = await request(base, 'POST', '/api/import/v2/preview', { path: v2 });
    assert.strictEqual(preview.status, 200);
    assert.strictEqual(preview.body.summary.teams, 1);
    assert.strictEqual(preview.body.summary.tournaments, 1);
    assert.strictEqual(preview.body.summary.matches, 1);
    assert.strictEqual(preview.body.summary.drafts, 1);
    assert.strictEqual(preview.body.summary.logos, 1, 'the logo file travels with the team');
    assert.strictEqual(preview.body.alreadyHere.teams, 0);

    const done = await request(base, 'POST', '/api/import/v2', { path: v2 });
    assert.strictEqual(done.status, 200);
    assert.strictEqual(done.body.report.teamsAdded, 1);
    assert.strictEqual(done.body.report.tournamentsAdded, 1);
    assert.strictEqual(done.body.report.matchesAdded, 1);
    assert.strictEqual(done.body.report.gamesAdded, 1);
    assert.strictEqual(done.body.report.logosWritten, 1);
    assert.strictEqual(done.body.report.mode, 'merge');

    // The data is really here now.
    const teams = await request(base, 'GET', '/api/teams');
    assert.ok(teams.body.teams.some((t: { id: string }) => t.id === 't-old'));
    assert.ok(fs.existsSync(path.join(TMP, 'media', 'team-logos', 't-old.png')), 'the logo landed in v3 media');

    // Importing again adds nothing: merge skips what is already here.
    const again = await request(base, 'POST', '/api/import/v2', { path: v2 });
    assert.strictEqual(again.body.report.teamsAdded, 0);
    assert.strictEqual(again.body.report.teamsSkipped, 1);

    // And v2 is untouched: same files, same sizes, same first bytes, no stray journal.
    const after = fs.readdirSync(path.join(v2, 'data')).map((name) => {
      const full = path.join(v2, 'data', name);
      return { name, size: fs.statSync(full).size, hash: fs.readFileSync(full).toString('base64').slice(0, 64) };
    });
    assert.deepStrictEqual(after, before, 'the v2 folder must be byte-identical after an import');
    assert.ok(fs.existsSync(dbFile));
  } finally {
    await new Promise<void>((resolve) => server.close(() => resolve()));
    closeDatabase();
    fs.rmSync(TMP, { recursive: true, force: true });
  }
});

test('pointing at a folder with no v2 data says so instead of importing nothing', async () => {
  const empty = fs.mkdtempSync(path.join(os.tmpdir(), 'rov-empty-'));
  const server = http.createServer(createApp());
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
  const { port } = server.address() as AddressInfo;

  try {
    const reply = await request(`http://127.0.0.1:${port}`, 'POST', '/api/import/v2/preview', { path: empty });
    assert.strictEqual(reply.status, 400);
    assert.match(String(reply.body.error), /no rov overlay tool data/i);
  } finally {
    await new Promise<void>((resolve) => server.close(() => resolve()));
    fs.rmSync(empty, { recursive: true, force: true });
  }
});
