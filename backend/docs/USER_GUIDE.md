# ROV Overlay Tool — User Guide / คู่มือการใช้งาน

This same guide is inside the app: open it from **GUIDE** in the top bar, or go to
`http://127.0.0.1:3000/guide` — that copy has a language switch and works offline.
คู่มือชุดเดียวกันนี้อยู่ในโปรแกรมด้วย กดที่ **GUIDE** บนแถบด้านบน หรือเปิด
`http://127.0.0.1:3000/guide` มีปุ่มสลับภาษาและใช้งานแบบออฟไลน์ได้

English first, Thai below it in every section.
แต่ละหัวข้อมีภาษาอังกฤษก่อน แล้วตามด้วยภาษาไทย

Everything runs on your own computer. No account, no internet needed.
ทุกอย่างทำงานในเครื่องของคุณเอง ไม่ต้องสมัครอะไร ไม่ต้องต่อเน็ต

---

## 1. Open the app / เปิดโปรแกรม

**EN**

1. Double-click **START_APP.cmd**
2. Wait for the window to open. Leave it open while you stream.

The pages you will use are on the top bar: **HOME · TEAMS · ANALYTICS · CONTROL · DESIGN · HOTKEYS**

**TH**

1. ดับเบิลคลิก **START_APP.cmd**
2. รอจนหน้าต่างเปิดขึ้นมา แล้วเปิดทิ้งไว้ตลอดเวลาที่ไลฟ์

หน้าที่ใช้บ่อยอยู่บนแถบด้านบน: **HOME · TEAMS · ANALYTICS · CONTROL · DESIGN · HOTKEYS**

---

## 2. Put the overlay into OBS / เอา overlay ใส่ OBS

**EN**

1. Go to **CONTROL**, scroll to **OBS browser sources**
2. Click **COPY URL** on *Overlay 1080p* (use *1440p* if your canvas is 2560×1440)
3. In OBS: **+ → Browser** → paste the URL → Width `1920`, Height `1080`
4. Tick **Control audio via OBS** so viewers can hear the sound effects
5. Leave **Shutdown source when not visible** unticked, so the overlay keeps running
6. Do the same for *Result* if you want the winner screen
7. *Standings*, *Team list*, *Stats board*, *Head to head* and *Team picks & bans* are
   copied the same way. Tick
   **Refresh browser when scene becomes active** on all of these, so they animate in and
   show current numbers every time you cut to them
8. *Previous picks & bans* shows the drafts of the earlier games of the series, picks and
   bans together on one board. Tick **Refresh browser when scene becomes active** on it as well

If the COPY button does nothing, click the URL text once — it selects itself — then press Ctrl+C.

**TH**

1. ไปหน้า **CONTROL** เลื่อนลงไปที่ **OBS browser sources**
2. กด **COPY URL** ของ *Overlay 1080p* (ถ้า canvas เป็น 2560×1440 ให้ใช้ *1440p*)
3. ใน OBS: **+ → Browser** → วาง URL → Width `1920`, Height `1080`
4. ติ๊ก **Control audio via OBS** เพื่อให้คนดูได้ยินเสียงเอฟเฟกต์
5. อย่าติ๊ก **Shutdown source when not visible** เพื่อให้ overlay ทำงานค้างไว้
6. ถ้าอยากได้หน้าประกาศผู้ชนะ ให้เพิ่ม *Result* ด้วยวิธีเดียวกัน
7. *Standings*, *Team list*, *Stats board*, *Head to head* และ *Team picks & bans*
   ก๊อปด้วยวิธีเดียวกัน พวกนี้ให้ติ๊ก
   **Refresh browser when scene becomes active** ด้วย จะได้เล่นอนิเมชันใหม่และได้ตัวเลขล่าสุดทุกครั้งที่ตัดเข้าซีน
8. *Previous picks & bans* คือดราฟต์ของเกมก่อนหน้าในซีรีส์ พิคกับแบนอยู่ในกระดานเดียวกัน
   อันนี้ให้ติ๊ก **Refresh browser when scene becomes active** ด้วยเหมือนกัน

ถ้ากดปุ่ม COPY แล้วไม่มีอะไรเกิดขึ้น ให้คลิกที่ตัว URL หนึ่งครั้ง มันจะเลือกให้เอง แล้วกด Ctrl+C

---

## 3. Run a quick match / คุมแมตช์แบบเร็ว

For a single match that is not part of a tournament.
ใช้กับแมตช์เดี่ยวที่ไม่ได้อยู่ในทัวร์นาเมนต์

**EN**

1. Go to **CONTROL**
2. Type the team names, or choose a saved team from **From registry**
3. Type the player names
4. During the draft, type hero names into **HERO PICK** and **BAN** — press Enter to put them on the board
5. **A pick takes one more step.** The hero appears on the overlay straight away, but
   silently: no sound, no animation, and **in grey**, so viewers can see it is not final.
   Press the gold ✓ next to it to lock it in. That is when the picture turns full colour,
   the pick sound and the animation play, and the draft moves on. Until then the teams can
   keep swapping heroes on screen and nothing is final. Bans are one step, as before
