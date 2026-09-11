// คีย์ลัด: ตัวจับคู่และตัวแปลงเป็นข้อความ ใช้ร่วมกันระหว่าง
// หน้า Control Panel (ที่กดใช้จริง) กับหน้า /hotkeys (ที่ตั้งค่า)
//
// สองที่นี้ต้องคิดเหมือนกันเป๊ะ ไม่งั้นหน้าตั้งค่าจะโชว์ปุ่มหนึ่ง
// แต่หน้า control ดักอีกปุ่มหนึ่ง จึงอยู่ไฟล์เดียวกันไปเลย
//
// ต้องตรงกับ HOTKEY_DEFAULTS ใน server.js ด้วย
(function (global) {
  const MODIFIER_CODES = ['Alt', 'Control', 'Shift', 'Meta'];

  const DEFAULTS = {
    toggleBanner: { code: 'Alt', ctrl: false, shift: false, alt: false, meta: false },
    pauseResume: { code: 'Space', ctrl: false, shift: false, alt: false, meta: false },
    prevPhase: { code: 'ArrowLeft', ctrl: false, shift: false, alt: false, meta: false },
    nextPhase: { code: 'ArrowRight', ctrl: false, shift: false, alt: false, meta: false },
    undo: { code: 'KeyZ', ctrl: true, shift: false, alt: false, meta: false }
  };

  // เรียงตามลำดับที่อยากให้โชว์ในหน้าตั้งค่า
  // label = ชื่อเต็มในหน้าตั้งค่า, short = คำสั้นๆ ในแถบคำใบ้ท้ายหน้า control
  const ACTIONS = [
    { key: 'toggleBanner', label: 'Show / hide overlay banner', short: 'show/hide overlay',
      note: 'Fades the banner off air and back' },
    { key: 'pauseResume', label: 'Pause / resume draft timer', short: 'pause', note: '' },
    { key: 'prevPhase', label: 'Previous draft phase', short: 'prev phase', note: '' },
    { key: 'nextPhase', label: 'Next draft phase', short: 'next phase', note: '' },
    { key: 'undo', label: 'Undo last pick or ban', short: 'undo', note: '' }
  ];

  function isModifierBinding(binding) {
    return MODIFIER_CODES.includes(binding?.code);
  }

  // ชื่อที่คนอ่านรู้เรื่อง ไม่ใช่ 'KeyZ' หรือ 'ArrowLeft'
  function codeLabel(code) {
    if (!code) return '-';
    if (/^Key[A-Z]$/.test(code)) return code.slice(3);
    if (/^Digit[0-9]$/.test(code)) return code.slice(5);
    if (/^Numpad(.+)$/.test(code)) return `Num ${code.slice(6)}`;
    const named = {
      Space: 'Space', Enter: 'Enter', Escape: 'Esc', Tab: 'Tab', Backspace: 'Backspace',
      ArrowLeft: '←', ArrowRight: '→', ArrowUp: '↑', ArrowDown: '↓',
      Alt: 'Alt', Control: 'Ctrl', Shift: 'Shift', Meta: 'Win',
      Minus: '-', Equal: '=', BracketLeft: '[', BracketRight: ']',
      Semicolon: ';', Quote: "'", Backquote: '`', Backslash: '\\',
      Comma: ',', Period: '.', Slash: '/'
    };
    return named[code] || code;
  }

  function bindingLabel(binding) {
    if (!binding || !binding.code) return 'Not set';
    if (isModifierBinding(binding)) return `Tap ${codeLabel(binding.code)}`;
    const parts = [];
    if (binding.ctrl) parts.push('Ctrl');
    if (binding.shift) parts.push('Shift');
    if (binding.alt) parts.push('Alt');
    if (binding.meta) parts.push('Win');
    parts.push(codeLabel(binding.code));
    return parts.join(' + ');
  }

  // เทียบให้ตรงทุก modifier ไม่ใช่แค่ปุ่มหลัก
  // ถ้าเช็คแค่ code เท่ากัน Ctrl+Z จะไปโดน binding ของ Z เปล่าๆ ด้วย
  function matchesBinding(event, binding) {
    if (!binding || !binding.code || isModifierBinding(binding)) return false;
    return event.code === binding.code &&
      event.ctrlKey === binding.ctrl &&
      event.shiftKey === binding.shift &&
      event.altKey === binding.alt &&
      event.metaKey === binding.meta;
  }

  function sameBinding(a, b) {
    return Boolean(a && b) && a.code === b.code && a.ctrl === b.ctrl &&
      a.shift === b.shift && a.alt === b.alt && a.meta === b.meta;
  }

  // อ่านปุ่มที่เพิ่งกดออกมาเป็น binding
  // กด modifier เดี่ยวๆ = binding แบบแตะ, กดพร้อมปุ่มอื่น = คีย์ผสม
  function bindingFromEvent(event) {
    const isModifierKey = MODIFIER_CODES.includes(event.key);
    if (isModifierKey) {
      return { code: event.key, ctrl: false, shift: false, alt: false, meta: false };
    }
    return {
      code: event.code,
      ctrl: event.ctrlKey,
      shift: event.shiftKey,
      alt: event.altKey,
      meta: event.metaKey
    };
  }

  global.HotkeyUtils = {
    MODIFIER_CODES, DEFAULTS, ACTIONS,
    isModifierBinding, codeLabel, bindingLabel, matchesBinding, sameBinding, bindingFromEvent
  };
})(window);
