// การ์ดทีม: สถิติ ฮีโร่ และผู้เล่นของทีมเดียว สำหรับช่วงพักก่อนเริ่มคู่หรือระหว่างเกม
//
// ตัวเลขมาจาก /api/team-card ซึ่งใช้ teamStats.forTeam ตัวเดียวกับแท็บสถิติในหน้าทีม
//
// พารามิเตอร์ที่รับ (ต่อท้าย URL ใน OBS ได้เลย):
//   ?side=blue|red          ทีมที่อยู่ฝั่งนั้นของคู่ที่ออกอากาศตอนนี้ ไม่ใส่ = blue
//   ?team=<teamId>          ระบุทีมเอง ไม่ตามฝั่ง
//   ?tournament=<id>|all    ขอบเขตที่นับ ไม่ใส่ = รายการของคู่ที่ออกอากาศ
//   ?top=1..8               จำนวนฮีโร่ในแถว ไม่ใส่ = 7
//   ?title= / ?subtitle=    เปลี่ยนข้อความหัว
//   ?refresh=<วินาที>       ดึงข้อมูลใหม่เป็นระยะ
//
// หน้านี้เป็นหน้าดูอย่างเดียว ต่อ socket เปล่าๆ ไม่ต้องมีโทเคน และไม่เข้าห้อง dataChanged
// (กฎของกราฟิกออกอากาศ ดู CLAUDE.md) ตัวเลขใหม่มาตอนตัดเข้าซีนด้วย
// "Refresh browser when scene becomes active" หรือ ?refresh=

const params = new URLSearchParams(window.location.search);

// overlay-size.js อ่านตัวแปรชื่อ socket ตัวนี้ ต้องประกาศก่อนไฟล์นั้นถูกโหลด
const socket = io();

// การ์ดสุดท้ายเข้ามาที่ 440ms + 520ms
const ENTER_MS = 960;
const SAFETY_MS = 900;

let settleTimer = null;
let lastSignature = null;

const refreshSeconds = window.RovOverlay.intParam(params, 'refresh', 0, 5, 3600);
// ชื่อ top ใช้ไม่ได้: สคริปต์แบบคลาสสิกอยู่บน global scope เดียวกับ window.top
const topCount = window.RovOverlay.intParam(params, 'top', 7, 1, 8);
const fixedTeam = params.get('team') || '';
const side = params.get('side') === 'red' ? 'red' : 'blue';

// จำนวนเต็มเปอร์เซ็นต์ ไม่มีทศนิยม ขึ้นจอให้อ่านจากไกลได้
// null = ยังไม่มีเกมที่รู้ผล ต่างจาก 0% ซึ่งแปลว่าแพ้หมด
function percent(won, played) {
    return played > 0 ? `${Math.round((won * 100) / played)}%` : null;
}

function el(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
}

function tile(label, value, detail, extraClass) {
    const box = el('div', extraClass ? `tc-tile ${extraClass}` : 'tc-tile');
    box.append(el('div', 'tc-tile-label', label), el('div', 'tc-tile-value', value));
    box.append(el('div', detail ? 'tc-tile-detail' : 'tc-tile-detail none', detail || 'No results yet'));
    return box;
}

function recordTile(label, won, lost, extraClass) {
    const rate = percent(won, won + lost);
    return tile(label, `${won} – ${lost}`, rate ? `${rate} won` : null, extraClass);
}

function formTile(form) {
    const box = el('div', 'tc-tile');
    box.append(el('div', 'tc-tile-label', 'Last 5 series'));
    if (!form || form.length === 0) {
        box.append(el('div', 'tc-tile-value', '—'), el('div', 'tc-tile-detail none', 'No finished series'));
        return box;
    }
    const chips = el('div', 'tc-form');
    form.forEach((outcome) => {
        chips.append(el('div', outcome === 'win' ? 'tc-chip' : 'tc-chip loss', outcome === 'win' ? 'W' : 'L'));
    });
    box.append(chips);
    return box;
}

