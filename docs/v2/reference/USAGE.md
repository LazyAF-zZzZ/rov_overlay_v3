# คู่มือการใช้งาน ROV Overlay Tool

## การตั้งค่าเริ่มต้น

### 1. ติดตั้ง Dependencies
```bash
npm install
```

### 2. ตรวจสอบรูปฮีโร่
```bash
npm run check-heroes
```

คำสั่งนี้จะแสดงรายการฮีโร่ที่มีรูปและยังไม่มีรูป

### 3. รัน Server
```bash
npm start
```

## การใช้งานขั้นสูง

### เปลี่ยน Port
แก้ไขไฟล์ `server.js`:
```javascript
const PORT = process.env.PORT || 3000; // เปลี่ยนเป็น 3001, 8080, ฯลฯ
```

### การใช้งานบนเครือข่าย LAN
1. หา IP Address ของเครื่อง Server
   - Windows: `ipconfig`
   - Mac/Linux: `ifconfig` หรือ `ip addr`

2. ใช้ IP นั้นแทน localhost:
   ```
   http://192.168.1.100:3000
   ```

### การเพิ่ม/ลดจำนวนผู้เล่น
แก้ไขไฟล์ `server.js` ในส่วน gameState:
```javascript
teamBlue: {
  picks: [null, null, null, null, null], // เพิ่มหรือลด null
  players: ['P1', 'P2', 'P3', 'P4', 'P5'] // เพิ่มหรือลดชื่อ
}
```

### การเพิ่ม/ลดจำนวน Ban
แก้ไขไฟล์ `server.js`:
```javascript
teamBlue: {
  bans: [null, null, null, null], // เพิ่มหรือลด null
}
```

จากนั้นแก้ไข HTML และ CSS ให้สอดคล้องกัน

## Tips สำหรับการใช้งานจริง

### 1. ใช้ Dual Monitor
- Monitor 1: OBS + Overlay
- Monitor 2: Control Panel

### 2. ใช้ Hotkeys ใน OBS
ตั้ง Hotkey สำหรับ:
- Show/Hide Overlay
- Switch Scene

### 3. สำรองข้อมูล
- Screenshot Control Panel ก่อนเริ่มแมตช์
- เก็บชื่อทีม, ผู้เล่น, สกอร์ไว้ในเอกสาร

### 4. Network Setup
สำหรับ Production ควรใช้:
- เครื่อง Server เสถียร
- Network Cable (ไม่ใช้ WiFi)
- Static IP Address

## Keyboard Shortcuts

ในหน้า Control Panel:
- `Tab`: ข้ามไปช่องถัดไป
- `Enter`: Submit form ปัจจุบัน
- `Ctrl + R`: Refresh หน้า

## API Endpoints

### GET `/api/heroes`
ดึงรายชื่อฮีโร่ทั้งหมด

### GET `/api/state`
ดึงสถานะปัจจุบันของเกม

### WebSocket Events

**Client → Server:**
- `updateTeamName` - อัพเดทชื่อทีม
- `updateScore` - อัพเดทสกอร์
- `updatePlayerName` - อัพเดทชื่อผู้เล่น
- `updatePick` - เลือกฮีโร่
- `updateBan` - Ban ฮีโร่
- `clearPick` - ล้างการเลือก
- `clearBan` - ล้าง Ban
- `clearAll` - ล้างทั้งหมด
- `updateTimer` - อัพเดท Timer
- `updateMatchInfo` - อัพเดทข้อมูลแมตช์

**Server → Client:**
- `stateUpdate` - แจ้งการอัพเดทสถานะ

## สร้าง Custom Theme

### เปลี่ยนสี
แก้ไข `public/css/overlay.css`:
```css
/* ตัวอย่าง: เปลี่ยนสี Blue Team เป็นสีเขียว */
.blue-team .pick-slot {
    background: linear-gradient(135deg, rgba(0, 255, 100, 0.2), rgba(0, 150, 50, 0.3));
    border: 3px solid rgba(0, 255, 100, 0.6);
    box-shadow: 0 0 20px rgba(0, 255, 100, 0.3);
}
```

### เปลี่ยน Font
เพิ่มใน CSS:
```css
body {
    font-family: 'Kanit', 'Arial', sans-serif;
}
```

จากนั้น import font ในไฟล์ HTML:
```html
<link href="https://fonts.googleapis.com/css2?family=Kanit:wght@400;600;700&display=swap" rel="stylesheet">
```

### เปลี่ยน Animation
แก้ไข Keyframes ใน CSS:
```css
@keyframes slideIn {
    from {
        transform: translateY(-50px) scale(0.8);
        opacity: 0;
    }
    to {
        transform: translateY(0) scale(1);
        opacity: 1;
    }
}
```

## การ Debug

### ดู Console Log
1. กด F12 ใน Browser
2. เปิดแท็บ Console
3. ดู Error Messages

### ตรวจสอบ Network
1. กด F12 → แท็บ Network
2. ดู WebSocket Connection (WS)
3. ตรวจสอบ Request/Response

### ตรวจสอบ Server
ใน Terminal ที่รัน Server จะมี log:
- Client connected: [socket.id]
- Client disconnected: [socket.id]

## Performance Optimization

### 1. ลด Animation
แก้ไข CSS ลดความเร็ว Animation:
```css
.pick-slot {
    transition: all 0.1s ease; /* จาก 0.3s เป็น 0.1s */
}
```

### 2. ใช้ Hardware Acceleration
เพิ่มใน CSS:
```css
.pick-slot {
    transform: translateZ(0);
    will-change: transform;
}
```

### 3. Optimize Images
- ใช้รูป PNG ที่บีบอัดแล้ว
- ขนาดไฟล์ไม่ควรเกิน 100KB ต่อรูป
- ขนาด 240x390 pixels

## Troubleshooting ขั้นสูง

### Port Already in Use
```bash
# Windows
netstat -ano | findstr :3000
taskkill /PID [PID] /F

# Mac/Linux
lsof -ti:3000 | xargs kill
```

### Memory Leak
รี Start server ทุก 2-3 ชั่วโมง หรือใช้ Process Manager:
```bash
npm install -g pm2
pm2 start server.js
pm2 restart server
```

### Socket Connection Failed
1. ตรวจสอบ CORS settings ใน server.js
2. ตรวจสอบ Firewall
3. ลองใช้ HTTP แทน HTTPS

---

**เพิ่มเติม:** ถ้ามีคำถามเพิ่มเติม สามารถดูได้ที่ README.md และ QUICKSTART.md