6. Use the **Draft Timer**: `START`, then `PREV` · `PAUSE`/`RESUME` (one button) · `NEXT`.
   `RESET` sits at the far end of that row, away from `START`
7. `SHOW` / `HIDE` at the top hides the banner without removing the source in OBS
8. **When a game ends, press `+1` beside the winning team's score.** One press gives that side
   the point, records who won the game, keeps the draft with that game and puts the next game
   on the board. When a team reaches the wins it needs (2 in a Bo3, 3 in a Bo5), the app
   declares the series for them. Pressed it by mistake? Press `−1` beside that team's score: it takes the point back,
   clears that game's winner, reopens the series if that point had ended it, and puts the game back
   on the board with its draft and sides. It only undoes the most recent game, and it is greyed out
   when the score is 0. By hand, `<` on ROUND brings the game back, and
   the score box takes the point off. Typing in the score box only corrects the number; it
   never moves the board

**Team setup folds away by itself when the draft starts.** Names, logos, nicknames and lanes
are done by then, so the picks get the room. The **Team setup** button above the two teams
opens it again at any time. Sound levels are folded under **SOUND EFFECTS** at the bottom.

In a tournament, when the game that decides the series is recorded, the panel shows
**SERIES OVER** with who won and the final score (for example *PSG Esports win the series 2–1*),
the next match that is ready to play and a **Put on air** button, so the
next match goes on air without a trip to the bracket. **HOME** shows the same thing from the
other side: what is on air now, and the matches ready to play, with the tournament on air first.

**The teams swap sides every game.** When the round changes, the two teams trade places: the
team drawn first is blue in games 1, 3 and 5 and red in games 2 and 4. Names, logos, players,
score and each team's draft all move with the team, and the recorded drafts and statistics
stay with the right team. Going back a round puts that game's sides back. If the teams choose
their own side, untick **Swap sides each game** beside ROUND.

**ROUND**, at the right of the draft panel, is which game of the series you are on. `+1`
moves it on for you; `>` does the same without touching the score: the draft on the board is
filed away as that round and the board starts clean.
`<` goes back and puts the earlier draft on screen again, so a mis-click costs nothing. Those
filed rounds are what the *Previous picks & bans* source shows. While a tournament
match is on air the number is the game number of that series, so it stays in step with the score
by itself.

Bottom buttons: **UNDO** (last change), **SWITCH TEAMS** (swap sides), **CLEAR PICKS & BANS**, **RESET MATCH** (start over).

**TH**

1. ไปหน้า **CONTROL**
2. พิมพ์ชื่อทีม หรือเลือกทีมที่บันทึกไว้จากช่อง **From registry**
3. พิมพ์ชื่อผู้เล่น
4. ตอนดราฟต์ พิมพ์ชื่อฮีโร่ในช่อง **HERO PICK** และ **BAN** แล้วกด Enter เพื่อวางลงกระดาน
5. **ช่องพิคมีอีกขั้นหนึ่ง** ฮีโร่จะขึ้นบน overlay ทันทีแต่ยังเงียบ ไม่มีเสียงและไม่มีอนิเมชัน
   และภาพจะเป็น **ขาวดำจาง ๆ** ให้คนดูรู้ว่ายังไม่ล็อก
   กดปุ่ม ✓ สีทองข้าง ๆ เพื่อยืนยัน ตอนนั้นแหละที่ภาพจะกลับมาเป็นสีเต็ม เสียงพิคกับอนิเมชันจะเล่น และดราฟต์ถึงจะไปเฟสถัดไป
   ก่อนกดยืนยัน ทีมยังสลับตัวไปมาบนจอได้เรื่อย ๆ โดยที่ยังไม่นับว่าล็อก ส่วนช่องแบนเหมือนเดิม กดครั้งเดียวจบ
6. ใช้ **Draft Timer**: `START` แล้วแถวล่างคือ `PREV` · `PAUSE`/`RESUME` (ปุ่มเดียวกัน) · `NEXT`
   ปุ่ม `RESET` อยู่ท้ายแถว ห่างจาก `START`
7. ปุ่ม `SHOW` / `HIDE` ด้านบนใช้ซ่อนแถบ overlay โดยไม่ต้องปิด source ใน OBS
8. **จบเกมแล้ว กด `+1` ข้างคะแนนของทีมที่ชนะ** กดครั้งเดียว โปรแกรมจะให้แต้มฝั่งนั้น
   บันทึกว่าใครชนะเกมนี้ เก็บดราฟต์ไว้กับเกมนี้ และขึ้นเกมถัดไปบนกระดานให้เลย
   เมื่อทีมไหนชนะครบตามที่ต้องการ (Bo3 = 2 เกม, Bo5 = 3 เกม) โปรแกรมจะประกาศว่าทีมนั้นชนะซีรีส์ให้เอง
   กดผิด? กด `−1` ข้างคะแนนของทีมนั้น โปรแกรมจะถอนแต้ม ล้างผู้ชนะของเกมนั้น เปิดซีรีส์กลับมาถ้าแต้มนั้นเคยปิดซีรีส์
   และเอาเกมนั้นกลับขึ้นกระดานพร้อมดราฟต์และฝั่งเดิม ถอนได้เฉพาะเกมล่าสุด และกดไม่ได้เมื่อคะแนนเป็น 0
   ถ้าจะแก้เอง กด `<` ที่ ROUND เพื่อพาเกมกลับมา แล้วแก้แต้มในช่องคะแนน
   การพิมพ์ในช่องคะแนนแค่แก้ตัวเลข ไม่เลื่อนกระดาน

