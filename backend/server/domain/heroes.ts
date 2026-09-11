// รายชื่อฮีโร่ และตัวตรวจว่าชื่อที่ส่งมามีอยู่จริงไหม
//
// ชื่อฮีโร่ = ชื่อไฟล์ภาพใน public/images/heroes (เช่น 'airi')
// โฟลเดอร์ภาพคือแหล่งความจริง ถ้ามีภาพอยู่ให้ใช้ตามภาพ
// heroes.json เป็นแค่ตัวสำรองตอนยังไม่มีโฟลเดอร์ภาพ
//
// ข้อควรระวังสำหรับของที่จะทำต่อ (สถิติ pick/ban):
// ชื่อฮีโร่ผูกกับชื่อไฟล์ ถ้าเปลี่ยนชื่อไฟล์ sanitizeHero จะมองว่าไม่รู้จัก
// บันทึกเกมย้อนหลังที่อ้างชื่อเดิมจึงห้ามเอามากรองผ่าน sanitizeHero ซ้ำ
// ให้เก็บเป็นข้อความดิบไว้ ไม่งั้นสถิติของเก่าจะหายไปเงียบๆ

import fs from 'fs';
import path from 'path';
import { HERO_IMAGE_DIR, APP_DATA_DIR } from '../config';
import { loadJson } from '../lib/json';

export interface HeroesData {
  heroes: string[];
}

const IMAGE_EXT = /\.(png|jpg|jpeg|webp)$/i;

export function listImageFiles(dir: string): string[] {
  try {
    return fs.readdirSync(dir, { withFileTypes: true })
      .filter((entry) => entry.isFile())
      .map((entry) => entry.name)
      .filter((name) => IMAGE_EXT.test(name))
      .sort((a, b) => a.localeCompare(b));
  } catch {
    return [];
  }
}

export function normalizeHeroesData(data: unknown): HeroesData {
  const imageHeroes = listImageFiles(HERO_IMAGE_DIR)
    .map((name) => name.replace(IMAGE_EXT, ''));
  const fromFile = (data as { heroes?: unknown })?.heroes;
  const source: unknown[] = imageHeroes.length > 0
    ? imageHeroes
    : (Array.isArray(fromFile) ? fromFile : []);
  const heroes = source
    .filter((hero): hero is string => typeof hero === 'string' && hero.trim() !== '')
    .map((hero) => hero.trim());
  return {
    heroes: Array.from(new Set(heroes)).sort((a, b) => a.localeCompare(b))
  };
}

export const heroesData: HeroesData = normalizeHeroesData(
  loadJson(path.join(APP_DATA_DIR, 'heroes.json'), { heroes: [] })
);

export const heroSet: ReadonlySet<string> = new Set(heroesData.heroes);

export function sanitizeHero(value: unknown): string | null {
  if (value === null || value === '') return null;
  if (typeof value !== 'string') return null;
  return heroSet.has(value) ? value : null;
}
