# ROV Overlay Tool v3: Plan and Decisions

This is the working design document for v3. It is written to stand alone: a fresh
session with no conversation history should be able to continue from here and
`CLAUDE.md`. The v2 design history is kept, read-only, in `docs/v2/`.

---

## 0. Where things stand

**Last updated 2026-09-14. M1 `e826fb3`, M2 `6f736b3`, M3 `ab3bdf8`, M4 `c3ac12a`, M5 `3fcb2a9`, M6 `c615d39`, M7 `79c5f41`, M8 `7e13667`, 3.0.6 and 3.0.7 in the commits after those, the flow and UI work in `83e558c`, 3.0.8 in `182ea91`.**

| Area | State |
|---|---|
| Backend (`backend/`) | Copied from v2 at `6c69766` (v2.0.2 plus two overlay commits). Builds; **373 tests, all passing, nothing skipped**. M6 added the v2 importer; M7 revived the "installer never ships uploaded images" guard; M8 added `tests/packaging.test.ts`, which keeps the bundle's page list honest in both directions. |
| Desktop app (`desktop/`) | WPF on .NET 10. Builds with no warnings. Starts or attaches to the backend, live title strip, sidebar, OBS source list, toasts, Thai/English, back stack (Esc / mouse back), notice bell, first-run licence. |
| Native screens | **All of them**: Home, tournament detail, team registry, team profile, Control Panel, bracket, analytics, pick/ban history, Design, Hotkeys, Guide, Settings. No screen opens a web page any more, and since M8 the installer no longer carries the ten HTML operator pages they replaced. The manual (`/guide`) and the sound check (`/sfx-test`) still ship: nothing replaced those, and the Guide screen has a button that opens the manual in a browser. |
| Verified how | Every native screen rendered with seeded data (--snapshot, §3) in both languages; anything in its own window cannot be (§8), which is how 3.0.0 shipped unable to open one at all. 3.0.5 has been **installed from its own Setup and watched opening a real window**, serving its overlays and answering 410 on the pages the installer drops. The v2 import runs against a synthetic v2 install in the tests, with the v2 folder asserted byte-identical afterwards. The **game-over flow has been clicked through for real** (2026-09-14): `scripts/uia.ps1` pressed GAME OVER, the confirm dialog, the deciding game and Put on air in a live window against a throwaway backend, with the server's record checked after every press. The other clicking flows are still unverified (§8). |
| Updates / notifications | **Built** (§5). Velopack 1.2.0 against GitHub Releases, applied when the app closes and never on its own; a notice feed with a bell in the title bar. |

Next: whatever the people using it ask for. 3.0.5 through **3.0.8** are published; updates
reach them on their own. 3.0.8 carries the flow and UI work: one-press GAME OVER with SERIES
OVER and Put on air (`POST /api/live-match/finish`), a Control Panel whose team setup and sound
fold away, and a Home that shows what is on air and what is ready to play
(`GET /api/ready-matches`). It was smoke-tested for real before packing. Its notice
(`update-3-0-8`, for 3.0.7 and older, expires 2026-10-15) replaced the 3.0.6 one in
`notices.json` on 2026-09-14.

---

## 1. Confirmed decisions

Settled with the user on 2026-09-11. Do not re-litigate.

- **v3 is a new app in its own folder** (`rov_overlay_v3`). **`../rov_pickban_overlay`
  (v2) is never modified**: no builds, no `npm start`, no edits. Read from it only.
  Running `npm start` there rebuilds its `build/` and opens its database.
- **The overlays stay HTML/CSS/JS**, served to OBS as browser sources, unchanged from v2.
- **The operator UI is native C#** (WPF). The user picked native over a React shell
  inside Electron.
- **The look is a pro broadcast tool**: dense, dark, lots on screen, like OBS, vMix or
  Discord. The v2 colour rules carry over (gold = happening now, blue/red = game sides).
- **Same functionality as v2, better UI.** Nothing is dropped; every v2 page either
  gets a native screen or stays reachable as its HTML page until it does.
- **Future: real-time patches and notifications to users** (§5). Built in later
  milestones, but designed now so nothing blocks them.
- **The tournament page keeps its OBS source list** (decided 2026-09-14). v2 removed it
  from `/tournament/:id` on 2026-09-08 at the user's request, and `backend/CLAUDE.md` still
  says so; asked again for v3, the user chose to keep the per-tournament links (Standings,
  Team list, Stats board for that tournament). Do not remove it on the rulebook's word.
