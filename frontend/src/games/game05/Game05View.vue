<template>
  <main class="game05-view">
    <section class="topbar">
      <RouterLink
        to="/"
        class="back-link"
      >
        ← 返回遊戲廳
      </RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 05</p>
        <h1>雙人貪食蛇對決</h1>
      </div>
      <div class="round-pill">
        第 {{ roundNumber }} 局 · 先贏 2 局
      </div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <span class="dot" />
            <strong>玩家 1</strong>
            <span class="wins">{{ roundWins.p1 }} 勝</span>
            <span class="len">長度 {{ hud.p1Len }} · 蘋果 {{ hud.p1Apples }}</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="len">長度 {{ hud.p2Len }} · 蘋果 {{ hud.p2Apples }}</span>
            <span class="wins">{{ roundWins.p2 }} 勝</span>
            <strong>玩家 2</strong>
            <span class="dot" />
          </div>
        </div>

        <div
          ref="stageRef"
          class="stage-frame"
        >
          <canvas
            ref="canvasRef"
            class="game-canvas"
            :width="CANVAS_W"
            :height="CANVAS_H"
          />

          <transition name="fade">
            <div
              v-if="phase !== 'playing'"
              class="overlay"
            >
              <div class="overlay-card">
                <template v-if="phase === 'intro'">
                  <p class="overlay-eyebrow">霓虹競技場</p>
                  <h2>兩條蛇，一個場地</h2>
                  <p class="overlay-text">
                    搶吃蘋果成長，撞牆、撞自己或撞對方都會出局。<br>
                    競技場會隨時間向內收縮，逼你們正面交鋒。
                  </p>
                  <button
                    class="primary-btn"
                    @click="startMatch"
                  >
                    開始對戰
                  </button>
                </template>

                <template v-else-if="phase === 'roundover'">
                  <p class="overlay-eyebrow">第 {{ roundNumber }} 局結束</p>
                  <h2>{{ roundResultText }}</h2>
                  <p class="overlay-text">
                    目前比數 — 玩家 1 <strong>{{ roundWins.p1 }}</strong> : <strong>{{ roundWins.p2 }}</strong> 玩家 2
                  </p>
                  <button
                    class="primary-btn"
                    @click="nextRound"
                  >
                    下一局
                  </button>
                </template>

                <template v-else-if="phase === 'matchover'">
                  <p class="overlay-eyebrow">對戰結束</p>
                  <h2 class="winner-text">{{ matchResultText }}</h2>
                  <p class="overlay-text">
                    最終比數 {{ roundWins.p1 }} : {{ roundWins.p2 }}
                  </p>
                  <button
                    class="primary-btn"
                    @click="startMatch"
                  >
                    再來一場
                  </button>
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
              <span>W A S D 控制方向</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span>↑ ↓ ← → 控制方向</span>
            </div>
          </div>
        </section>

        <section class="panel">
          <p class="eyebrow">場上道具</p>
          <ul class="legend">
            <li><span class="ic ic-apple">●</span> 蘋果：成長 +1、計分</li>
            <li><span class="ic ic-speed">S</span> 加速：5 秒高速衝刺</li>
            <li><span class="ic ic-ghost">G</span> 穿牆：5 秒可穿越邊界</li>
            <li><span class="ic ic-shrink">C</span> 瘦身：縮短 3 節更靈活</li>
          </ul>
        </section>

        <section class="panel">
          <div class="panel-head">
            <p class="eyebrow">對戰紀錄</p>
            <button
              v-if="records.length"
              class="ghost-btn"
              @click="onClearRecords"
            >
              清除
            </button>
          </div>
          <ul
            v-if="records.length"
            class="records"
          >
            <li
              v-for="(r, i) in records"
              :key="i"
            >
              <span class="rec-win">{{ r.winner }}</span>
              <span class="rec-score">{{ r.scoreP1 }} : {{ r.scoreP2 }}</span>
              <span class="rec-date">{{ formatDate(r.date) }}</span>
            </li>
          </ul>
          <p
            v-else
            class="empty"
          >
            尚無紀錄，開打後自動保存最近 10 場。
          </p>
        </section>
      </aside>
    </section>
  </main>
