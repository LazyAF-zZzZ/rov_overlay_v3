# 🆕 คู่มือฟีเจอร์ใหม่

## ฟีเจอร์ที่เพิ่มเข้ามา

### 1. 🎭 Hero Icons สำหรับ Ban Phase
### 2. 🏆 Team Logos
### 3. 🔄 Switch Teams Button

---

## 📁 โครงสร้างโฟลเดอร์ใหม่

```
public/images/
├── heroes/              # รูปฮีโร่เต็มตัว (240x390px) สำหรับ Pick
├── heroes-icons/        # ไอคอนฮีโร่ (80x80px) สำหรับ Ban ⭐ ใหม่
└── team-logos/          # โลโก้ทีม (200x200px) ⭐ ใหม่
```

---

## 🎭 1. Hero Icons สำหรับ Ban Phase

### ทำไมต้องมี?
- Ban Phase ใช้ไอคอนเล็กๆ ดูสวยกว่า
- ไม่ต้องใช้รูปเต็มตัวที่ใหญ่เกินไป
- ประหยัด Memory และโหลดเร็วขึ้น

### ขนาดและรูปแบบ
- **ขนาด:** 80 x 80 pixels (แนะนำ)
- **ไฟล์:** PNG (รองรับพื้นหลังโปร่งใส)
- **ชื่อ:** ตัวเล็กทั้งหมด ต้องตรงกับในโฟลเดอร์ heroes/

### ตัวอย่างชื่อไฟล์
```
public/images/heroes-icons/
├── airi.png
├── nakroth.png
├── tel'annas.png
├── azzen'ka.png
├── bolt baron.png
└── ... (125 ไฟล์)
```

### วิธีเตรียมไอคอน

**Option 1: Crop จากรูปเต็ม**
1. เปิดรูปฮีโร่เต็มตัวด้วย Photoshop/GIMP
2. Crop เฉพาะหน้าหรือสัญลักษณ์
3. Resize เป็น 80x80 pixels
4. Save as PNG

**Option 2: ใช้รูปจาก Game Assets**
- หาไอคอนฮีโร่จากเว็บไซต์ ROV
- Download และ Resize เป็น 80x80

**Option 3: ถ้าไม่มีไอคอน**
- ระบบจะใช้รูปเต็มตัวแทนอัตโนมัติ
- แต่แนะนำให้มีไอคอนเพื่อความสวยงาม

### การทดสอบ
1. วางไอคอนใน `public/images/heroes-icons/`
2. เปิด Control Panel
3. เลือกฮีโร่ใน Ban slots
4. ดูผลใน Overlay

---

## 🏆 2. Team Logos

### ทำไมต้องมี?
- แสดงโลโก้ทีมข้างชื่อทีม
- ดูเป็นมืออาชีพมากขึ้น
- ง่ายต่อการจดจำทีม

### ขนาดและรูปแบบ
- **ขนาด:** 200 x 200 pixels (แนะนำ)
- **ไฟล์:** PNG (รองรับพื้นหลังโปร่งใส)
- **รูปทรง:** สี่เหลี่ยมจัตุรัสหรือวงกลม

### ตัวอย่างชื่อไฟล์
```
public/images/team-logos/
├── fw.png           # Flash Wolves
├── ea.png           # Execution Army
├── rrq.png          # RRQ Hoshi
├── evos.png         # EVOS Esports
├── blue-team.png    # Default Blue Team
└── red-team.png     # Default Red Team
```

### การตั้งค่าใน Control Panel

1. เปิด Control Panel
2. ในส่วน Team Settings เห็น:
   ```
   Team Logo (ชื่อไฟล์ใน team-logos/):
   [blue-team.png]
   ```
3. พิมพ์ชื่อไฟล์ เช่น `fw.png`
4. คลิก "Update Team"
5. โลโก้จะแสดงใน Overlay

### Tips การเตรียมโลโก้
- ใช้พื้นหลังโปร่งใส (Transparent)
- ไม่ควรมีเงาหรือเอฟเฟกต์มาก
- ขนาดไฟล์ไม่ควรเกิน 500KB
- โลโก้ควรชัดเจน อ่านง่าย

---

## 🔄 3. Switch Teams Button

### ทำไมต้องมี?
- สลับข้อมูลทีมได้อย่างรวดเร็ว
- ไม่ต้องพิมพ์ใหม่ทั้งหมด
- ประหยัดเวลาในการเปลี่ยนทีม

### สิ่งที่ถูก Switch
เมื่อกดปุ่ม Switch Teams จะสลับ:
- ✅ ชื่อทีม
- ✅ สกอร์
- ✅ โลโก้ทีม
- ✅ ชื่อผู้เล่นทั้ง 5 คน
- ✅ Picks ทั้งหมด
- ✅ Bans ทั้งหมด

