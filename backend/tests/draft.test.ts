import test from 'node:test';
import assert from 'node:assert';
import {
  DRAFT_SEQUENCE,
  PICK_COUNT,
  BAN_COUNT,
  isPickIndex,
  isBanIndex,
  isSlotId,
  parseSlotId,
  parseTimeToSeconds,
  formatSeconds,
  sanitizeTimer
} from '../server/domain/draft';

test('the draft sequence fills every pick and ban slot exactly once', () => {
  const seen = new Map<string, number>();
  DRAFT_SEQUENCE.forEach((phase) => {
    (phase.slots || []).forEach((slot) => {
      seen.set(slot, (seen.get(slot) || 0) + 1);
    });
  });

  const expected: string[] = [];
  ['blue', 'red'].forEach((side) => {
    for (let i = 0; i < PICK_COUNT; i += 1) expected.push(`${side}Pick${i}`);
    for (let i = 0; i < BAN_COUNT; i += 1) expected.push(`${side}Ban${i}`);
  });

  expected.forEach((slot) => {
    assert.strictEqual(seen.get(slot), 1, `${slot} should appear exactly once`);
  });
  assert.strictEqual(seen.size, expected.length, 'no unexpected slots in the sequence');
});

test('every slot named in the sequence is a valid slot id', () => {
  DRAFT_SEQUENCE.forEach((phase) => {
    (phase.slots || []).forEach((slot) => {
      assert.ok(isSlotId(slot), `${slot} should be a valid slot id`);
    });
  });
});

test('slot index guards match the real slot counts', () => {
  assert.ok(isPickIndex(0) && isPickIndex(PICK_COUNT - 1));
  assert.ok(!isPickIndex(PICK_COUNT) && !isPickIndex(-1) && !isPickIndex(1.5));
  assert.ok(isBanIndex(0) && isBanIndex(BAN_COUNT - 1));
  assert.ok(!isBanIndex(BAN_COUNT) && !isBanIndex(-1));
});

test('parseSlotId maps ids onto the state shape', () => {
  assert.deepStrictEqual(parseSlotId('bluePick2'), { team: 'teamBlue', type: 'picks', index: 2 });
  assert.deepStrictEqual(parseSlotId('redBan3'), { team: 'teamRed', type: 'bans', index: 3 });
  assert.strictEqual(parseSlotId('purplePick0'), null);
  assert.strictEqual(parseSlotId(null), null);
});

test('timer parsing and formatting round-trip', () => {
  assert.strictEqual(parseTimeToSeconds('01:30'), 90);
  assert.strictEqual(parseTimeToSeconds('45'), 45);
  assert.strictEqual(parseTimeToSeconds('bogus'), 0);
  assert.strictEqual(formatSeconds(90), '01:30');
  assert.strictEqual(formatSeconds(-10), '00:00');
  assert.strictEqual(formatSeconds(0), '00:00');
});

test('sanitizeTimer only accepts the two supported shapes', () => {
  assert.strictEqual(sanitizeTimer('12:34'), '12:34');
  assert.strictEqual(sanitizeTimer('999'), '999');
  assert.strictEqual(sanitizeTimer('abc'), '00:00');
  assert.strictEqual(sanitizeTimer('1:2:3'), '00:00');
});
