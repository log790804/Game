<template>
  <main class="game12-view">
    <section class="topbar">
      <RouterLink to="/" class="back-link">← 返回遊戲廳</RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 12</p>
        <h1>砲彈對決</h1>
      </div>
      <div class="round-pill">即時對戰 · 三局兩勝</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <span class="dot" />
            <strong>玩家 1</strong>
            <span class="hp-wrap"><span class="hp-fill p1" :style="{ width: hud.p1Hp + '%' }" /></span>
            <span class="wins">{{ roundWins.p1 }} 勝</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="wins">{{ roundWins.p2 }} 勝</span>
            <span class="hp-wrap"><span class="hp-fill p2" :style="{ width: hud.p2Hp + '%' }" /></span>
            <strong>玩家 2</strong>
            <span class="dot" />
          </div>
        </div>

        <div ref="stageRef" class="stage-frame">
          <canvas ref="canvasRef" class="game-canvas" :width="CANVAS_W" :height="CANVAS_H" />
          <transition name="fade">
            <div v-if="phase !== 'playing'" class="overlay">
              <div class="overlay-card">
                <template v-if="phase === 'intro'">
                  <p class="overlay-eyebrow">機械城邦 · 即時砲戰</p>
                  <h2>同時開火，蓄力決勝</h2>
                  <p class="overlay-text">
                    雙方<b>同時</b>行動：前後移動找角度、長按蓄力放開發射巨型砲彈。<br>
                    不能越過中線進入對方領地，先把對手打到 0 血者贏下一局。
                  </p>
                  <button class="primary-btn" @click="startMatch">開始對戰</button>
                </template>
                <template v-else-if="phase === 'roundover'">
                  <p class="overlay-eyebrow">第 {{ roundNumber }} 局結束</p>
                  <h2>{{ roundResultText }}</h2>
                  <p class="overlay-text">目前 {{ roundWins.p1 }} : {{ roundWins.p2 }}</p>
                  <button class="primary-btn" @click="nextRound">下一局</button>
                </template>
                <template v-else-if="phase === 'matchover'">
                  <p class="overlay-eyebrow">對戰結束</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <p class="overlay-text">最終 {{ roundWins.p1 }} : {{ roundWins.p2 }}</p>
                  <button class="primary-btn" @click="startMatch">再來一場</button>
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
              <span><kbd>A</kbd><kbd>D</kbd> 移動 · <kbd>W</kbd><kbd>S</kbd> 仰角</span>
              <span><kbd>空白</kbd> 長按蓄力 · 放開發射</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span><kbd>←</kbd><kbd>→</kbd> 移動 · <kbd>↑</kbd><kbd>↓</kbd> 仰角</span>
              <span><kbd>/</kbd> 長按蓄力 · 放開發射</span>
            </div>
          </div>
        </section>
        <section class="panel">
          <p class="eyebrow">戰況</p>
          <div class="stat-grid">
            <div class="stat ctrl-1"><span>P1 仰角</span><b>{{ Math.round(live.p1Angle) }}°</b></div>
            <div class="stat ctrl-2"><span>P2 仰角</span><b>{{ Math.round(live.p2Angle) }}°</b></div>
          </div>
          <ul class="tips">
            <li>雙方同時行動，蓄力越久射程越遠。</li>
            <li>可前後移動，但不能越過中線踏進對方領地。</li>
            <li>直接命中傷害最高，近距離爆風也會扣血。</li>
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
              <span class="rec-score">{{ r.scoreP1 }} : {{ r.scoreP2 }}</span>
              <span class="rec-date">{{ formatDate(r.date) }}</span>
            </li>
          </ul>
          <p v-else class="empty">尚無紀錄，對戰結束後自動保存最近 10 場。</p>
        </section>
      </aside>
    </section>
  </main>
</template>