**ส่วนตั้งค่าทีมจะพับเก็บเองเมื่อเริ่มดราฟต์** ชื่อทีม โลโก้ ชื่อผู้เล่น และเลน ตั้งเสร็จไปแล้วตอนนั้น ช่องพิคจึงได้ที่เต็ม
กดปุ่ม **ตั้งค่าทีม** เหนือสองทีมเพื่อเปิดกลับมาได้ทุกเมื่อ ส่วนระดับเสียงพับไว้ใต้ **เสียงเอฟเฟกต์** ด้านล่าง

ในทัวร์นาเมนต์ เมื่อบันทึกเกมที่ตัดสินซีรีส์ แผงจะขึ้นว่า **ซีรีส์จบแล้ว** พร้อมบอกว่าใครชนะและสกอร์ (เช่น *PSG Esports ชนะซีรีส์ 2–1*) คู่ถัดไปที่พร้อมเล่นและปุ่ม **ขึ้นจอ**
เอาคู่ถัดไปขึ้นจอได้เลยโดยไม่ต้องกลับไปที่สายการแข่ง **หน้าแรก** ก็แสดงเรื่องเดียวกันจากอีกฝั่ง
คือคู่ที่กำลังออกอากาศ และคู่ที่พร้อมเล่น โดยรายการที่กำลังออกอากาศขึ้นก่อน

**สองทีมสลับฝั่งกันทุกเกม** เมื่อเปลี่ยนรอบ ทีมจะสลับที่กันเอง ทีมที่ถูกจับสายเป็นทีมแรกอยู่ฝั่งน้ำเงินในเกมที่ 1, 3, 5
และฝั่งแดงในเกมที่ 2, 4 ชื่อ โลโก้ ผู้เล่น คะแนน และดราฟต์ของแต่ละทีมย้ายตามทีมไป ดราฟต์ที่บันทึกไว้และสถิติยังเป็นของทีมที่ถูกต้อง
ย้อนรอบกลับ ฝั่งของเกมนั้นก็กลับมาด้วย ถ้าให้ทีมเลือกฝั่งเอง ให้เอาติ๊ก **สลับฝั่งทุกเกม** ข้าง ROUND ออก

**ROUND** ที่มุมขวาของแผงดราฟต์ คือเกมที่เท่าไหร่ของซีรีส์ ปุ่ม `+1` เดินรอบให้เอง ส่วน `>` ทำแบบเดียวกันแต่ไม่แตะคะแนน ดราฟต์ที่อยู่บนกระดานจะถูกเก็บเป็นรอบนั้น
แล้วกระดานเริ่มใหม่ กด `<` เพื่อถอยกลับ ดราฟต์ของรอบก่อนจะกลับขึ้นมา กดผิดจึงไม่เสียอะไร รอบที่เก็บไว้เหล่านี้คือสิ่งที่ซอร์ส
*Previous picks & bans* เอาไปแสดง ถ้ากำลังออกอากาศแมตช์ของทัวร์นาเมนต์ เลขนี้คือเลขเกมของซีรีส์นั้น
มันจึงตรงกับคะแนนเองโดยไม่ต้องมาคอยตั้ง

ปุ่มด้านล่าง: **UNDO** (ย้อนการแก้ล่าสุด), **SWITCH TEAMS** (สลับฝั่ง), **CLEAR PICKS & BANS**, **RESET MATCH** (เริ่มใหม่ทั้งแมตช์)

---

## 4. Save teams once, use them everywhere / บันทึกทีมไว้ใช้ซ้ำ

**EN**

1. Go to **TEAMS → + NEW TEAM**
2. Fill in the name, tag, players, and a logo
3. Click **CREATE TEAM**

Saved teams appear in **From registry** on the control panel and can be added to any tournament.

**TH**

1. ไปที่ **TEAMS → + NEW TEAM**
2. ใส่ชื่อทีม ตัวย่อ ผู้เล่น และโลโก้
3. กด **CREATE TEAM**

ทีมที่บันทึกไว้จะโผล่ในช่อง **From registry** ของหน้า Control และเอาไปใส่ทัวร์นาเมนต์ไหนก็ได้

---

## 5. Run a tournament / จัดทัวร์นาเมนต์

**EN**