</template>

<script setup>
import { onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import {
  clearGame05Records,
  fetchGame05Store,
  saveGame05Record
} from './game05Storage'
import { recordGameResult } from '@/data/lobbyScore'
import { assetUrl } from '@/utils/assetUrl'

const COLS = 32
const ROWS = 22
const CELL = 30
const CANVAS_W = COLS * CELL
const CANVAS_H = ROWS * CELL

const BASE_TICK = 135
const MIN_TICK = 72
const SHRINK_EVERY_MS = 11000
const MAX_BORDER = 4
const ROUNDS_TO_WIN = 2

const THEME = {
  p1: { body: '#22d3a8', glow: '#3affd0', head: '#7dffe6' },
  p2: { body: '#ff7ab0', glow: '#ff9ec8', head: '#ffd0e6' }
}

// 像素素材
const G05 = {}
function g05Sprite(name) {
  if (!G05[name]) {
    const img = new Image()
    img.src = assetUrl(`/assets/G05/${name}.png`)
    G05[name] = img
  }
  return G05[name]
}
;['bg-arena', 'food-apple', 'food-berry', 'food-star', 'snake-head-p1', 'snake-head-p2', 'snake-body-p1', 'snake-body-p2', 'tile-wall'].forEach(g05Sprite)
const FOOD_SPRITES = ['food-apple', 'food-berry', 'food-star']

const canvasRef = ref(null)
const stageRef = ref(null)

const phase = ref('intro')
const roundNumber = ref(1)
const roundWins = reactive({ p1: 0, p2: 0 })
const roundResultText = ref('')
const matchResultText = ref('')
const records = ref([])

const hud = reactive({ p1Len: 3, p2Len: 3, p1Apples: 0, p2Apples: 0 })

let ctx = null
let rafId = 0
let lastTime = 0
let game = null
const keys = new Set()

function makeSnake(id, startCell, dir) {
  const cells = []
  for (let i = 0; i < 3; i += 1) {
    cells.push({ x: startCell.x - dir.x * i, y: startCell.y - dir.y * i })
  }
  return {
    id,
    cells,
    prev: cells.map((c) => ({ ...c })),
    dir: { ...dir },
    pendingDir: { ...dir },
    acc: 0,
    tickMs: BASE_TICK,
    alive: true,
    apples: 0,
    growBy: 0,
    ghostUntil: 0,
    speedUntil: 0
  }
}

function createGame() {
  return {
    elapsed: 0,
    border: 0,
    p1: makeSnake('p1', { x: 6, y: ROWS >> 1 }, { x: 1, y: 0 }),
    p2: makeSnake('p2', { x: COLS - 7, y: ROWS >> 1 }, { x: -1, y: 0 }),
    food: [],
    powerups: [],
    particles: [],
    powerupTimer: 4200,
    over: false,
    result: null
  }
}

function bounds() {
  return {
    minX: game.border,
    maxX: COLS - 1 - game.border,
    minY: game.border,
    maxY: ROWS - 1 - game.border
  }
}

function cellOccupied(x, y, ignoreTail) {
  for (const s of [game.p1, game.p2]) {
    const len = s.cells.length
    for (let i = 0; i < len; i += 1) {
      if (ignoreTail && i === len - 1 && s.growBy === 0) continue
      if (s.cells[i].x === x && s.cells[i].y === y) return true
    }
  }
  return false
}

function randomFreeCell() {
  const b = bounds()
  for (let attempt = 0; attempt < 200; attempt += 1) {
    const x = b.minX + Math.floor(Math.random() * (b.maxX - b.minX + 1))
    const y = b.minY + Math.floor(Math.random() * (b.maxY - b.minY + 1))
    if (cellOccupied(x, y, false)) continue
    if (game.food.some((f) => f.x === x && f.y === y)) continue
    if (game.powerups.some((p) => p.x === x && p.y === y)) continue
    return { x, y }
  }
  return null
}

function spawnFood() {
  const c = randomFreeCell()
  if (c) game.food.push(c)
}

function spawnPowerup() {
  const c = randomFreeCell()
  if (!c) return
  const types = ['speed', 'ghost', 'shrink']
  const type = types[Math.floor(Math.random() * types.length)]
  game.powerups.push({ ...c, type, ttl: 9000 })
}

function emitParticles(cell, color, count) {
  const cx = cell.x * CELL + CELL / 2
  const cy = cell.y * CELL + CELL / 2
  for (let i = 0; i < count; i += 1) {
    const a = Math.random() * Math.PI * 2
    const sp = 40 + Math.random() * 140
    game.particles.push({
      x: cx,
      y: cy,
      vx: Math.cos(a) * sp,
      vy: Math.sin(a) * sp,
      life: 1,
      color
    })
  }
}

function startMatch() {
  roundWins.p1 = 0
  roundWins.p2 = 0
  roundNumber.value = 1
  beginRound()
}

function nextRound() {
  roundNumber.value += 1
  beginRound()
}

function beginRound() {
  game = createGame()
  spawnFood()
  spawnFood()
  syncHud()
  phase.value = 'playing'
  lastTime = performance.now()
  loop(lastTime)
}

function syncHud() {
  hud.p1Len = game.p1.cells.length
  hud.p2Len = game.p2.cells.length
  hud.p1Apples = game.p1.apples
  hud.p2Apples = game.p2.apples
}

function applyPending(s) {
  const d = s.pendingDir
  if (d.x === -s.dir.x && d.y === -s.dir.y) return
  s.dir = { ...d }
}

function moveSnake(s, other, now) {
  s.prev = s.cells.map((c) => ({ ...c }))
  applyPending(s)
  const head = s.cells[0]
  let nx = head.x + s.dir.x
  let ny = head.y + s.dir.y
  const b = bounds()
  const ghost = now < s.ghostUntil

  const outside = nx < b.minX || nx > b.maxX || ny < b.minY || ny > b.maxY
  if (outside) {
    if (ghost) {
      if (nx < b.minX) nx = b.maxX
      else if (nx > b.maxX) nx = b.minX
      if (ny < b.minY) ny = b.maxY
      else if (ny > b.maxY) ny = b.minY
    } else {
      killSnake(s)
      return
    }
  }

  // head-to-head
  if (nx === other.cells[0].x && ny === other.cells[0].y) {
    killSnake(s)
    killSnake(other)
    return
  }
  // body collisions (self + opponent)
  for (const seg of [...sBody(s), ...other.cells]) {
    if (seg.x === nx && seg.y === ny) {
      killSnake(s)
      return
    }
  }

  s.cells.unshift({ x: nx, y: ny })

  const fi = game.food.findIndex((f) => f.x === nx && f.y === ny)
  if (fi >= 0) {
    game.food.splice(fi, 1)
    s.apples += 1
    emitParticles({ x: nx, y: ny }, THEME[s.id].glow, 14)
    spawnFood()
  } else if (s.growBy > 0) {
    s.growBy -= 1
  } else {
    s.cells.pop()
  }

  const pi = game.powerups.findIndex((p) => p.x === nx && p.y === ny)
  if (pi >= 0) {
    applyPowerup(s, game.powerups[pi], now)
    game.powerups.splice(pi, 1)
  }
}

function sBody(s) {
  // self body excluding moving tail
  if (s.growBy > 0) return s.cells.slice(0)
  return s.cells.slice(0, -1)
}

function applyPowerup(s, p, now) {
  emitParticles({ x: p.x, y: p.y }, '#ffe66d', 18)
  if (p.type === 'speed') {
    s.speedUntil = now + 5000
  } else if (p.type === 'ghost') {
    s.ghostUntil = now + 5000
  } else if (p.type === 'shrink') {
    for (let i = 0; i < 3 && s.cells.length > 3; i += 1) s.cells.pop()
  }
}

function killSnake(s) {
  if (!s.alive) return
  s.alive = false
  for (const c of s.cells) emitParticles(c, THEME[s.id].body, 4)
}

function effectiveTick(s, now) {
  let t = BASE_TICK - game.p1.apples - game.p2.apples - game.elapsed / 700
  t = Math.max(MIN_TICK, t)
  if (now < s.speedUntil) t *= 0.58
  return t
}

function resolveRound() {
  const p1 = game.p1
  const p2 = game.p2
  if (!p1.alive && !p2.alive) {
    roundResultText.value = '雙雙出局，平手！'
  } else if (!p2.alive) {
    roundWins.p1 += 1
    roundResultText.value = '玩家 1 勝出！'
  } else {
    roundWins.p2 += 1
    roundResultText.value = '玩家 2 勝出！'
  }

  if (roundWins.p1 >= ROUNDS_TO_WIN || roundWins.p2 >= ROUNDS_TO_WIN) {
    finishMatch()
  } else {
    phase.value = 'roundover'
  }
}

async function finishMatch() {
  let winner
  if (roundWins.p1 > roundWins.p2) winner = '玩家 1 獲勝'
  else if (roundWins.p2 > roundWins.p1) winner = '玩家 2 獲勝'
  else winner = '平手'
  matchResultText.value = `🏆 ${winner}`
  phase.value = 'matchover'
  recordGameResult(
    '/game05',
    roundWins.p1 > roundWins.p2 ? 'p1' : roundWins.p2 > roundWins.p1 ? 'p2' : 'draw'
  )

  try {
    const store = await saveGame05Record({
      winner,
      scoreP1: roundWins.p1,
      scoreP2: roundWins.p2,
      date: new Date().toISOString()
    })
    records.value = store.records
  } catch {
    /* ignore storage errors */
  }
}

function update(dt, now) {
  game.elapsed += dt
  const newBorder = Math.min(MAX_BORDER, Math.floor(game.elapsed / SHRINK_EVERY_MS))
  game.border = newBorder

  for (const s of [game.p1, game.p2]) {
    if (!s.alive) continue
    s.tickMs = effectiveTick(s, now)
    s.acc += dt
    while (s.acc >= s.tickMs && s.alive) {
      s.acc -= s.tickMs
      const other = s === game.p1 ? game.p2 : game.p1
      moveSnake(s, other, now)
    }
  }

  game.powerupTimer -= dt
  if (game.powerupTimer <= 0) {
    game.powerupTimer = 7000 + Math.random() * 5000
    if (game.powerups.length < 2) spawnPowerup()
  }
  for (const p of game.powerups) p.ttl -= dt
  game.powerups = game.powerups.filter((p) => p.ttl > 0)

  for (const pt of game.particles) {
    pt.x += pt.vx * (dt / 1000)
    pt.y += pt.vy * (dt / 1000)
    pt.vx *= 0.92
    pt.vy *= 0.92
    pt.life -= dt / 600
  }
  game.particles = game.particles.filter((p) => p.life > 0)

  syncHud()

  if (!game.over && (!game.p1.alive || !game.p2.alive)) {
    game.over = true
    game.overAt = now
  }
  if (game.over && now - game.overAt > 650) {
    cancelAnimationFrame(rafId)
    rafId = 0
    resolveRound()
  }
}

function loop(now) {
  const dt = Math.min(48, now - lastTime)
  lastTime = now
  if (phase.value === 'playing') {
    update(dt, now)
    render(now)
    rafId = requestAnimationFrame(loop)
  }
}

function render(now) {
  ctx.clearRect(0, 0, CANVAS_W, CANVAS_H)

  // background
  ctx.imageSmoothingEnabled = false
  const bgImg = g05Sprite('bg-arena')
  if (bgImg.complete && bgImg.naturalWidth > 0) {
    ctx.drawImage(bgImg, 0, 0, CANVAS_W, CANVAS_H)
  } else {
    const bg = ctx.createLinearGradient(0, 0, CANVAS_W, CANVAS_H)
    bg.addColorStop(0, '#0b1224')
    bg.addColorStop(1, '#131a33')
    ctx.fillStyle = bg
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  }

  // grid
  ctx.strokeStyle = 'rgba(120,150,220,0.07)'
  ctx.lineWidth = 1
  for (let x = 0; x <= COLS; x += 1) {
    ctx.beginPath()
    ctx.moveTo(x * CELL, 0)
    ctx.lineTo(x * CELL, CANVAS_H)
    ctx.stroke()
  }
  for (let y = 0; y <= ROWS; y += 1) {
    ctx.beginPath()
    ctx.moveTo(0, y * CELL)
    ctx.lineTo(CANVAS_W, y * CELL)
    ctx.stroke()
  }

  // danger border —— 收縮區用磚牆鋪滿
  if (game.border > 0) {
    const b = bounds()
    const wall = g05Sprite('tile-wall')
    const wallReady = wall.complete && wall.naturalWidth > 0
    for (let x = 0; x < COLS; x += 1) {
      for (let y = 0; y < ROWS; y += 1) {
        if (x < b.minX || x > b.maxX || y < b.minY || y > b.maxY) {
          if (wallReady) {
            ctx.drawImage(wall, x * CELL, y * CELL, CELL, CELL)
          } else {
            ctx.fillStyle = 'rgba(255,70,90,0.14)'
            ctx.fillRect(x * CELL, y * CELL, CELL, CELL)
          }
        }
      }
    }
  }

  // food
  for (const f of game.food) {
    const cx = f.x * CELL + CELL / 2
    const cy = f.y * CELL + CELL / 2
    const pulse = 0.5 + 0.5 * Math.sin(now / 220)
    const name = FOOD_SPRITES[((f.x + f.y) % FOOD_SPRITES.length + FOOD_SPRITES.length) % FOOD_SPRITES.length]
    const fimg = g05Sprite(name)
    if (fimg.complete && fimg.naturalWidth > 0) {
      const sz = CELL * (0.86 + pulse * 0.1)
      ctx.drawImage(fimg, cx - sz / 2, cy - sz / 2, sz, sz)
    } else {
      ctx.save()
      ctx.fillStyle = '#ff6b7a'
      ctx.beginPath()
      ctx.arc(cx, cy, CELL * 0.32, 0, Math.PI * 2)
      ctx.fill()
      ctx.restore()
    }
  }

  // powerups
  for (const p of game.powerups) {
    drawPowerup(p, now)
  }

  // snakes
  drawSnake(game.p1, THEME.p1, now)
  drawSnake(game.p2, THEME.p2, now)

  // particles
  for (const pt of game.particles) {
    ctx.globalAlpha = Math.max(0, pt.life)
    ctx.fillStyle = pt.color
    ctx.beginPath()
    ctx.arc(pt.x, pt.y, 3 * pt.life + 1, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.globalAlpha = 1
}

function drawPowerup(p, now) {
  const cx = p.x * CELL + CELL / 2
  const cy = p.y * CELL + CELL / 2
  const colors = { speed: '#ffd23f', ghost: '#8e7bff', shrink: '#4dd0ff' }
  const letters = { speed: 'S', ghost: 'G', shrink: 'C' }
  const fade = p.ttl < 2500 ? 0.4 + 0.6 * Math.abs(Math.sin(now / 120)) : 1
  ctx.save()
  ctx.globalAlpha = fade
  ctx.translate(cx, cy)
  ctx.rotate(Math.PI / 4)
  ctx.shadowColor = colors[p.type]
  ctx.shadowBlur = 16
  ctx.fillStyle = colors[p.type]
  const r = CELL * 0.34
  ctx.fillRect(-r, -r, r * 2, r * 2)
  ctx.restore()
  ctx.save()
  ctx.globalAlpha = fade
  ctx.fillStyle = '#10131f'
  ctx.font = `bold ${CELL * 0.5}px system-ui, sans-serif`
  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  ctx.fillText(letters[p.type], cx, cy + 1)
  ctx.restore()
}

function lerp(a, b, t) {
  return a + (b - a) * t
}

function drawSnake(s, theme, now) {
  const t = s.alive ? Math.min(1, s.acc / s.tickMs) : 1
  const ghost = now < s.ghostUntil
  const len = s.cells.length
  ctx.save()
  if (s.alive) {
    ctx.shadowColor = theme.glow
    ctx.shadowBlur = ghost ? 22 : 12
  }
  ctx.globalAlpha = s.alive ? (ghost ? 0.6 : 1) : 0.25

  const headImg = g05Sprite(`snake-head-${s.id}`)
  const bodyImg = g05Sprite(`snake-body-${s.id}`)
  const spritesReady = headImg.complete && headImg.naturalWidth > 0 && bodyImg.complete && bodyImg.naturalWidth > 0

  for (let i = len - 1; i >= 0; i -= 1) {
    const cur = s.cells[i]
    const prev = s.prev[i] || cur
    // avoid sliding across wrap teleport
    let px = lerp(prev.x, cur.x, t)
    let py = lerp(prev.y, cur.y, t)
    if (Math.abs(cur.x - prev.x) > 1 || Math.abs(cur.y - prev.y) > 1) {
      px = cur.x
      py = cur.y
    }
    const x = px * CELL
    const y = py * CELL
    const head = i === 0

    if (spritesReady) {
      if (head) {
        ctx.save()
        ctx.translate(x + CELL / 2, y + CELL / 2)
        ctx.rotate(Math.atan2(s.dir.y, s.dir.x))
        ctx.drawImage(headImg, -CELL / 2, -CELL / 2, CELL, CELL)
        ctx.restore()
      } else {
        ctx.drawImage(bodyImg, x, y, CELL, CELL)
      }
    } else {
      ctx.fillStyle = head ? theme.head : theme.body
      const inset = head ? 1.5 : 3
      roundRect(x + inset, y + inset, CELL - inset * 2, CELL - inset * 2, head ? 9 : 7)
      ctx.fill()
    }
  }
  ctx.restore()

  // eyes on head（精靈自帶眼睛時略過）
  if (s.alive && !spritesReady) {
    const cur = s.cells[0]
    const prev = s.prev[0] || cur
    let px = lerp(prev.x, cur.x, t)
    let py = lerp(prev.y, cur.y, t)
    if (Math.abs(cur.x - prev.x) > 1 || Math.abs(cur.y - prev.y) > 1) {
      px = cur.x
      py = cur.y
    }
    const hx = px * CELL + CELL / 2
    const hy = py * CELL + CELL / 2
    const dx = s.dir.x
    const dy = s.dir.y
    ctx.fillStyle = '#0c1020'
    const off = CELL * 0.18
    const perp = CELL * 0.16
    ctx.beginPath()
    ctx.arc(hx + dx * off - dy * perp, hy + dy * off - dx * perp, 2.4, 0, Math.PI * 2)
    ctx.arc(hx + dx * off + dy * perp, hy + dy * off + dx * perp, 2.4, 0, Math.PI * 2)
    ctx.fill()
  }
}

function roundRect(x, y, w, h, r) {
  ctx.beginPath()
  ctx.moveTo(x + r, y)
  ctx.arcTo(x + w, y, x + w, y + h, r)
  ctx.arcTo(x + w, y + h, x, y + h, r)
  ctx.arcTo(x, y + h, x, y, r)
  ctx.arcTo(x, y, x + w, y, r)
  ctx.closePath()
}

function setDir(s, x, y) {
  s.pendingDir = { x, y }
}

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  if (keys.has(k)) return
  keys.add(k)

  if (phase.value === 'playing' && game) {
    switch (k) {
      case 'w': setDir(game.p1, 0, -1); break
      case 's': setDir(game.p1, 0, 1); break
      case 'a': setDir(game.p1, -1, 0); break
      case 'd': setDir(game.p1, 1, 0); break
      case 'arrowup': setDir(game.p2, 0, -1); e.preventDefault(); break
      case 'arrowdown': setDir(game.p2, 0, 1); e.preventDefault(); break
      case 'arrowleft': setDir(game.p2, -1, 0); e.preventDefault(); break
      case 'arrowright': setDir(game.p2, 1, 0); e.preventDefault(); break
      default: break
    }
  }

  if ((k === ' ' || k === 'enter')) {
    if (phase.value === 'intro' || phase.value === 'matchover') startMatch()
    else if (phase.value === 'roundover') nextRound()
  }
}

function onKeyUp(e) {
  keys.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame05Records()
  records.value = store.records
}

function formatDate(iso) {
  const d = new Date(iso)
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(
    d.getMinutes()
  ).padStart(2, '0')}`
}

onMounted(async () => {
  ctx = canvasRef.value.getContext('2d')
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('keyup', onKeyUp)
  try {
    const store = await fetchGame05Store()
    records.value = store.records
  } catch {
    /* ignore */
  }
  // idle preview render
  game = createGame()
  render(performance.now())
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeyDown)
  window.removeEventListener('keyup', onKeyUp)
  if (rafId) cancelAnimationFrame(rafId)
})
</script>

<style scoped>
.game05-view {
  min-height: 100vh;
  padding: 24px clamp(16px, 4vw, 48px) 48px;
  color: #e8edff;
  background: radial-gradient(circle at 20% 0%, #1a2347, #0a0e1d 60%);
  font-family: 'Segoe UI', system-ui, sans-serif;
}

.topbar {
  display: flex;
  align-items: center;
  gap: 20px;
  flex-wrap: wrap;
  margin-bottom: 22px;
}

.back-link {
  color: #9fb0e8;
  text-decoration: none;
  font-size: 14px;
  padding: 8px 14px;
  border: 1px solid rgba(159, 176, 232, 0.3);
  border-radius: 999px;
  transition: 0.2s;
}
.back-link:hover {
  background: rgba(159, 176, 232, 0.12);
  color: #fff;
}

.title-block .eyebrow {
  margin: 0;
  font-size: 12px;
  letter-spacing: 3px;
  color: #5fe6c4;
  text-transform: uppercase;
}
.title-block h1 {
  margin: 2px 0 0;
  font-size: 26px;
  background: linear-gradient(90deg, #5fe6c4, #ff7ab0);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}

.round-pill {
  margin-left: auto;
  padding: 8px 16px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.06);
  border: 1px solid rgba(255, 255, 255, 0.12);
  font-size: 13px;
}

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 300px;
  gap: 22px;
  align-items: start;
}

.stage-card {
  background: rgba(10, 14, 30, 0.6);
  border: 1px solid rgba(120, 150, 220, 0.18);
  border-radius: 20px;
  padding: 16px;
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.45);
}

.scoreband {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 14px;
  padding: 0 6px;
}
.team {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
}
.team-2 {
  justify-content: flex-end;
}
.team .dot {
  width: 14px;
  height: 14px;
  border-radius: 50%;
}
.team-1 .dot {
  background: #3affd0;
  box-shadow: 0 0 12px #3affd0;
}
.team-2 .dot {
  background: #ff9ec8;
  box-shadow: 0 0 12px #ff9ec8;
}
.team strong {
  font-size: 15px;
}
.team .wins {
  font-size: 13px;
  font-weight: 700;
  color: #ffe66d;
}
.team .len {
  font-size: 12px;
  color: #9fb0e8;
}
.vs {
  font-size: 13px;
  font-weight: 800;
  color: #6b78a8;
}

.stage-frame {
  position: relative;
  border-radius: 14px;
  overflow: hidden;
}
.game-canvas {
  display: block;
  width: 100%;
  height: auto;
  border-radius: 14px;
}

.overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(6, 9, 20, 0.78);
  backdrop-filter: blur(4px);
}
.overlay-card {
  text-align: center;
  max-width: 440px;
  padding: 32px;
}
.overlay-eyebrow {
  margin: 0;
  font-size: 12px;
  letter-spacing: 3px;
  text-transform: uppercase;
  color: #5fe6c4;
}
.overlay-card h2 {
  margin: 10px 0 14px;
  font-size: 30px;
}
.winner-text {
  background: linear-gradient(90deg, #ffe66d, #ff7ab0);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.overlay-text {
  color: #b9c4ec;
  line-height: 1.7;
  margin: 0 0 22px;
}
.primary-btn {
  border: none;
  padding: 12px 30px;
  border-radius: 999px;
  font-size: 16px;
  font-weight: 700;
  color: #06121a;
  background: linear-gradient(90deg, #5fe6c4, #46d0ff);
  cursor: pointer;
  transition: transform 0.15s, box-shadow 0.15s;
  box-shadow: 0 10px 26px rgba(70, 208, 255, 0.35);
}
.primary-btn:hover {
  transform: translateY(-2px);
  box-shadow: 0 14px 32px rgba(70, 208, 255, 0.5);
}

.sidebar {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.panel {
  background: rgba(10, 14, 30, 0.6);
  border: 1px solid rgba(120, 150, 220, 0.18);
  border-radius: 18px;
  padding: 18px;
}
.panel .eyebrow {
  margin: 0 0 12px;
  font-size: 11px;
  letter-spacing: 2px;
  text-transform: uppercase;
  color: #7d8cc0;
}
.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.controls-grid {
  display: grid;
  gap: 10px;
}
.ctrl {
  border-radius: 12px;
  padding: 12px 14px;
}
.ctrl strong {
  display: block;
  font-size: 14px;
  margin-bottom: 4px;
}
.ctrl span {
  font-size: 13px;
  color: #b9c4ec;
}
.ctrl-1 {
  background: rgba(58, 255, 208, 0.1);
  border: 1px solid rgba(58, 255, 208, 0.25);
}
.ctrl-2 {
  background: rgba(255, 158, 200, 0.1);
  border: 1px solid rgba(255, 158, 200, 0.25);
}

.legend {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 10px;
}
.legend li {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 13px;
  color: #c8d2f4;
}
.ic {
  width: 24px;
  height: 24px;
  border-radius: 7px;
  display: grid;
  place-items: center;
  font-size: 12px;
  font-weight: 800;
  color: #10131f;
  flex-shrink: 0;
}
.ic-apple {
  background: #ff6b7a;
  color: #fff;
}
.ic-speed {
  background: #ffd23f;
}
.ic-ghost {
  background: #8e7bff;
}
.ic-shrink {
  background: #4dd0ff;
}

.records {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 8px;
}
.records li {
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 8px;
  align-items: center;
  font-size: 12px;
  padding: 8px 10px;
  border-radius: 10px;
  background: rgba(255, 255, 255, 0.04);
}
.rec-win {
  font-weight: 700;
  color: #ffe66d;
}
.rec-score {
  color: #9fb0e8;
}
.rec-date {
  color: #6b78a8;
}
.empty {
  font-size: 13px;
  color: #7d8cc0;
  line-height: 1.6;
  margin: 0;
}

.ghost-btn {
  background: none;
  border: 1px solid rgba(159, 176, 232, 0.3);
  color: #9fb0e8;
  border-radius: 999px;
  padding: 4px 12px;
  font-size: 12px;
  cursor: pointer;
}
.ghost-btn:hover {
  background: rgba(159, 176, 232, 0.12);
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.25s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 920px) {
  .layout {
    grid-template-columns: 1fr;
  }
}
</style>
