# 🎨 คู่มือการปรับแต่ง Layout Overlay

## ไฟล์ที่ใช้ในการปรับแต่ง

```
rov-overlay-tool/public/
├── overlay.html        # โครงสร้าง HTML
├── css/
│   └── overlay.css    # รูปแบบและตำแหน่ง
└── js/
    └── overlay.js     # Logic (ไม่ต้องแก้สำหรับ Layout)
```

---

## 📐 โครงสร้าง Layout ปัจจุบัน

```
┌─────────────────────────────────────────────────┐
│  BAN SECTION (ด้านบน)                          │
│  [BAN] [x][x][x][x] [MATCH INFO] [x][x][x][x] [BAN] │
├─────────────────────────────────────────────────┤
│                                                 │
│              BACKGROUND (โปร่งใส)              │
│                                                 │
├─────────────────────────────────────────────────┤
│  PICK SECTION (ด้านล่าง)                       │
│  [Hero1][Hero2][Hero3][Hero4][Hero5]            │
└─────────────────────────────────────────────────┘
```

---

## 🔧 การปรับแต่งที่พบบ่อย

### 1. ปรับขนาดช่องฮีโร่

**ไฟล์:** `public/css/overlay.css`

```css
.pick-slot {
    width: 240px;      /* ความกว้าง */
    height: 390px;     /* ความสูง */
    /* ... */
}
```

**ตัวอย่าง:** ถ้าต้องการช่องเล็กลง
```css
.pick-slot {
    width: 200px;
    height: 350px;
}
```

### 2. ปรับระยะห่างระหว่างช่อง

```css
.team-picks {
    display: flex;
    gap: 15px;        /* ระยะห่างระหว่างช่อง */
}
```

**ตัวอย่าง:** เพิ่มระยะห่าง
```css
.team-picks {
    display: flex;
    gap: 25px;        /* เพิ่มเป็น 25px */
}
```

### 3. ปรับตำแหน่งส่วน Ban

**ไฟล์:** `public/css/overlay.css`

```css
.ban-section {
    position: absolute;
    top: 0;           /* ระยะจากด้านบน */
    left: 0;
    right: 0;
    height: 180px;    /* ความสูงของส่วน Ban */
}
```

### 4. ปรับตำแหน่งส่วน Pick

```css
.pick-section {
    position: absolute;
    bottom: 0;        /* ระยะจากด้านล่าง */
    left: 0;
    right: 0;
    height: 450px;    /* ความสูงของส่วน Pick */
}
```

### 5. ปรับตำแหน่งเลข Position

```css
.position-indicator {
    position: absolute;
    bottom: 60px;     /* ระยะจากด้านล่าง */
    left: 10px;       /* ระยะจากด้านซ้าย */
    width: 40px;
    height: 40px;
}
```

**ตัวอย่างตำแหน่งต่างๆ:**

```css
/* มุมบนซ้าย */
.position-indicator {
    top: 10px;
    left: 10px;
}

/* มุมบนขวา */
.position-indicator {
    top: 10px;
    right: 10px;
}

/* มุมล่างซ้าย (ตามรูปที่แนบ) */
.position-indicator {
    bottom: 60px;
    left: 10px;
}

/* มุมล่างขวา */
.position-indicator {
    bottom: 60px;
    right: 10px;
}
```

### 6. ปรับตำแหน่งชื่อผู้เล่น

```css
.player-info {
    position: absolute;
    bottom: 0;        /* ระยะจากด้านล่าง */
    left: 0;
    right: 0;
    padding: 15px;
}
```

### 7. เปลี่ยนสี Theme

**Blue Team:**
```css
.blue-team .pick-slot {
    border-color: #00bfff;           /* สีขอบ */
    box-shadow: 0 0 20px rgba(0, 191, 255, 0.3);  /* เงา */
}
```

**Red Team:**
```css
.red-team .pick-slot {
    border-color: #ff4444;
    box-shadow: 0 0 20px rgba(255, 68, 68, 0.3);
}
```

### 8. ปรับขนาดตัวอักษร

**ชื่อผู้เล่น:**
```css
.player-name {
    font-size: 18px;  /* ขนาดตัวอักษร */
    font-weight: bold;
}
```

**ชื่อทีม:**
```css
.team-name {
    font-size: 32px;
}
```

**Timer:**
```css
.timer {
    font-size: 28px;
}
```

---

## 🎯 ตัวอย่างการปรับแต่งตามรูปที่แนบ

### การปรับให้เหมือนรูป 100%

#### 1. แก้ไขตำแหน่งเลข Position
```css
.position-indicator {
    position: absolute;
    bottom: 60px;     /* ย้ายลงล่าง */
    left: 10px;       /* ชิดซ้าย */
    width: 40px;
    height: 40px;
    background: rgba(0, 0, 0, 0.9);
    border-radius: 50%;
    font-size: 20px;
    font-weight: bold;
    color: #ffffff;
    z-index: 10;
}
```

