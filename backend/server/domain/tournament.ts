// กติกาของทัวร์นาเมนต์: รูปแบบการแข่ง, จำนวนทีมที่รับได้, สถานะ
//
// ไฟล์นี้เป็นฟังก์ชันบริสุทธิ์ล้วน ไม่แตะฐานข้อมูล
// ตัวที่เขียนลงฐานอยู่ที่ store/tournaments.ts
// แยกกันเพื่อให้เขียนเทสต์กติกาได้โดยไม่ต้องเปิดฐานข้อมูล

import { sanitizeText, clampNumber } from '../lib/sanitize';

export const NAME_MAX = 60;
export const NOTE_MAX = 500;

// เพดานของทั้งระบบ: หนึ่งทัวร์นาเมนต์รับได้ 128 ทีม
// ทะเบียนทีมกลางไม่จำกัด เก็บได้เรื่อยๆ ข้ามปี
export const MAX_TEAMS = 128;

// พบกันหมดที่ 128 ทีม = 8,128 คู่ ตารางแข่งใหญ่เกินกว่าจะใช้งานจริงได้
// เกินเพดานนี้ให้ไปใช้แบ่งสายแทน (group_stage)
export const ROUND_ROBIN_MAX_TEAMS = 24;

export interface FormatSpec {
  label: string;
  minTeams: number;
  maxTeams: number;
}

export const FORMATS = {
  single_elim: { label: 'Single elimination', minTeams: 2, maxTeams: MAX_TEAMS },
  double_elim: { label: 'Double elimination', minTeams: 4, maxTeams: MAX_TEAMS },
  round_robin: { label: 'Round robin', minTeams: 2, maxTeams: ROUND_ROBIN_MAX_TEAMS },
  group_stage: { label: 'Group stage', minTeams: 4, maxTeams: MAX_TEAMS }
} as const satisfies Record<string, FormatSpec>;

export type TournamentFormat = keyof typeof FORMATS;
export type TournamentStatus = 'active' | 'finished';
export type BestOf = 1 | 3 | 5 | 7;

export const STATUSES: TournamentStatus[] = ['active', 'finished'];
export const BEST_OF_OPTIONS: BestOf[] = [1, 3, 5, 7];

export const DEFAULT_FORMAT: TournamentFormat = 'single_elim';
export const DEFAULT_STATUS: TournamentStatus = 'active';
export const DEFAULT_BEST_OF: BestOf = 3;

// ผลของการตรวจเพดาน แยก ok ออกมาให้ผู้เรียกเช็คได้ตรงๆ
export type LimitCheck =
  | { ok: true; limit: number; error?: undefined }
  | { ok: false; limit: number; error: string };

export interface TournamentInput {
  name: string;
  status: TournamentStatus;
  format: TournamentFormat;
  bestOf: BestOf;
  note: string;
}

export type TournamentInputResult =
  | { tournament: TournamentInput; error?: undefined }
  | { error: string; tournament?: undefined };

export function isFormat(value: unknown): value is TournamentFormat {
  return typeof value === 'string' && Object.prototype.hasOwnProperty.call(FORMATS, value);
}

export function sanitizeFormat(value: unknown): TournamentFormat {
  return isFormat(value) ? value : DEFAULT_FORMAT;
}

export function sanitizeStatus(value: unknown): TournamentStatus {
  return STATUSES.includes(value as TournamentStatus) ? (value as TournamentStatus) : DEFAULT_STATUS;
}

// Bo ต้องเป็นเลขคี่ที่กำหนดไว้เท่านั้น Bo2 หรือ Bo4 ตัดสินผู้ชนะไม่ได้
export function sanitizeBestOf(value: unknown): BestOf {
  const n = clampNumber(value, 1, 7);
  return BEST_OF_OPTIONS.includes(n as BestOf) ? (n as BestOf) : DEFAULT_BEST_OF;
}

// จำนวนทีมสูงสุดที่รูปแบบนี้รับได้
export function maxTeamsFor(format: unknown): number {
  return FORMATS[sanitizeFormat(format)].maxTeams;
}

// เช็คก่อนเพิ่มทีมเข้าทัวร์นาเมนต์
// currentCount = จำนวนทีมที่มีอยู่แล้ว, adding = จำนวนที่กำลังจะเพิ่ม
export function canAddTeams(format: unknown, currentCount: number, adding = 1): LimitCheck {
  const limit = maxTeamsFor(format);
  if (currentCount + adding > limit) {
    const { label } = FORMATS[sanitizeFormat(format)];
    return {
      ok: false,
      limit,
      error: currentCount >= limit
        ? `This tournament is full (${limit} teams max for ${label})`
        : `Only ${limit - currentCount} slot(s) left (${limit} teams max for ${label})`
    };
  }
  return { ok: true, limit };
}

// เช็คว่าเปลี่ยนรูปแบบได้ไหม เมื่อมีทีมอยู่แล้ว
// เช่น มี 40 ทีมอยู่ แล้วจะเปลี่ยนไป round_robin (เพดาน 24) ต้องห้ามไว้
export function canUseFormat(format: unknown, teamCount: number): LimitCheck {
  const limit = maxTeamsFor(format);
  if (teamCount > limit) {
    const { label } = FORMATS[sanitizeFormat(format)];
    return {
      ok: false,
      limit,
      error: `${label} allows at most ${limit} teams, but this tournament has ${teamCount}`
    };
  }
  return { ok: true, limit };
}

// พร้อมเริ่มจับคู่หรือยัง
export function canGenerateMatches(format: unknown, teamCount: number): LimitCheck {
  const spec = FORMATS[sanitizeFormat(format)];
  if (teamCount < spec.minTeams) {
    return {
      ok: false,
      limit: spec.maxTeams,
      error: `${spec.label} needs at least ${spec.minTeams} teams`
    };
  }
  return canUseFormat(format, teamCount);
}

// คืน { tournament } เมื่อผ่าน หรือ { error } เมื่อไม่ผ่าน
export function sanitizeTournamentInput(input: unknown): TournamentInputResult {
  const source = (input && typeof input === 'object' ? input : {}) as Record<string, unknown>;
  const name = sanitizeText(source.name, NAME_MAX);
  if (!name) return { error: 'Tournament name is required' };

  return {
    tournament: {
      name,
      status: sanitizeStatus(source.status),
      format: sanitizeFormat(source.format),
      bestOf: sanitizeBestOf(source.bestOf),
      note: sanitizeText(source.note, NOTE_MAX)
    }
  };
}
