# ROV Overlay Tool v3

Native Windows operator app (WPF, .NET 10) in front of v2's Node backend, which still
serves the HTML overlays to OBS. The design, decisions, status and traps are in
`docs/PLAN.md`. Read it before starting work.

## Hard rule

**Never modify, build or run anything in `../rov_pickban_overlay`** (v2). Read from it
only. Even `npm start` there changes its `build/` and opens its database.

## Standing rule: keep the plan current

Every change that moves a milestone updates `docs/PLAN.md` in the same change: §0
status, §7 milestone table, §8 open items (remove what is done, add what the work
exposed), §9 traps (anything that cost real debugging time). A stale plan is worse
than none.

## Commands

Backend (`backend/`):

```bash
npm run build
npm test
npm run dev
```

Desktop (`desktop/`). `dotnet` lives at `C:\Program Files\dotnet\dotnet.exe` and may not
be on PATH in an old shell:

```bash
dotnet build
dotnet run --project RovOverlay.Desktop
```

The desktop app needs the backend built first (it runs `backend/build/server`). Render
a screen without a person at the keyboard:

```bash
RovOverlay.Desktop/bin/Debug/net10.0-windows/RovOverlayTool.exe --page Home --lang en --snapshot out.png
```

## Where things go

- New operator screens: native, in `desktop/` (see `docs/PLAN.md` §3 for the recipe).
- New data or rules: the backend, with a test, exactly as in v2. The desktop app never
  holds tournament logic of its own.
- `backend/CLAUDE.md` is v2's rulebook. Its rules about the server, the data model and
  the overlays still hold. Its parts about Electron and the HTML operator pages
  describe the pages v3 is replacing.
