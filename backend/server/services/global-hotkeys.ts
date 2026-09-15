// สิ่งที่คีย์ลัดระดับระบบสั่งได้ และการลงมือทำจริง
//
// ตัวสั่งคือแอพเดสก์ท็อป (สมัย v2 คือ process หลักของ Electron) ซึ่งไม่ได้ส่ง socket
// มันยิง POST /api/global-hotkeys/fire เข้ามาแทน แล้วมาลงเอยที่ไฟล์นี้
//
// ตัวงานจริงคือฟังก์ชันชุดเดียวกับที่ sockets/handlers.ts และปุ่มบนหน้า Control เรียก
// (startDraftPhase / stopDraftTimer / resumeDraft / popUndo / finishGame / undoGame / stepRound)
// ไฟล์นี้เป็นแค่ตัวแปลง "ชื่อคีย์ลัด" เป็น "การเรียกฟังก์ชัน" ไม่มีกติกาของตัวเอง
// ถ้าวันหนึ่งกติกาย้ายมาอยู่ที่นี่ ทางหน้า Control กับทางคีย์ลัดระบบจะเริ่มไม่ตรงกัน

import { GLOBAL_HOTKEY_ACTIONS } from '../domain/settings';
import type { GlobalHotkeyAction } from '../domain/settings';
import { getState, emitState, popUndo } from '../store/live-state';
import { startDraftPhase, stopDraftTimer, resumeDraft } from './draft-engine';
import { finishGame, undoGame, stepRound } from './live-match';
import type { FinishGameResult, UndoGameResult } from './live-match';

// ปุ่มที่แอพเดสก์ท็อปจองได้จริงบนเครื่องนี้
//
// ไม่ได้เก็บลง state และไม่ได้เขียนลงไฟล์ ตั้งใจ: มันไม่ใช่การตั้งค่า
// แต่เป็นข้อเท็จจริงของเครื่องเครื่องนี้ในรอบการทำงานนี้ ปิดแอพแล้วก็ไม่จริงอีกต่อไป
//
// ที่ต้องมีเพราะการจองคืน false เงียบๆ เมื่อโปรแกรมอื่นจองปุ่มไว้ก่อน
// ถ้าไม่รายงานกลับมา หน้าตั้งค่าจะโชว์ว่าจองแล้วทุกปุ่ม แล้วคนกดจะเจอ
// อาการ "กดแล้วไม่มีอะไรเกิดขึ้น" ซึ่งแยกไม่ออกจากของเสีย
let heldAccelerators: string[] = [];
let heldReported = false;

export function setHeldAccelerators(list: unknown): void {
  heldAccelerators = Array.isArray(list)
    ? list.filter((item): item is string => typeof item === 'string')
    : [];
  heldReported = true;
}

// null = ยังไม่มีใครรายงานมา (เปิดในเบราว์เซอร์ หรือแอพเพิ่งเริ่ม)
// ต่างจาก [] ซึ่งแปลว่ารายงานแล้วว่าจองไม่ได้สักปุ่ม
export function getHeldAccelerators(): string[] | null {
  return heldReported ? heldAccelerators : null;
}

export function isGlobalHotkeyAction(value: unknown): value is GlobalHotkeyAction {
  return typeof value === 'string'
    && (GLOBAL_HOTKEY_ACTIONS as readonly string[]).includes(value);
}

// changed: false = กดแล้วไม่มีอะไรเปลี่ยน ไม่ใช่ข้อผิดพลาดของคำขอ
// คนกดอยู่ที่ OBS ไม่ได้มองหน้าจอนี้ จึงตอบ 200 เสมอ แล้วส่ง code ไปให้แอพบอกเหตุผลเป็นภาษาของคนใช้
//
// finish / undo คือผลก้อนเดียวกับที่ปุ่ม +1 / -1 บนหน้า Control ได้กลับไป
// หน้า Control ต้องเอาไปขึ้นแถบ SERIES OVER พร้อมคู่ถัดไปเหมือนตอนกดปุ่มเอง
// ไม่งั้นแต้มที่ปิดซีรีส์จาก OBS จะบันทึกแล้ว แต่หน้าคุมงานไม่มีปุ่มขึ้นคู่ถัดไปให้กด
export interface HotkeyOutcome {
  changed: boolean;
  code?: string;
  error?: string;
  finish?: FinishGameResult;
  undo?: UndoGameResult;
}

export function runGlobalHotkey(action: GlobalHotkeyAction): HotkeyOutcome {
  const state = getState();

  switch (action) {
    case 'toggleBanner':
      // สลับเอง ไม่รับค่ามาจากผู้เรียก เหตุผลเดียวกับ setOverlayVisible ใน handlers.ts
      state.overlayVisible = !state.overlayVisible;
      emitState();
      return { changed: true };

    case 'pauseResume':
      if (state.draftRunning) {
        stopDraftTimer();
        emitState();
      } else {
        resumeDraft();
      }
      return { changed: true };

    case 'prevPhase':
      startDraftPhase(Math.max(0, state.draftPhaseIndex - 1));
      return { changed: true };

    case 'nextPhase':
      startDraftPhase(state.draftPhaseIndex + 1);
      return { changed: true };

    case 'undo':
      if (!popUndo()) return { changed: false };
      emitState();
      return { changed: true };

    case 'bluePlus':
    case 'redPlus': {
      const out = finishGame(action === 'bluePlus' ? 'blue' : 'red');
      if (out.error !== undefined) return { changed: false, code: out.code, error: out.error };
      return { changed: true, finish: out.result };
    }

    case 'blueMinus':
    case 'redMinus': {
      const out = undoGame(action === 'blueMinus' ? 'blue' : 'red');
      if (out.error !== undefined) return { changed: false, code: out.code, error: out.error };
      return { changed: true, undo: out.result };
    }

    case 'prevRound':
    case 'nextRound': {
      const out = stepRound(action === 'nextRound' ? 1 : -1);
      if (out.error === undefined) return { changed: true };
      // stepRound ตอบเป็นประโยค แปลงเป็น code ที่นี่ที่เดียว แอพจะได้ไม่ต้องเทียบข้อความภาษาอังกฤษ
      const code = /round 1/i.test(out.error) ? 'round-first' : /limit/i.test(out.error) ? 'round-limit' : 'round-error';
      return { changed: false, code, error: out.error };
    }

    default:
      return { changed: false };
  }
}
