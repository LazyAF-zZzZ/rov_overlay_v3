// เทสต์การจับคู่และตารางแข่ง
//
// ครอบคลุมเป้าหมายการทดสอบข้อ 2 (Bo3/Bo5/พบกันหมด ต้องทำงานสม่ำเสมอ)
// และข้อ 3 (สุ่มจับคู่แล้วต้องไม่มีทีมซ้ำในรอบเดียวกัน)

import test from 'node:test';
import assert from 'node:assert';
import {
  shuffle,
  winsNeeded,
  seriesWinner,
  roundRobinRounds,
  seedOrder,
  singleElimination,
  doubleElimination,
  groupStage,
  generateMatches
} from '../server/domain/bracket';
import type { PlannedMatch } from '../server/domain/bracket';
import { BEST_OF_OPTIONS } from '../server/domain/tournament';
import type { BestOf } from '../server/domain/tournament';

const teams = (n: number): string[] => Array.from({ length: n }, (_, i) => `t${i + 1}`);

// ตัวสุ่มแบบกำหนดผลได้ เทสต์จะได้ผลเดิมทุกครั้ง
function seededRng(seed: number) {
  let state = seed >>> 0;
  return () => {
    state = (state * 1664525 + 1013904223) >>> 0;
    return state / 0x100000000;
  };
}

// ---- TEST GOAL 2: Bo3 / Bo5 ต้องตัดสินเหมือนกันทุกครั้ง ----

test('a series needs a strict majority of its games', () => {
  assert.strictEqual(winsNeeded(1), 1);
  assert.strictEqual(winsNeeded(3), 2);
  assert.strictEqual(winsNeeded(5), 3);
  assert.strictEqual(winsNeeded(7), 4);
});

test('a series is only decided once someone reaches the majority', () => {
  // Bo3
  assert.strictEqual(seriesWinner(3, 0, 0), null);
  assert.strictEqual(seriesWinner(3, 1, 1), null);
  assert.strictEqual(seriesWinner(3, 2, 0), 'a');
  assert.strictEqual(seriesWinner(3, 2, 1), 'a');
  assert.strictEqual(seriesWinner(3, 1, 2), 'b');

  // Bo5
  assert.strictEqual(seriesWinner(5, 2, 2), null, '2-2 in a Bo5 is not decided');
  assert.strictEqual(seriesWinner(5, 3, 2), 'a');
  assert.strictEqual(seriesWinner(5, 0, 3), 'b');

  // Bo1
  assert.strictEqual(seriesWinner(1, 1, 0), 'a');
  assert.strictEqual(seriesWinner(1, 0, 0), null);
});

test('every supported series length behaves the same way', () => {
  BEST_OF_OPTIONS.forEach((bestOf: BestOf) => {
    const need = winsNeeded(bestOf);
    // ชนะไม่ครบ = ยังไม่จบ
    assert.strictEqual(seriesWinner(bestOf, need - 1, 0), null, `Bo${bestOf} not decided early`);
    // ครบเมื่อไหร่จบเมื่อนั้น
    assert.strictEqual(seriesWinner(bestOf, need, 0), 'a', `Bo${bestOf} decided at ${need}`);
    assert.strictEqual(seriesWinner(bestOf, 0, need), 'b', `Bo${bestOf} decided at ${need}`);
    // ชนะเกินครึ่งเสมอ แพ้ไม่มีทางตามทัน
    assert.ok(need * 2 > bestOf, `Bo${bestOf} majority cannot be tied`);
  });
});

// ---- TEST GOAL 3: ทีมห้ามซ้ำในรอบเดียวกัน ----

// นับทีมที่โผล่ในแต่ละ (สาย, รอบ) แล้วต้องไม่มีตัวไหนเกินหนึ่ง
function assertNoTeamTwicePerRound(matches: PlannedMatch[], label: string) {
  const seen = new Map<string, Set<string>>();
  matches.forEach((m) => {
    const key = `${m.bracket}#${m.round}`;
    if (!seen.has(key)) seen.set(key, new Set());
    const round = seen.get(key) as Set<string>;
    [m.teamAId, m.teamBId].forEach((id) => {
      if (!id) return;
      assert.ok(!round.has(id), `${label}: ${id} appears twice in ${key}`);
      round.add(id);
    });
  });
}