function heroCell(hero, count, stat) {
    const cell = el('div', 'mu-hero');
    const art = el('div', 'mu-art');
    // ไอคอนก่อน ถอยไปรูปเต็มถ้าไม่มี (ดู lib/hero-art.js)
    window.RovHeroArt.paint(art, hero);
    cell.appendChild(art);

    // ป้ายจำนวนอยู่ในกรอบรูป ไม่ใช่ในช่อง: ช่องนี้มีอัตราชนะต่อใต้รูป
    // วัดมาแล้ว ป้ายที่ยึดขอบล่างของช่องไปตกอยู่ใต้รูป ข้างตัวเลขอัตราชนะ อ่านเป็นตัวเลขเดียวกัน
    if (count > 1) art.appendChild(el('div', 'mu-count', `x${count}`));

    // อัตราชนะใต้รูป เฉพาะแถวที่หยิบ แถวแบนไม่มีผลแพ้ชนะของฮีโร่ตัวนั้น
    if (stat) {
        const rate = percent(stat.wins, stat.decided);
        const low = rate !== null && stat.wins * 2 < stat.decided;
        cell.appendChild(el('div', rate === null ? 'tc-rate none' : low ? 'tc-rate low' : 'tc-rate', rate || '—'));
    }
    return cell;
}

function heroRow(list, pick) {
    const box = el('div', 'mu-heroes');
    if (list.length === 0) {
        box.appendChild(el('div', 'mu-empty', 'No games on record'));
        return box;
    }
    list.slice(0, topCount).forEach((stat) => {
        box.appendChild(pick ? heroCell(stat.hero, stat.picked, stat) : heroCell(stat.hero, stat.banned, null));
    });
    return box;
}

function heroesCard(stats) {
    const card = el('div', 'mu-side tc-card');
    card.append(
        el('div', 'mu-label', 'Most picked · win rate'),
        heroRow(stats.picks || [], true),
        // "โดนแบนใส่" ไม่ใช่ "แบน": คือตัวที่คู่แข่งกลัวทีมนี้ ซึ่งคนพากย์พูดถึงก่อนเริ่มคู่
        el('div', 'mu-label', 'Banned against them'),
        heroRow(stats.bansAgainst || [], false)
    );
    return card;
}

function playersCard(stats) {
    const card = el('div', 'mu-side tc-card');
    card.appendChild(el('div', 'mu-label', 'Players · favourite heroes'));

    const players = stats.players || [];
    if (players.length === 0) {
        // ฮีโร่ของผู้เล่นนับตั้งแต่ 3.0.13 เกมก่อนหน้านั้นไม่ได้บันทึกว่าใครนั่งแถวไหน
        card.appendChild(el('div', 'mu-empty', 'No player data yet'));
        return card;
    }

    players.slice(0, 5).forEach((player) => {
        const row = el('div', 'tc-player');
        const who = el('div', 'tc-player-who');
        who.append(el('div', 'tc-player-name', player.name), el('div', 'tc-player-games', `${player.games} games`));
        const heroes = el('div', 'tc-player-heroes');
        (player.heroes || []).slice(0, 4).forEach((stat) => heroes.appendChild(heroCell(stat.hero, stat.picked, null)));
        row.append(who, heroes);
        card.appendChild(row);
    });
    return card;
}

function logoNode(team) {
    if (team.logo && team.logo.v && team.logo.ext) {
        const img = document.createElement('img');
        img.alt = '';
        img.src = `/images/team-logos/${encodeURIComponent(team.id)}.${team.logo.ext}?v=${team.logo.v}`;
        return img;
    }
    return document.createTextNode((team.tag || team.name || '?').slice(0, 4).toUpperCase());
}

// การันตีว่าการ์ดจะถูกมองเห็น ต่อให้อนิเมชันไม่เคยเริ่มหรือไม่เคยจบ
// OBS หยุด browser source ที่ไม่ได้อยู่ในฉากที่ออกอากาศ
function settleSoon() {
    clearTimeout(settleTimer);
    settleTimer = setTimeout(() => {
        document.getElementById('stage').classList.add('settled');
    }, ENTER_MS + SAFETY_MS);
}

// โหลดไม่ได้ต้องล้างการ์ดเก่าทิ้ง ห้ามปล่อยทีมเดิมค้างไว้ใต้ข้อความ
// ฝั่งที่เปลี่ยนเป็นทีมพิมพ์ชื่อเองแล้วยังขึ้นการ์ดของทีมก่อนหน้า อ่านบนจอเหมือนข้อมูลจริง
function clearCard(message) {
    lastSignature = null;
    ['logo', 'sideLabel', 'name', 'subtitle', 'tiles', 'cols'].forEach((id) => {
        document.getElementById(id).textContent = '';
    });
    document.getElementById('stage').dataset.empty = 'true';
    window.RovOverlay.note(message);
    settleSoon();
}

