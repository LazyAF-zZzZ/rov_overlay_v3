// Bringing a v2 installation's data into v3.
//
// Two rules shape this file:
//
//   1. **v2's folder is never written to.** Not even by SQLite. A v2 database left in
//      WAL mode needs its -wal replayed before the newest rows are visible, and doing
//      that in place would modify the very folder we promised not to touch. So the
//      database and its side files are copied to a temp folder and the copy is opened.
//   2. **Nothing new is invented.** The rows are turned into exactly the structure a
//      backup file holds, then handed to the same restore the Backup button uses, in
//      merge mode. Anything already here is kept; duplicates are skipped.
//
// v2 and v3 share a schema (v3's backend is v2's, copied), so the column names below are
// the ones in store/migrations.ts. `position` was called `role` before the third
// migration, and both are read so an older v2 database still imports.

import fs from 'fs';
import os from 'os';
import path from 'path';
import { DatabaseSync } from 'node:sqlite';
import type {
  BackupFile, BackupData, BackupImage, BackupTeam, BackupTournament, BackupMatch, BackupGame
} from './backup';
import { BACKUP_FORMAT, BACKUP_VERSION } from './backup';
import type { ImageExt, SkinSlot } from './media';
import { SKIN_SLOTS } from './media';

const IMAGE_EXTS: ImageExt[] = ['png', 'jpg', 'webp'];
const DB_SUFFIXES = ['', '-wal', '-shm'];

export interface V2Source {
  /** The folder the operator picked, or one we guessed. */
  root: string;
  /** Where the database actually is inside it. */
  dbPath: string;
  /** Where uploaded images actually are: media/ when installed, public/images/ from source. */
  mediaDir: string | null;
  /** How this source was described to the operator. */
  label: string;
}

export type V2ReadResult =
  | { source: V2Source; file: BackupFile; error?: undefined }
  | { error: string; source?: undefined; file?: undefined };

// Where a v2 install keeps its data. The installed app writes to %APPDATA%; running from
// source writes inside the project folder instead, which is why both shapes are checked.
export function candidatePaths(): string[] {
  const list: string[] = [];
  const appData = process.env.APPDATA;
  if (appData) list.push(path.join(appData, 'ROV Overlay Tool'));

  // The v2 project folder, if it still sits beside this one. This file runs from
  // build/server/domain/, so the v3 root is four levels up, not three — counting to
  // `backend` and calling it the root made this candidate resolve to
  // `rov_overlay_v3/rov_pickban_overlay`, a path that never exists, and the whole
  // fallback silently found nothing.
  const v3Root = path.resolve(__dirname, '..', '..', '..', '..');
  list.push(path.join(path.dirname(v3Root), 'rov_pickban_overlay'));

  return list.filter((dir, index) => list.indexOf(dir) === index);
}

export function findSources(): V2Source[] {
  return candidatePaths().map(resolveSource).filter((s): s is V2Source => s !== null);
}

// Accepts the v2 root, its data folder, or the database file itself: an operator asked
// to "point at v2" should not have to know which of those we wanted.
export function resolveSource(target: string): V2Source | null {
  if (!target) return null;

  let dbPath: string | null = null;
  let root = target;

  if (target.toLowerCase().endsWith('.db') && isFile(target)) {
    dbPath = target;
    root = path.dirname(path.dirname(target));
  } else if (isFile(path.join(target, 'data', 'tournament.db'))) {
    dbPath = path.join(target, 'data', 'tournament.db');
  } else if (isFile(path.join(target, 'tournament.db'))) {
    dbPath = path.join(target, 'tournament.db');
    root = path.dirname(target);
  }

  if (!dbPath) return null;

  const mediaDir = [path.join(root, 'media'), path.join(root, 'public', 'images')]
    .find((dir) => isDir(path.join(dir, 'team-logos')) || isDir(path.join(dir, 'skins'))) ?? null;

  return { root, dbPath, mediaDir, label: path.basename(root) };
}

export function readV2(target: string): V2ReadResult {
  const source = resolveSource(target);
  if (!source) return { error: 'No ROV Overlay Tool data found in that folder' };

  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'rov-v2-import-'));
  try {
    // Copy first, read second: the original folder is never opened by SQLite.
    const copy = path.join(temp, 'tournament.db');
    for (const suffix of DB_SUFFIXES) {
      const from = source.dbPath + suffix;
      if (fs.existsSync(from)) fs.copyFileSync(from, copy + suffix);
    }

    const db = new DatabaseSync(copy);
    try {
      const data = collect(db, source.mediaDir);
      return {
        source,
        file: {
          format: BACKUP_FORMAT,
          version: BACKUP_VERSION,
          kind: 'full',
          app: 'v2-import',
          exportedAt: new Date().toISOString(),
          data
        }
      };
    } finally {
      db.close();
    }
  } catch (error) {
    return { error: `Could not read the v2 data: ${(error as Error).message}` };
  } finally {
    fs.rmSync(temp, { recursive: true, force: true });
  }
}

interface Row { [key: string]: unknown }

