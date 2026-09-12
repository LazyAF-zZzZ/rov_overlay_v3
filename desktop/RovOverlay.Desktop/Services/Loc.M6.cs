namespace RovOverlay.Desktop.Services;

// Importing a v2 installation.
public sealed partial class Loc
{
    private static readonly Dictionary<string, string> M6En = new()
    {
        ["Import.Section"] = "BRING YOUR V2 DATA ACROSS",
        ["Import.Hint"] = "Teams, logos, tournaments, brackets and every recorded draft from ROV Overlay Tool v2. Your v2 folder is only read, never changed, and anything already here is kept.",
        ["Import.PathHint"] = "Folder where v2 keeps its data",
        ["Import.Browse"] = "Browse…",
        ["Import.Import"] = "Import",
        ["Import.PickFolder"] = "Pick the ROV Overlay Tool v2 folder",
        ["Import.Found"] = "Found {0} place(s) that look like a v2 installation. Check the folder above, then press Import.",
        ["Import.NoneFound"] = "No v2 installation found automatically. Use Browse to point at the folder v2 keeps its data in.",
        ["Import.Title"] = "Import from v2?",
        ["Import.FoundIn"] = "Found in {0}",
        ["Import.Holds"] = "It holds {0} teams, {1} tournaments, {2} matches, {3} recorded drafts and {4} logos.",
        ["Import.MergeRule"] = "Anything already on this machine is kept. Records that are already here are skipped, not replaced, so importing twice changes nothing the second time.",
        ["Import.Untouched"] = "Your v2 installation is only read. Nothing in its folder is written to or moved.",
        ["Import.Done"] = "Imported {0} teams, {1} tournaments and {2} games",
        ["Import.LastRun"] = "Last import: {0} teams added, {1} already here."
    };

    private static readonly Dictionary<string, string> M6Th = new()
    {
        ["Import.Section"] = "ย้ายข้อมูลจากเวอร์ชัน 2",
        ["Import.Hint"] = "ทีม โลโก้ ทัวร์นาเมนต์ สาย และดราฟต์ทุกชุดจาก ROV Overlay Tool เวอร์ชัน 2 โฟลเดอร์ของเวอร์ชัน 2 ถูกอ่านอย่างเดียว ไม่ถูกแก้ และของที่มีอยู่ในเครื่องนี้แล้วจะไม่ถูกแตะ",
        ["Import.PathHint"] = "โฟลเดอร์ที่เวอร์ชัน 2 เก็บข้อมูลไว้",
        ["Import.Browse"] = "เลือกโฟลเดอร์…",
        ["Import.Import"] = "นำเข้า",
        ["Import.PickFolder"] = "เลือกโฟลเดอร์ของ ROV Overlay Tool เวอร์ชัน 2",
        ["Import.Found"] = "พบที่ที่น่าจะเป็นเวอร์ชัน 2 อยู่ {0} แห่ง ตรวจโฟลเดอร์ด้านบนแล้วกดนำเข้า",
        ["Import.NoneFound"] = "หาเวอร์ชัน 2 อัตโนมัติไม่เจอ กดเลือกโฟลเดอร์เพื่อชี้ไปที่โฟลเดอร์ที่เวอร์ชัน 2 เก็บข้อมูลไว้",
        ["Import.Title"] = "นำเข้าจากเวอร์ชัน 2 ใช่ไหม",
        ["Import.FoundIn"] = "พบที่ {0}",
        ["Import.Holds"] = "ในนั้นมี {0} ทีม {1} ทัวร์นาเมนต์ {2} คู่แข่ง ดราฟต์ที่บันทึกไว้ {3} ชุด และโลโก้ {4} รูป",
        ["Import.MergeRule"] = "ของที่มีอยู่ในเครื่องนี้แล้วจะไม่ถูกแตะ รายการที่ซ้ำจะถูกข้าม ไม่ใช่เขียนทับ นำเข้าซ้ำอีกครั้งจึงไม่เปลี่ยนอะไร",
        ["Import.Untouched"] = "โปรแกรมเวอร์ชัน 2 ถูกอ่านอย่างเดียว ไม่มีการเขียนหรือย้ายไฟล์ในโฟลเดอร์นั้น",
        ["Import.Done"] = "นำเข้าแล้ว {0} ทีม {1} ทัวร์นาเมนต์ และ {2} เกม",
        ["Import.LastRun"] = "นำเข้าล่าสุด: เพิ่ม {0} ทีม มีอยู่แล้ว {1} ทีม"
    };
}
