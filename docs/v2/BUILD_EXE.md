# สร้างไฟล์ .exe

## สิ่งที่ต้องมี

- Windows
- Node.js LTS
- Internet สำหรับติดตั้ง dependencies ครั้งแรก

## ติดตั้ง dependencies

```powershell
npm.cmd install
```

## สร้าง Portable.exe

```powershell
npm.cmd run portable
```

ไฟล์ที่ได้:

```text
dist/ROV-Overlay-Tool-Portable.exe
```

## สร้าง Setup.exe

```powershell
npm.cmd run dist
```

ไฟล์ทั้งหมดจะอยู่ใน:

```text
dist/
```

## หมายเหตุ

ถ้า Windows เตือน SmartScreen เป็นเรื่องปกติสำหรับแอพที่ยังไม่ได้เซ็น certificate