1. **HOME → + NEW TOURNAMENT** → name, format, Best of → **CREATE**
2. **+ ADD TEAM** — pick saved teams, or create new ones here
3. Click **OPEN MATCH SESSION**, then **DRAW MATCHES**
4. Double-click a match box, or press its play button, to put it on air — the Control Panel
   opens with both teams filled in. The **ON AIR** strip there has a button back to the bracket
5. Type the scores into the bracket. Winners move to the next round by themselves
6. With four teams or more in a single elimination draw, a **Third place** section appears
   below the bracket. The two semifinal losers drop into it on their own

To delete a tournament: open it and click **DELETE TOURNAMENT**, or use the **DELETE** button on its card on the home page. This cannot be undone — the bracket and every saved draft go with it. Your teams stay.

**TH**

1. **HOME → + NEW TOURNAMENT** → ชื่อ รูปแบบ จำนวนเกม → **CREATE**
2. **+ ADD TEAM** — เลือกทีมที่บันทึกไว้ หรือสร้างทีมใหม่ตรงนั้นเลย
3. กด **OPEN MATCH SESSION** แล้วกด **DRAW MATCHES**
4. ดับเบิลคลิกที่กล่องคู่แข่ง หรือกดปุ่มเล่นบนกล่องนั้น เพื่อเอาขึ้นจอ หน้าคุมงานจะเปิดพร้อมชื่อทีมทั้งสองฝั่งให้เลย
   และบนแถบ **ออนแอร์** ในหน้าคุมงานมีปุ่มกลับไปที่สายการแข่งด้วย
5. กรอกคะแนนในสาย ผู้ชนะจะเลื่อนไปรอบถัดไปเอง
6. ถ้าเป็นแบบแพ้คัดออกและมีตั้งแต่สี่ทีมขึ้นไป จะมีส่วน **ชิงที่ 3** อยู่ใต้สาย
   ผู้แพ้จากรอบรองชนะเลิศสองคู่จะตกลงมาที่นัดนี้เอง

ถ้าจะลบทัวร์นาเมนต์: เข้าไปในทัวร์นาเมนต์แล้วกด **DELETE TOURNAMENT** หรือกดปุ่ม **DELETE** บนการ์ดที่หน้าแรก ลบแล้วกู้ไม่ได้ สายการแข่งกับดราฟต์ที่บันทึกไว้หายไปด้วย แต่ทีมในทะเบียนยังอยู่

---

## 6. Sound effects / เสียงเอฟเฟกต์

**EN**

1. Menu **ROV Tool → Open Sounds Folder**
2. Put your own files in, named exactly:

   | File | Plays |
   |---|---|
   | `pick.mp3` | when a hero is picked |
   | `ban.mp3` | when a hero is banned |
   | `timer-warning.mp3` | every second of the last 10 |

   `.wav` works too. You do not need all three.
3. The URLs you copy from the app already have sound switched on
4. Adjust volume on **CONTROL → Sound effects**. **TEST** plays on your computer only, not on stream

Keep `timer-warning` short (under half a second) — it plays once a second.

**The overlay has to stay live for sound to reach viewers.** OBS only mixes audio from
sources in the scene you are broadcasting, so the overlay must be in *that* scene, not only in
another one. Leave **Shutdown source when not visible** unticked, or OBS stops the page every
time you switch away. Keep the app window open too — closing it stops the server.

If you hear nothing, open **http://127.0.0.1:3000/sfx-test** — it tells you what is wrong.

**TH**

1. เมนู **ROV Tool → Open Sounds Folder**
2. เอาไฟล์เสียงของคุณไปวาง ตั้งชื่อให้ตรงนี้เป๊ะๆ:

   | ไฟล์ | ดังตอน |
   |---|---|
   | `pick.mp3` | เลือกฮีโร่ |
   | `ban.mp3` | แบนฮีโร่ |
   | `timer-warning.mp3` | ทุกวินาทีในสิบวินาทีสุดท้าย |

   ใช้ `.wav` ก็ได้ ไม่จำเป็นต้องมีครบทั้งสามไฟล์
3. URL ที่ก๊อปจากในโปรแกรมเปิดเสียงมาให้แล้ว
4. ปรับความดังที่ **CONTROL → Sound effects** ปุ่ม **TEST** ดังที่เครื่องคุณเท่านั้น ไม่ออกอากาศ

ไฟล์ `timer-warning` ควรสั้นๆ (ไม่เกินครึ่งวินาที) เพราะมันดังทุกวินาที

**ต้องเปิด overlay ค้างไว้ตลอด เสียงถึงจะออกไปถึงคนดู** OBS ผสมเสียงเฉพาะ source ที่อยู่ในซีน
ที่กำลังออกอากาศ ดังนั้น overlay ต้องอยู่ในซีนนั้นด้วย ไม่ใช่อยู่แค่ในซีนอื่น
และอย่าติ๊ก **Shutdown source when not visible** ไม่งั้นพอสลับซีนออกไป OBS จะปิดหน้านั้นทิ้ง
ต้องเปิดหน้าต่างโปรแกรมค้างไว้ด้วย ถ้าปิดโปรแกรม เซิร์ฟเวอร์จะหยุดทำงาน

