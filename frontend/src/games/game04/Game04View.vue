<template>
  <main class="game04-view">
    <section class="topbar">
      <RouterLink to="/" class="back-link">← 返回遊戲廳</RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 04</p>
        <h1>毛毛蟲賽車</h1>
      </div>
      <div class="round-pill">{{ LAPS }} 圈 · 先抵達終點者勝</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <span class="dot" :style="{ background: P1_COLOR, boxShadow: `0 0 10px ${P1_COLOR}` }" />
            <strong>玩家 1</strong>
            <span class="lap">第 {{ hud.p1Lap }}/{{ LAPS }} 圈</span>
            <span class="rank">第 {{ hud.p1Rank }} 名</span>
            <span class="item-chip" :class="hud.p1Item || 'empty'">{{ itemLabel(hud.p1Item) }}</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="item-chip" :class="hud.p2Item || 'empty'">{{ itemLabel(hud.p2Item) }}</span>
            <span class="rank">第 {{ hud.p2Rank }} 名</span>
            <span class="lap">第 {{ hud.p2Lap }}/{{ LAPS }} 圈</span>
            <strong>玩家 2</strong>
            <span class="dot" :style="{ background: P2_COLOR, boxShadow: `0 0 10px ${P2_COLOR}` }" />
          </div>
        </div>

        <div ref="stageRef" class="stage-frame">
          <canvas ref="canvasRef" class="game-canvas" :width="CANVAS_W" :height="CANVAS_H" />
          <transition name="fade">
            <div v-if="phase !== 'playing'" class="overlay">
              <div class="overlay-card">
                <template v-if="phase === 'intro'">
                  <p class="overlay-eyebrow">2.5D 第三人稱道具競速</p>
                  <h2>駕著毛毛蟲衝過終點</h2>
                  <p class="overlay-text">
                    左右分割畫面，各自從自己毛毛蟲的後上方視角看向前方賽道。<br>
                    開過白色氣球 🎈 隨機獲得道具：加速、追蹤球、無敵藥水，賽道上還有跳躍／加速／減速特殊地板，跑滿 {{ LAPS }} 圈先到終點的玩家獲勝。
                  </p>
                  <button class="primary-btn" @click="startRace">開始比賽</button>
                </template>
                <template v-else-if="phase === 'result'">
                  <p class="overlay-eyebrow">比賽結束</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <p class="overlay-text">{{ resultSub }}</p>
                  <button class="primary-btn" @click="startRace">再跑一場</button>
                </template>
              </div>
            </div>
          </transition>
        </div>
      </div>

      <aside class="sidebar">
        <section class="panel">
          <p class="eyebrow">操作方式</p>
          <div class="controls-grid">
            <div class="ctrl ctrl-1">
              <strong>玩家 1</strong>
              <span><kbd>W</kbd> 加速 · <kbd>S</kbd> 煞車 · <kbd>A</kbd><kbd>D</kbd> 轉向</span>
              <span><kbd>F</kbd> 使用道具</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span><kbd>↑</kbd> 加速 · <kbd>↓</kbd> 煞車 · <kbd>←</kbd><kbd>→</kbd> 轉向</span>
              <span><kbd>/</kbd> 使用道具</span>
            </div>
          </div>
        </section>
        <section class="panel">
          <p class="eyebrow">道具</p>
          <ul class="legend">
            <li><span class="ic" style="background:#ffd23f">🍄</span> 加速：短暫高速衝刺</li>
            <li><span class="ic" style="background:#ff7a3a">🟠</span> 追蹤球：射向前方對手</li>
            <li><span class="ic" style="background:#ff4d6d">🧪</span> 無敵藥水：無敵＋撞飛對手</li>
          </ul>
          <p class="eyebrow" style="margin-top:14px">特殊地板</p>
          <ul class="legend">
            <li><span class="ic" style="background:#46d0ff">↑</span> 跳躍板：彈起，可越過陷阱</li>
            <li><span class="ic" style="background:#ffd23f">»</span> 加速板：踩到瞬間加速</li>
            <li><span class="ic" style="background:#d05a3a">×</span> 減速板：踩到大幅減速</li>
          </ul>
        </section>
        <section class="panel">
          <div class="panel-head">
            <p class="eyebrow">對戰紀錄</p>
            <button v-if="records.length" class="ghost-btn" @click="onClearRecords">清除</button>
          </div>
          <ul v-if="records.length" class="records">
            <li v-for="(r, i) in records" :key="i">
              <span class="rec-win">{{ r.winner }}</span>
              <span class="rec-score">{{ r.time }}s</span>
              <span class="rec-date">{{ formatDate(r.date) }}</span>
            </li>
          </ul>
          <p v-else class="empty">尚無紀錄，比賽結束後自動保存最近 10 場。</p>
        </section>
      </aside>
    </section>
  </main>
</template>

