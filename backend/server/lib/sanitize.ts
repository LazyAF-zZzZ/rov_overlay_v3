// ตัวกรองค่าพื้นฐาน ใช้ซ้ำทุกที่ที่รับค่าจากผู้ใช้
//
// หลักการ: ทุกค่าที่เข้ามาจาก socket / HTTP body ต้องผ่านตัวใดตัวหนึ่งในนี้ก่อน
// ไม่มีการเชื่อค่าที่ส่งมาแล้วเอาไปใช้ตรงๆ
//
// พารามิเตอร์เป็น unknown ตั้งใจ ไม่ใช่ any
// ค่าที่มาจากเครือข่ายไม่มีอะไรรับประกันชนิดเลย การประกาศเป็น unknown
// บังคับให้ต้องตรวจก่อนใช้ ซึ่งเป็นหน้าที่ของไฟล์นี้พอดี
// ถ้าใช้ any ตัวตรวจชนิดจะเงียบ แล้วเราจะกลับไปอยู่จุดเดิมก่อนมี TypeScript

export function clampNumber(value: unknown, min: number, max: number): number {
  const n = Number(value);
  if (!Number.isFinite(n)) return min;
  return Math.min(max, Math.max(min, Math.trunc(n)));
}

// อักขระควบคุม C0 (0x00-0x1f) กับ DEL (0x7f)
//
// เขียนเป็นการเทียบรหัสตัวอักษรแทน regex ตั้งใจ ไม่ใช่ความชอบส่วนตัว
// regex ตัวเดิมต้องใส่ escape ของช่วงอักขระควบคุม ซึ่งพอไฟล์ถูกแก้ผ่าน
// เครื่องมือที่ตีความ escape ผิด จะกลายเป็นอักขระควบคุมจริงฝังในซอร์ส
// (รวมถึงไบต์ 0x00) แล้ว editor/diff/git พังตามกันหมด
// วิธีนี้ไม่มี escape ให้พังตั้งแต่แรก
function isControlCharCode(code: number): boolean {
  return code < 0x20 || code === 0x7f;
}

export function stripControlChars(value: string): string {
  let out = '';
  for (let i = 0; i < value.length; i += 1) {
    if (!isControlCharCode(value.charCodeAt(i))) out += value[i];
  }
  return out;
}

// ค่านี้ถูกเอาไปแสดงบนหน้าออกอากาศ ตัดอักขระควบคุมทิ้งก่อนเสมอ
export function sanitizeText(value: unknown, maxLength: number): string {
  if (typeof value !== 'string') return '';
  return stripControlChars(value).trim().slice(0, maxLength);
}

export function normalizeArray<T>(
  values: unknown,
  length: number,
  sanitizer: (value: unknown, index: number) => T
): T[] {
  const input = Array.isArray(values) ? (values as unknown[]) : [];
  return Array.from({ length }, (_, index) => sanitizer(input[index], index));
}