ถ้าไม่ได้ยินเสียงเลย ให้เปิด **http://127.0.0.1:3000/sfx-test** มันจะบอกว่าติดตรงไหน

---

## 7. Change how it looks / เปลี่ยนหน้าตา

**EN**

Go to **DESIGN**. You can change colours, text sizes, logo size, and upload your own background images. The preview at the top updates as you change things.

**TH**

ไปหน้า **DESIGN** เปลี่ยนสี ขนาดตัวหนังสือ ขนาดโลโก้ และอัปโหลดภาพพื้นหลังของคุณเองได้ ตัวอย่างด้านบนจะเปลี่ยนตามทันที

---

## 8. Keyboard shortcuts / คีย์ลัด

| Key / ปุ่ม | What it does | ทำอะไร |
|---|---|---|
| `Alt` | Show / hide the banner | ซ่อน/แสดงแถบ overlay |
| `Space` | Pause / resume the timer | หยุด/เดินเวลาต่อ |
| `←` `→` | Previous / next draft phase | ย้อน/ไปเฟสถัดไป |
| `Ctrl + Z` | Undo | ย้อนกลับ |
| `Enter` | Confirm the hero you typed | ยืนยันฮีโร่ที่พิมพ์ |
| `Esc` | Leave the box, or go back a page | ออกจากช่อง หรือย้อนกลับหน้าก่อนหน้า |

Change these on the **HOTKEYS** page. They only work while the app window is in front — not while you are in OBS or in the game.

เปลี่ยนได้ที่หน้า **HOTKEYS** คีย์ลัดทำงานเฉพาะตอนที่หน้าต่างโปรแกรมอยู่ข้างหน้าเท่านั้น ไม่ทำงานตอนอยู่ใน OBS หรือในเกม

If you need keys that keep working while you are in OBS or the game, switch on **System-wide hotkeys** on the same page. They are off until you do, and each one needs Ctrl, Alt, Shift or Win. This only works in the desktop app.

ถ้าต้องการปุ่มที่กดได้ตอนอยู่ใน OBS หรือในเกม ให้เปิด **System-wide hotkeys** ที่หน้าเดียวกัน ค่าเริ่มต้นคือปิดไว้ และทุกปุ่มต้องมี Ctrl, Alt, Shift หรือ Win ประกอบ ใช้ได้เฉพาะในแอพเดสก์ท็อป

---

## 9. When something looks wrong / เวลามีอะไรผิดปกติ

| Problem / อาการ | Try this / ลองทำแบบนี้ |
|---|---|
| Overlay not updating in OBS | Right-click the source → **Refresh** |
| overlay ใน OBS ไม่อัปเดต | คลิกขวาที่ source → **Refresh** |
| No sound | Open `/sfx-test`, and tick **Control audio via OBS** in the source properties |
| ไม่มีเสียง | เปิด `/sfx-test` และติ๊ก **Control audio via OBS** ใน properties ของ source |
| Sound plays twice | Only one browser source may have `?sfx=1` in its URL |
| เสียงดังซ้อนสองครั้ง | ให้มี `?sfx=1` ใน URL ของ source เดียวเท่านั้น |
| Sound stops after switching scenes | The overlay must be in the scene you are broadcasting, and **Shutdown source when not visible** must be unticked |
| สลับซีนแล้วเสียงหาย | overlay ต้องอยู่ในซีนที่กำลังออกอากาศ และห้ามติ๊ก **Shutdown source when not visible** |
| Wrong teams on screen | Press **RESET MATCH**, or put the right match on air again |
| ทีมบนจอผิด | กด **RESET MATCH** หรือเอาแมตช์ที่ถูกขึ้นจอใหม่ |
| Changed a sound file but hear the old one | Add `&v=2` to the end of the source URL, then Refresh |
| เปลี่ยนไฟล์เสียงแล้วยังได้ยินเสียงเก่า | เติม `&v=2` ท้าย URL ของ source แล้ว Refresh |

---

## Team statistics / สถิติของทีม

**EN**

Open a team (from **TEAMS**, or by clicking a team name anywhere) and switch to the
**Statistics** tab. Everything on it can be narrowed to one tournament with the dropdown at
the top; it shows all tournaments by default.

- **Series** and **games** won–lost, with win rates, and the **last 5 series** as W and L
- **On blue side / On red side**: won–lost on each side of the screen
- **Heroes picked**: how many games, the pick rate and the win rate with each hero
- **Banned by this team** and **banned against this team**
- **Record vs each opponent**: series and games against every team faced, and the last meeting
- **Player hero pools**: the heroes each player picked, and how often they won with them

Rates are out of the games whose draft was finished; win rates only count games with a
recorded winner (+1 records it). **Side records and player hero pools count from version
3.0.13 on.** Earlier games did not record which side a team actually played on screen or who
sat in which row, so they are left out of those two rather than guessed. Player pools use the
player names on the Control Panel at the time of the draft.

