using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace RovOverlay.Desktop.Services;

// Thai and English, Thai by default, as in v2.
//
// XAML uses {svc:T Key}; code uses Loc.T("Key") or Loc.F("Key", args). Switching
// language raises "Item[]", so every XAML binding refreshes on its own; code that
// builds text itself listens to Changed.
//
// Keys are short identifiers rather than v2's English source strings, but the Thai
// wording is taken from v2's i18n.js wherever the same text existed there, so
// operators see the words they already know.
public sealed partial class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    // The screen strings live in Loc.Screens.cs. Static field initialisers in every part
    // of the class run before this body, so both halves exist by the time they merge.
    static Loc()
    {
        foreach (var (key, text) in ScreensEn) En[key] = text;
        foreach (var (key, text) in ScreensTh) Th[key] = text;
        foreach (var (key, text) in ControlEn) En[key] = text;
        foreach (var (key, text) in ControlTh) Th[key] = text;
        foreach (var (key, text) in M4En) En[key] = text;
        foreach (var (key, text) in M4Th) Th[key] = text;
        foreach (var (key, text) in M5En) En[key] = text;
        foreach (var (key, text) in M5Th) Th[key] = text;
        foreach (var (key, text) in M6En) En[key] = text;
        foreach (var (key, text) in M6Th) Th[key] = text;
        foreach (var (key, text) in M7En) En[key] = text;
        foreach (var (key, text) in M7Th) Th[key] = text;
        foreach (var (key, text) in FlowEn) En[key] = text;
        foreach (var (key, text) in FlowTh) Th[key] = text;
        foreach (var (key, text) in TeamStatsEn) En[key] = text;
        foreach (var (key, text) in TeamStatsTh) Th[key] = text;
    }

    private string _language = "th";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;

    public string Language
    {
        get => _language;
        set
        {
            if (value is not ("th" or "en") || value == _language) return;
            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            Changed?.Invoke();
        }
    }

    public CultureInfo Culture => _language == "th" ? new CultureInfo("th-TH") : new CultureInfo("en-GB");

    public string this[string key] =>
        _language == "th" && Th.TryGetValue(key, out var thai) ? thai
        : En.TryGetValue(key, out var english) ? english
        : key;

    public static string T(string key) => Instance[key];
    public static string F(string key, params object?[] args) => string.Format(Instance[key], args);

    private static readonly Dictionary<string, string> En = new()
    {
        ["Nav.Home"] = "Home",
        ["Nav.Control"] = "Control",
        ["Nav.Teams"] = "Teams",
        ["Nav.Analytics"] = "Analytics",
        ["Nav.Design"] = "Design",
        ["Nav.Hotkeys"] = "Hotkeys",
        ["Nav.Guide"] = "Guide",
        ["Nav.Settings"] = "Settings",

        ["Conn.Starting"] = "Starting server…",
        ["Conn.Connecting"] = "Connecting…",
        ["Conn.Live"] = "Connected",
        ["Conn.Offline"] = "Server offline",

        ["Banner.On"] = "ON AIR",
        ["Banner.Off"] = "HIDDEN",
        ["Banner.OnTip"] = "Banner on air",
        ["Banner.OffTip"] = "Banner hidden",
        ["Live.Quick"] = "Quick match",
        ["Live.OpenBracket"] = "Open its bracket",
        ["Live.Match"] = "{0} · {1} · Game {2}",
        ["Live.MatchNoGame"] = "{0} · {1}",

        ["Obs.Button"] = "OBS sources",
        ["Obs.Tip"] = "Browser-source URLs for OBS",
        ["Obs.Title"] = "OBS browser sources",
        ["Obs.Hint"] = "Add each URL in OBS as a Browser source at the size shown.",
        ["Obs.Sfx"] = "SOUND",
        ["Obs.SfxHint"] = "Only the sources marked SOUND play the draft sounds, and only if \"Control audio via OBS\" is ticked in that source's properties.",
        ["Obs.CacheHint"] = "After the app updates, right-click each source and choose \"Refresh cache of current page\". OBS keeps the overlay it remembers until you do.",
        ["Obs.SameSize"] = "matches overlay size",
        ["Obs.Copied"] = "URL copied",
        ["Lang.Tip"] = "Switch language",

        ["Common.Copy"] = "Copy",
        ["Common.CopyUrl"] = "Copy URL",
        ["Common.OpenBrowser"] = "Open in browser",
        ["Common.Create"] = "Create",
        ["Common.Cancel"] = "Cancel",
        ["Common.Open"] = "Open",
        ["Common.Refresh"] = "Refresh",
        ["Common.Retry"] = "Retry",
        ["Common.CopyFailed"] = "Could not copy. Select the text and copy it by hand.",

        ["Home.Title"] = "Tournaments",
        ["Home.New"] = "New tournament",
        ["Home.Name"] = "Tournament name",
        ["Home.Format"] = "Format",
        ["Home.BestOf"] = "Series length",
        ["Home.Note"] = "Note (optional)",
        ["Home.NotePlaceholder"] = "Venue, dates, anything you want to remember",
        ["Home.FormatHint"] = "{0}–{1} teams",
        ["Home.Search"] = "Search tournaments",
        ["Home.Empty"] = "No tournaments yet. Create one to get started.",
        ["Home.NoMatch"] = "No tournament matches the search.",
        ["Home.Created"] = "Created {0}",
        ["Home.OpenTip"] = "Opens in the browser until this screen moves into the app",

        ["Col.Name"] = "NAME",
        ["Col.Format"] = "FORMAT",
        ["Col.Series"] = "SERIES",
        ["Col.Teams"] = "TEAMS",
        ["Col.Status"] = "STATUS",
        ["Col.Updated"] = "UPDATED",

        ["Filter.all"] = "All",
        ["Filter.active"] = "Active",
        ["Filter.finished"] = "Finished",
        ["TStatus.active"] = "Active",
        ["TStatus.finished"] = "Finished",

        ["Format.single_elim"] = "Single elimination",
        ["Format.double_elim"] = "Double elimination",
        ["Format.round_robin"] = "Round robin",
        ["Format.group_stage"] = "Group stage",

        ["Legacy.Body"] = "This screen hasn't moved into the new app yet. The web version still has every feature and runs on the same server, so anything you change there shows up here straight away.",

        ["Settings.Language"] = "LANGUAGE",
        ["Settings.Data"] = "YOUR DATA",
        ["Settings.DataHint"] = "Teams, tournaments and every recorded draft. Kept apart from v2, whose data this app never touches.",
        ["Settings.DataFolder"] = "Data folder",
        ["Settings.MediaFolder"] = "Uploaded images",
        ["Settings.OpenFolder"] = "Open folder",
        ["Settings.Server"] = "SERVER",
        ["Settings.Address"] = "Address",
        ["Settings.Mode"] = "Mode",
        ["Settings.ModeOwned"] = "Started by this app",
        ["Settings.ModeAttached"] = "Was already running; this app attached to it",
        ["Settings.Node"] = "Node.js",
        ["Settings.Backend"] = "Backend folder",
        ["Settings.Log"] = "SERVER LOG",
        ["Settings.About"] = "ABOUT",
        ["Settings.Version"] = "Version",

        ["Startup.Title"] = "Starting the server…",
        ["Error.Title"] = "The server isn't running",
        ["Error.PortBusy"] = "Port {0} is already used by another program, most likely ROV Overlay Tool v2. Close it, then press Retry.",
        ["Error.NoBackend"] = "The backend folder is missing next to the app.",
        ["Error.NotBuilt"] = "The backend hasn't been built yet. Run  npm run build  in:\n{0}",
        ["Error.NoNode"] = "Node.js wasn't found. Install Node.js 22.5 or newer, or put node.exe in backend\\runtime.",
        ["Error.Crashed"] = "The server stopped while starting. The last lines it wrote:",
        ["Error.Timeout"] = "The server didn't answer within 20 seconds.",
        ["Error.Exited"] = "The server stopped. The overlays in OBS are offline until it runs again.",
        ["App.AlreadyOpen"] = "ROV Overlay Tool is already open."
    };

    private static readonly Dictionary<string, string> Th = new()
    {
        ["Nav.Home"] = "หน้าแรก",
        ["Nav.Control"] = "คุมงาน",
        ["Nav.Teams"] = "ทีม",
        ["Nav.Analytics"] = "สถิติ",
        ["Nav.Design"] = "ดีไซน์",
        ["Nav.Hotkeys"] = "คีย์ลัด",
        ["Nav.Guide"] = "คู่มือ",
        ["Nav.Settings"] = "ตั้งค่า",

        ["Conn.Starting"] = "กำลังเปิดเซิร์ฟเวอร์…",
        ["Conn.Connecting"] = "กำลังเชื่อมต่อ",
        ["Conn.Live"] = "เชื่อมต่อแล้ว",
        ["Conn.Offline"] = "เซิร์ฟเวอร์ไม่ทำงาน",

        ["Banner.On"] = "ออนแอร์",
        ["Banner.Off"] = "ซ่อนอยู่",
        ["Banner.OnTip"] = "แบนเนอร์ออกอากาศอยู่",
        ["Banner.OffTip"] = "ซ่อนแบนเนอร์อยู่",
        ["Live.Quick"] = "แมตช์เดี่ยว",
        ["Live.OpenBracket"] = "ไปที่สายการแข่ง",
        ["Live.Match"] = "{0} · {1} · เกม {2}",
        ["Live.MatchNoGame"] = "{0} · {1}",

        ["Obs.Button"] = "ลิงก์ OBS",
        ["Obs.Tip"] = "ลิงก์ Browser source สำหรับ OBS",
        ["Obs.Title"] = "Browser source สำหรับ OBS",
        ["Obs.Hint"] = "เพิ่มแต่ละลิงก์ใน OBS เป็น Browser source ตามขนาดที่บอกไว้",
        ["Obs.Sfx"] = "มีเสียง",
        ["Obs.SfxHint"] = "เฉพาะ source ที่ติดป้ายว่ามีเสียงเท่านั้นที่จะมีเสียงดราฟต์ และต้องติ๊ก \"Control audio via OBS\" ในหน้าตั้งค่าของ source นั้นด้วย",
        ["Obs.CacheHint"] = "หลังแอปอัปเดต ให้คลิกขวาที่ source แล้วเลือก \"Refresh cache of current page\" ไม่อย่างนั้น OBS จะยังใช้ overlay ตัวเก่าที่จำไว้",
        ["Obs.SameSize"] = "ขนาดเดียวกับ overlay",
        ["Obs.Copied"] = "คัดลอก URL แล้ว",
        ["Lang.Tip"] = "เปลี่ยนภาษา",

        ["Common.Copy"] = "คัดลอก",
        ["Common.CopyUrl"] = "คัดลอก URL",
        ["Common.OpenBrowser"] = "เปิดในเบราว์เซอร์",
        ["Common.Create"] = "สร้าง",
        ["Common.Cancel"] = "ยกเลิก",
        ["Common.Open"] = "เปิด",
        ["Common.Refresh"] = "รีเฟรช",
        ["Common.Retry"] = "ลองใหม่",
        ["Common.CopyFailed"] = "คัดลอกไม่สำเร็จ ลากเลือกข้อความแล้วคัดลอกเองได้",

        ["Home.Title"] = "ทัวร์นาเมนต์",
        ["Home.New"] = "สร้างทัวร์นาเมนต์",
        ["Home.Name"] = "ชื่อทัวร์นาเมนต์",
        ["Home.Format"] = "รูปแบบการแข่ง",
        ["Home.BestOf"] = "จำนวนเกมต่อคู่",
        ["Home.Note"] = "โน้ต (จะใส่หรือไม่ก็ได้)",
        ["Home.NotePlaceholder"] = "สถานที่ วันที่ หรืออะไรก็ได้ที่อยากจดไว้",
        ["Home.FormatHint"] = "{0}–{1} ทีม",
        ["Home.Search"] = "ค้นหาทัวร์นาเมนต์",
        ["Home.Empty"] = "ยังไม่มีทัวร์นาเมนต์ กดสร้างเพื่อเริ่มใช้งาน",
        ["Home.NoMatch"] = "ไม่มีทัวร์นาเมนต์ที่ตรงกับคำค้น",
        ["Home.Created"] = "สร้าง {0} แล้ว",
        ["Home.OpenTip"] = "เปิดในเบราว์เซอร์ไปก่อน จนกว่าหน้านี้จะย้ายมาอยู่ในแอป",

        ["Col.Name"] = "ชื่อ",
        ["Col.Format"] = "รูปแบบ",
        ["Col.Series"] = "ต่อคู่",
        ["Col.Teams"] = "ทีม",
        ["Col.Status"] = "สถานะ",
        ["Col.Updated"] = "แก้ไขล่าสุด",

        ["Filter.all"] = "ทั้งหมด",
        ["Filter.active"] = "กำลังแข่ง",
        ["Filter.finished"] = "จบแล้ว",
        ["TStatus.active"] = "กำลังแข่ง",
        ["TStatus.finished"] = "จบแล้ว",

        ["Format.single_elim"] = "แพ้คัดออก",
        ["Format.double_elim"] = "แพ้คัดออกสองสาย",
        ["Format.round_robin"] = "พบกันหมด",
        ["Format.group_stage"] = "แบ่งกลุ่ม",

        ["Legacy.Body"] = "หน้านี้ยังไม่ได้ย้ายมาอยู่ในแอปใหม่ ใช้เวอร์ชันเว็บไปก่อนได้ ฟังก์ชันครบเหมือนเดิมและใช้เซิร์ฟเวอร์ตัวเดียวกัน แก้อะไรที่นั่นก็เห็นที่นี่ทันที",

        ["Settings.Language"] = "ภาษา",
        ["Settings.Data"] = "ข้อมูลของคุณ",
        ["Settings.DataHint"] = "ทีม ทัวร์นาเมนต์ และดราฟต์ทั้งหมด เก็บแยกจากเวอร์ชัน 2 แอปนี้ไม่ไปแตะข้อมูลของเวอร์ชันเก่า",
        ["Settings.DataFolder"] = "โฟลเดอร์ข้อมูล",
        ["Settings.MediaFolder"] = "รูปที่อัปโหลด",
        ["Settings.OpenFolder"] = "เปิดโฟลเดอร์",
        ["Settings.Server"] = "เซิร์ฟเวอร์",
        ["Settings.Address"] = "ที่อยู่",
        ["Settings.Mode"] = "สถานะ",
        ["Settings.ModeOwned"] = "แอปนี้เปิดเอง",
        ["Settings.ModeAttached"] = "เปิดอยู่ก่อนแล้ว แอปนี้เชื่อมต่อเข้าไป",
        ["Settings.Node"] = "Node.js",
        ["Settings.Backend"] = "โฟลเดอร์ backend",
        ["Settings.Log"] = "บันทึกของเซิร์ฟเวอร์",
        ["Settings.About"] = "เกี่ยวกับ",
        ["Settings.Version"] = "เวอร์ชัน",

        ["Startup.Title"] = "กำลังเปิดเซิร์ฟเวอร์…",
        ["Error.Title"] = "เซิร์ฟเวอร์ไม่ทำงาน",
        ["Error.PortBusy"] = "พอร์ต {0} ถูกโปรแกรมอื่นใช้อยู่ น่าจะเป็น ROV Overlay Tool เวอร์ชัน 2 ปิดโปรแกรมนั้นก่อนแล้วกดลองใหม่",
        ["Error.NoBackend"] = "ไม่พบโฟลเดอร์ backend ข้างตัวแอป",
        ["Error.NotBuilt"] = "ยังไม่ได้ build backend รัน  npm run build  ในโฟลเดอร์:\n{0}",
        ["Error.NoNode"] = "ไม่พบ Node.js ติดตั้ง Node.js 22.5 ขึ้นไป หรือวาง node.exe ไว้ใน backend\\runtime",
        ["Error.Crashed"] = "เซิร์ฟเวอร์หยุดทำงานระหว่างเปิด บรรทัดสุดท้ายที่มันเขียนไว้:",
        ["Error.Timeout"] = "เซิร์ฟเวอร์ไม่ตอบภายใน 20 วินาที",
        ["Error.Exited"] = "เซิร์ฟเวอร์หยุดทำงาน overlay ใน OBS จะดับจนกว่าจะเปิดใหม่",
        ["App.AlreadyOpen"] = "ROV Overlay Tool เปิดอยู่แล้ว"
    };
}

// {svc:T Key} in XAML: a one-way binding to Loc.Instance[Key], so it follows language switches.
[MarkupExtensionReturnType(typeof(object))]
public sealed class TExtension : MarkupExtension
{
    public TExtension() { }
    public TExtension(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
