// ตัวชี้ว่าตอนนี้แมตช์ไหนกำลังออกอากาศ
//
// มีได้ทีละหนึ่งเท่านั้น (ตาราง live_match บังคับ id = 1)
// ตรงกับความจริงของงาน: overlay มีชุดเดียว จึงถ่ายทอดได้ทีละแมตช์
// แมตช์อื่นยังเก็บดราฟต์ของตัวเองไว้ สลับไปมาแล้วไม่มีอะไรหาย

import type { DatabaseSync } from 'node:sqlite';

export interface LivePointer {
  matchId: string | null;
  gameId: string | null;
  since: number;
}

export interface LiveMatchStore {
  get(): LivePointer;
  set(matchId: string | null, gameId: string | null): LivePointer;
  clear(): LivePointer;
}

export function createLiveMatchStore(db: DatabaseSync): LiveMatchStore {
  const q = {
    read: db.prepare('SELECT match_id, game_id, since FROM live_match WHERE id = 1'),
    write: db.prepare(
      `INSERT INTO live_match (id, match_id, game_id, since) VALUES (1, ?, ?, ?)
       ON CONFLICT(id) DO UPDATE SET match_id = excluded.match_id,
                                     game_id  = excluded.game_id,
                                     since    = excluded.since`
    )
  };

  const store: LiveMatchStore = {
    get() {
      const row = q.read.get() as { match_id: string | null; game_id: string | null; since: number } | undefined;
      return {
        matchId: row?.match_id ?? null,
        gameId: row?.game_id ?? null,
        since: row?.since ?? 0
      };
    },

    set(matchId, gameId) {
      q.write.run(matchId, gameId, Date.now());
      return store.get();
    },

    clear() {
      return store.set(null, null);
    }
  };

  return store;
}