**TH**

เปิดหน้าทีม (จาก **ทีม** หรือกดชื่อทีมที่ไหนก็ได้) แล้วเลือกแท็บ **สถิติ** ทุกอย่างในแท็บนี้
เลือกให้ดูเฉพาะทัวร์นาเมนต์เดียวได้จากช่องด้านบน ปกติจะแสดงทุกทัวร์นาเมนต์

- **ซีรีส์** และ **เกม** ชนะ–แพ้ พร้อมอัตราชนะ และ **ซีรีส์ 5 คู่ล่าสุด** เป็น ช กับ พ
- **ฝั่งน้ำเงิน / ฝั่งแดง**: ชนะ–แพ้ ของแต่ละฝั่งบนจอ
- **ฮีโร่ที่เลือก**: กี่เกม อัตราการเลือก และอัตราชนะของฮีโร่แต่ละตัว
- **ฮีโร่ที่ทีมนี้แบน** และ **ฮีโร่ที่โดนแบนใส่**
- **สถิติกับคู่แข่งแต่ละทีม**: ซีรีส์และเกมกับทุกทีมที่เคยเจอ และนัดล่าสุด
- **ฮีโร่ของผู้เล่นแต่ละคน**: ฮีโร่ที่ผู้เล่นแต่ละคนเลือก และชนะบ่อยแค่ไหน

อัตราคิดจากเกมที่ดราฟต์ครบ ส่วนอัตราชนะนับเฉพาะเกมที่รู้ผลแพ้ชนะ (ปุ่ม +1 เป็นตัวบันทึก)
**สถิติฝั่งและฮีโร่ของผู้เล่นแต่ละคน นับตั้งแต่เวอร์ชัน 3.0.13** เกมก่อนหน้านั้นไม่ได้บันทึกว่าทีมเล่นฝั่งไหนบนจอจริง
หรือใครนั่งแถวไหน จึงไม่ถูกนับในสองส่วนนี้ แทนที่จะเดาเอา สถิติผู้เล่นใช้ชื่อผู้เล่นในหน้าคุมงานตอนที่ดราฟต์

---

## Pick / ban history / ประวัติพิค-แบน

**EN**

Open a tournament and click **PICK / BAN HISTORY** to see every draft played in that
event, newest first. Each game shows both teams with their five picks and four bans; the
team that won the game is in gold.

Two filters at the top:

- **Team** — only games that team played in
- **Find a hero** — only games where that hero was picked or banned, with the matching
  tiles outlined so you can see where in the draft it appeared

This is the record of what happened. The **ANALYTICS** page answers the different question
of which heroes are picked and banned most across the event.

**TH**

เปิดทัวร์นาเมนต์แล้วกด **ประวัติพิค/แบน** จะเห็นดราฟต์ทุกเกมที่เล่นในรายการนั้น
เรียงจากใหม่ไปเก่า แต่ละเกมแสดงทั้งสองทีม พร้อมพิคห้าตัวและแบนสี่ตัว
ทีมที่ชนะเกมนั้นจะเป็นสีทอง

มีตัวกรองสองอันด้านบน

- **ทีม** — เอาเฉพาะเกมที่ทีมนั้นลงเล่น
- **ค้นหาฮีโร่** — เอาเฉพาะเกมที่ฮีโร่ตัวนั้นถูกพิคหรือแบน และตีกรอบช่องที่ตรงให้ด้วย
  จะได้เห็นว่ามันอยู่ตรงไหนของดราฟต์

หน้านี้คือบันทึกว่าเกิดอะไรขึ้น ส่วนหน้า **สถิติ** ตอบคนละคำถาม
คือฮีโร่ตัวไหนถูกพิคถูกแบนมากที่สุดในรายการ

---

## Group standings and playoffs / ตารางคะแนนและรอบน็อกเอาต์

**EN**

For **Round robin** and **Group stage**, the tournament page shows a **Standings** table
that fills in as you record results — played, won, lost, game difference, points
(3 for a win).

1. Set **Teams through per group**; those rows highlight so you can see the cut line
2. Once every group match has a result, click **DRAW PLAYOFF**
3. The top teams go into a knockout bracket inside the same tournament — the group stage
   and its drafts are untouched

If two teams are level on every measure their rows are marked `=`, and the playoff will
refuse to draw. Settle those by your own rules and record the result first.

Add **Standings** as a Browser source to put the table on stream between games.

**TH**

รูปแบบ **พบกันหมด** และ **แบ่งกลุ่ม** หน้าทัวร์นาเมนต์จะมี **ตารางคะแนน**
ที่เติมเองเมื่อกรอกผล มีทั้งจำนวนนัด ชนะ แพ้ ผลต่างเกม และแต้ม (ชนะได้ 3)