test('round robin never puts a team in two matches of the same round', () => {
  for (let n = 2; n <= 24; n += 1) {
    const rounds = roundRobinRounds(teams(n));
    rounds.forEach((pairs, r) => {
      const used = new Set<string>();
      pairs.forEach(([a, b]) => {
        assert.ok(!used.has(a), `n=${n} round ${r}: ${a} twice`);
        assert.ok(!used.has(b), `n=${n} round ${r}: ${b} twice`);
        used.add(a);
        used.add(b);
      });
    });
  }
});

test('round robin has every pair exactly once, and the right number of rounds', () => {
  for (let n = 2; n <= 16; n += 1) {
    const rounds = roundRobinRounds(teams(n));
    const expectedRounds = n % 2 === 0 ? n - 1 : n;
    assert.strictEqual(rounds.length, expectedRounds, `n=${n} round count`);

    const pairKeys = rounds.flat().map(([a, b]) => [a, b].sort().join('|'));
    const unique = new Set(pairKeys);
    assert.strictEqual(unique.size, pairKeys.length, `n=${n}: a pair was scheduled twice`);
    assert.strictEqual(pairKeys.length, (n * (n - 1)) / 2, `n=${n}: wrong total matches`);
  }
});

test('every team plays every other team exactly once', () => {
  const n = 9; // เลขคี่ ต้องมีทีมได้พักในบางรอบ
  const rounds = roundRobinRounds(teams(n));
  const opponents = new Map<string, string[]>();
  rounds.flat().forEach(([a, b]) => {
    if (!opponents.has(a)) opponents.set(a, []);
    if (!opponents.has(b)) opponents.set(b, []);
    (opponents.get(a) as string[]).push(b);
    (opponents.get(b) as string[]).push(a);
  });

  teams(n).forEach((id) => {
    const faced = opponents.get(id) || [];
    assert.strictEqual(faced.length, n - 1, `${id} should play ${n - 1} matches`);
    assert.strictEqual(new Set(faced).size, n - 1, `${id} faced someone twice`);
    assert.ok(!faced.includes(id), `${id} was scheduled against itself`);
  });
});

test('single elimination never repeats a team within a round', () => {
  for (let n = 2; n <= 40; n += 1) {
    assertNoTeamTwicePerRound(singleElimination(teams(n)), `single elim n=${n}`);
  }
});

test('group stage never repeats a team within a group round', () => {
  [8, 12, 16, 20].forEach((n) => {
    [2, 4].forEach((groups) => {
      assertNoTeamTwicePerRound(groupStage(teams(n), groups), `groups n=${n} g=${groups}`);
    });
  });
});

test('a shuffled draw still never repeats a team in a round', () => {
  const rng = seededRng(12345);
  for (let attempt = 0; attempt < 60; attempt += 1) {
    const n = 2 + Math.floor(rng() * 30);
    const drawn = shuffle(teams(n), rng);
    ['single_elim', 'round_robin'].forEach((format) => {
      const result = generateMatches({ format: format as 'single_elim', teamIds: drawn });
      assert.ok(result.matches, `${format} n=${n} should generate`);
      assertNoTeamTwicePerRound(result.matches as PlannedMatch[], `${format} n=${n}`);
    });
  }
});

// ---- SHUFFLE ----

test('shuffling keeps every team exactly once - nobody is lost or cloned', () => {
  const rng = seededRng(99);
  for (let n = 1; n <= 40; n += 1) {
    const source = teams(n);
    const drawn = shuffle(source, rng);
    assert.strictEqual(drawn.length, n);
    assert.deepStrictEqual([...drawn].sort(), [...source].sort(), `n=${n} lost or duplicated a team`);
  }
});

test('shuffle does not mutate the list it was given', () => {
  const source = teams(10);
  const copy = source.slice();
  shuffle(source, seededRng(1));
  assert.deepStrictEqual(source, copy);
});

// ---- SINGLE ELIMINATION SHAPE ----

test('seed order keeps the top seeds apart until the end', () => {
  assert.deepStrictEqual(seedOrder(2), [1, 2]);
  assert.deepStrictEqual(seedOrder(4), [1, 4, 2, 3], '1v4 and 2v3');
  assert.deepStrictEqual(seedOrder(8), [1, 8, 4, 5, 2, 7, 3, 6], '1v8, 4v5, 2v7, 3v6');

  // ทุกขนาด: seed 1 กับ seed 2 ต้องอยู่คนละครึ่งสาย
  [4, 8, 16, 32].forEach((size) => {
    const order = seedOrder(size);
    assert.strictEqual(order.length, size);
    assert.deepStrictEqual([...order].sort((a, b) => a - b), Array.from({ length: size }, (_, i) => i + 1));
    const half = size / 2;
    assert.ok(order.indexOf(1) < half, `size ${size}: seed 1 in the first half`);
    assert.ok(order.indexOf(2) >= half, `size ${size}: seed 2 in the second half`);
  });
});

