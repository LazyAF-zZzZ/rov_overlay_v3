namespace RovOverlay.Desktop.Services;

// Ending a game from the score, the calmer Control Panel, and Home showing what is live.
public sealed partial class Loc
{
    private static readonly Dictionary<string, string> FlowEn = new()
    {
        ["Flow.AddPoint"] = "+1",
        ["Flow.AddPointName"] = "+1 {0}",
        ["Flow.AddPointTip"] = "This side won the game: gives it the point, records the winner, keeps the draft with that game and puts the next game on the board. A wrong press is fixed with the round arrow and the score box.",
        ["Flow.NextGame"] = "{0} won the game · game {1} is on the board",
        ["Flow.SeriesOver"] = "SERIES OVER",
        ["Flow.SeriesWon"] = "{0} win the series {1}–{2}",
        ["Flow.Next"] = "Next: {0} vs {1}",
        ["Flow.PutOnAir"] = "Put on air",
        ["Flow.NoNext"] = "No other match in this tournament is ready yet",
        ["Flow.Err.GameDecided"] = "This game already has a winner. Use the round arrow to move on.",
        ["Flow.Err.SeriesOver"] = "This series is already over.",
        ["Flow.Err.RoundLimit"] = "There are no more rounds.",
        ["Flow.Err.NotRecorded"] = "The point could not be recorded for this match, so nothing was changed. Check the score boxes.",
        ["Flow.SwapSides"] = "Swap sides each game",
        ["Flow.SwapSidesTip"] = "When the round changes, the two teams swap sides: team A is blue in games 1, 3 and 5, and red in games 2 and 4. Each team keeps its own score and draft. Switch this off if the teams choose their own side.",
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
        ["Flow.AddPoint"] = "+1",
        ["Flow.AddPointName"] = "+1 {0}",
        ["Flow.AddPointTip"] = "ฝั่งนี้ชนะเกมนี้: ให้แต้ม บันทึกผู้ชนะ เก็บดราฟต์ไว้กับเกมนี้ แล้วขึ้นเกมถัดไปบนกระดาน กดผิดแก้ได้ด้วยลูกศรรอบกับช่องคะแนน",
        ["Flow.NextGame"] = "{0} ชนะเกมนี้ · เกมที่ {1} ขึ้นกระดานแล้ว",
        ["Flow.SeriesOver"] = "ซีรีส์จบแล้ว",
        ["Flow.SeriesWon"] = "{0} ชนะซีรีส์ {1}–{2}",
        ["Flow.Next"] = "คู่ถัดไป: {0} พบ {1}",
        ["Flow.PutOnAir"] = "ขึ้นจอ",
        ["Flow.NoNext"] = "ยังไม่มีคู่อื่นในทัวร์นาเมนต์นี้ที่พร้อมเล่น",
        ["Flow.Err.GameDecided"] = "เกมนี้มีผู้ชนะแล้ว ใช้ลูกศรรอบเพื่อไปเกมถัดไป",
        ["Flow.Err.SeriesOver"] = "ซีรีส์นี้จบไปแล้ว",
        ["Flow.Err.RoundLimit"] = "ไม่มีรอบถัดไปแล้ว",
        ["Flow.Err.NotRecorded"] = "บันทึกแต้มลงแมตช์นี้ไม่สำเร็จ จึงไม่มีอะไรเปลี่ยน ลองดูช่องคะแนนอีกครั้ง",
        ["Flow.SwapSides"] = "สลับฝั่งทุกเกม",
        ["Flow.SwapSidesTip"] = "เมื่อเปลี่ยนรอบ สองทีมจะสลับฝั่งกันเอง ทีม A อยู่ฝั่งน้ำเงินในเกมที่ 1, 3, 5 และฝั่งแดงในเกมที่ 2, 4 แต้มและดราฟต์ของแต่ละทีมตามทีมไปด้วย ปิดไว้ถ้าให้ทีมเลือกฝั่งเอง",
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
