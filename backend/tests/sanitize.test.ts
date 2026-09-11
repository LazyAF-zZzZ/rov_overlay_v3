import test from 'node:test';
import assert from 'node:assert';
import { clampNumber, sanitizeText, normalizeArray, stripControlChars } from '../server/lib/sanitize';

test('sanitizeText trims, caps length, and drops control characters', () => {
  assert.strictEqual(sanitizeText('  FW  ', 24), 'FW');
  assert.strictEqual(sanitizeText('x'.repeat(50), 24), 'x'.repeat(24));
  assert.strictEqual(sanitizeText(null, 24), '');
  assert.strictEqual(sanitizeText(12345, 24), '');
  assert.strictEqual(sanitizeText('ทีมสีน้ำเงิน', 24), 'ทีมสีน้ำเงิน');
});

test('stripControlChars removes C0 and DEL but keeps printable text', () => {
  const dirty = `a${String.fromCharCode(0)}b${String.fromCharCode(31)}c${String.fromCharCode(127)}d`;
  assert.strictEqual(stripControlChars(dirty), 'abcd');
  assert.strictEqual(stripControlChars('normal'), 'normal');
});

test('sanitizeText keeps emoji and multi-byte characters intact', () => {
  assert.strictEqual(sanitizeText('team 😀', 24), 'team 😀');
});

test('clampNumber bounds values and rejects nonsense', () => {
  assert.strictEqual(clampNumber(5, 0, 99), 5);
  assert.strictEqual(clampNumber(-5, 0, 99), 0);
  assert.strictEqual(clampNumber(999, 0, 99), 99);
  assert.strictEqual(clampNumber('abc', 0, 99), 0);
  assert.strictEqual(clampNumber(undefined, 0, 99), 0);
  assert.strictEqual(clampNumber(7.9, 0, 99), 7);
});

test('normalizeArray always returns exactly the requested length', () => {
  assert.deepStrictEqual(normalizeArray([1, 2], 4, (v) => v ?? null), [1, 2, null, null]);
  assert.deepStrictEqual(normalizeArray([1, 2, 3, 4, 5], 3, (v) => v), [1, 2, 3]);
  assert.deepStrictEqual(normalizeArray('not an array', 2, (v) => v ?? null), [null, null]);
});