- **All data stays local**, as in v2. The update and notice checks only *read* public
  files; there are no accounts and no telemetry.

## 2. Architecture

```
┌──────────────────────────────┐        HTTP + Socket.IO        ┌──────────────────────────┐
│ desktop/  RovOverlayTool.exe │ ─────────────────────────────▶ │ backend/  node server.js │
│ WPF, .NET 10                 │   127.0.0.1:3000               │ Express, Socket.IO       │
│ operator screens             │ ◀── stateUpdate, dataChanged ── │ node:sqlite, state.json  │
└──────────────┬───────────────┘                                └────────────┬─────────────┘
               │ starts, owns, stops (stdin pipe + job object)               │ serves
               └─────────────────────────────────────────────────────────────┤
                                                        OBS browser sources ◀┘  /overlay, /result, ...
```

**Why the Node backend stays.** The overlays speak Socket.IO, and they must not change,
so the server has to speak it too; SignalR cannot. The backend also carries all the
tournament logic, the one `sanitizeState` both sides trust, and 349 tests. Rewriting it
in C# would be months of work for no user-visible gain. The desktop app is purely a
client of the same API the HTML pages use.

**Process model** (`desktop/.../Services/BackendHost.cs`):

1. Probe `GET /api/app-info`. If a **v3** backend answers, attach to it (this is how
   `npm run dev` works during development). Settings shows "attached".
2. If anything else holds the port (almost always v2), **refuse** with a clear message.
   `/api/app-info` exists only in v3, which is what makes the check trustworthy.
3. Otherwise start `node server.js` with `ROV_USER_DATA_DIR`, `ROV_USER_MEDIA_DIR`,
   `PORT`, `HOST=127.0.0.1` and `ROV_EXIT_WITH_PARENT=1`, and wait for `/api/app-info`.
4. **Stopping:** close the child's stdin. `backend/server/lifecycle.ts` flushes the
   debounced `state.json` write, closes SQLite and exits (tested in
   `tests/lifecycle.test.ts`). Kill only if it has not gone after 3 s.
5. **Crash safety:** the child is in a Windows job object with `KILL_ON_JOB_CLOSE`, so
   it can never outlive the app and squat on port 3000.

**Where node comes from:** `backend/runtime/node.exe` if present (the packaged app),
otherwise `node` on PATH (development). Node must be 22.5+ for `node:sqlite`.

**Data:** `%APPDATA%\RovOverlayTool3\` holds `data\` (tournament.db, state.json),
`media\` (logos, skins) and `settings.json` (language, port). It is separate from
v2's `%APPDATA%\ROV Overlay Tool\`, which v3 never reads on its own, and from the
install folder, which the updater replaces wholesale.

**Port:** 3000 by default, so existing OBS scenes keep working. v2 and v3 cannot run
at the same time on it.

## 3. Desktop app conventions

- **MVVM without a toolkit.** `Core/ObservableObject`, `RelayCommand`,
  `AsyncRelayCommand` (no double-fire; failures become an error toast). Kept dependency
  free so the project builds offline from the .NET SDK alone.
- **Adding a native screen:** a view model in `ViewModels/`, a `UserControl` in
  `Views/`, one `DataTemplate` line in `App.xaml`, then swap the `Legacy(...)` entry in
  `ShellViewModel` for the new view model.
- **Page view models are created once and kept** (`NavItem.Page`), so switching screens
  keeps unsaved input.
- **Live data:** `AppServices` raises `StateUpdated` (the overlay state),
  `DataChanged` (topics `teams`, `tournaments`, `roster`, `matches`, `games`, `live`)
  and `ConnectionChanged`, all on the UI thread. Screens re-read on the topics they show,
  exactly as the v2 pages did.
- **Text:** `Services/Loc.cs`. XAML `{svc:T Key}`, code `Loc.T` / `Loc.F`. Thai is the
  default. Reuse v2's Thai wording (`backend/public/js/lib/i18n.js`) when the same text
  existed there.
- **Theme:** `Theme/Theme.xaml` is the only place colours are defined. Green appears
  only on the connection light.
- **Motion is short, and there is very little of it.** Hover fades in over 90ms and out
  over 140ms; a screen fades up over 140ms as it arrives; a toast slides in from the edge
  it lives on; dialogs fade and scale from 0.97. That is the whole budget. This is a panel
  someone stares at for an entire event, and anything that moves while they are reading it
  is a defect. The single repeating animation is the draft clock pulsing under ten
  seconds, and that one exists to be caught by peripheral vision rather than to look nice.
  Animate `Opacity` or a transform, never the shared brushes: they are frozen resources
  and animating them throws.
- **Icons:** Segoe Fluent Icons (Windows 11) with Segoe MDL2 Assets as fallback. Write
  glyphs as `\uE80F` in C# and `&#xE80F;` in XAML, never as raw characters (§9).
