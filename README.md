# ROV Overlay Tool v3

Draft pick/ban overlays and tournament management for Arena of Valor (ROV), for OBS.

v3 rebuilds the operator app as a native Windows program. The overlays OBS shows are
the same HTML pages as v2, served by the same backend, so existing scenes keep working.

- `desktop/`: the operator app (WPF, .NET 10)
- `backend/`: server, overlays and tests (Node)
- `docs/PLAN.md`: design, status and roadmap

Status: early development. See `docs/PLAN.md` §0.

## Licence

Free for tournaments, community streams, school events and personal use. Selling,
reselling, renting or bundling it into anything paid is not allowed, and neither is
passing it off as your own work. See [LICENSE.md](LICENSE.md).

v2, the released version people use today, is at
[rov_pickban_overlay](https://github.com/LazyAF-zZzZ/rov_pickban_overlay).