### วิธีใช้งาน

1. เปิด Control Panel
2. หาส่วน **"🔄 Switch Teams"**
3. คลิกปุ่ม **"🔄 Switch Blue ↔️ Red"**
4. ยืนยันการสลับ
5. ข้อมูลจะสลับทันที
6. หน้า Control Panel จะ Refresh เพื่ออัปเดตข้อมูล

### ตัวอย่างการใช้งาน

**ก่อน Switch:**
```
Blue Team: FW (Score: 0)
Players: [P1, P2, P3, P4, P5]
Picks: [Nakroth, Violet, ...]

Red Team: EA (Score: 2)
Players: [A, B, C, D, E]
Picks: [Murad, Elsu, ...]
```

**หลัง Switch:**
```
Blue Team: EA (Score: 2)
Players: [A, B, C, D, E]
Picks: [Murad, Elsu, ...]

Red Team: FW (Score: 0)
Players: [P1, P2, P3, P4, P5]
Picks: [Nakroth, Violet, ...]
```

### เมื่อไหร่ควรใช้?
- เปลี่ยนรอบแข่ง (ทีมสลับด้าน)
- ตั้งค่าผิดทีม
- ต้องการเริ่มเกมใหม่โดยสลับทีม

---

## 📝 Checklist การใช้งานฟีเจอร์ใหม่

### Hero Icons
- [ ] เตรียมไอคอนฮีโร่ 125 ไฟล์ (หรือเท่าที่จะใช้)
- [ ] ขนาด 80x80 pixels
- [ ] ชื่อไฟล์ตัวเล็กทั้งหมด
- [ ] วางใน `public/images/heroes-icons/`
- [ ] ทดสอบใน Overlay

### Team Logos
- [ ] เตรียมโลโก้ทีม
- [ ] ขนาด 200x200 pixels
- [ ] พื้นหลังโปร่งใส (แนะนำ)
- [ ] วางใน `public/images/team-logos/`
- [ ] ตั้งชื่อไฟล์ใน Control Panel
- [ ] ทดสอบการแสดงผล

### Switch Teams
- [ ] ทดสอบกดปุ่ม Switch
- [ ] ตรวจสอบว่าข้อมูลสลับถูกต้อง
- [ ] ทดสอบใน Overlay
- [ ] ทดสอบหลายรอบ

---

## 🎨 การปรับแต่งเพิ่มเติม

### ปรับขนาดโลโก้ทีม

แก้ไขใน `public/css/overlay.css`:
```css
.team-logo {
    width: 60px;      /* ปรับขนาด */
    height: 60px;
}
```

### ปรับขนาดไอคอน Ban

```css
.ban-slot {
    width: 80px;      /* ปรับขนาด */
    height: 80px;
}
```

### ซ่อนโลโก้ทีม

```css
.team-logo {
    display: none;
}
```

---

## ❓ FAQ

**Q: ถ้าไม่มีไอคอนฮีโร่จะเป็นไร?**
A: ระบบจะใช้รูปเต็มตัวจากโฟลเดอร์ heroes/ แทนอัตโนมัติ

**Q: โลโก้ทีมต้องเป็นขนาดเท่าไหร่?**
A: แนะนำ 200x200 แต่ขนาดอื่นก็ได้ ระบบจะปรับให้เอง

**Q: Switch Teams แล้วต้องตั้งค่าใหม่ไหม?**
A: ไม่ต้อง ทุกอย่างสลับอัตโนมัติ แค่ Refresh หน้า Control Panel

**Q: สามารถ Switch กลางเกมได้ไหม?**
A: ได้ แต่ควรทำตอนพักหรือก่อนเริ่มเกมใหม่

**Q: โลโก้ไม่แสดง?**
A: ตรวจสอบ:
- ชื่อไฟล์ถูกต้อง
- ไฟล์อยู่ใน public/images/team-logos/
- Refresh หน้า Overlay (Ctrl+F5)

---

## 🚀 ตัวอย่างการใช้งานจริง

### Scenario 1: เตรียมการแข่งขัน
1. เตรียมโลโก้ทีม FW และ EA
2. เตรียมไอคอนฮีโร่
3. ตั้งค่าชื่อทีมและผู้เล่น
4. ตั้งชื่อไฟล์โลโก้: `fw.png`, `ea.png`
5. Update ทั้งสองทีม
6. เริ่มการแข่งขัน

### Scenario 2: เปลี่ยนรอบ
1. จบเกมแรก FW (Blue) vs EA (Red)
2. เกมที่สอง ทีมสลับด้าน
3. กดปุ่ม Switch Teams
4. ทีมสลับอัตโนมัติ
5. Clear Picks & Bans
6. เริ่มเกมใหม่

---

**มีความสุขกับฟีเจอร์ใหม่! 🎉**