function collect(db: DatabaseSync, mediaDir: string | null): BackupData {
  const all = (sql: string): Row[] => {
    try {
      return db.prepare(sql).all() as unknown as Row[];
    } catch {
      return [];   // an older v2 database may not have every table yet
    }
  };

  const playersByTeam = new Map<string, BackupTeam['players']>();
  all('SELECT * FROM team_players ORDER BY team_id, slot').forEach((r) => {
    const list = playersByTeam.get(String(r.team_id)) || [];
    list.push({
      slot: Number(r.slot),
      name: String(r.name ?? ''),
      // 'role' before the third migration, 'position' after it.
      position: String(r.position ?? r.role ?? ''),
      isCaptain: Number(r.is_captain) === 1
    });
    playersByTeam.set(String(r.team_id), list);
  });

  const teams: BackupTeam[] = all('SELECT * FROM teams ORDER BY created_at').map((r) => ({
    id: String(r.id),
    name: String(r.name ?? ''),
    tag: String(r.tag ?? ''),
    logoV: Number(r.logo_v ?? 0),
    logoExt: String(r.logo_ext ?? ''),
    createdAt: Number(r.created_at),
    updatedAt: Number(r.updated_at),
    players: playersByTeam.get(String(r.id)) || []
  }));

  const seedsByTournament = new Map<string, BackupTournament['teams']>();
  all('SELECT * FROM tournament_teams ORDER BY tournament_id, seed').forEach((r) => {
    const list = seedsByTournament.get(String(r.tournament_id)) || [];
    list.push({ teamId: String(r.team_id), seed: Number(r.seed ?? 0), addedAt: Number(r.added_at) });
    seedsByTournament.set(String(r.tournament_id), list);
  });

  const tournaments: BackupTournament[] = all('SELECT * FROM tournaments ORDER BY created_at').map((r) => ({
    id: String(r.id),
    name: String(r.name ?? ''),
    status: String(r.status ?? 'active'),
    format: String(r.format ?? 'single_elim'),
    bestOf: Number(r.best_of ?? 3),
    note: String(r.note ?? ''),
    createdAt: Number(r.created_at),
    updatedAt: Number(r.updated_at),
    teams: seedsByTournament.get(String(r.id)) || []
  }));

  const matches: BackupMatch[] = all('SELECT * FROM matches ORDER BY created_at').map((r) => ({
    id: String(r.id),
    tournamentId: String(r.tournament_id),
    bracket: String(r.bracket ?? 'main'),
    round: Number(r.round),
    slot: Number(r.slot),
    teamAId: r.team_a_id == null ? null : String(r.team_a_id),
    teamBId: r.team_b_id == null ? null : String(r.team_b_id),
    bestOf: Number(r.best_of ?? 3),
    status: String(r.status ?? 'pending'),
    scoreA: Number(r.score_a ?? 0),
    scoreB: Number(r.score_b ?? 0),
    winnerId: r.winner_id == null ? null : String(r.winner_id),
    isBye: Number(r.is_bye) === 1,
    nextRound: r.next_round == null ? null : Number(r.next_round),
    nextSlot: r.next_slot == null ? null : Number(r.next_slot),
    nextSide: r.next_side == null ? null : Number(r.next_side),
    nextBracket: r.next_bracket == null ? null : String(r.next_bracket),
    loserRound: r.loser_round == null ? null : Number(r.loser_round),
    loserSlot: r.loser_slot == null ? null : Number(r.loser_slot),
    loserSide: r.loser_side == null ? null : Number(r.loser_side),
    loserBracket: r.loser_bracket == null ? null : String(r.loser_bracket),
    createdAt: Number(r.created_at)
  }));

  const slotsByGame = new Map<string, BackupGame['slots']>();
  all('SELECT * FROM game_slots').forEach((r) => {
    const list = slotsByGame.get(String(r.game_id)) || [];
    list.push({ side: String(r.side), kind: String(r.kind), idx: Number(r.idx), hero: String(r.hero) });
    slotsByGame.set(String(r.game_id), list);
  });

  const games: BackupGame[] = all('SELECT * FROM games ORDER BY started_at').map((r) => ({
    id: String(r.id),
    matchId: String(r.match_id),
    gameNo: Number(r.game_no),
    blueTeamId: r.blue_team_id == null ? null : String(r.blue_team_id),
    redTeamId: r.red_team_id == null ? null : String(r.red_team_id),
    blueName: String(r.blue_name ?? ''),
    redName: String(r.red_name ?? ''),
    draftLocked: Number(r.draft_locked) === 1,
    winner: r.winner == null ? null : String(r.winner),
    startedAt: Number(r.started_at),
    updatedAt: Number(r.updated_at),
    slots: slotsByGame.get(String(r.id)) || []
  }));

  const logos: Record<string, BackupImage> = {};
  const skins: Record<string, BackupImage> = {};

  if (mediaDir) {
    const logoDir = path.join(mediaDir, 'team-logos');
    teams.forEach((team) => {
      if (!team.logoV) return;
      const image = findImage(logoDir, team.id);
      if (image) logos[team.id] = image;
    });

    const skinDir = path.join(mediaDir, 'skins');
    (Object.keys(SKIN_SLOTS) as SkinSlot[]).forEach((slot) => {
      const image = findImage(skinDir, SKIN_SLOTS[slot]);
      if (image) skins[slot] = image;
    });
  }

  return { teams, tournaments, matches, games, logos, skins, state: null };
}

function findImage(dir: string, base: string): BackupImage | null {
  for (const ext of IMAGE_EXTS) {
    const file = path.join(dir, `${base}.${ext}`);
    try {
      if (fs.existsSync(file)) return { ext, bytes: fs.readFileSync(file).toString('base64') };
    } catch {
      // Unreadable file: skip it rather than failing the whole import.
    }
  }
  return null;
}

function isFile(target: string): boolean {
  try {
    return fs.statSync(target).isFile();
  } catch {
    return false;
  }
}

function isDir(target: string): boolean {
  try {
    return fs.statSync(target).isDirectory();
  } catch {
    return false;
  }
}
