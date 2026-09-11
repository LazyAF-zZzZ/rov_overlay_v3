// ตัวช่วยสำหรับเทสต์
//
// must() ใช้แทนการเขียน ! ท้ายค่า
// ได้ทั้งให้ตัวตรวจชนิดยอม และเวลาเทสต์ล้มก็บอกสาเหตุตรงๆ
// แทนที่จะไปพังตอนอ่าน property ของ null ซึ่งอ่านไม่ออกว่าอะไรพัง

import assert from 'node:assert';

export function must<T>(value: T | null | undefined, message = 'expected a value'): T {
  assert.ok(value !== null && value !== undefined, message);
  return value;
}
