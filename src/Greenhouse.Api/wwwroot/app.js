// Дашборд: картки датчиків з графіками, журнал тривог, останні виміри.
// Живі оновлення через SignalR (/hubs/telemetry); якщо він недоступний — опитування API.
"use strict";

const POLL_MS = 3000;
const HISTORY = 60; // скільки останніх точок показувати на графіку
const RECENT_ROWS = 10; // скільки рядків у таблиці «Останні виміри»
const ALERT_ROWS = 8;

const LABELS = { temperature: "Температура", humidity: "Вологість повітря", light: "Освітленість" };
const STATUS_TEXT = { ok: "Норма", low: "Низько", high: "Високо", no_data: "Немає даних" };
const ALERT_TEXT = { low: "Низько", high: "Високо" };

const $ = sel => document.querySelector(sel);
const cardsEl = $("#cards");
const connEl = $("#conn");
const pickerEl = $("#greenhouse");
const recentBody = $("#recent tbody");
const alertsBody = $("#alerts tbody");
const loginForm = $("#login");
const whoEl = $("#who");
const logoutBtn = $("#logout");

let greenhouseId = null;
let cards = {}; // sensor name -> { el, sensor, points }
let session = null; // { access_token, refresh_token, username, role } — лише в пам'яті сторінки
let pollTimer = null;
let refreshQueued = false;

// ---------- HTTP ----------

async function api(url, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (options.body) headers["Content-Type"] = "application/json";
  if (session) headers.Authorization = `Bearer ${session.access_token}`;
  let res = await fetch(url, { ...options, headers });
  if (res.status === 401 && session && await renewSession()) {
    headers.Authorization = `Bearer ${session.access_token}`;
    res = await fetch(url, { ...options, headers });
  }
  if (!res.ok) throw new Error(`${url}: ${res.status}`);
  return res.status === 204 ? null : res.json();
}

async function renewSession() {
  const res = await fetch("/api/auth/refresh", {
    method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refresh_token: session.refresh_token }),
  });
  if (!res.ok) { setSession(null); return false; }
  setSession(await res.json());
  return true;
}

// ---------- Форматування ----------