<script setup>
import { onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { clearGame04Records, fetchGame04Store, saveGame04Record } from './game04Storage'
import { recordGameResult } from '@/data/lobbyScore'

const CANVAS_W = 960
const CANVAS_H = 620
const LAPS = 3
const ROAD_HALF = 138
const ITEM_TYPES = ['speed', 'shell', 'star']
const P1_COLOR = '#ff6f3c'
const P2_COLOR = '#36c9ff'

// 賽道中心線（封閉迴圈，放大的複雜單圈；世界座標可大於畫面，由鏡頭跟隨）
const TRACK_SCALE = 3
const CENTER = (() => {
  const cx = 480
  const cy = 310
  const pts = []
  const n = 80
  for (let i = 0; i < n; i += 1) {
    const a = (i / n) * Math.PI * 2
    const rx = 332 * TRACK_SCALE * (1 + 0.17 * Math.sin(3 * a + 0.5) + 0.07 * Math.sin(5 * a + 1.1))
    const ry = 198 * TRACK_SCALE * (1 + 0.14 * Math.cos(2 * a) + 0.06 * Math.cos(4 * a + 0.3))
    pts.push({ x: cx + Math.cos(a) * rx, y: cy + Math.sin(a) * ry })
  }
  return pts
})()
const NPTS = CENTER.length
const BOUND = (() => {
  let minX = Infinity
  let minY = Infinity
  let maxX = -Infinity
  let maxY = -Infinity
  for (const p of CENTER) {
    minX = Math.min(minX, p.x)
    maxX = Math.max(maxX, p.x)
    minY = Math.min(minY, p.y)
    maxY = Math.max(maxY, p.y)
  }
  const m = ROAD_HALF + 160
  return { minX: minX - m, maxX: maxX + m, minY: minY - m, maxY: maxY + m }
})()
const specialFloors = buildFloors()

// 2.5D 追車鏡頭參數（鏡頭在自己蟲的後上方，看向前方）
const VIEW_W = CANVAS_W / 2
const HORIZON = Math.round(CANVAS_H * 0.34)
const CAM_BACK = 72
const CAM_NEAR = 12
const CAM_HEIGHT = 9800
const CAM_LAT = 188
const SPR_HEAD = 1320
const SPR_BODY = 800
const ROAD_AHEAD = 20

const canvasRef = ref(null)
const stageRef = ref(null)
const phase = ref('intro')
const resultText = ref('')
const resultSub = ref('')
const records = ref([])
const hud = reactive({
  p1Lap: 1, p2Lap: 1, p1Rank: 1, p2Rank: 1, p1Item: null, p2Item: null
})

let ctx = null
let rafId = 0
let lastTime = 0
let game = null
const keys = new Set()

const CPU_DEFS = [
  { name: '電腦 A', color: '#5fd35f', skill: 0.56 },
  { name: '電腦 B', color: '#b07bff', skill: 0.53 },
  { name: '電腦 C', color: '#ffd23f', skill: 0.57 }
]

function makeRacer(config) {
  const start = CENTER[0]
  const next = CENTER[1]
  const angle = Math.atan2(next.y - start.y, next.x - start.x)
  const nx = Math.cos(angle + Math.PI / 2) * config.lane
  const ny = Math.sin(angle + Math.PI / 2) * config.lane
  return {
    name: config.name,
    color: config.color,
    isCPU: !!config.isCPU,
    accelKey: config.accelKey,
    brakeKey: config.brakeKey,
    leftKey: config.leftKey,
    rightKey: config.rightKey,
    itemKey: config.itemKey,
    skill: config.skill ?? 1,
    x: start.x + nx,
    y: start.y + ny,
    angle,
    speed: 0,
    trail: [],
    item: null,
    boostUntil: 0,
    starUntil: 0,
    spinUntil: 0,
    slowUntil: 0,
    jumpUntil: 0,
    jumpStart: -99,
    floorCd: 0,
    itemTimer: 1.5,
    aiWaypoint: 2,
    lap: 0,
    prog: 0,
    lastProg: 0,
    total: 0,
    rank: 1,
    finished: false,
    finishTime: 0,
    itemHeld: false
  }
}

function createGame() {
  const racers = [
    makeRacer({ name: '玩家 1', color: P1_COLOR, lane: -16, accelKey: 'w', brakeKey: 's', leftKey: 'a', rightKey: 'd', itemKey: 'f' }),
    makeRacer({ name: '玩家 2', color: P2_COLOR, lane: 16, accelKey: 'arrowup', brakeKey: 'arrowdown', leftKey: 'arrowleft', rightKey: 'arrowright', itemKey: '/' }),
    ...CPU_DEFS.map((d, i) => makeRacer({ name: d.name, color: d.color, isCPU: true, skill: d.skill, lane: [-40, 40, 0][i % 3] }))
  ]
  return {
    racers,
    p1: racers[0],
    p2: racers[1],
    boxes: buildBoxes(),
    hazards: [],
    projectiles: [],
    particles: [],
    boxTimer: 4,
    countdown: 3.2,
    elapsed: 0,
    over: false
  }
}

function perpAt(i) {
  const a = CENTER[(i - 1 + NPTS) % NPTS]
  const b = CENTER[(i + 1) % NPTS]
  let tx = b.x - a.x
  let ty = b.y - a.y
  const tl = Math.hypot(tx, ty) || 1
  return { px: -ty / tl, py: tx / tl }
}

// 6 線道的車道中心（取得道具用）
const LANE_FRACS = [-5 / 6, -3 / 6, -1 / 6, 1 / 6, 3 / 6, 5 / 6]

function buildBoxes() {
  const boxes = []
  for (let i = 6; i < NPTS; i += 10) {
    const c = CENTER[i]
    const { px, py } = perpAt(i)
    for (const f of LANE_FRACS) {
      boxes.push({ x: c.x + px * ROAD_HALF * f, y: c.y + py * ROAD_HALF * f, active: true, respawnAt: 0 })
    }
  }
  return boxes
}

// 特殊地板：踩到會跳躍 / 加速 / 減速
function buildFloors() {
  const defs = [
    { seg: 12, f: -0.55, type: 'boost' },
    { seg: 22, f: 0.4, type: 'jump' },
    { seg: 34, f: 0, type: 'slow' },
    { seg: 46, f: 0.6, type: 'boost' },
    { seg: 58, f: -0.45, type: 'jump' },
    { seg: 68, f: 0.25, type: 'slow' },
    { seg: 76, f: -0.6, type: 'boost' }
  ]
  return defs.map((d) => {
    const i = d.seg % NPTS
    const c = CENTER[i]
    const { px, py } = perpAt(i)
    return { x: c.x + px * ROAD_HALF * d.f, y: c.y + py * ROAD_HALF * d.f, type: d.type }
  })
}

// 投影到中心線：回傳 { prog 0..NPTS, dist }
function project(x, y) {
  let best = Infinity
  let prog = 0
  for (let i = 0; i < NPTS; i += 1) {
    const a = CENTER[i]
    const b = CENTER[(i + 1) % NPTS]
    const dx = b.x - a.x
    const dy = b.y - a.y
    const len2 = dx * dx + dy * dy
    let t = ((x - a.x) * dx + (y - a.y) * dy) / len2
    t = t < 0 ? 0 : t > 1 ? 1 : t
    const px = a.x + dx * t
    const py = a.y + dy * t
    const d = Math.hypot(x - px, y - py)
    if (d < best) {
      best = d
      prog = i + t
    }
  }
  return { prog, dist: best }
}

function startRace() {
  game = createGame()
  syncHud()
  phase.value = 'playing'
  lastTime = performance.now()
  if (!rafId) loop(lastTime)
}

function syncHud() {
  hud.p1Lap = Math.min(LAPS, game.p1.lap + 1)
  hud.p2Lap = Math.min(LAPS, game.p2.lap + 1)
  hud.p1Rank = game.p1.rank
  hud.p2Rank = game.p2.rank
  hud.p1Item = game.p1.item
  hud.p2Item = game.p2.item
}

function spawnParticles(x, y, color, n) {
  for (let i = 0; i < n; i += 1) {
    const a = Math.random() * Math.PI * 2
    const sp = 40 + Math.random() * 150
    game.particles.push({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 1, color })
  }
}

function useItem(r) {
  if (!r.item) return
  const type = r.item
  r.item = null
  if (type === 'speed') {
    r.boostUntil = game.elapsed + 1.6
  } else if (type === 'star') {
    r.starUntil = game.elapsed + 4.5
  } else if (type === 'bomb') {
    game.hazards.push({ x: r.x - Math.cos(r.angle) * 32, y: r.y - Math.sin(r.angle) * 32, owner: r, grace: game.elapsed + 0.7, ttl: 14 })
  } else if (type === 'shell') {
    const target = nearestAhead(r)
    game.projectiles.push({ x: r.x + Math.cos(r.angle) * 26, y: r.y + Math.sin(r.angle) * 26, vx: Math.cos(r.angle) * 224, vy: Math.sin(r.angle) * 224, owner: r, target, ttl: 5 })
  }
  syncHud()
}

function nearestAhead(r) {
  let best = null
  let bestGap = Infinity
  for (const o of game.racers) {
    if (o === r) continue
    let gap = o.total - r.total
    if (gap > 0 && gap < bestGap) { bestGap = gap; best = o }
  }
  return best || game.racers.find((o) => o !== r)
}

function spin(r) {
  if (game.elapsed < r.starUntil) return
  if (game.elapsed < r.jumpUntil) return
  r.spinUntil = game.elapsed + 1.0
  r.speed *= 0.25
  spawnParticles(r.x, r.y, '#fff', 10)
}

function maxSpeedFor(r, onRoad) {
  const jumping = game.elapsed < r.jumpUntil
  let m = 162
  if (game.elapsed < r.boostUntil || game.elapsed < r.starUntil) m = 238
  if (!onRoad && !jumping) m = Math.min(m, 73)
  if (game.elapsed < r.slowUntil) m = Math.min(m, 78)
  if (r.isCPU) m *= r.skill
  return m
}

function checkFloors(r) {
  if (r.floorCd > 0) return
  for (const fl of specialFloors) {
    if (Math.hypot(fl.x - r.x, fl.y - r.y) < 28) {
      r.floorCd = 0.5
      if (fl.type === 'boost') {
        r.boostUntil = game.elapsed + 1.2
        spawnParticles(fl.x, fl.y, '#ffd23f', 12)
      } else if (fl.type === 'slow') {
        r.speed *= 0.4
        r.slowUntil = game.elapsed + 0.6
        spawnParticles(fl.x, fl.y, '#c0563a', 12)
      } else if (fl.type === 'jump') {
        r.jumpStart = game.elapsed
        r.jumpUntil = game.elapsed + 0.6
        spawnParticles(fl.x, fl.y, '#46d0ff', 14)
      }
      break
    }
  }
}

function controlRacer(r, dt) {
  if (r.floorCd > 0) r.floorCd = Math.max(0, r.floorCd - dt)
  const p = project(r.x, r.y)
  const onRoad = p.dist <= ROAD_HALF
  const spinning = game.elapsed < r.spinUntil

  let accel = 0
  let steer = 0
  if (r.isCPU) {
    // 朝前方 waypoint 行駛
    const wp = CENTER[r.aiWaypoint % NPTS]
    if (Math.hypot(wp.x - r.x, wp.y - r.y) < 70) r.aiWaypoint += 1
    const desired = Math.atan2(wp.y - r.y, wp.x - r.x)
    let diff = desired - r.angle
    while (diff > Math.PI) diff -= Math.PI * 2
    while (diff < -Math.PI) diff += Math.PI * 2
    steer = Math.max(-1, Math.min(1, diff * 2.2))
    accel = 1
    r.itemTimer -= dt
    if (r.item && r.itemTimer <= 0) { useItem(r); r.itemTimer = 1.4 + Math.random() * 2.2 }
  } else {
    if (keys.has(r.accelKey)) accel = 1
    else if (keys.has(r.brakeKey)) accel = -1
    if (keys.has(r.leftKey)) steer -= 1
    if (keys.has(r.rightKey)) steer += 1
  }

  const maxSpeed = maxSpeedFor(r, onRoad)

  if (spinning) {
    r.angle += 9 * dt
    r.speed *= Math.pow(0.2, dt)
  } else {
    if (accel > 0) r.speed += 252 * dt
    else if (accel < 0) r.speed -= 294 * dt
    else r.speed -= 126 * dt
    if (r.speed > maxSpeed) r.speed -= (r.speed - maxSpeed) * Math.min(1, 6 * dt)
    if (r.speed < -49) r.speed = -49
    if (r.speed < 0 && accel >= 0) r.speed = Math.min(0, r.speed + 154 * dt)
    // 轉向：低速幾乎轉不動，隨速度提升
    const grip = Math.min(1, Math.abs(r.speed) / 64)
    r.angle += steer * 2.7 * grip * Math.sign(r.speed || 1) * dt
  }

  r.x += Math.cos(r.angle) * r.speed * dt
  r.y += Math.sin(r.angle) * r.speed * dt
  // 世界邊界（賽道外緣留一段草地）
  r.x = Math.max(BOUND.minX, Math.min(BOUND.maxX, r.x))
  r.y = Math.max(BOUND.minY, Math.min(BOUND.maxY, r.y))

  // 拖尾
  r.trail.unshift({ x: r.x, y: r.y })
  if (r.trail.length > 90) r.trail.pop()

  // 進度與圈數
  const newProg = project(r.x, r.y).prog
  if (r.lastProg > NPTS - 5 && newProg < 5) {
    r.lap += 1
    if (r.lap >= LAPS && !r.finished) finishRacer(r)
  } else if (r.lastProg < 5 && newProg > NPTS - 5) {
    r.lap = Math.max(0, r.lap - 1)
  }
  r.lastProg = newProg
  r.prog = newProg
  r.total = r.lap * NPTS + newProg
}

function finishRacer(r) {
  r.finished = true
  r.finishTime = game.elapsed
  if (!r.isCPU && !game.over) {
    game.over = true
    endRace(r)
  }
}

function checkBoxes(r) {
  for (const box of game.boxes) {
    if (box.active && !r.item && Math.hypot(box.x - r.x, box.y - r.y) < 26) {
      r.item = ITEM_TYPES[Math.floor(Math.random() * ITEM_TYPES.length)]
      box.active = false
      box.respawnAt = game.elapsed + 2
      spawnParticles(box.x, box.y, '#fff', 12)
      syncHud()
    }
  }
}

function checkHazards(r) {
  for (let i = game.hazards.length - 1; i >= 0; i -= 1) {
    const h = game.hazards[i]
    if (h.owner === r && game.elapsed < h.grace) continue
    if (game.elapsed < r.starUntil) continue
    if (Math.hypot(h.x - r.x, h.y - r.y) < 22) {
      spin(r)
      spawnParticles(h.x, h.y, '#ff7a3a', 16)
      game.hazards.splice(i, 1)
    }
  }
}

function updateRanks() {
  const ordered = [...game.racers].sort((a, b) => b.total - a.total)
  ordered.forEach((r, i) => { r.rank = i + 1 })
}

function update(dt) {
  if (game.countdown > 0) {
    game.countdown -= dt
    return
  }
  game.elapsed += dt

  for (const r of game.racers) {
    controlRacer(r, dt)
    checkBoxes(r)
    checkHazards(r)
    checkFloors(r)
  }

  // 蟲身互撞（星星可撞飛）
  for (let i = 0; i < game.racers.length; i += 1) {
    for (let j = i + 1; j < game.racers.length; j += 1) {
      const a = game.racers[i]
      const b = game.racers[j]
      const d = Math.hypot(a.x - b.x, a.y - b.y)
      if (d < 22 && d > 0) {
        const nx = (a.x - b.x) / d
        const ny = (a.y - b.y) / d
        const push = (22 - d) / 2
        a.x += nx * push; a.y += ny * push
        b.x -= nx * push; b.y -= ny * push
        if (game.elapsed < a.starUntil && game.elapsed >= b.starUntil) spin(b)
        else if (game.elapsed < b.starUntil && game.elapsed >= a.starUntil) spin(a)
        else { a.speed *= 0.7; b.speed *= 0.7 }
      }
    }
  }

  // 道具箱重生
  for (const box of game.boxes) {
    if (!box.active && game.elapsed >= box.respawnAt) box.active = true
  }
  // 追蹤球
  for (let i = game.projectiles.length - 1; i >= 0; i -= 1) {
    const p = game.projectiles[i]
    p.ttl -= dt
    if (p.target) {
      const ang = Math.atan2(p.target.y - p.y, p.target.x - p.x)
      const cur = Math.atan2(p.vy, p.vx)
      let diff = ang - cur
      while (diff > Math.PI) diff -= Math.PI * 2
      while (diff < -Math.PI) diff += Math.PI * 2
      const na = cur + Math.max(-3 * dt, Math.min(3 * dt, diff))
      p.vx = Math.cos(na) * 224
      p.vy = Math.sin(na) * 224
      if (game.elapsed >= p.target.starUntil && Math.hypot(p.target.x - p.x, p.target.y - p.y) < 20) {
        spin(p.target)
        spawnParticles(p.x, p.y, '#ff7a3a', 16)
        game.projectiles.splice(i, 1)
        continue
      }
    }
    p.x += p.vx * dt
    p.y += p.vy * dt
    if (p.ttl <= 0 || p.x < 0 || p.x > CANVAS_W || p.y < 0 || p.y > CANVAS_H) game.projectiles.splice(i, 1)
  }
  for (const h of game.hazards) h.ttl -= dt
  game.hazards = game.hazards.filter((h) => h.ttl > 0)
  for (const pt of game.particles) {
    pt.x += pt.vx * dt; pt.y += pt.vy * dt
    pt.vx *= 0.92; pt.vy *= 0.92
    pt.life -= dt / 0.6
  }
  game.particles = game.particles.filter((p) => p.life > 0)

  updateRanks()
  syncHud()
}

async function endRace(winner) {
  setTimeout(() => { phase.value = 'result' }, 600)
  resultText.value = `🏁 ${winner.name} 獲勝`
  resultSub.value = `完成時間 ${winner.finishTime.toFixed(2)} 秒 · 第 ${winner.rank} 名`
  recordGameResult('/game04', winner === game.p1 ? 'p1' : 'p2')
  try {
    const store = await saveGame04Record({ winner: `${winner.name} 獲勝`, time: winner.finishTime.toFixed(2), date: new Date().toISOString() })
    records.value = store.records
  } catch {
    /* ignore */
  }
}

function loop(now) {
  let dt = (now - lastTime) / 1000
  lastTime = now
  if (dt > 0.05) dt = 0.05
  if (phase.value === 'playing') {
    update(dt)
    render()
    rafId = requestAnimationFrame(loop)
  } else {
    rafId = 0
  }
}

function render() {
  ctx.clearRect(0, 0, CANVAS_W, CANVAS_H)
  drawViewport(0, game.p1)
  drawViewport(1, game.p2)

  // 中央分隔
  ctx.fillStyle = 'rgba(255,255,255,0.85)'
  ctx.fillRect(VIEW_W - 3, 0, 6, CANVAS_H)
  ctx.fillStyle = 'rgba(60,40,30,0.25)'
  ctx.fillRect(VIEW_W - 1, 0, 2, CANVAS_H)

  if (game.countdown > 0) {
    ctx.fillStyle = 'rgba(0,0,0,0.4)'
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
    const n = Math.ceil(game.countdown)
    ctx.fillStyle = '#fff'
    ctx.font = 'bold 100px system-ui, sans-serif'
    ctx.textAlign = 'center'
    ctx.textBaseline = 'middle'
    ctx.fillText(n > 0 ? String(n) : 'GO', CANVAS_W / 2, CANVAS_H / 2)
  }
}

function makeCam(player) {
  const cos = Math.cos(player.angle)
  const sin = Math.sin(player.angle)
  return { cos, sin, x: player.x - cos * CAM_BACK, y: player.y - sin * CAM_BACK, cx: 0 }
}

function projPt(cam, wx, wy) {
  const dx = wx - cam.x
  const dy = wy - cam.y
  const depth = dx * cam.cos + dy * cam.sin
  if (depth < CAM_NEAR) return null
  const side = -dx * cam.sin + dy * cam.cos
  const scale = 1 / depth
  return { sx: cam.cx + side * scale * CAM_LAT, sy: HORIZON + scale * CAM_HEIGHT, scale, depth }
}

function projClamp(cam, wx, wy) {
  const dx = wx - cam.x
  const dy = wy - cam.y
  let depth = dx * cam.cos + dy * cam.sin
  if (depth < CAM_NEAR) depth = CAM_NEAR
  const side = -dx * cam.sin + dy * cam.cos
  const scale = 1 / depth
  return { sx: cam.cx + side * scale * CAM_LAT, sy: HORIZON + scale * CAM_HEIGHT, scale, depth }
}

function fillCircle(x, y, r) {
  ctx.beginPath()
  ctx.arc(x, y, r, 0, Math.PI * 2)
  ctx.fill()
}

function drawViewport(viewportIndex, player) {
  const vx = viewportIndex * VIEW_W
  const cam = makeCam(player)
  cam.cx = vx + VIEW_W / 2

  ctx.save()
  ctx.beginPath()
  ctx.rect(vx, 0, VIEW_W, CANVAS_H)
  ctx.clip()

  // 天空
  const sky = ctx.createLinearGradient(0, 0, 0, HORIZON)
  sky.addColorStop(0, '#8ed0ff')
  sky.addColorStop(1, '#e6f5ff')
  ctx.fillStyle = sky
  ctx.fillRect(vx, 0, VIEW_W, HORIZON)
  // 遠方山丘
  ctx.fillStyle = '#74ab63'
  ctx.fillRect(vx, HORIZON - 14, VIEW_W, 16)
  // 草地
  ctx.fillStyle = '#3f8f3a'
  ctx.fillRect(vx, HORIZON, VIEW_W, CANVAS_H - HORIZON)

  drawRoad(cam, player)
  drawSprites(cam, player)
  drawViewportHud(vx, player)

  ctx.restore()
}

function drawRoad(cam, player) {
  const i0 = Math.floor(player.prog)
  const secs = []
  for (let k = -2; k <= ROAD_AHEAD; k += 1) {
    const m = ((i0 + k) % NPTS + NPTS) % NPTS
    const c = CENTER[m]
    const { px, py } = perpAt(m)
    const cd = (c.x - cam.x) * cam.cos + (c.y - cam.y) * cam.sin
    secs.push({
      m,
      cd,
      cx: c.x,
      cy: c.y,
      px,
      py,
      L: projClamp(cam, c.x + px * ROAD_HALF, c.y + py * ROAD_HALF),
      R: projClamp(cam, c.x - px * ROAD_HALF, c.y - py * ROAD_HALF)
    })
  }
  // 路面（遠 → 近）
  for (let i = secs.length - 1; i >= 1; i -= 1) {
    const s2 = secs[i]
    const s1 = secs[i - 1]
    if (s1.cd < 3 && s2.cd < 3) continue
    ctx.fillStyle = s2.m % 2 === 0 ? '#43474f' : '#484d56'
    ctx.beginPath()
    ctx.moveTo(s1.L.sx, s1.L.sy)
    ctx.lineTo(s1.R.sx, s1.R.sy)
    ctx.lineTo(s2.R.sx, s2.R.sy)
    ctx.lineTo(s2.L.sx, s2.L.sy)
    ctx.closePath()
    ctx.fill()
  }
  // 6 線道分隔線（5 條界線）
  for (const o of [-2 / 3, -1 / 3, 0, 1 / 3, 2 / 3]) {
    ctx.strokeStyle = o === 0 ? 'rgba(255,255,255,0.5)' : 'rgba(255,255,255,0.26)'
    for (let i = secs.length - 1; i >= 1; i -= 1) {
      const s2 = secs[i]
      const s1 = secs[i - 1]
      if (s1.cd < 3 || s2.cd < 3) continue
      if (s2.m % 2 !== 0) continue
      const a = projClamp(cam, s1.cx + s1.px * ROAD_HALF * o, s1.cy + s1.py * ROAD_HALF * o)
      const b = projClamp(cam, s2.cx + s2.px * ROAD_HALF * o, s2.cy + s2.py * ROAD_HALF * o)
      ctx.lineWidth = Math.max(1, a.scale * 130)
      ctx.beginPath()
      ctx.moveTo(a.sx, a.sy)
      ctx.lineTo(b.sx, b.sy)
      ctx.stroke()
    }
  }
  // 邊線
  ctx.strokeStyle = '#e6e6e6'
  ctx.lineWidth = 3
  strokeEdge(secs.map((s) => (s.cd > 3 ? s.L : null)))
  strokeEdge(secs.map((s) => (s.cd > 3 ? s.R : null)))
  // 特殊地板
  drawFloors(cam)
  // 起點/終點格線
  for (const s of secs) {
    if (s.m === 0 && s.cd > 3) drawStartAcross(s)
  }
}

function drawFloors(cam) {
  for (const fl of specialFloors) {
    const p = projPt(cam, fl.x, fl.y)
    if (!p) continue
    const sz = Math.max(8, Math.min(64, p.scale * 1200))
    const col = fl.type === 'boost' ? '#ffd23f' : fl.type === 'slow' ? '#d05a3a' : '#46d0ff'
    ctx.save()
    ctx.globalAlpha = 0.9
    ctx.fillStyle = col
    ctx.beginPath()
    ctx.ellipse(p.sx, p.sy, sz, sz * 0.5, 0, 0, Math.PI * 2)
    ctx.fill()
    ctx.globalAlpha = 1
    ctx.fillStyle = '#16202a'
    ctx.font = `bold ${Math.round(sz * 0.82)}px system-ui, sans-serif`
    ctx.textAlign = 'center'
    ctx.textBaseline = 'middle'
    const sym = fl.type === 'boost' ? '»' : fl.type === 'slow' ? '×' : '↑'
    ctx.fillText(sym, p.sx, p.sy - sz * 0.08)
    ctx.restore()
  }
}

function strokeEdge(pts) {
  let started = false
  ctx.beginPath()
  for (const p of pts) {
    if (!p) {
      started = false
      continue
    }
    if (!started) {
      ctx.moveTo(p.sx, p.sy)
      started = true
    } else {
      ctx.lineTo(p.sx, p.sy)
    }
  }
  ctx.stroke()
}

function drawStartAcross(s) {
  const steps = 10
  for (let i = 0; i < steps; i += 1) {
    const t1 = i / steps
    const t2 = (i + 1) / steps
    ctx.strokeStyle = i % 2 === 0 ? '#fff' : '#222'
    ctx.lineWidth = 12
    ctx.beginPath()
    ctx.moveTo(s.L.sx + (s.R.sx - s.L.sx) * t1, s.L.sy + (s.R.sy - s.L.sy) * t1)
    ctx.lineTo(s.L.sx + (s.R.sx - s.L.sx) * t2, s.L.sy + (s.R.sy - s.L.sy) * t2)
    ctx.stroke()
  }
}

function drawSprites(cam, player) {
  const sprites = []
  for (const r of game.racers) {
    const h = projPt(cam, r.x, r.y)
    if (h) sprites.push({ depth: h.depth, kind: 'worm', r })
  }
  for (const box of game.boxes) {
    if (!box.active) continue
    const p = projPt(cam, box.x, box.y)
    if (p) sprites.push({ depth: p.depth, kind: 'balloon', p })
  }
  for (const h of game.hazards) {
    const p = projPt(cam, h.x, h.y)
    if (p) sprites.push({ depth: p.depth, kind: 'bomb', p })
  }
  for (const pr of game.projectiles) {
    const p = projPt(cam, pr.x, pr.y)
    if (p) sprites.push({ depth: p.depth, kind: 'ball', p })
  }
  sprites.sort((a, b) => b.depth - a.depth)

  for (const s of sprites) {
    if (s.kind === 'worm') {
      drawWormSprite(cam, s.r)
    } else if (s.kind === 'balloon') {
      drawBalloonProj(s.p)
    } else if (s.kind === 'bomb') {
      const sz = Math.max(14, Math.min(86, s.p.scale * 1000))
      ctx.font = `${Math.round(sz)}px serif`
      ctx.textAlign = 'center'
      ctx.textBaseline = 'bottom'
      ctx.fillText('💣', s.p.sx, s.p.sy)
    } else if (s.kind === 'ball') {
      const rr = Math.max(3, Math.min(38, s.p.scale * 540))
      ctx.save()
      ctx.shadowColor = '#ff7a3a'
      ctx.shadowBlur = 8
      ctx.fillStyle = '#ff7a3a'
      fillCircle(s.p.sx, s.p.sy, rr)
      ctx.restore()
    }
  }

  for (const pt of game.particles) {
    const p = projPt(cam, pt.x, pt.y)
    if (!p) continue
    ctx.globalAlpha = Math.max(0, pt.life)
    ctx.fillStyle = pt.color
    fillCircle(p.sx, p.sy, Math.max(1, p.scale * 260) * pt.life + 1)
  }
  ctx.globalAlpha = 1
}

function drawBalloonProj(p) {
  const sz = Math.max(7, Math.min(64, p.scale * 920))
  ctx.save()
  ctx.translate(p.sx, p.sy)
  ctx.strokeStyle = 'rgba(255,255,255,0.6)'
  ctx.lineWidth = Math.max(1, sz * 0.06)
  ctx.beginPath()
  ctx.moveTo(0, 0)
  ctx.lineTo(0, sz * 0.55)
  ctx.stroke()
  ctx.shadowColor = 'rgba(255,255,255,0.7)'
  ctx.shadowBlur = 8
  const g = ctx.createRadialGradient(-sz * 0.3, -sz * 0.8, 1, 0, -sz * 0.5, sz)
  g.addColorStop(0, '#ffffff')
  g.addColorStop(1, '#cfd6e0')
  ctx.fillStyle = g
  ctx.beginPath()
  ctx.ellipse(0, -sz * 0.5, sz * 0.78, sz, 0, 0, Math.PI * 2)
  ctx.fill()
  ctx.restore()
}

function drawWormSprite(cam, r) {
  const star = game.elapsed < r.starUntil
  const segGap = 9
  const segCount = 2
  let acc = 0
  const worldSegs = [{ x: r.x, y: r.y }]
  for (let i = 1; i < r.trail.length && worldSegs.length < segCount; i += 1) {
    acc += Math.hypot(r.trail[i].x - r.trail[i - 1].x, r.trail[i].y - r.trail[i - 1].y)
    if (acc >= segGap * worldSegs.length) worldSegs.push(r.trail[i])
  }
  while (worldSegs.length < segCount) worldSegs.push(worldSegs[worldSegs.length - 1])
  const proj = worldSegs.map((p) => projPt(cam, p.x, p.y))
  const head = proj[0]
  if (!head) return

  const hr = Math.max(3, Math.min(56, head.scale * SPR_HEAD))
  const jumping = game.elapsed < r.jumpUntil
  const hop = jumping ? Math.sin(((game.elapsed - r.jumpStart) / 0.6) * Math.PI) * (26 + head.scale * 1100) : 0
  if (hop > 1) {
    ctx.fillStyle = 'rgba(0,0,0,0.22)'
    ctx.beginPath()
    ctx.ellipse(head.sx, head.sy, hr, hr * 0.4, 0, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.save()
  ctx.translate(0, -hop)

  // 身體：尾 → 頭
  for (let i = segCount - 1; i >= 1; i -= 1) {
    const pr = proj[i]
    if (!pr) continue
    const rad = Math.max(2, Math.min(48, pr.scale * SPR_BODY))
    ctx.fillStyle = star ? (i % 2 ? '#fff3b0' : r.color) : (i % 2 ? shade(r.color, 18) : r.color)
    fillCircle(pr.sx, pr.sy, rad)
  }
  // 頭
  ctx.save()
  if (star) {
    ctx.shadowColor = '#ffd23f'
    ctx.shadowBlur = 16
  }
  ctx.fillStyle = star ? '#ffe88a' : r.color
  fillCircle(head.sx, head.sy, hr)
  ctx.restore()

  // 眼睛朝行進方向（投影前方參考點）
  const fwd = projPt(cam, r.x + Math.cos(r.angle) * 18, r.y + Math.sin(r.angle) * 18)
  let ex = 0
  let ey = -1
  if (fwd) {
    ex = fwd.sx - head.sx
    ey = fwd.sy - head.sy
    const l = Math.hypot(ex, ey) || 1
    ex /= l
    ey /= l
  }
  const pxs = -ey
  const pys = ex
  for (const s of [-1, 1]) {
    ctx.fillStyle = '#fff'
    fillCircle(head.sx + ex * hr * 0.25 + pxs * s * hr * 0.45, head.sy + ey * hr * 0.25 + pys * s * hr * 0.45, hr * 0.32)
    ctx.fillStyle = '#10141c'
    fillCircle(head.sx + ex * hr * 0.42 + pxs * s * hr * 0.45, head.sy + ey * hr * 0.42 + pys * s * hr * 0.45, hr * 0.16)
  }
  // 觸角
  ctx.strokeStyle = r.color
  ctx.lineWidth = Math.max(1.5, hr * 0.12)
  for (const s of [-1, 1]) {
    ctx.beginPath()
    ctx.moveTo(head.sx + ex * hr * 0.4 + pxs * s * hr * 0.4, head.sy + ey * hr * 0.4 + pys * s * hr * 0.4)
    ctx.lineTo(head.sx + ex * hr * 1.0 + pxs * s * hr * 0.7, head.sy + ey * hr * 1.0 + pys * s * hr * 0.7)
    ctx.stroke()
  }

  if (game.elapsed < r.spinUntil) {
    ctx.font = '16px serif'
    ctx.textAlign = 'center'
    ctx.fillText('💫', head.sx, head.sy - hr - 6)
  }
  ctx.fillStyle = r.isCPU ? 'rgba(255,255,255,0.65)' : '#fff'
  ctx.font = `${r.isCPU ? '600 11px' : '700 12px'} system-ui, sans-serif`
  ctx.textAlign = 'center'
  ctx.fillText(r.name, head.sx, head.sy - hr - 8)
  ctx.restore()
}

function drawViewportHud(vx, player) {
  ctx.fillStyle = 'rgba(8,18,12,0.55)'
  roundRectPath(vx + 12, 12, 214, 86, 12)
  ctx.fill()
  ctx.textAlign = 'left'
  ctx.textBaseline = 'alphabetic'
  ctx.fillStyle = '#fff'
  ctx.font = '700 16px "Segoe UI", system-ui, sans-serif'
  ctx.fillText(player.name, vx + 26, 38)
  ctx.fillStyle = '#ffd23f'
  ctx.font = '700 15px "Segoe UI", system-ui, sans-serif'
  ctx.fillText(`第 ${player.rank}/${game.racers.length} 名`, vx + 122, 38)
  ctx.fillStyle = '#cfe7d2'
  ctx.font = '600 14px "Segoe UI", system-ui, sans-serif'
  ctx.fillText(`第 ${Math.min(LAPS, player.lap + 1)}/${LAPS} 圈`, vx + 26, 62)
  ctx.fillText(`速度 ${Math.round(Math.abs(player.speed))}`, vx + 122, 62)
  ctx.fillStyle = '#fff'
  ctx.font = '700 14px "Segoe UI", system-ui, sans-serif'
  ctx.fillText(`道具：${itemLabel(player.item)}`, vx + 26, 88)
}

function roundRectPath(x, y, w, h, r) {
  ctx.beginPath()
  ctx.moveTo(x + r, y)
  ctx.arcTo(x + w, y, x + w, y + h, r)
  ctx.arcTo(x + w, y + h, x, y + h, r)
  ctx.arcTo(x, y + h, x, y, r)
  ctx.arcTo(x, y, x + w, y, r)
  ctx.closePath()
}

function shade(hex, amt) {
  const v = hex.replace('#', '')
  const r = Math.min(255, parseInt(v.slice(0, 2), 16) + amt)
  const g = Math.min(255, parseInt(v.slice(2, 4), 16) + amt)
  const b = Math.min(255, parseInt(v.slice(4, 6), 16) + amt)
  return `rgb(${r},${g},${b})`
}

function itemLabel(item) {
  return { speed: '🍄 加速', bomb: '💣 炸彈', shell: '🟠 追蹤球', star: '🧪 無敵' }[item] || '— 空 —'
}

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  if (k.startsWith('arrow') || k === '/' || k === ' ') e.preventDefault()
  if (phase.value !== 'playing') {
    if (k === ' ' || k === 'enter') startRace()
    keys.add(k)
    return
  }
  if (!e.repeat && game) {
    if (k === game.p1.itemKey && !game.p1.itemHeld) useItem(game.p1)
    if (k === game.p2.itemKey && !game.p2.itemHeld) useItem(game.p2)
  }
  keys.add(k)
}
function onKeyUp(e) {
  keys.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame04Records()
  records.value = store.records
}
function formatDate(iso) {
  const d = new Date(iso)
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}
function idleRender() {
  game = createGame()
  game.countdown = 0
  render()
}

onMounted(async () => {
  ctx = canvasRef.value.getContext('2d')
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('keyup', onKeyUp)
  try {
    const store = await fetchGame04Store()
    records.value = store.records
  } catch {
    /* ignore */
  }
  idleRender()
})
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeyDown)
  window.removeEventListener('keyup', onKeyUp)
  if (rafId) cancelAnimationFrame(rafId)
})
</script>

