// Дашборд: раз на REFRESH_MS опитує бекенд і перемальовує картки та графіки.
"use strict";

const REFRESH_MS = 3000;
const HISTORY = 60; // скільки останніх точок показувати на графіку

const LABELS = { temperature: "Температура", humidity: "Вологість повітря", light: "Освітленість" };
const STATUS_TEXT = { ok: "Норма", low: "Низько", high: "Високо", no_data: "Немає даних" };

const cardsEl = document.getElementById("cards");
const connEl = document.getElementById("conn");
const recentBody = document.querySelector("#recent tbody");
const cards = {}; // sensor name -> { el, sensor }

async function getJSON(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${url}: ${res.status}`);
  return res.json();
}

function fmt(value, unit) {
  if (value === null || value === undefined) return "—";
  const digits = unit === "lx" ? 0 : 1;
  return value.toLocaleString("uk-UA", { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

function timeOf(iso) {
  return iso ? iso.slice(11, 19) : "";
}

function createCard(sensor) {
  const el = document.createElement("article");
  el.className = "card";
  el.innerHTML = `
    <div class="label"></div>
    <div><span class="value">—</span><span class="unit"></span></div>
    <div class="range"></div>
    <span class="badge no_data">${STATUS_TEXT.no_data}</span>
    <svg class="chart" role="img"></svg>
    <div class="tooltip" hidden></div>`;
  el.querySelector(".label").textContent = `${LABELS[sensor.kind] || sensor.kind} · ${sensor.name}`;
  el.querySelector(".unit").textContent = sensor.unit;
  el.querySelector(".range").textContent =
    `Норма: ${fmt(sensor.comfort_min, sensor.unit)}–${fmt(sensor.comfort_max, sensor.unit)} ${sensor.unit}`;
  el.querySelector("svg").setAttribute("aria-label", `Графік: ${LABELS[sensor.kind] || sensor.kind}`);
  cardsEl.appendChild(el);
  cards[sensor.name] = { el, sensor, points: [] };
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
    const left = Math.min(ev.clientX - cardR.left + 12, cardR.width - tip.offsetWidth - 8);
    tip.style.left = `${left}px`;
    tip.style.top = `${r.top - cardR.top - 4}px`;
  });
  hit.addEventListener("mouseleave", () => {
    cross.setAttribute("visibility", "hidden"); dot.setAttribute("visibility", "hidden"); tip.hidden = true;
  });
}

async function refresh() {
  try {
    const [latest, recent] = await Promise.all([
      getJSON("/api/readings/latest"),
      getJSON("/api/readings?limit=10"),
    ]);
    for (const row of latest) {
      const card = cards[row.sensor];
      if (!card) continue;
      card.el.querySelector(".value").textContent = fmt(row.value, row.unit);
      const badge = card.el.querySelector(".badge");
      badge.className = `badge ${row.status}`;
      badge.textContent = STATUS_TEXT[row.status];
      const history = await getJSON(`/api/readings?sensor=${encodeURIComponent(row.sensor)}&limit=${HISTORY}`);
      card.points = history.reverse();
      drawChart(card);
    }
    recentBody.innerHTML = "";
    for (const r of recent) {
      const tr = document.createElement("tr");
      const unit = cards[r.sensor] ? cards[r.sensor].sensor.unit : "";
      for (const [text, cls] of [[r.recorded_at.replace("T", " ").slice(0, 19), ""], [r.sensor, ""], [`${fmt(r.value, unit)} ${unit}`, "num"]]) {
        const td = document.createElement("td");
        td.textContent = text;
        if (cls) td.className = cls;
        tr.appendChild(td);
      }
      recentBody.appendChild(tr);
    }
    connEl.textContent = `Оновлено ${new Date().toLocaleTimeString("uk-UA")}`;
  } catch (err) {
    connEl.textContent = "Немає зв'язку з бекендом";
    console.error(err);
  }
}

async function init() {
  try {
    const sensors = await getJSON("/api/sensors");
    sensors.forEach(createCard);
  } catch (err) {
    connEl.textContent = "Немає зв'язку з бекендом";
    console.error(err);
    setTimeout(init, REFRESH_MS);
    return;
  }
  await refresh();
  setInterval(refresh, REFRESH_MS);
}

init();