function render(data) {
    const stats = data.stats;
    const record = stats.record;

    // ข้อความแจ้งเตือนต้องตัดสินใหม่ทุกครั้งที่โหลดสำเร็จ อยู่เหนือด่านลายเซ็น
    // (บทเรียนเดียวกับ overlay-team-drafts.js: ข้อความเก่าค้างทั้งที่ข้อมูลมาครบแล้ว)
    const nothing = stats.draftedGames === 0 && record.seriesWon + record.seriesLost === 0;
    window.RovOverlay.note(nothing ? 'No games on record for this team yet.' : '');

    // วาดใหม่เฉพาะตอนข้อมูลเปลี่ยนจริง ไม่งั้น ?refresh= จะเล่นอนิเมชันเข้าซ้ำเรื่อยๆ
    const signature = JSON.stringify(data);
    if (signature === lastSignature) return;
    lastSignature = signature;

    const stage = document.getElementById('stage');
    stage.classList.remove('settled');
    delete stage.dataset.empty;
    // สีประจำการ์ด: ฝั่งที่ตามอยู่ หรือสีทองถ้าระบุทีมเอง (ทองคือสีของ "สิ่งที่กำลังเกิดขึ้น")
    stage.dataset.side = data.side || 'team';

    const logo = document.getElementById('logo');
    logo.textContent = '';
    logo.appendChild(logoNode(data.team));

    document.getElementById('sideLabel').textContent = data.side ? `${data.side} side` : '';
    document.getElementById('name').textContent = params.get('title') || data.team.name;
    document.getElementById('subtitle').textContent =
        params.get('subtitle') || (data.tournament ? data.tournament.name : 'All tournaments');
    document.title = `${data.team.name} - ROV Overlay`;

    const tiles = document.getElementById('tiles');
    tiles.textContent = '';
    tiles.append(
        recordTile('Series', record.seriesWon, record.seriesLost),
        recordTile('Games', record.gamesWon, record.gamesLost),
        recordTile('On blue side', stats.sides.blue.won, stats.sides.blue.played - stats.sides.blue.won, 'blue'),
        recordTile('On red side', stats.sides.red.won, stats.sides.red.played - stats.sides.red.won, 'red'),
        formTile(record.form)
    );

    const cols = document.getElementById('cols');
    cols.textContent = '';
    cols.append(heroesCard(stats), playersCard(stats));

    settleSoon();
}

async function load() {
    const query = [fixedTeam ? `team=${encodeURIComponent(fixedTeam)}` : `side=${side}`];
    if (params.get('tournament')) query.push(`tournament=${encodeURIComponent(params.get('tournament'))}`);

    try {
        const response = await fetch(`/api/team-card?${query.join('&')}`);
        if (!response.ok) {
            clearCard(fixedTeam
                ? 'Team not found.'
                : `No team from the registry is on the ${side} side yet.`);
            return;
        }
        render(await response.json());
    } catch (error) {
        clearCard('Could not load the team card.');
    }
}

if (fixedTeam) {
    load();
} else {
    // ตามฝั่ง: โหลดใหม่เมื่อทีมที่อยู่ฝั่งนี้เปลี่ยน (เปลี่ยนคู่ หรือสลับฝั่งหลังจบเกม)
    // ไม่โหลดทุก stateUpdate เพราะมันมาทุกครั้งที่ใครพิมพ์ฮีโร่ระหว่างดราฟต์
    // id ของทีมอยู่ใน logo.src ซึ่ง goLive และการเลือกทีมจากทะเบียนใส่ไว้
    let followed;
    socket.on('stateUpdate', (state) => {
        const team = state && state[side === 'red' ? 'teamRed' : 'teamBlue'];
        const id = team && team.logo && team.logo.src ? String(team.logo.src) : '';
        if (id === followed) return;
        followed = id;
        load();
    });
    // socket ไม่มา (เซิร์ฟเวอร์เก่า หรือเชื่อมช้า) ก็ยังต้องมีการ์ดขึ้น
    setTimeout(() => { if (followed === undefined) load(); }, 1500);
}

if (refreshSeconds > 0) setInterval(load, refreshSeconds * 1000);