<script setup>
import { onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { clearGame12Records, fetchGame12Store, saveGame12Record } from './game12Storage'
import { recordGameResult } from '@/data/lobbyScore'
import { assetUrl } from '@/utils/assetUrl'

const CANVAS_W = 960
const CANVAS_H = 560
const CENTER = CANVAS_W / 2
const GROUND = 472
const MARGIN = 52
const MIDZONE = 74 // half-width of the central no-man's-land
const ROUNDS_TO_WIN = 2

const MOVE_SPEED = 2.4 // px per frame
const AIM_RATE = 1.1 // deg per frame
const ANGLE_MIN = 12
const ANGLE_MAX = 82
const CHARGE_RATE = 0.13 // power per ms; oscillates 0->100->0 (~0.77s each way)
const GRAV = 0.17 // per substep
const SUB = 2
const RELOAD_MS = 420
const SHELL_R = 11
const BLAST = 88
const TANK_HIT_R = 28

const P1_MIN = MARGIN
const P1_MAX = CENTER - MIDZONE
const P2_MIN = CENTER + MIDZONE
const P2_MAX = CANVAS_W - MARGIN

// 像素素材
const G12 = {}
function g12Sprite(name) {
  if (!G12[name]) {
    const img = new Image()
    img.src = assetUrl(`/assets/G12/${name}.png`)
    G12[name] = img
  }
  return G12[name]
}
;['bg-castle', 'tank-p1-body-l', 'tank-p1-body-r', 'tank-p2-body-l', 'tank-p2-body-r', 'tank-p1-hit-l', 'tank-p1-hit-r', 'tank-p2-hit-l', 'tank-p2-hit-r', 'barrel-p1', 'barrel-p2', 'cannonball'].forEach(g12Sprite)
function g12ready(img) {
  return img && img.complete && img.naturalWidth > 0
}

const PAL = {
  p1: { body: '#2bd6b0', dark: '#11705e', glow: '#5fffe0', trim: '#aaffee' },
  p2: { body: '#ff6fa8', dark: '#9c3563', glow: '#ffb0d4', trim: '#ffd6e8' }
}

const canvasRef = ref(null)
const stageRef = ref(null)
const phase = ref('intro')
const roundNumber = ref(1)
const roundWins = reactive({ p1: 0, p2: 0 })
const hud = reactive({ p1Hp: 100, p2Hp: 100 })
const live = reactive({ p1Angle: 45, p2Angle: 45 })
const roundResultText = ref('')
const resultText = ref('')
const records = ref([])

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null
const keys = new Set()

function makeTank(x, facing) {
  return {
    x,
    facing,
    hp: 100,
    angle: 45,
    charging: false,
    power: 0,
    chargeDir: 1,
    shell: null,
    cooldown: 0,
    flash: 0,
    hitFlash: 0,
    recoil: 0
  }
}

function makeEmbers() {
  const list = []
  for (let i = 0; i < 46; i += 1) {
    list.push(spawnEmber(true))
  }
  return list
}
function spawnEmber(initial) {
  return {
    x: Math.random() * CANVAS_W,
    y: initial ? Math.random() * CANVAS_H : CANVAS_H + 10,
    vx: (Math.random() - 0.5) * 14,
    vy: -16 - Math.random() * 30,
    r: 0.8 + Math.random() * 2,
    hue: Math.random() < 0.5 ? '#ffb15a' : '#ff6f9c',
    tw: Math.random() * 6
  }
}

function makeGears() {
  return [
    { x: 150, y: 150, r: 60, teeth: 12, spd: 0.00016, dir: 1, col: 'rgba(120,150,210,0.16)' },
    { x: 210, y: 230, r: 38, teeth: 10, spd: -0.00028, dir: -1, col: 'rgba(120,150,210,0.13)' },
    { x: CANVAS_W - 150, y: 150, r: 60, teeth: 12, spd: -0.00016, dir: -1, col: 'rgba(120,150,210,0.16)' },
    { x: CANVAS_W - 210, y: 230, r: 38, teeth: 10, spd: 0.00028, dir: 1, col: 'rgba(120,150,210,0.13)' },
    { x: CENTER, y: 120, r: 46, teeth: 11, spd: 0.0002, dir: 1, col: 'rgba(150,180,240,0.12)' }
  ]
}

function createGame() {
  return {
    p1: makeTank(CENTER - 230, 1),
    p2: makeTank(CENTER + 230, -1),
    particles: [],
    embers: makeEmbers(),
    gears: makeGears(),
    shake: 0,
    ending: false,
    endAt: 0
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
  hud.p1Hp = 100
  hud.p2Hp = 100
  phase.value = 'playing'
  lastFrame = performance.now()
  loop(lastFrame)
}

function barrelTip(p) {
  const rad = (p.angle * Math.PI) / 180
  const bx = p.x
  const by = GROUND - 40 - p.recoil
  const len = 40
  return {
    x: bx + Math.cos(rad) * len * p.facing,
    y: by - Math.sin(rad) * len
  }
}

function fireShell(p) {
  const rad = (p.angle * Math.PI) / 180
  const v0 = 3.2 + p.power * 0.082
  const tip = barrelTip(p)
  p.shell = {
    x: tip.x,
    y: tip.y,
    vx: Math.cos(rad) * v0 * p.facing,
    vy: -Math.sin(rad) * v0,
    trail: [],
    spin: 0
  }
  emitMuzzle(p, tip)
  p.flash = 1
  p.recoil = 8
  p.power = 0
  p.charging = false
  game.shake = Math.max(game.shake, 5)
}

function releaseFire(p) {
  if (p.charging && !p.shell && p.cooldown <= 0) fireShell(p)
  p.charging = false
}

function emitMuzzle(p, tip) {
  const rad = (p.angle * Math.PI) / 180
  const dx = Math.cos(rad) * p.facing
  const dy = -Math.sin(rad)
  for (let i = 0; i < 14; i += 1) {
    const spread = (Math.random() - 0.5) * 1.0
    const sp = 80 + Math.random() * 200
    const ca = Math.cos(spread)
    const sa = Math.sin(spread)
    const rx = dx * ca - dy * sa
    const ry = dx * sa + dy * ca
    game.particles.push({
      x: tip.x,
      y: tip.y,
      vx: rx * sp,
      vy: ry * sp,
      life: 0.5 + Math.random() * 0.3,
      r: 2 + Math.random() * 2,
      color: Math.random() < 0.6 ? '#fff0b0' : '#ff9a3a'
    })
  }
}

function explode(ex, ey) {
  game.shake = Math.max(game.shake, 12)
  for (let i = 0; i < 38; i += 1) {
    const a = Math.random() * Math.PI * 2
    const sp = 50 + Math.random() * 260
    const fire = Math.random() < 0.7
    game.particles.push({
      x: ex,
      y: ey,
      vx: Math.cos(a) * sp,
      vy: Math.sin(a) * sp - 40,
      life: 0.5 + Math.random() * 0.6,
      r: 2 + Math.random() * 3.5,
      color: fire ? (Math.random() < 0.5 ? '#ff7a3a' : '#ffd23f') : 'rgba(120,120,130,0.9)'
    })
  }
  // shockwave ring marker
  game.particles.push({ ring: true, x: ex, y: ey, life: 0.4, r: 8, color: 'rgba(255,210,120,0.8)' })

  for (const id of ['p1', 'p2']) {
    const t = game[id]
    const ty = GROUND - 24
    const d = Math.hypot(ex - t.x, ey - ty)
    if (d < BLAST) {
      const dmg = d < 28 ? 48 : Math.round(40 * (1 - d / BLAST))
      if (dmg > 0) {
        t.hp = Math.max(0, t.hp - dmg)
        t.hitFlash = 1
      }
    }
  }
}

function stepTank(p, id, dt) {
  const f = dt / 16.67
  let left = false
  let right = false
  let up = false
  let down = false
  if (id === 'p1') {
    left = keys.has('a')
    right = keys.has('d')
    up = keys.has('w')
    down = keys.has('s')
  } else {
    left = keys.has('arrowleft')
    right = keys.has('arrowright')
    up = keys.has('arrowup')
    down = keys.has('arrowdown')
  }
  if (left) p.x -= MOVE_SPEED * f
  if (right) p.x += MOVE_SPEED * f
  if (id === 'p1') p.x = Math.max(P1_MIN, Math.min(P1_MAX, p.x))
  else p.x = Math.max(P2_MIN, Math.min(P2_MAX, p.x))

  if (up) p.angle = Math.min(ANGLE_MAX, p.angle + AIM_RATE * f)
  if (down) p.angle = Math.max(ANGLE_MIN, p.angle - AIM_RATE * f)

  if (p.charging && !p.shell && p.cooldown <= 0) {
    // oscillate 0 -> 100 -> 0 -> 100 while held; release fires at current value
    p.power += dt * CHARGE_RATE * p.chargeDir
    if (p.power >= 100) { p.power = 100; p.chargeDir = -1 }
    else if (p.power <= 0) { p.power = 0; p.chargeDir = 1 }
  }
  p.cooldown = Math.max(0, p.cooldown - dt)
  p.flash = Math.max(0, p.flash - dt / 280)
  p.hitFlash = Math.max(0, p.hitFlash - dt / 420)
  p.recoil += (0 - p.recoil) * Math.min(1, dt / 90)
}

function updateShell(p) {
  if (!p.shell) return
  const s = p.shell
  for (let i = 0; i < SUB; i += 1) {
    s.vy += GRAV
    s.x += s.vx
    s.y += s.vy
    s.spin += 0.3
    for (const id of ['p1', 'p2']) {
      const t = game[id]
      if (t === p) continue
      const ty = GROUND - 26
      if (Math.hypot(s.x - t.x, s.y - ty) < TANK_HIT_R + SHELL_R) {
        explode(s.x, s.y)
        p.shell = null
        p.cooldown = RELOAD_MS
        return
      }
    }
    if (s.y >= GROUND - SHELL_R) {
      explode(s.x, GROUND - SHELL_R)
      p.shell = null
      p.cooldown = RELOAD_MS
      return
    }
    if (s.x < -30 || s.x > CANVAS_W + 30) {
      p.shell = null
      p.cooldown = RELOAD_MS
      return
    }
  }
  s.trail.push({ x: s.x, y: s.y })
  if (s.trail.length > 22) s.trail.shift()
}

function updateParticles(dt) {
  for (const pt of game.particles) {
    if (pt.ring) {
      pt.r += dt * 0.5
      pt.life -= dt / 400
      continue
    }
    pt.x += pt.vx * (dt / 1000)
    pt.y += pt.vy * (dt / 1000)
    pt.vy += 360 * (dt / 1000)
    pt.life -= dt / 800
  }
  game.particles = game.particles.filter((p) => p.life > 0)
}

function updateEmbers(dt) {
  for (const e of game.embers) {
    e.x += e.vx * (dt / 1000)
    e.y += e.vy * (dt / 1000)
    e.tw += dt / 200
    if (e.y < -10) Object.assign(e, spawnEmber(false))
  }
}

function update(dt, now) {
  stepTank(game.p1, 'p1', dt)
  stepTank(game.p2, 'p2', dt)
  updateShell(game.p1)
  updateShell(game.p2)
  updateParticles(dt)
  updateEmbers(dt)
  game.shake *= 0.88

  hud.p1Hp = game.p1.hp
  hud.p2Hp = game.p2.hp
  live.p1Angle = game.p1.angle
  live.p2Angle = game.p2.angle

  if (!game.ending && (game.p1.hp <= 0 || game.p2.hp <= 0)) {
    game.ending = true
    game.endAt = now
  }
  if (game.ending && now - game.endAt > 800 && game.particles.length < 8) {
    resolveRound()
  }
}

function resolveRound() {
  cancelAnimationFrame(rafId)
  rafId = 0
  if (game.p1.hp <= 0 && game.p2.hp <= 0) roundResultText.value = '同歸於盡，平手！'
  else if (game.p2.hp <= 0) {
    roundWins.p1 += 1
    roundResultText.value = '玩家 1 勝出！'
  } else {
    roundWins.p2 += 1
    roundResultText.value = '玩家 2 勝出！'
  }
  if (roundWins.p1 >= ROUNDS_TO_WIN || roundWins.p2 >= ROUNDS_TO_WIN) finishMatch()
  else phase.value = 'roundover'
}

async function finishMatch() {
  let winner
  if (roundWins.p1 > roundWins.p2) winner = '玩家 1 獲勝'
  else if (roundWins.p2 > roundWins.p1) winner = '玩家 2 獲勝'
  else winner = '平手'
  resultText.value = `💥 ${winner}`
  phase.value = 'matchover'
  recordGameResult('/game12', roundWins.p1 > roundWins.p2 ? 'p1' : roundWins.p2 > roundWins.p1 ? 'p2' : 'draw')
  try {
    const store = await saveGame12Record({ winner, scoreP1: roundWins.p1, scoreP2: roundWins.p2, date: new Date().toISOString() })
    records.value = store.records
  } catch {
    /* ignore */
  }
}

function loop(now) {
  const dt = Math.min(40, now - lastFrame)
  lastFrame = now
  if (phase.value === 'playing') {
    update(dt, now)
    render(now)
    rafId = requestAnimationFrame(loop)
  }
}

/* ---------------- rendering ---------------- */

function render(now) {
  const sx = (Math.random() - 0.5) * game.shake
  const sy = (Math.random() - 0.5) * game.shake
  ctx.save()
  ctx.translate(sx, sy)

  ctx.imageSmoothingEnabled = false
  const bgImg = g12Sprite('bg-castle')
  if (g12ready(bgImg)) {
    ctx.drawImage(bgImg, 0, 0, CANVAS_W, CANVAS_H)
  } else {
    drawSky(now)
    drawCastle(now)
  }
  drawEmbers(now)
  drawGround(now)

  drawAimGuide(game.p1, PAL.p1)
  drawAimGuide(game.p2, PAL.p2)

  drawTank(game.p1, PAL.p1, now)
  drawTank(game.p2, PAL.p2, now)

  drawShell(game.p1, PAL.p1)
  drawShell(game.p2, PAL.p2)

  drawParticles()
  ctx.restore()
}

function drawSky(now) {
  const sky = ctx.createLinearGradient(0, 0, 0, CANVAS_H)
  sky.addColorStop(0, '#1a1140')
  sky.addColorStop(0.45, '#241a52')
  sky.addColorStop(1, '#3a2a5e')
  ctx.fillStyle = sky
  ctx.fillRect(-20, -20, CANVAS_W + 40, CANVAS_H + 40)

  // moon glow
  const mg = ctx.createRadialGradient(CENTER, 96, 8, CENTER, 96, 150)
  mg.addColorStop(0, 'rgba(255,225,170,0.5)')
  mg.addColorStop(1, 'rgba(255,225,170,0)')
  ctx.fillStyle = mg
  ctx.fillRect(CENTER - 160, -40, 320, 280)
  ctx.fillStyle = 'rgba(255,238,200,0.92)'
  ctx.beginPath()
  ctx.arc(CENTER, 92, 30, 0, Math.PI * 2)
  ctx.fill()

  // sweeping searchlights
  for (let i = 0; i < 2; i += 1) {
    const baseX = i === 0 ? 150 : CANVAS_W - 150
    const ang = -Math.PI / 2 + Math.sin(now / 2600 + i * 2) * 0.5
    ctx.save()
    ctx.translate(baseX, 250)
    ctx.rotate(ang)
    const beam = ctx.createLinearGradient(0, 0, 0, -360)
    beam.addColorStop(0, 'rgba(150,190,255,0.18)')
    beam.addColorStop(1, 'rgba(150,190,255,0)')
    ctx.fillStyle = beam
    ctx.beginPath()
    ctx.moveTo(0, 0)
    ctx.lineTo(-46, -360)
    ctx.lineTo(46, -360)
    ctx.closePath()
    ctx.fill()
    ctx.restore()
  }
}

function drawGear(g, now) {
  const rot = now * g.spd
  ctx.save()
  ctx.translate(g.x, g.y)
  ctx.rotate(rot)
  ctx.fillStyle = g.col
  ctx.beginPath()
  const inner = g.r * 0.74
  for (let i = 0; i < g.teeth; i += 1) {
    const a0 = (i / g.teeth) * Math.PI * 2
    const a1 = ((i + 0.5) / g.teeth) * Math.PI * 2
    const a2 = ((i + 1) / g.teeth) * Math.PI * 2
    ctx.lineTo(Math.cos(a0) * g.r, Math.sin(a0) * g.r)
    ctx.lineTo(Math.cos(a1) * g.r, Math.sin(a1) * g.r)
    ctx.lineTo(Math.cos(a1) * inner, Math.sin(a1) * inner)
    ctx.lineTo(Math.cos(a2) * inner, Math.sin(a2) * inner)
  }
  ctx.closePath()
  ctx.fill()
  ctx.beginPath()
  ctx.arc(0, 0, g.r * 0.34, 0, Math.PI * 2)
  ctx.fillStyle = 'rgba(20,16,40,0.5)'
  ctx.fill()
  ctx.restore()
}

function drawCastle(now) {
  for (const g of game.gears) drawGear(g, now)

  // distant castle silhouette
  const baseY = GROUND
  ctx.fillStyle = 'rgba(26,20,52,0.92)'
  const towers = [
    { x: 60, w: 120, h: 250 },
    { x: 200, w: 90, h: 320 },
    { x: 300, w: 110, h: 210 },
    { x: CANVAS_W - 180, w: 120, h: 250 },
    { x: CANVAS_W - 290, w: 90, h: 320 },
    { x: CANVAS_W - 410, w: 110, h: 210 },
    { x: CENTER - 70, w: 140, h: 300 }
  ]
  for (const t of towers) {
    ctx.fillStyle = 'rgba(28,22,56,0.95)'
    ctx.fillRect(t.x, baseY - t.h, t.w, t.h)
    // battlements
    for (let bx = t.x; bx < t.x + t.w; bx += 22) {
      ctx.fillRect(bx, baseY - t.h - 10, 12, 10)
    }
    // glowing windows
    for (let wy = baseY - t.h + 26; wy < baseY - 24; wy += 42) {
      for (let wx = t.x + 16; wx < t.x + t.w - 12; wx += 34) {
        const flick = 0.45 + 0.3 * Math.sin(now / 600 + wx + wy)
        ctx.fillStyle = `rgba(255,190,90,${flick})`
        ctx.fillRect(wx, wy, 9, 14)
      }
    }
  }
}

function drawEmbers(now) {
  for (const e of game.embers) {
    const a = 0.35 + 0.35 * Math.sin(e.tw)
    ctx.globalAlpha = a
    ctx.fillStyle = e.hue
    ctx.beginPath()
    ctx.arc(e.x, e.y, e.r, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.globalAlpha = 1
}

function drawGround(now) {
  // metallic platform
  const g = ctx.createLinearGradient(0, GROUND, 0, CANVAS_H)
  g.addColorStop(0, '#3b3358')
  g.addColorStop(0.12, '#2a2442')
  g.addColorStop(1, '#171328')
  ctx.fillStyle = g
  ctx.fillRect(-20, GROUND, CANVAS_W + 40, CANVAS_H - GROUND + 20)
  // neon top edge
  ctx.fillStyle = 'rgba(120,200,255,0.5)'
  ctx.fillRect(-20, GROUND - 2, CANVAS_W + 40, 3)
  // panel lines
  ctx.strokeStyle = 'rgba(120,150,210,0.14)'
  ctx.lineWidth = 1
  for (let x = 0; x < CANVAS_W; x += 60) {
    ctx.beginPath()
    ctx.moveTo(x, GROUND)
    ctx.lineTo(x - 30, CANVAS_H)
    ctx.stroke()
  }
  for (let y = GROUND + 22; y < CANVAS_H; y += 26) {
    ctx.beginPath()
    ctx.moveTo(-20, y)
    ctx.lineTo(CANVAS_W + 20, y)
    ctx.stroke()
  }

  // central no-man's-land gate with warning stripes
  const pulse = 0.4 + 0.25 * Math.sin(now / 300)
  ctx.fillStyle = `rgba(255,90,110,${0.1 + pulse * 0.12})`
  ctx.fillRect(CENTER - MIDZONE, GROUND - 2, MIDZONE * 2, CANVAS_H - GROUND + 2)
  ctx.save()
  ctx.beginPath()
  ctx.rect(CENTER - MIDZONE, GROUND, MIDZONE * 2, 14)
  ctx.clip()
  ctx.fillStyle = 'rgba(255,200,60,0.55)'
  for (let x = CENTER - MIDZONE - 14; x < CENTER + MIDZONE; x += 22) {
    ctx.beginPath()
    ctx.moveTo(x, GROUND)
    ctx.lineTo(x + 10, GROUND)
    ctx.lineTo(x + 24, GROUND + 14)
    ctx.lineTo(x + 14, GROUND + 14)
    ctx.closePath()
    ctx.fill()
  }
  ctx.restore()
  // gate posts
  ctx.strokeStyle = `rgba(255,120,140,${0.5 + pulse * 0.4})`
  ctx.lineWidth = 2
  ctx.beginPath()
  ctx.moveTo(CENTER - MIDZONE, GROUND)
  ctx.lineTo(CENTER - MIDZONE, GROUND - 40)
  ctx.moveTo(CENTER + MIDZONE, GROUND)
  ctx.lineTo(CENTER + MIDZONE, GROUND - 40)
  ctx.stroke()
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

function drawTank(p, pal, now) {
  const x = p.x
  const gy = GROUND
  ctx.save()

  // shadow
  ctx.fillStyle = 'rgba(0,0,0,0.4)'
  ctx.beginPath()
  ctx.ellipse(x, gy + 4, 40, 8, 0, 0, Math.PI * 2)
  ctx.fill()

  const turY = gy - 38 - p.recoil * 0.3
  const pid = p === game.p2 ? 'p2' : 'p1'
  const sideSuffix = p.facing === 1 ? 'r' : 'l'
  const hurt = p.hitFlash > 0
  const bodyImg = g12Sprite(`tank-${pid}-${hurt ? 'hit' : 'body'}-${sideSuffix}`)
  if (g12ready(bodyImg)) {
    const bw = hurt ? 96 : 84
    const bh = bw * (bodyImg.naturalHeight / bodyImg.naturalWidth)
    ctx.drawImage(bodyImg, x - bw / 2, gy + 4 - bh, bw, bh)
  } else {
    // fallback：簡化車身
    ctx.fillStyle = pal.dark
    roundRect(x - 34, gy - 16, 68, 18, 9)
    ctx.fill()
    ctx.fillStyle = pal.body
    ctx.beginPath()
    ctx.moveTo(x - 32, gy - 16)
    ctx.lineTo(x - 26, gy - 32)
    ctx.lineTo(x + 22, gy - 32)
    ctx.lineTo(x + 32, gy - 16)
    ctx.closePath()
    ctx.fill()
    ctx.fillStyle = pal.glow
    ctx.beginPath()
    ctx.arc(x, turY, 14, Math.PI, Math.PI * 2)
    ctx.fill()
  }

  // barrel
  ctx.save()
  ctx.translate(x, turY - 2)
  if (p.facing === 1) {
    ctx.rotate(-(p.angle * Math.PI) / 180)
  } else {
    ctx.scale(-1, 1)
    ctx.rotate(-(p.angle * Math.PI) / 180)
  }
  const barrelImg = g12Sprite(`barrel-${pid}`)
  if (g12ready(barrelImg)) {
    const bw2 = 50
    const bh2 = bw2 * (barrelImg.naturalHeight / barrelImg.naturalWidth)
    ctx.drawImage(barrelImg, -8 - p.recoil, -bh2 / 2, bw2, bh2)
  } else {
    const bg = ctx.createLinearGradient(0, -5, 0, 5)
    bg.addColorStop(0, '#6a6880')
    bg.addColorStop(1, '#26243a')
    ctx.fillStyle = bg
    roundRect(-4 - p.recoil, -5, 42, 10, 4)
    ctx.fill()
    ctx.fillStyle = '#15131f'
    ctx.fillRect(34 - p.recoil, -6, 7, 12)
  }
  // muzzle flash
  if (p.flash > 0) {
    ctx.globalAlpha = p.flash
    ctx.fillStyle = '#fff2b0'
    ctx.beginPath()
    ctx.arc(44 - p.recoil, 0, 6 + p.flash * 8, 0, Math.PI * 2)
    ctx.fill()
    ctx.fillStyle = 'rgba(255,150,50,0.7)'
    ctx.beginPath()
    ctx.arc(44 - p.recoil, 0, 10 + p.flash * 12, 0, Math.PI * 2)
    ctx.fill()
    ctx.globalAlpha = 1
  }
  ctx.restore()

  // hit flash overlay
  if (p.hitFlash > 0) {
    ctx.globalAlpha = p.hitFlash * 0.6
    ctx.fillStyle = '#ff5a6a'
    ctx.beginPath()
    ctx.arc(x, gy - 26, 30, 0, Math.PI * 2)
    ctx.fill()
    ctx.globalAlpha = 1
  }

  // charge meter above tank
  if (p.charging || p.power > 1) {
    const w = 52
    const bx = x - w / 2
    const by = gy - 76
    ctx.fillStyle = 'rgba(0,0,0,0.5)'
    roundRect(bx, by, w, 8, 4)
    ctx.fill()
    const pw = (p.power / 100) * w
    ctx.fillStyle = p.power > 85 ? '#ff5d6c' : p.power > 55 ? '#ffd23f' : pal.glow
    ctx.fillRect(bx, by, pw, 8)
    ctx.strokeStyle = 'rgba(255,255,255,0.3)'
    ctx.lineWidth = 1
    roundRect(bx, by, w, 8, 4)
    ctx.stroke()
  }

  ctx.restore()
}

function drawAimGuide(p, pal) {
  if (p.shell) return
  const turY = GROUND - 40
  const rad = (p.angle * Math.PI) / 180
  const len = 36 + p.power * 0.9
  const ex = p.x + Math.cos(rad) * len * p.facing
  const ey = turY - Math.sin(rad) * len
  ctx.save()
  ctx.setLineDash([7, 7])
  ctx.strokeStyle = p.facing === 1 ? 'rgba(95,255,224,0.7)' : 'rgba(255,176,212,0.75)'
  ctx.lineWidth = 2.5
  ctx.beginPath()
  ctx.moveTo(p.x, turY)
  ctx.lineTo(ex, ey)
  ctx.stroke()
  ctx.restore()
}

function drawShell(p, pal) {
  if (!p.shell) return
  const s = p.shell
  // trail
  ctx.strokeStyle = 'rgba(255,180,90,0.45)'
  ctx.lineWidth = 5
  ctx.lineCap = 'round'
  ctx.beginPath()
  s.trail.forEach((t, i) => (i === 0 ? ctx.moveTo(t.x, t.y) : ctx.lineTo(t.x, t.y)))
  ctx.stroke()
  ctx.lineCap = 'butt'

  // 砲彈
  const ball = g12Sprite('cannonball')
  if (g12ready(ball)) {
    const sz = SHELL_R * 2.4
    ctx.save()
    ctx.translate(s.x, s.y)
    ctx.rotate(s.spin || 0)
    ctx.drawImage(ball, -sz / 2, -sz / 2, sz, sz)
    ctx.restore()
  } else {
    ctx.save()
    ctx.shadowColor = '#ffba55'
    ctx.shadowBlur = 20
    const g = ctx.createRadialGradient(s.x - 3, s.y - 3, 2, s.x, s.y, SHELL_R)
    g.addColorStop(0, '#fff4c0')
    g.addColorStop(0.5, '#ff9a3a')
    g.addColorStop(1, '#d6481f')
    ctx.fillStyle = g
    ctx.beginPath()
    ctx.arc(s.x, s.y, SHELL_R, 0, Math.PI * 2)
    ctx.fill()
    ctx.restore()
  }
}

function drawParticles() {
  for (const pt of game.particles) {
    if (pt.ring) {
      ctx.globalAlpha = Math.max(0, pt.life * 2)
      ctx.strokeStyle = pt.color
      ctx.lineWidth = 3
      ctx.beginPath()
      ctx.arc(pt.x, pt.y, pt.r, 0, Math.PI * 2)
      ctx.stroke()
      continue
    }
    ctx.globalAlpha = Math.max(0, Math.min(1, pt.life))
    ctx.fillStyle = pt.color
    ctx.beginPath()
    ctx.arc(pt.x, pt.y, (pt.r || 2) * Math.max(0.3, pt.life), 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.globalAlpha = 1
}

/* ---------------- input ---------------- */

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  if (k.startsWith('arrow') || k === ' ' || k === '/') e.preventDefault()

  if (phase.value !== 'playing') {
    if (k === ' ' || k === 'enter') {
      if (phase.value === 'intro' || phase.value === 'matchover') startMatch()
      else if (phase.value === 'roundover') nextRound()
    }
    return
  }

  const repeat = keys.has(k)
  keys.add(k)
  if (repeat) return

  if (k === ' ' && !game.p1.charging && !game.p1.shell && game.p1.cooldown <= 0) {
    game.p1.power = 0
    game.p1.chargeDir = 1
    game.p1.charging = true
  }
  if (k === '/' && !game.p2.charging && !game.p2.shell && game.p2.cooldown <= 0) {
    game.p2.power = 0
    game.p2.chargeDir = 1
    game.p2.charging = true
  }
}

function onKeyUp(e) {
  const k = e.key.toLowerCase()
  keys.delete(k)
  if (phase.value !== 'playing' || !game) return
  if (k === ' ') releaseFire(game.p1)
  if (k === '/') releaseFire(game.p2)
}

async function onClearRecords() {
  const store = await clearGame12Records()
  records.value = store.records
}
function formatDate(iso) {
  const d = new Date(iso)
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}
function idleRender() {
  game = createGame()
  render(performance.now())
}

onMounted(async () => {
  ctx = canvasRef.value.getContext('2d')
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('keyup', onKeyUp)
  try {
    const store = await fetchGame12Store()
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
.game12-view { min-height: 100vh; padding: 24px clamp(16px,4vw,48px) 48px; color: #e9eefb; background: radial-gradient(circle at 50% -10%, #2a1c52, #0b0818 60%); font-family: 'Segoe UI', system-ui, sans-serif; }
.topbar { display: flex; align-items: center; gap: 20px; flex-wrap: wrap; margin-bottom: 22px; }
.back-link { color: #b9a8e8; text-decoration: none; font-size: 14px; padding: 8px 14px; border: 1px solid rgba(185,168,232,0.3); border-radius: 999px; transition: 0.2s; }
.back-link:hover { background: rgba(185,168,232,0.12); color: #fff; }
.title-block .eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; color: #ffb15a; text-transform: uppercase; }
.title-block h1 { margin: 2px 0 0; font-size: 26px; background: linear-gradient(90deg,#5fffe0,#ff6fa8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.round-pill { margin-left: auto; padding: 8px 16px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 13px; }
.layout { display: grid; grid-template-columns: minmax(0,1fr) 300px; gap: 22px; align-items: start; }
.stage-card { background: rgba(16,10,34,0.6); border: 1px solid rgba(150,170,220,0.16); border-radius: 20px; padding: 16px; box-shadow: 0 24px 60px rgba(0,0,0,0.5); }
.scoreband { display: flex; align-items: center; gap: 16px; margin-bottom: 14px; padding: 0 6px; }
.team { display: flex; align-items: center; gap: 10px; flex: 1; }
.team-2 { justify-content: flex-end; }
.team .dot { width: 12px; height: 12px; border-radius: 50%; }
.team-1 .dot { background: #3affd0; box-shadow: 0 0 10px #3affd0; }
.team-2 .dot { background: #ff9ec8; box-shadow: 0 0 10px #ff9ec8; }
.team strong { font-size: 14px; }
.hp-wrap { width: 110px; height: 10px; border-radius: 999px; background: rgba(255,255,255,0.12); overflow: hidden; }
.hp-fill { display: block; height: 100%; border-radius: 999px; transition: width 0.25s; }
.hp-fill.p1 { background: linear-gradient(90deg,#13d9aa,#3affd0); }
.hp-fill.p2 { background: linear-gradient(90deg,#ff6fa8,#ff9ec8); }
.team .wins { font-size: 13px; font-weight: 700; color: #ffe66d; }
.vs { font-size: 13px; font-weight: 800; color: #8a78b8; }
.stage-frame { position: relative; border-radius: 14px; overflow: hidden; }
.game-canvas { display: block; width: 100%; height: auto; border-radius: 14px; }
.overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(10,6,20,0.82); backdrop-filter: blur(4px); }
.overlay-card { text-align: center; max-width: 460px; padding: 32px; }
.overlay-eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; color: #ffb15a; }
.overlay-card h2 { margin: 10px 0 14px; font-size: 28px; }
.winner-text { background: linear-gradient(90deg,#5fffe0,#ff6fa8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.overlay-text { color: #c6bce4; line-height: 1.7; margin: 0 0 22px; }
.primary-btn { border: none; padding: 12px 30px; border-radius: 999px; font-size: 16px; font-weight: 700; color: #1a0f2e; background: linear-gradient(90deg,#5fffe0,#ff9ec8); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; box-shadow: 0 10px 26px rgba(95,255,224,0.35); }
.primary-btn:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(255,158,200,0.5); }
.sidebar { display: flex; flex-direction: column; gap: 16px; }
.panel { background: rgba(16,10,34,0.6); border: 1px solid rgba(150,170,220,0.16); border-radius: 18px; padding: 18px; }
.panel .eyebrow { margin: 0 0 12px; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #9a86c8; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.controls-grid { display: grid; gap: 10px; }
.ctrl { border-radius: 12px; padding: 12px 14px; }
.ctrl strong { display: block; font-size: 14px; margin-bottom: 6px; }
.ctrl span { display: block; font-size: 13px; color: #c6bce4; margin-bottom: 3px; }
.ctrl-1 { background: rgba(43,214,176,0.1); border: 1px solid rgba(43,214,176,0.28); }
.ctrl-2 { background: rgba(255,111,168,0.1); border: 1px solid rgba(255,111,168,0.28); }
.stat-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; margin-bottom: 12px; }
.stat { border-radius: 10px; padding: 8px 10px; text-align: center; }
.stat span { display: block; font-size: 11px; color: #9a86c8; }
.stat b { font-size: 18px; font-variant-numeric: tabular-nums; }
kbd { background: rgba(255,255,255,0.12); border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 2px 7px; font-size: 12px; font-family: inherit; margin-right: 3px; }
.tips { margin: 0; padding-left: 18px; display: grid; gap: 8px; font-size: 13px; color: #c6bce4; line-height: 1.5; }
.records { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.records li { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: center; font-size: 12px; padding: 8px 10px; border-radius: 10px; background: rgba(255,255,255,0.04); }
.rec-win { font-weight: 700; color: #ffe66d; }
.rec-score { color: #b9a8e8; }
.rec-date { color: #8a78b8; }
.empty { font-size: 13px; color: #9a86c8; line-height: 1.6; margin: 0; }
.ghost-btn { background: none; border: 1px solid rgba(185,168,232,0.3); color: #b9a8e8; border-radius: 999px; padding: 4px 12px; font-size: 12px; cursor: pointer; }
.ghost-btn:hover { background: rgba(185,168,232,0.12); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } }
</style>
