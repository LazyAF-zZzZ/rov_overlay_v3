# 🚀 Quick Start Guide - ROV Overlay Tool

## การเริ่มต้นใช้งานแบบรวดเร็ว

### ขั้นตอนที่ 1: ติดตั้ง Node.js

ตรวจสอบว่ามี Node.js ติดตั้งแล้ว:
```bash
node --version
npm --version
```

หากยังไม่มี ดาวน์โหลดได้ที่: https://nodejs.org/

### ขั้นตอนที่ 2: ติดตั้ง Dependencies

```bash
npm install
```

### ขั้นตอนที่ 3: เตรียมรูปฮีโร่

วางรูปฮีโร่ในโฟลเดอร์:
```
public/images/heroes/
```

ตัวอย่างชื่อไฟล์:
- airi.png
- nakroth.png
- valhein.png
- tel'annas.png (ใช้ ' ตามชื่อจริง)
- azzen'ka.png
- d'arcy.png
- y'bneth.png
- bolt baron.png (มีช่องว่าง)
- lu bu.png
- wonder woman.png

**สำคัญ:** ชื่อไฟล์ต้องเป็น**ตัวเล็กทั้งหมด (lowercase)** และตรงกับชื่อในระบบทุกตัวอักษร

### ขั้นตอนที่ 4: รัน Server

```bash
npm start
```

คุณจะเห็นข้อความ:
```
===========================================
ROV Overlay Tool Server Running
===========================================
Control Panel: http://localhost:3000
Overlay: http://localhost:3000/overlay
===========================================
```

### ขั้นตอนที่ 5: เปิด Control Panel

เปิดเว็บเบราว์เซอร์และไปที่:
```
http://localhost:3000
```

### ขั้นตอนที่ 6: เปิด Overlay

**สำหรับทดสอบ:**
เปิดหน้าต่างใหม่:
```
http://localhost:3000/overlay
```

**สำหรับใช้งานจริงกับ OBS:**
1. เปิด OBS Studio
2. คลิก `+` ใน Sources
3. เลือก `Browser`
4. ตั้งค่า:
   - URL: `http://localhost:3000/overlay`
   - Width: 1920
   - Height: 1080
5. กด OK

### ขั้นตอนที่ 7: ทดสอบระบบ

1. ใน Control Panel ลองเลือกฮีโร่
2. ดูผลลัพธ์ใน Overlay
3. ลองเปลี่ยนชื่อทีม, สกอร์, Timer

## การใช้งานพื้นฐาน

### อัพเดทข้อมูลทีม
1. กรอกชื่อทีมใน Blue Team / Red Team
2. กรอกสกอร์
3. กรอกชื่อผู้เล่น
4. คลิก "Update Team" และ "Update Players"

### เลือกฮีโร่
1. เลือกฮีโร่จาก Dropdown ในแต่ละ Slot
2. Overlay จะอัพเดทอัตโนมัติ

### เลือก Ban
1. เลือกฮีโร่ที่ถูก Ban จาก Dropdown
2. Overlay จะแสดง Effect Ban

### ล้างการเลือก
- คลิก "Clear" ข้างแต่ละ Slot เพื่อลล้างทีละตัว
- คลิก "Clear All Picks & Bans" เพื่อล้างทั้งหมด

## Tips & Tricks

### 1. การใช้ร่วมกับ OBS
- ใช้ Scene Collection แยกสำหรับแต่ละแมตช์
- ตั้ง Hotkey สำหรับ Show/Hide Overlay
- ใช้ Transition Effect เมื่อเปลี่ยน Scene

### 2. การใช้บนเครื่องหลายคน
เปลี่ยน localhost เป็น IP Address ของเครื่อง Server:
```
http://192.168.1.100:3000
```

### 3. การ Backup ข้อมูล
Server จะจดจำสถานะล่าสุดตราบที่ยังไม่ปิด
ถ้าต้องการ Save ข้อมูล แนะนำให้ Screenshot หน้า Control Panel

### 4. Performance
- ปิดแท็บอื่นที่ไม่ใช้ใน Browser
- ใช้ Hardware Acceleration ใน OBS
- ใช้ GPU Encoding ถ้าเป็นไปได้

## คำสั่งที่มีประโยชน์

### รัน Development Mode (Auto-restart)
```bash
npm run dev
```

### ตรวจสอบ Port ที่ใช้งาน
```bash
netstat -ano | findstr :3000
```

### เปลี่ยน Port (แก้ไข server.js)
```javascript
const PORT = process.env.PORT || 3001; // เปลี่ยนเป็น 3001
```

## Checklist ก่อนแข่ง

- [ ] ติดตั้ง Node.js และ Dependencies แล้ว
- [ ] รูปฮีโร่ครบทุกตัวที่จะใช้
- [ ] ทดสอบ Server รันได้ปกติ
- [ ] OBS ตั้งค่า Browser Source เรียบร้อย
- [ ] ทดสอบการเลือกฮีโร่และ Ban ใน Overlay
- [ ] เช็คว่า Network เสถียร (ถ้าใช้หลายเครื่อง)
- [ ] เตรียม Backup Plan (Screenshot, Manual overlay)

## การแก้ปัญหาด่วน

### Overlay ไม่แสดง
1. กด F5 Refresh
2. กด F12 เช็ค Console Error
3. Restart Server

### รูปฮีโร่ไม่ขึ้น
1. เช็คชื่อไฟล์ให้ตรงกับระบบ
2. เช็คว่าเป็นไฟล์ .png
3. Refresh หน้า Overlay

### ไม่สามารถเชื่อมต่อ
1. เช็คว่า Server ยังรันอยู่
2. เช็ค Port 3000 ว่าไม่ถูกใช้งานโดยโปรแกรมอื่น
3. ปิด Firewall ชั่วคราว

---

**หากยังมีปัญหา:** ให้เช็ค README.md เพื่อดูข้อมูลเพิ่มเติม

**มีความสุขกับการใช้งาน! 🎮**
