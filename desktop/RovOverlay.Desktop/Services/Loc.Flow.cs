namespace RovOverlay.Desktop.Services;

// Ending a game in one press, the calmer Control Panel, and Home showing what is live.
public sealed partial class Loc
{
    private static readonly Dictionary<string, string> FlowEn = new()
    {
        ["Flow.GameOver"] = "GAME OVER",
        ["Flow.Won"] = "{0} won",
        ["Flow.WonTip"] = "Gives this side the point, records who won the game, keeps the draft with it and moves the board to the next game",
        ["Flow.FinishTitle"] = "Game over",
        ["Flow.FinishQ"] = "{0} won game {1}?",
        ["Flow.FinishBody"] = "The point goes to {0}, this draft is saved with the game, and the board moves on to the next game. If it was the wrong side, the round arrow brings this game back.",
        ["Flow.NextGame"] = "Game {0} is on the board",
        ["Flow.SeriesOver"] = "SERIES OVER",
        ["Flow.SeriesDone"] = "This series is finished. Its last game stays on the board.",
        ["Flow.Next"] = "Next: {0} vs {1}",
        ["Flow.PutOnAir"] = "Put on air",
        ["Flow.NoNext"] = "No other match in this tournament is ready yet",
        ["Flow.Err.GameDecided"] = "This game already has a winner. Use the round arrow to move on.",
        ["Flow.Err.SeriesOver"] = "This series is already over.",
        ["Flow.Err.RoundLimit"] = "There are no more rounds.",
        ["Flow.Err.NotRecorded"] = "The point could not be recorded for this match, so nothing was changed. Check the score boxes.",
        ["Flow.TeamSetup"] = "Team setup",
        ["Flow.TeamSetupTip"] = "Names, logos, nicknames and lanes. Folds away by itself when the draft starts, so the picks have the room.",

        ["Home.OnAirNow"] = "ON AIR NOW",
        ["Home.QuickMatch"] = "Quick match, not part of a tournament",
        ["Home.GoToControl"] = "Go to Control",
        ["Home.OpenBracket"] = "Open bracket",
        ["Home.ReadyToPlay"] = "READY TO PLAY",
        ["Home.NoReady"] = "Nothing is waiting. Matches show up here once a tournament has its bracket drawn.",
        ["Home.ReadyTip"] = "Put on air and go to the Control Panel"
    };

    private static readonly Dictionary<string, string> FlowTh = new()
    {
        ["Flow.GameOver"] = "จบเกม",
        ["Flow.Won"] = "{0} ชนะ",
        ["Flow.WonTip"] = "ให้แต้มฝั่งนี้ บันทึกว่าใครชนะเกมนี้ เก็บดราฟต์ไว้กับเกม แล้วเลื่อนกระดานไปเกมถัดไป",
        ["Flow.FinishTitle"] = "จบเกม",
        ["Flow.FinishQ"] = "{0} ชนะเกมที่ {1} ใช่ไหม",
        ["Flow.FinishBody"] = "แต้มจะเป็นของ {0} ดราฟต์นี้ถูกเก็บไว้กับเกมนี้ แล้วกระดานจะเลื่อนไปเกมถัดไป ถ้ากดผิดฝั่ง ลูกศรรอบจะพาเกมนี้กลับมา",
        ["Flow.NextGame"] = "เกมที่ {0} ขึ้นกระดานแล้ว",
        ["Flow.SeriesOver"] = "ซีรีส์จบแล้ว",
        ["Flow.SeriesDone"] = "ซีรีส์นี้จบแล้ว เกมสุดท้ายยังค้างอยู่บนกระดาน",
        ["Flow.Next"] = "คู่ถัดไป: {0} พบ {1}",
        ["Flow.PutOnAir"] = "ขึ้นจอ",
        ["Flow.NoNext"] = "ยังไม่มีคู่อื่นในทัวร์นาเมนต์นี้ที่พร้อมเล่น",
        ["Flow.Err.GameDecided"] = "เกมนี้มีผู้ชนะแล้ว ใช้ลูกศรรอบเพื่อไปเกมถัดไป",
        ["Flow.Err.SeriesOver"] = "ซีรีส์นี้จบไปแล้ว",
        ["Flow.Err.RoundLimit"] = "ไม่มีรอบถัดไปแล้ว",
        ["Flow.Err.NotRecorded"] = "บันทึกแต้มลงแมตช์นี้ไม่สำเร็จ จึงไม่มีอะไรเปลี่ยน ลองดูช่องคะแนนอีกครั้ง",
        ["Flow.TeamSetup"] = "ตั้งค่าทีม",
        ["Flow.TeamSetupTip"] = "ชื่อทีม โลโก้ ชื่อผู้เล่น และเลน พับเก็บเองเมื่อเริ่มดราฟต์ ช่องพิคจะได้มีที่",

        ["Home.OnAirNow"] = "กำลังออกอากาศ",
        ["Home.QuickMatch"] = "แมตช์เดี่ยว ไม่ได้อยู่ในทัวร์นาเมนต์",
        ["Home.GoToControl"] = "ไปหน้าคุมงาน",
        ["Home.OpenBracket"] = "เปิดสายการแข่ง",
        ["Home.ReadyToPlay"] = "พร้อมเล่น",
        ["Home.NoReady"] = "ยังไม่มีคู่ที่รออยู่ คู่แข่งจะมาแสดงตรงนี้เมื่อทัวร์นาเมนต์จับสายแล้ว",
        ["Home.ReadyTip"] = "ขึ้นจอแล้วไปหน้าคุมงาน"
    };
}