- **Checking a screen without a person:** `RovOverlayTool.exe --page Home --lang en
  --snapshot out.png` renders the window to a PNG and exits. The snapshot run uses its
  own single-instance name, so it works beside an open app.

## 4. Screen migration

| v2 page | v3 now | Target |
|---|---|---|
| `/` Home: tournament list, create | **Native** | done (M1) |
| `/` Home: backup and restore | **Native**, in Settings | done (M2) |
| `/tournament/:id` | **Native** (details, roster with inline team editor, add/create team, match summary, standings and playoff draw, per-tournament OBS URLs) | done (M2) |
| `/teams` | **Native** (search, create with players and logo, multi-select bulk delete, W-L and tournament counts) | done (M2) |
| `/teams/:id` | **Native** (roster and logo editor, tournaments, match history) | done (M2) |
| `/control` Control Panel | **Native** (match info, on-air switches, draft timer with the 16-phase sequence and round stepper, both sides with rosters, lanes, picks, bans, logos, registry load, sound levels, undo/switch/reset, keyboard shortcuts) | done (M3) |
| `/tournament/:id/bracket` | **Native** (bracket drawn on a canvas with connectors, draw/redraw/clear, random draw, score boxes, put a match on air) | done (M4) |
| `/analytics` | **Native** (presence/pick/ban/win/ban-priority table, tournament and team scope, hero search, the draft in progress shown apart) | done (M4) |
| `/tournament/:id/drafts` | **Native** (every recorded draft as portraits, team and hero filters) | done (M4) |
| `/design` | **Native** (theme colours and sizes, six background-image slots with upload/clear and previews, the two skin switches) | done (M5) |
| `/hotkeys` | **Native**, including system-wide hotkeys through Win32 `RegisterHotKey` | done (M5) |
| `/guide` | **Native**, rendered from `backend/docs/USER_GUIDE.md` in the app language, with a search box | done (M5) |
| OBS URL list | **Native** (title bar) | done (M1) |
| Settings | **Native** | done (M1) |

**Electron-only v2 features that need a v3 answer:**

- Global hotkeys: **done in M5** through Win32 `RegisterHotKey` in `Services/GlobalHotkeyHost.cs`.
- The first-run licence agreement dialog: M7, with the installer.
- The app menu that opened overlay windows so sound effects play: see §8.

## 5. Updates and notifications (built in M7)

**Updates: Velopack 1.2.0**, the only third-party package in the app.

- `scripts/pack.ps1` stages a self-contained `dotnet publish`, the backend with a clean
  production `npm ci`, and `node.exe` into `backend/runtime/`, then runs `vpk pack`.
  Staged bundle 255 MB; installer 124 MB, plus a portable zip and a full `.nupkg`.
- It strips `public/images/team-logos` and `skins` from the stage first. Those are
  whatever the person building happened to upload, and v2 once shipped them to everyone.
- Feed: **GitHub Releases on `LazyAF-zZzZ/rov_overlay_v3`**, public because a private
  feed would need every user to hold a token. `pack.ps1 -Publish` uploads; without that
  switch nothing reaches anyone.
- Checked on start and every six hours, downloaded in the background. The operator is
  told it is ready and **the app never restarts itself**:
  `WaitExitThenApplyUpdates(restart: false)` puts it in when they close the app. Someone
  may be live on air.
- Channels `stable` and `beta`, chosen in Settings and read fresh on every check.
- Only an installed copy can update. A portable copy, or a build from the repo, says so
  in Settings instead of pretending to check.

**Notifications: a notice feed**, with no server of ours.

- `notices.json` in the repository root, read over HTTPS on start and every six hours:
  `{ id, level: info|warning|critical, title: {th, en}, body: {th, en}, url?,
  minVersion?, maxVersion?, expires? }`.
- A bell with a count in the title bar, a panel to read them, and a toast for new ones.
  Dismissed ids are kept in `settings.json`, so a notice does not come back.
- The version filtering happens on the operator's machine, so **nothing about them is
  sent**: the app only GETs one public file. Offline, nothing is shown and nothing warns.

## 6. Layout