#### 2. ทำให้รูปฮีโร่แสดงเต็มช่อง
```css
.hero-image {
    width: 100%;
    height: 100%;
    background-size: cover;      /* ครอบคลุมทั้งหมด */
    background-position: center;
    opacity: 1;                  /* แสดงเต็มที่ */
    transition: opacity 0.3s ease;
}

.pick-slot .hero-image {
    opacity: 1 !important;       /* แสดงชัดเจน */
}
```

#### 3. ปรับพื้นหลังชื่อผู้เล่น
```css
.player-info {
    position: absolute;
    bottom: 0;
    left: 0;
    right: 0;
    padding: 12px;
    background: linear-gradient(to top, rgba(0, 0, 0, 0.95), rgba(0, 0, 0, 0.8));
    z-index: 10;
}
```

---

## 📱 Layout Variations (เลย์เอาต์แบบต่างๆ)

### Layout 1: ด้านล่างเต็มแถว (ปัจจุบัน)
```css
.pick-section {
    display: flex;
    justify-content: space-between;
    bottom: 0;
}
```

### Layout 2: ด้านข้างซ้าย-ขวา
```css
.pick-section {
    display: flex;
    flex-direction: column;
    height: 100%;
    justify-content: center;
}

.blue-team {
    position: absolute;
    left: 50px;
    flex-direction: column;
}

.red-team {
    position: absolute;
    right: 50px;
    flex-direction: column;
}
```

### Layout 3: กึ่งกลางหน้าจอ
```css
.pick-section {
    display: flex;
    justify-content: center;
    align-items: center;
    bottom: 50%;
    transform: translateY(50%);
}
```

---

## 🔍 Tips การปรับแต่ง

### 1. ทดสอบแบบ Real-time
- เปิด Overlay ใน Browser
- กด F12 → เปิด DevTools
- แก้ไข CSS ใน DevTools ได้ทันที
- เมื่อชอบแล้วค่อยคัดลอกไปใส่ในไฟล์จริง

### 2. ใช้ Chrome DevTools
```
1. กด F12
2. คลิก Element ที่ต้องการแก้
3. แก้ไข CSS ในแท็บ Styles
4. ดูผลลัพธ์ทันที
```

### 3. Backup ก่อนแก้ไข
```bash
cp public/css/overlay.css public/css/overlay.css.backup
```

### 4. ใช้ CSS Variables (แนะนำ)
เพิ่มที่ตอนต้นไฟล์:
```css
:root {
    --hero-width: 240px;
    --hero-height: 390px;
    --gap: 15px;
    --blue-color: #00bfff;
    --red-color: #ff4444;
}

.pick-slot {
    width: var(--hero-width);
    height: var(--hero-height);
}

.team-picks {
    gap: var(--gap);
}
```

---

## 📋 Checklist การแก้ไข

เมื่อแก้ไข Layout แล้ว:

- [ ] ทดสอบใน Browser ปกติ
- [ ] ทดสอบใน OBS Browser Source
- [ ] ตรวจสอบความละเอียด 1920x1080
- [ ] ทดสอบกับรูปฮีโร่จริง
- [ ] ทดสอบทั้งทีม Blue และ Red
- [ ] ตรวจสอบว่าข้อความไม่ทับกัน
- [ ] Backup ไฟล์เดิม

---

## 🚀 ตัวอย่าง Layout Custom

### Example 1: ช่องฮีโร่เล็กลง + ระยะห่างมาก

```css
.pick-slot {
    width: 180px;
    height: 300px;
}

.team-picks {
    gap: 30px;
}
```

### Example 2: เลข Position ใหญ่ขึ้น

```css
.position-indicator {
    width: 50px;
    height: 50px;
    font-size: 24px;
}
```

### Example 3: ชื่อผู้เล่นใหญ่และชัดเจน

```css
.player-name {
    font-size: 22px;
    font-weight: 900;
    text-shadow: 3px 3px 6px rgba(0, 0, 0, 1);
}
```

---

## ❓ FAQ

**Q: แก้แล้วไม่เห็นเปลี่ยน?**
A: กด Ctrl+F5 (Hard Refresh) หรือ Clear Cache

**Q: ต้องการเปลี่ยนทั้งหมดให้เหมือนรูปที่แนบ?**
A: ดูในส่วน "ตัวอย่างการปรับแต่งตามรูปที่แนบ"

**Q: จะทำให้ Responsive ได้ไหม?**
A: ได้ แต่ต้องใช้ @media queries และ % แทน px

**Q: ต้องการทำ Animation เพิ่ม?**
A: ใช้ @keyframes ใน CSS

---

## 📞 ต้องการความช่วยเหลือ?

หากต้องการปรับแต่ง Layout แบบเฉพาะเจาะจง:
1. ส่งรูป mockup ที่ต้องการ
2. อธิบายว่าต้องการเปลี่ยนอะไรบ้าง
3. ระบุขนาดและตำแหน่งที่ต้องการ

**Happy Customizing! 🎨**
