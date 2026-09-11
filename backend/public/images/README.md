# Hero Icons & Team Logos

## โฟลเดอร์นี้เก็บอะไรบ้าง

### 1. heroes/ 
รูปฮีโร่แบบเต็มตัว (สำหรับช่อง Pick)
- ขนาด: 240 x 390 pixels
- ไฟล์: PNG
- ชื่อ: ตัวเล็กทั้งหมด เช่น airi.png, nakroth.png

### 2. heroes-icons/
ไอคอนฮีโร่ (สำหรับช่อง Ban)
- ขนาด: 80 x 80 pixels (แนะนำ)
- ไฟล์: PNG
- ชื่อ: ตัวเล็กทั้งหมด เช่น airi.png, nakroth.png
- หมายเหตุ: ชื่อไฟล์ต้องตรงกับในโฟลเดอร์ heroes/

### 3. team-logos/
โลโก้ทีม
- ขนาด: 200 x 200 pixels (แนะนำ)
- ไฟล์: PNG (รองรับพื้นหลังโปร่งใส)
- ชื่อ: ตามชื่อทีม เช่น fw.png, ea.png, rrq.png

## วิธีเตรียมไฟล์

### Heroes Icons (สำหรับ Ban)
```
public/images/heroes-icons/
├── airi.png
├── nakroth.png
├── tel'annas.png
├── ...
└── zuka.png
```

### Team Logos
```
public/images/team-logos/
├── fw.png
├── ea.png
├── rrq.png
├── evos.png
└── ...
```

## Tips
- ใช้ไอคอนที่มีพื้นหลังโปร่งใส (transparent)
- ควรเป็นรูปหน้าหรือสัญลักษณ์ฮีโร่
- โลโก้ทีมควรเป็นรูปทรงสี่เหลี่ยมจัตุรัสหรือวงกลม