```
backend/            v2's server, overlays and tests, adapted for v3 (see §9 for what changed)
  server/http/api-app-info.ts   v3 identity route
  server/lifecycle.ts           stdin-driven clean shutdown
desktop/
  RovOverlay.slnx
  RovOverlay.Desktop/
    Core/           ObservableObject, commands, converters, helpers
    Services/       BackendHost, ApiClient, SocketIoClient, AppServices, Loc, Toasts, settings
    Models/         API reply records
    ViewModels/     Shell, Home, pages
    Views/          MainWindow, HomeView, SettingsView, LegacyPageView
    Theme/Theme.xaml
docs/PLAN.md        this file
docs/v2/            v2's plan, guide and notes, for reference
```

## 7. Milestones

| # | What | State |
|---|---|---|
| M1 | Copy backend; WPF shell; backend host; Socket.IO client; Home; Settings; OBS list | done, `e826fb3` |
| M2 | Native tournament detail, team registry and team profile; backup/restore in Settings | done, commit after `e826fb3` |
| M3 | Native Control Panel (draft, picks/bans, timer, scores, live match) | done, commit after `6f736b3` |
| M4 | Bracket, analytics, tournament drafts | done, commit after `ab3bdf8` |
| M5 | Design, Hotkeys with native global hotkeys, Guide | done, commit after `c3ac12a` |
| M6 | Import from v2: read its database and images, merge them in, never write to its folder | done, commit after `3fcb2a9` |
| M7 | Packaging: bundled node, Velopack installer, updates, notice feed, licence dialog | done, commit after `c615d39` |
| M8 | Release 3.0.0 | done. 3.0.0 could not open a window; **3.0.5** was the first published release and **3.0.6** the first that reached anyone by updating itself |

## 8. Open items

- **The installed copy was installed from the agent session, so it lives in a sandbox.**
  Its desktop shortcut points into `Packages\Claude_*\LocalCache` and shows no icon. It
  needs uninstalling from Windows Settings and reinstalling by double-clicking the Setup
  file in Explorer, which is the only way to get real paths, working shortcuts and a
  correct icon. Check the tournaments survived afterwards; if not, the data is under
  `Packages\Claude_*\LocalCache\Roaming\RovOverlayTool3` and copies straight across.
- **3.0.5 is published.** Released 2026-09-12 as `v3.0.5` on
  `LazyAF-zZzZ/rov_overlay_v3`, public, five assets, with `releases.win.json` offering
  3.0.5 Full. That is the first thing anyone outside this machine can install.
- **An update has now gone from one version to the next on its own.** The installed 3.0.4
  found 3.0.5 in the feed, downloaded it into `packages/`, and `current/` became 3.0.5
  without anyone running Setup. What is *not* verified is the operator's view of it: the
  updated app restarts outside the agent session's sandbox, so its API stops being
  reachable from here and the window is the only thing left to read.
- **3.0.7 was published twice, replacing itself.** The first `v3.0.7` (the grey filter
  alone) was deleted with `gh release delete v3.0.7 --cleanup-tag` and packed again from
  `d1c607f`, which added the guide's v2-import section and the `candidatePaths()` fix.
  Two things this depends on: the local `releases/` 3.0.7 nupkgs must be deleted first or
  the delta is built against 3.0.7 rather than 3.0.6, and **anyone who already downloaded
  the first 3.0.7 never receives the second** — same version number, so the updater has
  nothing to offer them. One `-full.nupkg` download had already happened. Replacing a
  version in place is only safe in the first minutes after publishing, before a notice
  goes out; otherwise cut the next number.
- **3.0.7 shipped without the smoke test, because the operator's own app was running.**
  `scripts\smoke.ps1` refuses to start a second copy (the single-instance mutex would
  make it show "already open" and exit, which is not what it tests), and closing the
  user's live app to satisfy it was not something to do unasked. 3.0.7 changes only
  `overlay.css`, `overlay.js`, the guide and the version number — no C# at all — and
  3.0.6 was the build running on screen at the time. **That reasoning does not
  generalise**: any release that touches `desktop/` must wait for the app to be closed
  and be smoke-tested for real.
- **Third place exists only for single elimination.** The knockout stage drawn after a
  group stage is the same shape with the same need; `addThirdPlace()` drops straight into
  that path when someone asks for it.
- **A notice has been delivered; dismissing one has not been checked.** A test entry was
  pushed to `notices.json` on 2026-09-12 and reached the installed 3.0.4 on its next
  start: the bell showed a count of one, photographed from the running app. What is still
  untried is pressing "Got it" and confirming it stays gone after a restart, which is the
  half that writes to `settings.json`.