<style scoped>
.game04-view { min-height: 100vh; padding: 24px clamp(16px,4vw,48px) 48px; color: #e8f3e6; background: radial-gradient(circle at 50% -10%, #1f4a2c, #08160d 60%); font-family: 'Segoe UI', system-ui, sans-serif; }
.topbar { display: flex; align-items: center; gap: 20px; flex-wrap: wrap; margin-bottom: 22px; }
.back-link { color: #9ed8ab; text-decoration: none; font-size: 14px; padding: 8px 14px; border: 1px solid rgba(158,216,171,0.3); border-radius: 999px; transition: 0.2s; }
.back-link:hover { background: rgba(158,216,171,0.12); color: #fff; }
.title-block .eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; color: #ffd23f; text-transform: uppercase; }
.title-block h1 { margin: 2px 0 0; font-size: 26px; background: linear-gradient(90deg,#8de96a,#ffd23f); -webkit-background-clip: text; background-clip: text; color: transparent; }
.round-pill { margin-left: auto; padding: 8px 16px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 13px; }
.layout { display: grid; grid-template-columns: minmax(0,1fr) 300px; gap: 22px; align-items: start; }
.stage-card { background: rgba(8,18,12,0.6); border: 1px solid rgba(141,233,106,0.16); border-radius: 20px; padding: 16px; box-shadow: 0 24px 60px rgba(0,0,0,0.5); }
.scoreband { display: flex; align-items: center; gap: 12px; margin-bottom: 14px; padding: 0 6px; }
.team { display: flex; align-items: center; gap: 9px; flex: 1; }
.team-2 { justify-content: flex-end; }
.team .dot { width: 13px; height: 13px; border-radius: 50%; }
.team strong { font-size: 14px; }
.team .lap { font-size: 12px; color: #b6d6bf; font-variant-numeric: tabular-nums; }
.team .rank { font-size: 12px; font-weight: 700; color: #ffd23f; }
.item-chip { font-size: 11px; font-weight: 700; padding: 3px 8px; border-radius: 999px; background: rgba(255,255,255,0.1); }
.item-chip.empty { color: #7fa085; }
.item-chip.speed { background: rgba(255,210,63,0.25); color: #ffe08a; }
.item-chip.bomb { background: rgba(120,130,160,0.3); color: #d6dde6; }
.item-chip.shell { background: rgba(255,122,58,0.25); color: #ffc0a0; }
.item-chip.star { background: rgba(255,77,109,0.25); color: #ffb3c2; }
.vs { font-size: 12px; font-weight: 800; color: #5e8567; }
.stage-frame { position: relative; border-radius: 14px; overflow: hidden; }
.game-canvas { display: block; width: 100%; height: auto; border-radius: 14px; }
.overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(4,12,7,0.82); backdrop-filter: blur(4px); }
.overlay-card { text-align: center; max-width: 470px; padding: 32px; }
.overlay-eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; color: #ffd23f; }
.overlay-card h2 { margin: 10px 0 14px; font-size: 26px; }
.winner-text { background: linear-gradient(90deg,#8de96a,#ffd23f); -webkit-background-clip: text; background-clip: text; color: transparent; }
.overlay-text { color: #c2dcc8; line-height: 1.7; margin: 0 0 22px; }
.primary-btn { border: none; padding: 12px 30px; border-radius: 999px; font-size: 16px; font-weight: 700; color: #0a2410; background: linear-gradient(90deg,#8de96a,#ffd23f); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; box-shadow: 0 10px 26px rgba(141,233,106,0.4); }
.primary-btn:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(141,233,106,0.55); }
.sidebar { display: flex; flex-direction: column; gap: 16px; }
.panel { background: rgba(8,18,12,0.6); border: 1px solid rgba(141,233,106,0.16); border-radius: 18px; padding: 18px; }
.panel .eyebrow { margin: 0 0 12px; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #72956f; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.controls-grid { display: grid; gap: 10px; }
.ctrl { border-radius: 12px; padding: 12px 14px; }
.ctrl strong { display: block; font-size: 14px; margin-bottom: 6px; }
.ctrl span { display: block; font-size: 13px; color: #c2dcc8; margin-bottom: 3px; }
.ctrl-1 { background: rgba(255,111,60,0.12); border: 1px solid rgba(255,111,60,0.3); }
.ctrl-2 { background: rgba(54,201,255,0.12); border: 1px solid rgba(54,201,255,0.3); }
kbd { background: rgba(255,255,255,0.12); border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 2px 7px; font-size: 12px; font-family: inherit; margin-right: 2px; }
.legend { list-style: none; margin: 0; padding: 0; display: grid; gap: 9px; }
.legend li { display: flex; align-items: center; gap: 10px; font-size: 13px; color: #c8e0cc; }
.ic { width: 26px; height: 26px; border-radius: 7px; display: grid; place-items: center; font-size: 14px; flex-shrink: 0; }
.records { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.records li { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: center; font-size: 12px; padding: 8px 10px; border-radius: 10px; background: rgba(255,255,255,0.04); }
.rec-win { font-weight: 700; color: #ffd23f; }
.rec-score { color: #9ed8ab; }
.rec-date { color: #5e8567; }
.empty { font-size: 13px; color: #72956f; line-height: 1.6; margin: 0; }
.ghost-btn { background: none; border: 1px solid rgba(158,216,171,0.3); color: #9ed8ab; border-radius: 999px; padding: 4px 12px; font-size: 12px; cursor: pointer; }
.ghost-btn:hover { background: rgba(158,216,171,0.12); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } }
</style>