1. ตั้ง **ผ่านเข้ารอบกลุ่มละ** กี่ทีม แถวที่ได้ไปต่อจะถูกไฮไลต์ให้เห็นเส้นตัด
2. เมื่อทุกนัดในกลุ่มมีผลครบแล้ว กด **จับสายรอบน็อกเอาต์**
3. ทีมหัวกลุ่มจะถูกวางลงสายน็อกเอาต์ในทัวร์นาเมนต์เดิม รอบแบ่งกลุ่มและดราฟต์ไม่ถูกแตะ

ถ้าสองทีมเท่ากันทุกตัวชี้วัด แถวนั้นจะมี `=` และโปรแกรมจะไม่ยอมจับสายให้
ให้ตัดสินด้วยกติกาของรายการเองแล้วกรอกผลก่อน

เพิ่ม **Standings** เป็น Browser source เพื่อเอาตารางขึ้นจอระหว่างพักได้

---

## Head to head on stream / หัวต่อหัวบนจอ

**EN**

Add **Head to head** as a Browser source. It follows whatever match is on air, so one URL
lasts the whole event. It shows how many series and games the two teams have won against
each other, and what each side picked and banned in their previous meetings.

All of it comes from drafts the app already recorded — there is nothing extra to fill in.

**TH**

เพิ่ม **Head to head** เป็น Browser source มันจะตามคู่ที่กำลังออกอากาศเอง
ใช้ URL เดียวได้ทั้งงาน แสดงว่าสองทีมนี้เคยเจอกันแล้วใครชนะกี่ซีรีส์กี่เกม
และแต่ละฝั่งเคยหยิบหรือแบนตัวไหนใส่กันบ้าง

ทั้งหมดมาจากดราฟต์ที่โปรแกรมบันทึกไว้อยู่แล้ว ไม่ต้องกรอกอะไรเพิ่ม

---

## Team picks and bans on stream / ตัวที่แต่ละทีมหยิบและแบนบนจอ

**EN**

Add **Team picks & bans** as a Browser source. It looks exactly like Head to head and
follows the match on air the same way, but it counts a wider set of games: **everything
each team has played in this tournament**, not only the games the two of them played
against each other.

Use it for the first round, or any time the two teams have never met — Head to head is
blank in that situation, and this board is not. The number in the middle is how many games
each team has played in the event, and the line under it says so; it is not a score.

**TH**

เพิ่ม **Team picks & bans** เป็น Browser source หน้าตาเหมือน Head to head ทุกอย่าง
และตามคู่ที่ออกอากาศเหมือนกัน ต่างกันตรงขอบเขตที่นับ: อันนี้นับ **ทุกเกมที่แต่ละทีม
ลงเล่นในรายการนี้** ไม่ใช่เฉพาะเกมที่สองทีมนี้เจอกันเอง

เหมาะกับรอบแรก หรือคู่ไหนก็ตามที่ยังไม่เคยเจอกันมาก่อน เพราะ Head to head จะว่างเปล่า
แต่กระดานนี้ยังมีข้อมูลขึ้น ตัวเลขตรงกลางคือจำนวนเกมที่แต่ละทีมลงเล่นในรายการนี้
มีบรรทัดกำกับไว้ข้างล่างแล้ว ไม่ใช่สกอร์

---

## Back up your work / สำรองข้อมูลของคุณ

**EN**

Everything lives on this computer only. There is no cloud copy, so if the machine dies,
the teams, brackets and every recorded draft go with it.

1. On **HOME**, scroll to **Backup**
2. Click **SAVE A BACKUP** — one file with everything: teams, logos, brackets, drafts,
   and your background images
3. Keep it somewhere that is not this PC. A backup on the same machine does not survive
   the machine

To bring it back, on a new PC or after a reinstall, click **RESTORE…** and pick the file.
It shows what is inside and asks before changing anything. Anything already on the machine
is kept — records already there are skipped, never overwritten.

Worth doing after the draw, and again at the end of each event day. The file is small.

**TH**

ข้อมูลทั้งหมดอยู่ในเครื่องนี้เครื่องเดียว ไม่มีสำเนาบนคลาวด์ ถ้าเครื่องพัง ทีม สายการแข่ง
และดราฟต์ที่บันทึกไว้ทั้งหมดจะหายไปด้วย

1. ที่ **หน้าแรก** เลื่อนลงไปที่ **สำรองข้อมูล**
2. กด **บันทึกไฟล์สำรอง** จะได้ไฟล์เดียวที่มีทุกอย่าง ทั้งทีม โลโก้ สายการแข่ง ดราฟต์
   และภาพพื้นหลังที่ทำไว้
3. เก็บไฟล์ไว้ที่อื่นที่ไม่ใช่เครื่องนี้ ไฟล์สำรองที่อยู่ในเครื่องเดียวกันจะหายไปพร้อมเครื่อง

เวลาจะเอากลับมา ไม่ว่าจะเครื่องใหม่หรือลงโปรแกรมใหม่ ให้กด **กู้คืน…** แล้วเลือกไฟล์
โปรแกรมจะแสดงให้ดูก่อนว่าในไฟล์มีอะไร แล้วค่อยถามยืนยัน ของที่มีอยู่ในเครื่องแล้วจะไม่ถูกแตะ
รายการที่ซ้ำจะถูกข้าม ไม่ใช่เขียนทับ