- **Anything in its own window cannot be checked by a render.** `--snapshot` draws the
  main window's content with `RenderTargetBitmap`, and a `Popup` or a modal `Window` is a
  separate HWND it never sees. So the notice panel, the OBS source list, the confirm box
  and the **first-run licence dialog** are verified by their markup, their strings and
  their data, never by a screenshot. The licence dialog is the first thing a new user
  meets, so look at it by hand at least once.
- **No v2 data exists on this machine to import.** `%APPDATA%\ROV Overlay Tool` does not
  exist and the database in the v2 repo has zero rows, so the importer was proved
  against a synthetic v2 install instead. Run it once against a real one.
- **Click through M2 and M3.** Create a tournament and a team, edit a roster
  inline, upload and clear a logo, remove a team, delete a tournament, save a backup and
  restore it; then run a real draft: type heroes, Enter to confirm, the timer, the
  shortcuts, swap, undo. Rendering is verified; these flows are not. **There is UI
  automation now**: `scripts/uia.ps1` presses buttons by their text, reads fields back and
  captures windows and dialogs, against a snapshot build left open with a long
  `--snapshot-delay` beside a throwaway backend. It has only been pointed at the game-over
  flow so far, and on its first run it found a real bug there (the match title field
  frozen after Put on air). The flows above are the next thing to point it at.
- **The Control Panel's shortcuts only work while the app has focus.** System-wide
  hotkeys are M5 (Win32 `RegisterHotKey`), as v2 had through Electron.
- **Sound effects still need an overlay page open to be heard on air** (§8, unchanged by
  M3). The Control Panel's TEST button plays locally only.
- **Standings ignore the "teams through" box until it is a valid 1-8**; an invalid value
  keeps the last good one. Fine, but it shows no error.
- **Sound effects.** In v2 the Electron menu opened the overlay in a window so its
  `?sfx=1` audio played. v3 has no such window. Decide in M3: an OBS browser source
  with "Control audio via OBS", or a hidden WebView2 player.
- **Code signing.** An unsigned installer gets a SmartScreen warning.
- **`CONTROL_TOKEN`** is not used by v3 (the server binds 127.0.0.1 only). Revisit if
  the server is ever exposed on the LAN.

## 9. Traps already paid for

- **Do not run anything in `../rov_pickban_overlay`.** Its `npm start` rebuilds its
  `build/` and opens its `data/tournament.db`.
- **To UI Automation, a window with an Owner is not a top-level window.** The confirm
  dialog (`Owner` = the main window) is listed *underneath* the main window, not among the
  desktop's children. Searching the desktop's children for it finds nothing, which reads
  exactly like "the button never opened a dialog". Look for `ControlType.Window`
  descendants of the main window instead (`scripts/uia.ps1` does).
- **PowerShell variable names ignore case.** A function parameter named `$scope` hid the
  script's `$Scope` (the `TreeScope` type) inside that one function, so
  `$Scope::Descendants` became null there and worked everywhere else.
- **Icon glyphs as raw characters vanish or get mangled.** Perl's and sed's `\u` in a
  replacement means "uppercase the next character", which turned `\uE80F` into `E80F`.
  Write escapes by hand, or rewrite with Node.
- **WPF's implicit usings leave out `System.IO`** (it clashes with `Shapes.Path`), so
  `MemoryStream` and `File` need `using System.IO;`.
- **The TextBox template must not set scrollbar visibility** on `PART_ContentHost`, or
  no TextBox (the log box included) can ever scroll.
- **A closed ComboBox shows the selected item's `ToString()`, not its
  `DisplayMemberPath`**, with our own ComboBox template. The position picker showed
  `RovOverlay.Desktop.ViewModels.PositionChoice`. Every choice type overrides
  `ToString()` to return its label; do the same for any new one.
- **`vpk` builds its feed from whatever is sitting in `releases/`.** A rehearsal build
  left in that folder goes out with the real one, and users are offered a version nobody
  meant to ship. Clear the folder before packing a release, and accept that the first
  release therefore has no delta to build against.