test('a single elimination bracket takes exactly n-1 played matches to decide', () => {
  // ตัดคนออกทีละคนจนเหลือแชมป์ = ต้องแข่งจริง n-1 นัดเสมอ
  // บายไม่นับ เพราะไม่มีใครลงแข่ง
  for (let n = 2; n <= 33; n += 1) {
    const matches = singleElimination(teams(n));
    const played = matches.filter((m) => !m.isBye);
    assert.strictEqual(played.length, n - 1, `n=${n} should play exactly ${n - 1} matches`);
    assert.strictEqual(
      matches.filter((m) => m.winnerTo === null).length,
      1,
      `n=${n} should have exactly one final`
    );
  }
});

test('byes only ever happen in the first round', () => {
  // รอบหลัง null แปลว่า "ยังไม่รู้ผล" ไม่ใช่ "ไม่มีคู่แข่ง"
  // เคยพลาดตรงนี้: ทีมที่ได้บายรอบแรกถูกดันเข้ารอบชิงโดยไม่ต้องแข่งเลย
  for (let n = 2; n <= 33; n += 1) {
    singleElimination(teams(n))
      .filter((m) => m.isBye)
      .forEach((m) => assert.strictEqual(m.round, 1, `n=${n}: a bye appeared in round ${m.round}`));
  }
});

test('a team that got a bye still has to play in the next round', () => {
  const matches = singleElimination(teams(5));
  const round2 = matches.filter((m) => m.round === 2);
  round2.forEach((m) => {
    assert.strictEqual(m.isBye, false, 'round 2 is never a bye with 5 teams');
    assert.strictEqual(m.winnerId, null, 'nobody has won round 2 before it is played');
  });
});

test('byes go to the top seeds and their winner is known up front', () => {
  // 5 ทีมในสาย 8 -> 3 บาย ตกกับทีมวางอันดับต้น
  const matches = singleElimination(teams(5));
  const firstRound = matches.filter((m) => m.round === 1);
  const byes = firstRound.filter((m) => m.isBye);

  assert.strictEqual(byes.length, 3, 'five teams in an eight bracket means three byes');
  assert.strictEqual(firstRound.filter((m) => !m.isBye).length, 1, 'and one real first-round match');
  byes.forEach((m) => {
    assert.ok(m.winnerId, 'a bye already knows its winner');
    assert.strictEqual(m.winnerId, m.teamAId ?? m.teamBId);
  });
  // ทีมวางอันดับ 1 ต้องได้บาย
  assert.ok(byes.some((m) => m.teamAId === 't1' || m.teamBId === 't1'), 'seed 1 gets a bye');
});

test('bye winners are carried into the next round automatically', () => {
  const matches = singleElimination(teams(5));
  const round2 = matches.filter((m) => m.round === 2);
  const named = round2.flatMap((m) => [m.teamAId, m.teamBId]).filter(Boolean);
  assert.ok(named.includes('t1'), 'the seed 1 bye winner is already placed in round 2');
});

test('winners are routed to the correct next slot and side', () => {
  const matches = singleElimination(teams(8));
  matches.filter((m) => m.round === 1).forEach((m) => {
    assert.strictEqual(m.winnerTo?.round, 2);
    assert.strictEqual(m.winnerTo?.slot, Math.floor(m.slot / 2));
    assert.strictEqual(m.winnerTo?.side, (m.slot % 2) as 0 | 1);
    assert.strictEqual(m.winnerTo?.bracket, 'main');
  });
  const final = matches.find((m) => m.winnerTo === null);
  assert.strictEqual(final?.round, 3, 'eight teams finish in three rounds');
});

// ---- ENTRY POINT ----

test('too few teams produces no matches rather than a broken bracket', () => {
  assert.deepStrictEqual(generateMatches({ format: 'single_elim', teamIds: ['solo'] }).matches, []);
  assert.deepStrictEqual(generateMatches({ format: 'round_robin', teamIds: ['solo'] }).matches, []);
});
