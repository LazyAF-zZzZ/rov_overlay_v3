namespace RovOverlay.Desktop.Services;

// Updates, notices from the maker, and the licence shown on the first run.
public sealed partial class Loc
{
    private static readonly Dictionary<string, string> M7En = new()
    {
        ["Update.Section"] = "UPDATES",
        ["Update.Hint"] = "New versions arrive on their own. The app never restarts itself while you are on air: an update is downloaded quietly and put in place the next time you close the app.",
        ["Update.Idle"] = "Checking every few hours.",
        ["Update.Checking"] = "Looking for a new version…",
        ["Update.Downloading"] = "Downloading {0}…",
        ["Update.UpToDate"] = "You are on {0}, the newest version.",
        ["Update.Ready"] = "Version {0} is ready. It will be put in place when you close the app.",
        ["Update.ReadyToast"] = "Version {0} is ready, and goes in when you close the app",
        ["Update.Failed"] = "Could not check for updates. The app is fine; it will try again later.",
        ["Update.NotInstalled"] = "This copy updates by hand. Installed copies update themselves.",
        ["Update.CheckNow"] = "Check now",
        ["Update.Channel"] = "Which versions",
        ["Update.Stable"] = "Finished versions",
        ["Update.Beta"] = "Test versions too",
        ["Update.ChannelHint"] = "Test versions arrive earlier and break more often. Leave this alone unless you are helping test.",
        ["Notices.Title"] = "Messages",
        ["Notices.Tip"] = "Messages about the app",
        ["Notices.Empty"] = "Nothing right now.",
        ["Notices.New"] = "{0} new messages about the app",
        ["Notices.Dismiss"] = "Got it",
        ["Notices.DismissAll"] = "Clear all",
        ["Notices.Open"] = "Read more",
        ["Licence.Title"] = "ROV Overlay Tool licence",
        ["Licence.Agree"] = "I agree",
        ["Licence.Exit"] = "Close the app",
        ["Licence.Free"] = "ROV Overlay Tool is free for tournaments, community streams, education and personal broadcasts.",
        ["Licence.May"] = "You may use it, copy it and pass it on, free of charge.",
        ["Licence.MayNot"] = "You may not sell it, resell it, rent it, put it in a paid package, charge for access to it, or present it as your own product.",
        ["Licence.Keep"] = "If you change it or pass on a changed copy, keep the licence and the credit, and keep it free for the people who use it.",
        ["Licence.Assets"] = "Game names, hero images, logos and other material belong to the people who own them.",
        ["Licence.Read"] = "Read the full licence"
    };

    private static readonly Dictionary<string, string> M7Th = new()
    {
        ["Update.Section"] = "อัปเดต",
        ["Update.Hint"] = "เวอร์ชันใหม่มาเองโดยไม่ต้องโหลดใหม่ทั้งก้อน แอปจะไม่รีสตาร์ตเองตอนคุณกำลังออกอากาศ แต่จะดาวน์โหลดไว้เงียบ ๆ แล้วติดตั้งตอนคุณปิดแอปครั้งถัดไป",
        ["Update.Idle"] = "ตรวจให้ทุกไม่กี่ชั่วโมง",
        ["Update.Checking"] = "กำลังดูว่ามีเวอร์ชันใหม่ไหม…",
        ["Update.Downloading"] = "กำลังดาวน์โหลด {0}…",
        ["Update.UpToDate"] = "คุณใช้ {0} ซึ่งเป็นเวอร์ชันล่าสุดอยู่แล้ว",
        ["Update.Ready"] = "เวอร์ชัน {0} พร้อมแล้ว จะติดตั้งให้ตอนคุณปิดแอป",
        ["Update.ReadyToast"] = "เวอร์ชัน {0} พร้อมแล้ว จะติดตั้งตอนคุณปิดแอป",
        ["Update.Failed"] = "ตรวจหาอัปเดตไม่สำเร็จ แอปยังใช้ได้ปกติ เดี๋ยวจะลองใหม่ให้เอง",
        ["Update.NotInstalled"] = "ไฟล์ชุดนี้ต้องอัปเดตเอง ถ้าติดตั้งแบบปกติจะอัปเดตให้อัตโนมัติ",
        ["Update.CheckNow"] = "ตรวจเดี๋ยวนี้",
        ["Update.Channel"] = "รับเวอร์ชันแบบไหน",
        ["Update.Stable"] = "เฉพาะเวอร์ชันที่เสร็จแล้ว",
        ["Update.Beta"] = "รับเวอร์ชันทดสอบด้วย",
        ["Update.ChannelHint"] = "เวอร์ชันทดสอบมาถึงเร็วกว่าแต่พังบ่อยกว่า ถ้าไม่ได้ช่วยทดสอบ ปล่อยไว้แบบเดิมดีที่สุด",
        ["Notices.Title"] = "ข้อความถึงคุณ",
        ["Notices.Tip"] = "ข้อความเกี่ยวกับแอป",
        ["Notices.Empty"] = "ตอนนี้ยังไม่มีอะไร",
        ["Notices.New"] = "มีข้อความเกี่ยวกับแอป {0} เรื่อง",
        ["Notices.Dismiss"] = "รับทราบ",
        ["Notices.DismissAll"] = "ล้างทั้งหมด",
        ["Notices.Open"] = "อ่านเพิ่ม",
        ["Licence.Title"] = "เงื่อนไขการใช้งาน ROV Overlay Tool",
        ["Licence.Agree"] = "ยอมรับ",
        ["Licence.Exit"] = "ปิดแอป",
        ["Licence.Free"] = "ROV Overlay Tool ใช้ฟรีสำหรับทัวร์นาเมนต์ การถ่ายทอดของชุมชน การเรียนการสอน และการใช้ส่วนตัว",
        ["Licence.May"] = "คุณใช้ คัดลอก และส่งต่อให้คนอื่นได้ฟรี",
        ["Licence.MayNot"] = "ห้ามนำไปขาย ขายต่อ ให้เช่า รวมในแพ็กเกจเสียเงิน เก็บเงินค่าเข้าใช้ หรืออ้างว่าเป็นผลงานของตัวเอง",
        ["Licence.Keep"] = "ถ้าแก้ไขหรือส่งต่อฉบับที่แก้แล้ว ต้องคงเงื่อนไขและเครดิตไว้ และต้องให้คนที่เอาไปใช้ได้ใช้ฟรีเหมือนกัน",
        ["Licence.Assets"] = "ชื่อเกม รูปฮีโร่ โลโก้ และสื่ออื่น ๆ เป็นของเจ้าของสิทธิ์นั้น ๆ",
        ["Licence.Read"] = "อ่านเงื่อนไขฉบับเต็ม"
    };
}