function fmt(value, unit) {
  if (value === null || value === undefined) return "—";
  const digits = unit === "lx" ? 0 : 1;
  return value.toLocaleString("uk-UA", { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

const timeOf = iso => (iso ? iso.slice(11, 19) : "");
const dateTime = iso => iso.replace("T", " ").slice(0, 19);
const canOperate = () => session && (session.role === "operator" || session.role === "admin");

function cell(tr, text, cls) {
  const td = document.createElement("td");
  td.textContent = text;
  if (cls) td.className = cls;
  tr.appendChild(td);
  return td;
}

// ---------- Картки і графіки ----------

function createCard(sensor) {
  const el = document.createElement("article");
  el.className = "card";
  el.innerHTML = `
    <div class="label"></div>
    <div><span class="value">—</span><span class="unit"></span></div>
    <div class="range"></div>
    <form class="threshold" hidden>
      <input name="min" type="number" step="any" aria-label="Мінімум норми" required>
      <span>–</span>
      <input name="max" type="number" step="any" aria-label="Максимум норми" required>
      <button type="submit">Зберегти норму</button>
    </form>
    <span class="badge no_data">${STATUS_TEXT.no_data}</span>
    <svg class="chart" role="img"></svg>
    <div class="tooltip" hidden></div>`;
  el.querySelector(".label").textContent = `${LABELS[sensor.kind] || sensor.kind} · ${sensor.name}`;
  el.querySelector(".unit").textContent = sensor.unit;
  el.querySelector("svg").setAttribute("aria-label", `Графік: ${LABELS[sensor.kind] || sensor.kind}`);
  const form = el.querySelector(".threshold");
  form.addEventListener("submit", async ev => {
    ev.preventDefault();
    const data = new FormData(form);
    try {
      await api(`/api/greenhouses/${greenhouseId}/thresholds/${sensor.kind}`, {
        method: "PUT",
        body: JSON.stringify({ comfort_min: Number(data.get("min")), comfort_max: Number(data.get("max")) }),
      });
      await loadGreenhouse();
    } catch (err) {
      connEl.textContent = "Норму не збережено: перевірте значення";
      console.error(err);
    }
  });
  cardsEl.appendChild(el);
  cards[sensor.name] = { el, sensor, points: [] };
  renderRange(cards[sensor.name]);
}

function renderRange(card) {
  const { sensor, el } = card;
  el.querySelector(".range").textContent =
    `Норма: ${fmt(sensor.comfort_min, sensor.unit)}–${fmt(sensor.comfort_max, sensor.unit)} ${sensor.unit}`;
  const form = el.querySelector(".threshold");
  form.hidden = !canOperate();
  form.elements.min.value = sensor.comfort_min;
  form.elements.max.value = sensor.comfort_max;
}

function drawChart(card) {
  const svg = card.el.querySelector("svg");
  const { sensor } = card;
  const pts = card.points; // старіші першими
  const W = svg.clientWidth || 300, H = 120, padL = 4, padR = 4, padT = 6, padB = 18;
  svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
  if (pts.length === 0) { svg.innerHTML = ""; return; }

  let lo = Math.min(sensor.comfort_min, ...pts.map(p => p.value));
  let hi = Math.max(sensor.comfort_max, ...pts.map(p => p.value));
  const pad = (hi - lo) * 0.1 || 1;
  lo -= pad; hi += pad;
  const x = i => padL + (pts.length === 1 ? 0 : i * (W - padL - padR) / (pts.length - 1));
  const y = v => padT + (hi - v) * (H - padT - padB) / (hi - lo);

  const bandTop = y(sensor.comfort_max), bandBot = y(sensor.comfort_min);
  const path = pts.map((p, i) => `${i ? "L" : "M"}${x(i).toFixed(1)},${y(p.value).toFixed(1)}`).join("");
  svg.innerHTML = `
    <rect class="band" x="0" y="${bandTop}" width="${W}" height="${bandBot - bandTop}"></rect>
    <line class="grid" x1="0" x2="${W}" y1="${H - padB}" y2="${H - padB}"></line>
    <path class="line" d="${path}"></path>
    <text class="axis-label" x="${padL}" y="${H - 4}">${timeOf(pts[0].recorded_at)}</text>
    <text class="axis-label" x="${W - padR}" y="${H - 4}" text-anchor="end">${timeOf(pts[pts.length - 1].recorded_at)}</text>
    <line class="cross" y1="${padT}" y2="${H - padB}" visibility="hidden"></line>
    <circle class="dot" r="4" visibility="hidden"></circle>
    <rect x="0" y="0" width="${W}" height="${H}" fill="transparent"></rect>`;

  const cross = svg.querySelector(".cross"), dot = svg.querySelector(".dot");
  const tip = card.el.querySelector(".tooltip");
  const hit = svg.lastElementChild;
  hit.addEventListener("mousemove", ev => {
    const r = svg.getBoundingClientRect();
    const px = (ev.clientX - r.left) * W / r.width;
    const i = Math.max(0, Math.min(pts.length - 1, Math.round((px - padL) / ((W - padL - padR) / Math.max(1, pts.length - 1)))));
    const p = pts[i];
    cross.setAttribute("x1", x(i)); cross.setAttribute("x2", x(i)); cross.setAttribute("visibility", "visible");
    dot.setAttribute("cx", x(i)); dot.setAttribute("cy", y(p.value)); dot.setAttribute("visibility", "visible");
    tip.textContent = `${timeOf(p.recorded_at)} · ${fmt(p.value, sensor.unit)} ${sensor.unit}`;
    tip.hidden = false;
    const cardR = card.el.getBoundingClientRect();
    tip.style.left = `${Math.min(ev.clientX - cardR.left + 12, cardR.width - tip.offsetWidth - 8)}px`;
    tip.style.top = `${r.top - cardR.top - 4}px`;
  });
  hit.addEventListener("mouseleave", () => {
    cross.setAttribute("visibility", "hidden"); dot.setAttribute("visibility", "hidden"); tip.hidden = true;
  });
}

// ---------- Таблиці ----------

function renderHeader() {
  const tr = $("#recent thead tr");
  tr.innerHTML = "<th>Час (UTC)</th>";
  for (const { sensor } of Object.values(cards)) {
    const th = document.createElement("th");
    th.className = "num";
    th.textContent = `${LABELS[sensor.kind] || sensor.kind}, ${sensor.unit}`;
    tr.appendChild(th);
  }
}

function renderRecent(recent) {
  const byTime = new Map(); // recorded_at -> { sensor name -> value }, новіші першими
  for (const r of recent) {
    if (!byTime.has(r.recorded_at)) byTime.set(r.recorded_at, {});
    byTime.get(r.recorded_at)[r.sensor] = r.value;
  }
  recentBody.innerHTML = "";
  for (const [time, values] of [...byTime].slice(0, RECENT_ROWS)) {
    const tr = document.createElement("tr");
    cell(tr, dateTime(time));
    for (const { sensor } of Object.values(cards)) cell(tr, fmt(values[sensor.name], sensor.unit), "num");
    recentBody.appendChild(tr);
  }
}

function renderAlerts(alerts) {
  alertsBody.innerHTML = "";
  if (alerts.length === 0) {
    const tr = document.createElement("tr");
    cell(tr, "Тривог немає", "muted").colSpan = 6;
    alertsBody.appendChild(tr);
    return;
  }
  for (const a of alerts) {
    const tr = document.createElement("tr");
    const unit = cards[a.sensor]?.sensor.unit ?? "";
    cell(tr, dateTime(a.opened_at));
    cell(tr, a.sensor);
    cell(tr, ALERT_TEXT[a.kind] ?? a.kind);
    cell(tr, `${fmt(a.trigger_value, unit)} ${unit}`, "num");
    const state = a.closed_at ? "Закрита" : "Активна";
    cell(tr, a.acknowledged_by ? `${state}, підтвердив ${a.acknowledged_by}` : state);
    const actions = cell(tr, "");
    if (canOperate() && !a.acknowledged_at) {
      const btn = document.createElement("button");
      btn.textContent = "Підтвердити";
      btn.addEventListener("click", async () => {
        try { await api(`/api/alerts/${a.id}/ack`, { method: "POST" }); } catch (err) { console.error(err); }
        await refresh();
      });
      actions.appendChild(btn);
    }
    alertsBody.appendChild(tr);
  }
}

// ---------- Дані ----------

async function refresh() {
  if (greenhouseId === null) return;
  try {
    const q = `greenhouse_id=${greenhouseId}`;
    const [latest, recent, alerts] = await Promise.all([
      api(`/api/readings/latest?${q}`),
      api(`/api/readings?${q}&limit=${(RECENT_ROWS + 1) * Math.max(1, Object.keys(cards).length)}`),
      api(`/api/alerts?${q}&limit=${ALERT_ROWS}`),
    ]);
    await Promise.all(latest.map(async row => {
      const card = cards[row.sensor];
      if (!card) return;
      card.el.querySelector(".value").textContent = fmt(row.value, row.unit);
      const badge = card.el.querySelector(".badge");
      badge.className = `badge ${row.status}`;
      badge.textContent = STATUS_TEXT[row.status];
      const history = await api(`/api/readings?sensor=${encodeURIComponent(row.sensor)}&limit=${HISTORY}`);
      card.points = history.reverse();
      drawChart(card);
    }));
    renderRecent(recent);
    renderAlerts(alerts);
    connEl.textContent = `Оновлено ${new Date().toLocaleTimeString("uk-UA")}${hub?.state === "Connected" ? " · наживо" : ""}`;
  } catch (err) {
    connEl.textContent = "Немає зв'язку з бекендом";
    console.error(err);
  }
}

// Події SignalR приходять пачками; щоб не перемальовувати сторінку на кожну, об'єднуємо їх.
function queueRefresh() {
  if (refreshQueued) return;
  refreshQueued = true;
  setTimeout(() => { refreshQueued = false; refresh(); }, 300);
}

async function loadGreenhouse() {
  const sensors = await api(`/api/sensors?greenhouse_id=${greenhouseId}`);
  cardsEl.innerHTML = "";
  cards = {};
  sensors.forEach(createCard);
  renderHeader();
  await refresh();
}

async function loadGreenhouses() {
  const list = await api("/api/greenhouses");
  pickerEl.innerHTML = "";
  for (const g of list) {
    const opt = document.createElement("option");
    opt.value = g.id;
    opt.textContent = g.location ? `${g.name} (${g.location})` : g.name;
    pickerEl.appendChild(opt);
  }
  greenhouseId = list.length ? list[0].id : null;
}

pickerEl.addEventListener("change", async () => {
  greenhouseId = Number(pickerEl.value);
  await subscribe();
  await loadGreenhouse();
});

// ---------- Вхід ----------

function setSession(value) {
  session = value;
  loginForm.hidden = Boolean(value);
  logoutBtn.hidden = !value;
  whoEl.hidden = !value;
  whoEl.textContent = value ? `${value.username} (${value.role})` : "";
  Object.values(cards).forEach(renderRange);
}

loginForm.addEventListener("submit", async ev => {
  ev.preventDefault();
  const data = Object.fromEntries(new FormData(loginForm));
  const res = await fetch("/api/auth/login", {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(data),
  });
  if (!res.ok) {
    whoEl.hidden = false;
    whoEl.textContent = res.status === 429 ? "Забагато спроб, зачекайте хвилину" : "Неправильний логін або пароль";
    return;
  }
  setSession(await res.json());
  loginForm.reset();
  await refresh();
});

logoutBtn.addEventListener("click", async () => {
  if (session) {
    await fetch("/api/auth/logout", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refresh_token: session.refresh_token }),
    });
  }
  setSession(null);
  await refresh();
});

// ---------- SignalR ----------

let hub = null;

async function subscribe() {
  if (hub?.state === "Connected" && greenhouseId !== null) await hub.invoke("Subscribe", greenhouseId);
}

async function connectHub() {
  if (!window.signalR) return false;
  hub = new signalR.HubConnectionBuilder().withUrl("/hubs/telemetry").withAutomaticReconnect().build();
  hub.on("readingsAdded", queueRefresh);
  hub.on("alertsChanged", queueRefresh);
  hub.onreconnected(subscribe);
  try {
    await hub.start();
    await subscribe();
    return true;
  } catch (err) {
    console.error(err);
    return false;
  }
}

async function init() {
  try {
    await loadGreenhouses();
    await loadGreenhouse();
  } catch (err) {
    connEl.textContent = "Немає зв'язку з бекендом";
    console.error(err);
    setTimeout(init, POLL_MS);
    return;
  }
  const live = await connectHub();
  // Опитування лишається запасним шляхом і рідшим, коли працює SignalR.
  clearInterval(pollTimer);
  pollTimer = setInterval(refresh, live ? POLL_MS * 10 : POLL_MS);
}

init();