ควรทำหลังจับสาย และทำอีกครั้งตอนจบวันแข่ง ไฟล์เล็กมาก

---

## Coming from version 2 / ย้ายมาจากเวอร์ชัน 2

**EN**

If you used **ROV Overlay Tool v2**, everything in it can be brought across. You do not
have to type your teams in again.

1. Go to **SETTINGS**, scroll to **BRING YOUR V2 DATA ACROSS**
2. The app looks for v2 by itself. If it found it, the folder is already filled in — if
   not, press **Browse…** and point at it. The v2 folder, its `data` folder, or the
   `tournament.db` file itself all work
3. Press **Import**. It first shows what the folder holds — how many teams, tournaments,
   matches, recorded drafts and logos — and asks before writing anything

What comes across: teams and their players, team logos, tournaments, brackets, every
match and every recorded draft, and your background images.

What does not: the board as you left it (current picks and bans, the score), your colours
from the Design page, sound levels and hotkeys. Those are quick to set again and are
meant to be per-machine.

Two things worth knowing:

- **Your v2 is only read, never changed.** Nothing in its folder is written to or moved,
  and v2 keeps working exactly as it did
- **Importing twice is safe.** Anything already here is kept and duplicates are skipped,
  so a second import changes nothing

Old v2 installations work too, including ones from before v2 renamed player roles.

**TH**

ถ้าคุณเคยใช้ **ROV Overlay Tool เวอร์ชัน 2** ย้ายของทั้งหมดมาได้เลย ไม่ต้องพิมพ์ทีมใหม่

1. ไปหน้า **ตั้งค่า** เลื่อนลงไปที่ **ย้ายข้อมูลจากเวอร์ชัน 2**
2. โปรแกรมจะหาเวอร์ชัน 2 ให้เอง ถ้าเจอ ช่องโฟลเดอร์จะถูกเติมไว้แล้ว ถ้าไม่เจอ ให้กด
   **เลือกโฟลเดอร์…** แล้วชี้ไปที่โฟลเดอร์นั้น จะชี้ที่โฟลเดอร์ของเวอร์ชัน 2 ที่โฟลเดอร์ `data`
   ข้างใน หรือที่ไฟล์ `tournament.db` ตรง ๆ ก็ได้ทั้งหมด
3. กด **นำเข้า** โปรแกรมจะแสดงให้ดูก่อนว่าในโฟลเดอร์มีอะไร กี่ทีม กี่ทัวร์นาเมนต์ กี่คู่แข่ง
   ดราฟต์กี่ชุด โลโก้กี่รูป แล้วค่อยถามยืนยันก่อนเขียนอะไรลงเครื่อง

**ของที่ย้ายมา** ทีมและผู้เล่น โลโก้ทีม ทัวร์นาเมนต์ สายการแข่ง คู่แข่งทุกคู่ ดราฟต์ที่บันทึกไว้ทุกชุด
และภาพพื้นหลัง

**ของที่ไม่ย้าย** กระดานที่ค้างอยู่ (พิคแบนและสกอร์ปัจจุบัน) สีจากหน้า Design ระดับเสียง และคีย์ลัด
พวกนี้ตั้งใหม่ไม่กี่นาที และตั้งใจให้เป็นของแต่ละเครื่องอยู่แล้ว

สองอย่างที่ควรรู้

- **เวอร์ชัน 2 ถูกอ่านอย่างเดียว ไม่ถูกแก้** ไม่มีการเขียนหรือย้ายไฟล์ในโฟลเดอร์นั้น
  และเวอร์ชัน 2 ยังใช้งานได้เหมือนเดิมทุกอย่าง
- **นำเข้าซ้ำได้ ไม่เสียหาย** ของที่มีอยู่แล้วจะไม่ถูกแตะ รายการที่ซ้ำจะถูกข้าม นำเข้าอีกรอบจึงไม่เปลี่ยนอะไร

เวอร์ชัน 2 รุ่นเก่าก็นำเข้าได้ รวมถึงรุ่นก่อนที่เวอร์ชัน 2 จะเปลี่ยนชื่อตำแหน่งผู้เล่น

---

## Where your files are / ไฟล์ของคุณอยู่ที่ไหน

| What / อะไร | Where / ที่ไหน |
|---|---|
| Sounds / เสียง | Menu **ROV Tool → Open Sounds Folder** |
| Teams, tournaments / ทีม ทัวร์นาเมนต์ | `%APPDATA%\rov-overlay-tool\data` |
| Logos, backgrounds / โลโก้ ภาพพื้นหลัง | `%APPDATA%\rov-overlay-tool\media` |

Back these up by copying the folders. Nothing is stored online.
สำรองข้อมูลด้วยการก๊อปโฟลเดอร์พวกนี้ไปเก็บไว้ ไม่มีอะไรถูกเก็บบนออนไลน์