- **Never run the installer from inside the agent session.** That session is sandboxed:
  writes to `%LOCALAPPDATA%` and `%APPDATA%` are redirected into
  `AppData\Local\Packages\Claude_*\LocalCache\`, so Setup records container paths in the
  shortcuts it creates. The desktop shortcut ends up with a target inside the container
  and an icon path outside it, which Explorer draws as a blank page. Worse, the session
  cannot detect any of this: reads fall through, so both paths look identical and equally
  present from in here. Build the installer here; let the user double-click it.
- **Backslashes disappear when a script is written through a shell heredoc.** The command
  text is JSON-encoded before the shell sees it, so `\\` arrives as a single `\`, and
  JavaScript then reads the `\s` and `\p` of `.\scripts\pack.ps1` as plain letters. This
  file twice ended up telling the reader to run `.scriptspack.ps1`. Write anything
  containing Windows paths with the editing tool instead, or build the character with
  `String.fromCharCode(92)` as the glyph fixer does.
- **A window shown before `Application.Run()` pumps messages is never created at all.**
  `OnStartup` is raised inside `Run()` but *before* the message loop starts. The licence
  dialog was asked for there, and being `WindowStyle=None`, `ShowInTaskbar=False` and
  `CenterOwner` with no owner yet, it never materialised: `ShowDialog()` waited for an
  answer from a window that did not exist. 3.0.0 installed, started, and sat as a healthy
  process with no window. Show the main window first and let anything modal own it.
- **A handler that sets `Handled = true` can hide the failure completely.** The startup
  exception path showed a toast, and a toast needs a window. Before the window exists,
  write the failure to `%APPDATA%\RovOverlayTool3\startup-error.log`, show a plain
  `MessageBox` (no `Loc` — it may be what broke) and stop.
- **`--snapshot` proves a screen renders, not that the app starts.** It deliberately skips
  the first-run licence, so the one path every new user takes was the one path never run
  in seven milestones of verification. `scripts/smoke.ps1` launches the built app and
  fails unless a real visible window appears; `-FreshLicence` does it as a new user.
  Run it before any release.
- **`execFileSync` blocks Node's event loop**, so a server in the same script cannot
  answer while a child process runs. A local notice feed served that way looked exactly
  like a broken notice service: the app's request went unanswered until its own 15-second
  timeout. Use `spawn` and await the exit.
- **Velopack has to run before WPF opens anything.** WPF generates its own `Main` from
  App.xaml, so ours lives in `Program.cs` and `<StartupObject>` picks it. `vpk pack`
  checks this really happened: "Verified VelopackApp.Run() in ... Program::Main".
- **A v2 database can be in WAL mode**, and its newest rows live in the `-wal` file.
  Copy the database and its `-wal`/`-shm` aside and open the copy: opening v2's own
  file would replay the log and write to the folder we promised never to touch.
- **A `Style` attribute plus a `<TextBlock.Style>` element on the same control is a
  compile error, not a merge.** Put `BasedOn` inside the inline style instead.
- **`PathFigure` / `PolyLineSegment` do not take bindings** the way a normal element
  does; the bracket connectors are `Polyline`s bound to a `PointCollection`.
- **`BackendHost.BackendDir` must be found even when the app only attaches** to a
  server someone else started, or screens that read files shipped with the backend
  (the Guide) come up empty in development.
- **Setting `DataContext` on an element that also binds through the outer one blanks the
  field silently.** `DataContext="{Binding Player}"` next to `Text="{Binding Player.Name}"`
  resolves as `Player.Player.Name`: no error, just an empty box. Set the DataContext, then
  use plain property names.
- **A second ItemsControl pulled over the first with a negative margin does not line
  up.** Picks were drawn that way at first and simply never appeared. One list whose rows
  carry everything in the row is the fix.
- **Pages opened on top (tournament, team) must unsubscribe** from
  `AppServices.DataChanged` and `Loc.Changed` in `IClosablePage.OnClosed`, or every page
  ever opened keeps reloading itself for the rest of the session.
- **Rows are updated in place by id, never rebuilt**, when a change is pushed from
  elsewhere: rebuilding would throw away an inline editor someone is typing in (the same
  rule as v2's `deferWhileEditing`).
- **`RenderTargetBitmap` renders nothing behind the content**, so the window's root
  border carries the background brush itself, or snapshots come out transparent.
- **The job object kills the backend at once if the app crashes**, before
  `lifecycle.ts` can flush. A normal close is graceful; a crash can lose the last
  150 ms of state. Accepted: the alternative is a server squatting on port 3000.
- **Backend changes from v2:** `package.json` (Electron removed, version 3.0.0-dev),
  `server.js` (lifecycle hook), `server/index.ts` (app-info route),
  `server/store/live-state.ts` (`flushState`), `tests/media.test.ts` (its installer
  guard now reads `scripts/pack.ps1` instead of electron-builder), plus
  the new files named in §6. Everything else is byte-for-byte v2.
