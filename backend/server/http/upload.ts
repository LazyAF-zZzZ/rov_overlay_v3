// การตรวจไฟล์ภาพที่อัปโหลดเข้ามา ใช้ร่วมกันทุก endpoint ที่รับภาพ
//
// รับ body เป็นไฟล์ดิบ ไม่ใช้ multipart จะได้ไม่ต้องเพิ่ม dependency
//
// ตรวจสามชั้น และต้องครบทั้งสามเสมอ:
//   1. ชื่อ slot / id ต้องอยู่ในตารางหรือผ่านตัวตรวจ (ผู้เรียกทำเอง)
//   2. content-type ต้องเป็นชนิดที่รองรับ
//   3. magic bytes ต้องตรงกับชนิดที่บอกมาจริงๆ
//
// ชั้นที่ 3 สำคัญที่สุด: content-type เป็นแค่คำบอกเล่าของผู้ส่ง
// ไฟล์อะไรก็ได้ส่งมาพร้อม content-type: image/png ได้ทั้งนั้น

import express from 'express';
import type { Request, RequestHandler } from 'express';
import type { ImageExt } from '../domain/media';
import { SKIN_TYPES, SKIN_MAGIC } from '../domain/media';

export type UploadCheck =
  | { ext: ImageExt; body: Buffer; error?: undefined; status?: undefined }
  | { status: number; error: string; ext?: undefined; body?: undefined };

export function validateUpload(req: Request): UploadCheck {
  const contentType = String(req.get('content-type')).split(';')[0]!.trim();
  const ext = SKIN_TYPES[contentType];
  if (!ext) return { status: 415, error: 'Use a PNG, JPG or WEBP image' };

  const body = req.body as unknown;
  if (!Buffer.isBuffer(body) || body.length === 0) {
    return { status: 400, error: 'Empty upload' };
  }
  if (!SKIN_MAGIC[ext](body)) {
    return { status: 400, error: 'File contents do not match its type' };
  }
  return { ext, body };
}

export function rawImage(limit: number): RequestHandler {
  return express.raw({ type: Object.keys(SKIN_TYPES), limit });
}
