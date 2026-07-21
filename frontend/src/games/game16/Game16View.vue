<template>
  <main class="game16-view">
    <section class="topbar">
      <RouterLink to="/" class="back-link">← 返回遊戲廳</RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 16</p>
        <h1>投籃對決</h1>
      </div>
      <div v-if="phase === 'playing'" class="time-pill" :class="{ urgent: timeLeft <= 10 }">⏱ {{ timeLeft }}s</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <strong>玩家 1</strong>
            <span class="score">{{ hud.p1Score }}</span>
            <span class="cards">🃏 {{ hud.p1Cards }}</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="cards">🃏 {{ hud.p2Cards }}</span>
            <span class="score">{{ hud.p2Score }}</span>
            <strong>玩家 2</strong>
          </div>
        </div>

        <div ref="stageRef" class="stage-frame">
          <canvas ref="canvasRef" class="game-canvas" :width="CANVAS_W" :height="CANVAS_H" />
          <transition name="fade">
            <div v-if="phase !== 'playing'" class="overlay">
              <div class="overlay-card">
                <template v-if="phase === 'intro'">
                  <p class="overlay-eyebrow">空心入網</p>
                  <h2>左右瞄準，對準籃框投籃</h2>
                  <p class="overlay-text">
                    左右移動瞄準點（已鎖定在籃框移動範圍內），<b>引導線</b>變綠顯示「會進」就按鍵投籃。<br>
                    小心隨機出現的<b>小精靈</b>會把球拍掉！投進有機率獲得干擾卡，60 秒比進球。
                  </p>
                  <button class="primary-btn" @click="startGame">開始遊戲</button>
                </template>
                <template v-else-if="phase === 'result'">
                  <p class="overlay-eyebrow">時間到</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <p class="overlay-text">玩家 1 <strong>{{ hud.p1Score }}</strong> ： <strong>{{ hud.p2Score }}</strong> 玩家 2</p>
                  <button class="primary-btn" @click="startGame">再玩一次</button>
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
              <span><kbd>A</kbd><kbd>D</kbd> 左右瞄準 · <kbd>W</kbd> 投籃</span>
              <span><kbd>Q</kbd> 使用干擾卡</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span><kbd>←</kbd><kbd>→</kbd> 左右瞄準 · <kbd>↑</kbd> 投籃</span>
              <span><kbd>,</kbd> 使用干擾卡</span>
            </div>
          </div>
        </section>
        <section class="panel">
          <p class="eyebrow">玩法提示</p>
          <ul class="tips">
            <li><b>引導線變綠</b>代表路徑正確、放手即空心進球。</li>
            <li>紫色<b>小精靈</b>會在籃框附近飛、拍掉飛行中的球。</li>
            <li>每次投進有 40% 機率獲得干擾卡（最多 3 張）。</li>
            <li>發動干擾卡讓對手籃框亂飄 5 秒；連續命中得分加倍。</li>
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
          <p v-else class="empty">尚無紀錄，遊戲結束後自動保存最近 10 場。</p>
        </section>
      </aside>
    </section>
  </main>
</template>

<script setup>
import { onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { clearGame16Records, fetchGame16Store, saveGame16Record } from './game16Storage'
import { recordGameResult } from '@/data/lobbyScore'

const CANVAS_W = 920
const CANVAS_H = 600
const HALF = CANVAS_W / 2
const GAME_SEC = 60
const HOOP_Y = 150
const RIM = 34

// 像素素材
const G16 = {}
function g16Sprite(name) {
  if (!G16[name]) {
    const img = new Image()
    img.src = `/assets/G16/${name}.png`
    G16[name] = img
  }
  return G16[name]
}
;['bg-court', 'hoop', 'ball-basketball', 'player-p1', 'player-p2', 'shooter-p1-1', 'shooter-p1-2', 'shooter-p1-3', 'shooter-p2-1', 'shooter-p2-2', 'shooter-p2-3', 'blocker-1', 'blocker-2'].forEach(g16Sprite)
function g16ready(img) {
  return img && img.complete && img.naturalWidth > 0
}
const GRAVITY = 0.34
const AMP = HALF / 2 - 120 // hoop horizontal travel half-width
const SHOT_V = 18.5 // fixed shot speed (no charge); reaches the rim across the whole band
const AIM_SPEED = 3 // px per frame the aim point slides left/right

const canvasRef = ref(null)
const stageRef = ref(null)
const phase = ref('intro')
const timeLeft = ref(GAME_SEC)
const resultText = ref('')
const records = ref([])
const hud = reactive({ p1Score: 0, p2Score: 0, p1Cards: 0, p2Cards: 0 })

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null
const keys = new Set()

/* ---------- sound (Web Audio, no assets) ---------- */
let audioCtx = null
let noiseBuffer = null
function ensureAudio() {
  if (!audioCtx) {
    const AC = window.AudioContext || window.webkitAudioContext
    if (!AC) return
    audioCtx = new AC()
    const len = Math.floor(audioCtx.sampleRate * 0.5)
    noiseBuffer = audioCtx.createBuffer(1, len, audioCtx.sampleRate)
    const data = noiseBuffer.getChannelData(0)
    for (let i = 0; i < len; i += 1) data[i] = Math.random() * 2 - 1
  }
  if (audioCtx.state === 'suspended') audioCtx.resume()
}
function blip(freq, t0, dur, type, vol) {
  const o = audioCtx.createOscillator()
  const g = audioCtx.createGain()
  o.type = type
  o.frequency.setValueAtTime(freq, t0)
  g.gain.setValueAtTime(0.0001, t0)
  g.gain.linearRampToValueAtTime(vol, t0 + 0.012)
  g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur)
  o.connect(g).connect(audioCtx.destination)
  o.start(t0)
  o.stop(t0 + dur + 0.02)
}
function noiseBurst(t0, dur, filterType, freq, vol) {
  const src = audioCtx.createBufferSource()
  src.buffer = noiseBuffer
  const bp = audioCtx.createBiquadFilter()
  bp.type = filterType
  bp.frequency.value = freq
  const g = audioCtx.createGain()
  g.gain.setValueAtTime(vol, t0)
  g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur)
  src.connect(bp).connect(g).connect(audioCtx.destination)
  src.start(t0)
  src.stop(t0 + dur + 0.02)
}
function playSwish() {
  ensureAudio()
  if (!audioCtx) return
  const t = audioCtx.currentTime
  noiseBurst(t, 0.22, 'bandpass', 2600, 0.22)
  blip(660, t + 0.02, 0.18, 'triangle', 0.16)
  blip(990, t + 0.11, 0.24, 'triangle', 0.14)
}
function playSwat() {
  ensureAudio()
  if (!audioCtx) return
  const t = audioCtx.currentTime
  blip(170, t, 0.13, 'square', 0.2)
  noiseBurst(t, 0.12, 'lowpass', 900, 0.22)
}

/* ---------- aim: angle that lands the fixed-speed shot on aimX ---------- */
function aimAngleDeg(side) {
  const dx = side.aimX - side.launchX
  const dy = side.launchY - HOOP_Y // target height above launch (positive)
  const v = SHOT_V
  const g = GRAVITY
  const disc = v * v * v * v - g * (g * dx * dx + 2 * dy * v * v)
  if (disc < 0) return 90 // unreachable (shouldn't happen within band) -> straight up
  const rad = Math.atan2(v * v + Math.sqrt(disc), g * dx)
  return (rad * 180) / Math.PI
}

/* ---------- shot trajectory simulation (matches live physics) ---------- */
function simulateShot(side) {
  const rad = (side.angle * Math.PI) / 180
  const v = SHOT_V
  let x = side.launchX
  let y = side.launchY
  let vx = Math.cos(rad) * v
  let vy = -Math.sin(rad) * v
  const pts = [{ x, y }]
  let willScore = false
  for (let i = 0; i < 260; i += 1) {
    vy += GRAVITY
    x += vx
    y += vy
    if (x < side.originX + 10) { x = side.originX + 10; vx = Math.abs(vx) * 0.6 }
    if (x > side.originX + HALF - 10) { x = side.originX + HALF - 10; vx = -Math.abs(vx) * 0.6 }
    pts.push({ x, y })
    // stop at the rim plane on the way down — that landing point is where we aimed
    if (vy > 0 && y >= HOOP_Y) {
      willScore = Math.abs(x - side.hoopX) < RIM / 2
      break
    }
    if (y > CANVAS_H) break
  }
  return { pts, willScore }
}

function makeSide(half) {
  const originX = half === 0 ? 0 : HALF
  return {
    half,
    originX,
    launchX: originX + HALF / 2,
    launchY: CANVAS_H - 44,
    angle: 90,
    aimX: originX + HALF / 2,
    ball: null,
    hoopX: originX + HALF / 2,
    hoopPhase: Math.random() * Math.PI * 2,
    hoopSpeed: 0.0012,
    seed: Math.random() * 1000,
    score: 0,
    combo: 0,
    cards: 0,
    interferedUntil: 0,
    fx: [],
    flash: 0,
    scoreAnim: 0,
    lastSwish: false,
    sprite: null,
    spriteTimer: 3500 + Math.random() * 3500
  }
}

function createGame() {
  return { p1: makeSide(0), p2: makeSide(1), shake: 0 }
}

function startGame() {
  ensureAudio()
  game = createGame()
  hud.p1Score = 0
  hud.p2Score = 0
  hud.p1Cards = 0
  hud.p2Cards = 0
  timeLeft.value = GAME_SEC
  phase.value = 'playing'
  lastFrame = performance.now()
  loop(lastFrame)
}

/* ---------- 小精靈 blocker ---------- */
function spawnSprite(side) {
  const dir = Math.random() < 0.5 ? 1 : -1
  // patrol the airspace just above/at the rim so it can intercept incoming shots
  const baseY = HOOP_Y - 58 + Math.random() * 44
  side.sprite = {
    x: side.originX + HALF / 2 + (Math.random() - 0.5) * 140,
    y: baseY,
    baseY,
    vx: dir * (42 + Math.random() * 46),
    phase: Math.random() * 6,
    life: 4200 + Math.random() * 2600,
    swat: 0
  }
}

function updateSprite(side, dt, now) {
  const s = side.sprite
  s.life -= dt
  s.phase += dt / 300
  s.x += s.vx * (dt / 1000)
  const minX = side.originX + 70
  const maxX = side.originX + HALF - 70
  if (s.x < minX) { s.x = minX; s.vx = Math.abs(s.vx) }
  if (s.x > maxX) { s.x = maxX; s.vx = -Math.abs(s.vx) }
  s.y = s.baseY + Math.sin(s.phase) * 22
  if (s.swat > 0) s.swat = Math.max(0, s.swat - dt / 250)

  const b = side.ball
  if (b && !b.scored && !b.batted && Math.hypot(b.x - s.x, b.y - s.y) < 28) {
    b.batted = true
    b.vy = Math.abs(b.vy) * 0.3 + 2.6
    b.vx = (b.x < s.x ? -1 : 1) * (3 + Math.random() * 3)
    s.swat = 1
    s.vx *= -1
    game.shake = Math.max(game.shake, 6)
    emit(side, b.x, b.y, '#c98bff', 16)
    playSwat()
  }

  if (s.life <= 0) side.sprite = null
}

function shoot(side) {
  if (side.ball) return
  const rad = (side.angle * Math.PI) / 180
  const v = SHOT_V
  side.ball = {
    x: side.launchX,
    y: side.launchY,
    vx: Math.cos(rad) * v,
    vy: -Math.sin(rad) * v,
    scored: false,
    batted: false,
    trail: []
  }
}

function useCard(side, target) {
  if (side.cards <= 0) return
  side.cards -= 1
  if (side.half === 0) hud.p1Cards = side.cards
  else hud.p2Cards = side.cards
  target.interferedUntil = performance.now() + 5000
}

function syncHud() {
  hud.p1Score = game.p1.score
  hud.p2Score = game.p2.score
  hud.p1Cards = game.p1.cards
  hud.p2Cards = game.p2.cards
}

function emit(side, x, y, color, n) {
  for (let i = 0; i < n; i += 1) {
    const a = Math.random() * Math.PI * 2
    const sp = 40 + Math.random() * 140
    side.fx.push({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 1, color })
  }
}

function updateSide(side, now, dt) {
  // 小精靈 spawn / update
  if (!side.sprite) {
    side.spriteTimer -= dt
    if (side.spriteTimer <= 0) {
      spawnSprite(side)
      side.spriteTimer = 5000 + Math.random() * 4000
    }
  } else {
    updateSprite(side, dt, now)
  }
  side.scoreAnim = Math.max(0, side.scoreAnim - dt / 700)

  // hoop movement
  const interfered = now < side.interferedUntil
  const speed = interfered ? side.hoopSpeed * 2.8 : side.hoopSpeed
  side.hoopPhase += speed * dt
  let jitter = 0
  if (interfered) jitter = Math.sin(now / 60) * 20 + (Math.random() - 0.5) * 12
  side.hoopX = side.originX + HALF / 2 + Math.sin(side.hoopPhase) * AMP + jitter
  side.hoopX = Math.max(side.originX + 60, Math.min(side.originX + HALF - 60, side.hoopX))

  // ball
  if (side.ball) {
    const b = side.ball
    b.vy += GRAVITY * (dt / 16.67)
    b.x += b.vx * (dt / 16.67)
    b.y += b.vy * (dt / 16.67)
    b.trail.push({ x: b.x, y: b.y })
    if (b.trail.length > 12) b.trail.shift()
    // score detection: descending through rim plane
    if (!b.scored && !b.batted && b.vy > 0 && b.y > HOOP_Y - 6 && b.y < HOOP_Y + 14) {
      if (Math.abs(b.x - side.hoopX) < RIM / 2) {
        b.scored = true
        const swish = Math.abs(b.x - side.hoopX) < RIM * 0.42
        side.lastSwish = swish
        side.combo += 1
        const pts = (swish ? 2 : 1) * (side.combo >= 3 ? 2 : 1)
        side.score += pts
        side.flash = 1
        side.scoreAnim = 1
        emit(side, side.hoopX, HOOP_Y, '#ffd23f', 12)
        emit(side, side.hoopX, HOOP_Y, side.half === 0 ? '#5fe6c4' : '#ff9ec8', 12)
        emit(side, side.hoopX, HOOP_Y, '#ffffff', 6)
        game.shake = Math.max(game.shake, 5)
        playSwish()
        if (side.cards < 3 && Math.random() < 0.4) {
          side.cards += 1
        }
        syncHud()
      }
    }
    // wall bounds
    if (b.x < side.originX + 10) { b.x = side.originX + 10; b.vx = Math.abs(b.vx) * 0.6 }
    if (b.x > side.originX + HALF - 10) { b.x = side.originX + HALF - 10; b.vx = -Math.abs(b.vx) * 0.6 }
    if (b.y > CANVAS_H + 40) {
      if (!b.scored) side.combo = 0
      side.ball = null
    }
  }

  // aim: slide the target left/right, locked to the hoop's travel range
  const move = AIM_SPEED * (dt / 16.67)
  if (side.half === 0) {
    if (keys.has('a')) side.aimX -= move
    if (keys.has('d')) side.aimX += move
  } else {
    if (keys.has('arrowleft')) side.aimX -= move
    if (keys.has('arrowright')) side.aimX += move
  }
  const bandMin = side.originX + HALF / 2 - AMP
  const bandMax = side.originX + HALF / 2 + AMP
  side.aimX = Math.max(bandMin, Math.min(bandMax, side.aimX))
  side.angle = aimAngleDeg(side)

  side.flash = Math.max(0, side.flash - dt / 500)
  for (const f of side.fx) {
    f.x += f.vx * (dt / 1000)
    f.y += f.vy * (dt / 1000)
    f.vy += 300 * (dt / 1000)
    f.life -= dt / 700
  }
  side.fx = side.fx.filter((f) => f.life > 0)
}

function update(dt, now) {
  game.elapsed = (game.elapsed || 0) + dt
  timeLeft.value = Math.max(0, Math.ceil((GAME_SEC * 1000 - game.elapsed) / 1000))
  updateSide(game.p1, now, dt)
  updateSide(game.p2, now, dt)
  if (game.elapsed >= GAME_SEC * 1000) finishGame()
}

async function finishGame() {
  cancelAnimationFrame(rafId)
  rafId = 0
  let winner
  if (game.p1.score > game.p2.score) winner = '玩家 1 獲勝'
  else if (game.p2.score > game.p1.score) winner = '玩家 2 獲勝'
  else winner = '平手'
  resultText.value = `🏀 ${winner}`
  phase.value = 'result'
  recordGameResult('/game16', game.p1.score > game.p2.score ? 'p1' : game.p2.score > game.p1.score ? 'p2' : 'draw')
  try {
    const store = await saveGame16Record({ winner, scoreP1: game.p1.score, scoreP2: game.p2.score, date: new Date().toISOString() })
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

function render(now) {
  ctx.fillStyle = '#140e1f'
  ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  const shake = game.shake || 0
  const sx = (Math.random() - 0.5) * shake
  const sy = (Math.random() - 0.5) * shake
  ctx.save()
  ctx.translate(sx, sy)
  drawCourt(game.p1, now)
  drawCourt(game.p2, now)
  // center divider
  ctx.fillStyle = 'rgba(0,0,0,0.35)'
  ctx.fillRect(HALF - 2, 0, 4, CANVAS_H)
  ctx.strokeStyle = 'rgba(255,255,255,0.18)'
  ctx.lineWidth = 1
  ctx.beginPath()
  ctx.moveTo(HALF, 0)
  ctx.lineTo(HALF, CANVAS_H)
  ctx.stroke()
  ctx.restore()
  game.shake = shake * 0.85
}

const FLOOR_TOP = 250

function drawCourt(side, now) {
  const ox = side.originX
  const cx = ox + HALF / 2
  const interfered = now < side.interferedUntil

  // arena backdrop（球場像素背景）
  ctx.imageSmoothingEnabled = false
  const courtImg = g16Sprite('bg-court')
  if (g16ready(courtImg)) {
    const sw = courtImg.naturalWidth / 2
    const sxc = side.half === 0 ? 0 : sw
    ctx.drawImage(courtImg, sxc, 0, sw, courtImg.naturalHeight, ox, 0, HALF, CANVAS_H)
  } else {
    const wall = ctx.createLinearGradient(0, 0, 0, CANVAS_H)
    wall.addColorStop(0, '#241a36')
    wall.addColorStop(0.5, '#2c2042')
    wall.addColorStop(1, '#181024')
    ctx.fillStyle = wall
    ctx.fillRect(ox, 0, HALF, CANVAS_H)
    drawCrowd(side, now)
    drawFloor(ox)
    drawKey(cx)
  }

  if (interfered) {
    ctx.fillStyle = `rgba(255,93,108,${0.12 + 0.06 * Math.sin(now / 80)})`
    ctx.fillRect(ox, 0, HALF, CANVAS_H)
  }

  drawHoop(side, now)
  drawGuide(side)

  // ball + trail
  if (side.ball) {
    const b = side.ball
    ctx.strokeStyle = 'rgba(255,160,70,0.35)'
    ctx.lineWidth = 4
    ctx.lineCap = 'round'
    ctx.beginPath()
    b.trail.forEach((t, i) => (i === 0 ? ctx.moveTo(t.x, t.y) : ctx.lineTo(t.x, t.y)))
    ctx.stroke()
    ctx.lineCap = 'butt'
    drawShooter(side, now, 3)
    drawBall(b.x, b.y)
  } else {
    drawShooter(side, now, side.charging ? 2 : 1)
    drawBall(side.launchX, side.launchY)
  }

  // 小精靈
  drawSprite(side, now)

  // score animation
  if (side.scoreAnim > 0) {
    const a = side.scoreAnim
    // expanding ring at hoop
    ctx.strokeStyle = `rgba(255,220,100,${a})`
    ctx.lineWidth = 4
    ctx.beginPath()
    ctx.arc(side.hoopX, HOOP_Y, (1 - a) * 56 + 12, 0, Math.PI * 2)
    ctx.stroke()
    // scaling celebratory text
    ctx.save()
    ctx.globalAlpha = Math.min(1, a * 1.5)
    ctx.translate(cx, HOOP_Y - 34)
    ctx.scale(1 + (1 - a) * 0.7, 1 + (1 - a) * 0.7)
    ctx.fillStyle = side.lastSwish ? '#7dffb0' : '#ffe27a'
    ctx.shadowColor = side.lastSwish ? '#5fe6a0' : '#ff9f3a'
    ctx.shadowBlur = 18
    ctx.font = 'bold 30px system-ui, sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText(side.lastSwish ? '🏀 空心球！' : '🏀 進球！', 0, 0)
    ctx.restore()
    if (side.combo >= 3) {
      ctx.fillStyle = `rgba(255,122,58,${a})`
      ctx.font = 'bold 16px system-ui, sans-serif'
      ctx.textAlign = 'center'
      ctx.fillText('🔥 連續得分加倍', cx, HOOP_Y - 4)
    }
  }
  if (interfered) {
    ctx.fillStyle = '#ff5d6c'
    ctx.font = 'bold 20px system-ui, sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText('⚡ 被干擾！', cx, 150)
  }

  // combo indicator
  if (side.combo >= 2) {
    ctx.fillStyle = side.combo >= 3 ? '#ff7a3a' : '#ffd23f'
    ctx.font = 'bold 15px system-ui, sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText(`COMBO x${side.combo}`, cx, CANVAS_H - 52)
  }

  // particles
  for (const f of side.fx) {
    ctx.globalAlpha = Math.max(0, f.life)
    ctx.fillStyle = f.color
    ctx.beginPath()
    ctx.arc(f.x, f.y, 3 * f.life + 1, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.globalAlpha = 1

  // cards indicator
  for (let i = 0; i < side.cards; i += 1) {
    ctx.font = '20px serif'
    ctx.textAlign = 'left'
    ctx.fillText('🃏', ox + 16 + i * 24, CANVAS_H - 36)
  }
}

const CROWD_PALETTE = ['#5b6cff', '#ff6f9c', '#ffce4d', '#4dd6a6', '#c98bff', '#ff8a5b']

function drawCrowd(side, now) {
  const ox = side.originX
  const rows = 4
  const cols = 13
  const stepX = (HALF - 24) / (cols - 1)
  const base = Math.floor(side.seed)
  for (let r = 0; r < rows; r += 1) {
    for (let c = 0; c < cols; c += 1) {
      const i = r * cols + c
      const x = ox + 12 + c * stepX + (r % 2) * (stepX / 2)
      const bob = Math.sin(now / 500 + i * 1.7 + side.seed) * 2
      const y = 24 + r * 21 + bob
      ctx.globalAlpha = 0.5
      ctx.fillStyle = CROWD_PALETTE[(i + base) % CROWD_PALETTE.length]
      ctx.beginPath()
      ctx.arc(x, y, 6, 0, Math.PI * 2)
      ctx.fill()
    }
  }
  ctx.globalAlpha = 1
  // stand rail
  ctx.fillStyle = 'rgba(0,0,0,0.28)'
  ctx.fillRect(ox, 112, HALF, 9)
}

function drawFloor(ox) {
  const g = ctx.createLinearGradient(0, FLOOR_TOP, 0, CANVAS_H)
  g.addColorStop(0, '#b9824a')
  g.addColorStop(1, '#7c5024')
  ctx.fillStyle = g
  ctx.fillRect(ox, FLOOR_TOP, HALF, CANVAS_H - FLOOR_TOP)
  // top edge highlight
  ctx.fillStyle = 'rgba(255,220,150,0.3)'
  ctx.fillRect(ox, FLOOR_TOP, HALF, 3)
  // planks
  ctx.strokeStyle = 'rgba(60,35,12,0.32)'
  ctx.lineWidth = 1
  for (let y = FLOOR_TOP + 28; y < CANVAS_H; y += 28) {
    ctx.beginPath()
    ctx.moveTo(ox, y)
    ctx.lineTo(ox + HALF, y)
    ctx.stroke()
  }
  ctx.strokeStyle = 'rgba(60,35,12,0.16)'
  for (let x = ox + 50; x < ox + HALF; x += 90) {
    ctx.beginPath()
    ctx.moveTo(x, FLOOR_TOP)
    ctx.lineTo(x, CANVAS_H)
    ctx.stroke()
  }
}

function drawKey(cx) {
  ctx.save()
  ctx.beginPath()
  ctx.moveTo(cx - 58, FLOOR_TOP)
  ctx.lineTo(cx + 58, FLOOR_TOP)
  ctx.lineTo(cx + 94, CANVAS_H)
  ctx.lineTo(cx - 94, CANVAS_H)
  ctx.closePath()
  ctx.fillStyle = 'rgba(60,120,200,0.2)'
  ctx.fill()
  ctx.strokeStyle = 'rgba(255,255,255,0.4)'
  ctx.lineWidth = 2
  ctx.stroke()
  // free-throw circle
  ctx.beginPath()
  ctx.arc(cx, FLOOR_TOP + 72, 46, 0, Math.PI * 2)
  ctx.stroke()
  ctx.restore()
}

function drawHoop(side, now) {
  const hx = side.hoopX
  const topY = HOOP_Y - 58

  // mounting arm
  ctx.strokeStyle = '#3a3550'
  ctx.lineWidth = 7
  ctx.beginPath()
  ctx.moveTo(hx, 0)
  ctx.lineTo(hx, topY + 10)
  ctx.stroke()

  const hoopImg = g16Sprite('hoop')
  if (g16ready(hoopImg)) {
    const hw = 96
    const hh = hw * (hoopImg.naturalHeight / hoopImg.naturalWidth)
    // 讓籃框（圖片下緣附近）對齊 HOOP_Y
    ctx.drawImage(hoopImg, hx - hw / 2, HOOP_Y - hh * 0.72, hw, hh)
    return
  }

  // backboard shadow
  ctx.fillStyle = 'rgba(0,0,0,0.3)'
  ctx.fillRect(hx - 33, topY + 3, 72, 50)
  // backboard
  const board = ctx.createLinearGradient(hx - 36, topY, hx + 36, topY)
  board.addColorStop(0, '#ffffff')
  board.addColorStop(1, '#dde4ee')
  ctx.fillStyle = board
  ctx.fillRect(hx - 36, topY, 72, 50)
  ctx.strokeStyle = '#c2872f'
  ctx.lineWidth = 2
  ctx.strokeRect(hx - 36, topY, 72, 50)
  // inner target
  ctx.strokeStyle = '#ff7a3a'
  ctx.lineWidth = 3
  ctx.strokeRect(hx - 15, HOOP_Y - 34, 30, 22)

  // rim (ellipse for depth)
  ctx.strokeStyle = '#ff6a2a'
  ctx.lineWidth = 5
  ctx.beginPath()
  ctx.ellipse(hx, HOOP_Y, RIM / 2, 6, 0, 0, Math.PI * 2)
  ctx.stroke()
  ctx.strokeStyle = '#ffb37a'
  ctx.lineWidth = 2
  ctx.beginPath()
  ctx.ellipse(hx, HOOP_Y, RIM / 2, 6, 0, 0, Math.PI)
  ctx.stroke()

  // net
  ctx.strokeStyle = 'rgba(255,255,255,0.6)'
  ctx.lineWidth = 1
  const netH = 26
  const strands = 8
  for (let i = 0; i <= strands; i += 1) {
    const t = i / strands
    const topX = hx - RIM / 2 + RIM * t
    const sway = Math.sin(now / 600 + i) * 1.5
    const botX = hx - RIM / 4 + (RIM / 2) * t + sway
    ctx.beginPath()
    ctx.moveTo(topX, HOOP_Y + 4)
    ctx.lineTo(botX, HOOP_Y + netH)
    ctx.stroke()
  }
  for (let r = 1; r <= 2; r += 1) {
    const y = HOOP_Y + 4 + (netH - 4) * (r / 3)
    const w = (RIM / 2) * (1 - 0.28 * (r / 3))
    ctx.beginPath()
    ctx.ellipse(hx, y, w, 3, 0, 0, Math.PI)
    ctx.stroke()
  }
}

function drawGuide(side) {
  if (side.ball) return
  const { pts, willScore } = simulateShot(side)
  ctx.save()
  ctx.setLineDash([2, 9])
  ctx.lineWidth = 3
  ctx.lineCap = 'round'
  if (willScore) {
    ctx.strokeStyle = 'rgba(125,255,176,0.95)'
    ctx.shadowColor = '#5fe6a0'
    ctx.shadowBlur = 10
  } else {
    ctx.strokeStyle = side.half === 0 ? 'rgba(94,255,214,0.7)' : 'rgba(255,158,200,0.72)'
  }
  ctx.beginPath()
  pts.forEach((p, i) => (i === 0 ? ctx.moveTo(p.x, p.y) : ctx.lineTo(p.x, p.y)))
  ctx.stroke()
  ctx.restore()
  // landing marker
  const last = pts[pts.length - 1]
  ctx.fillStyle = willScore ? '#7dffb0' : 'rgba(255,255,255,0.45)'
  ctx.beginPath()
  ctx.arc(last.x, last.y, 4, 0, Math.PI * 2)
  ctx.fill()
  if (willScore) {
    ctx.fillStyle = '#7dffb0'
    ctx.font = 'bold 13px system-ui, sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText('會進！', side.hoopX, HOOP_Y - 72)
  }
}

function drawShooter(side, now, pose) {
  const pid = side.half === 0 ? 'p1' : 'p2'
  const img = g16Sprite(`shooter-${pid}-${pose}`)
  if (!g16ready(img)) return
  const w = 70
  const h = w * (img.naturalHeight / img.naturalWidth)
  ctx.imageSmoothingEnabled = false
  ctx.drawImage(img, side.launchX - w / 2, CANVAS_H - 4 - h, w, h)
}

function drawSprite(side, now) {
  const s = side.sprite
  if (!s) return
  const blkImg = g16Sprite(Math.floor(now / 180) % 2 ? 'blocker-2' : 'blocker-1')
  if (g16ready(blkImg)) {
    const bw = 54
    const bh = bw * (blkImg.naturalHeight / blkImg.naturalWidth)
    ctx.imageSmoothingEnabled = false
    ctx.drawImage(blkImg, s.x - bw / 2, s.y - bh / 2, bw, bh)
    return
  }
  ctx.save()
  ctx.translate(s.x, s.y)
  // wings
  const flap = Math.sin(now / 55) * 0.6
  ctx.save()
  ctx.shadowColor = '#c98bff'
  ctx.shadowBlur = 14
  ctx.fillStyle = 'rgba(201,139,255,0.55)'
  ctx.beginPath()
  ctx.ellipse(-13, -2, 9, 5, flap, 0, Math.PI * 2)
  ctx.fill()
  ctx.beginPath()
  ctx.ellipse(13, -2, 9, 5, -flap, 0, Math.PI * 2)
  ctx.fill()
  ctx.restore()
  // body
  const bg = ctx.createRadialGradient(-3, -4, 2, 0, 0, 13)
  bg.addColorStop(0, '#ecccff')
  bg.addColorStop(1, '#8a3ad6')
  ctx.fillStyle = bg
  ctx.beginPath()
  ctx.arc(0, 0, 12, 0, Math.PI * 2)
  ctx.fill()
  // horns
  ctx.fillStyle = '#6a28b0'
  ctx.beginPath()
  ctx.moveTo(-7, -9)
  ctx.lineTo(-10, -17)
  ctx.lineTo(-2, -10)
  ctx.closePath()
  ctx.fill()
  ctx.beginPath()
  ctx.moveTo(7, -9)
  ctx.lineTo(10, -17)
  ctx.lineTo(2, -10)
  ctx.closePath()
  ctx.fill()
  // eyes
  ctx.fillStyle = '#fff'
  ctx.beginPath()
  ctx.arc(-4, -2, 2.7, 0, Math.PI * 2)
  ctx.arc(4, -2, 2.7, 0, Math.PI * 2)
  ctx.fill()
  ctx.fillStyle = '#22102e'
  ctx.beginPath()
  ctx.arc(-4, -2, 1.4, 0, Math.PI * 2)
  ctx.arc(4, -2, 1.4, 0, Math.PI * 2)
  ctx.fill()
  // grin
  ctx.strokeStyle = '#3a1550'
  ctx.lineWidth = 1.5
  ctx.beginPath()
  ctx.arc(0, 3, 4, 0.1 * Math.PI, 0.9 * Math.PI)
  ctx.stroke()
  // swat arm
  if (s.swat > 0) {
    ctx.strokeStyle = '#ecccff'
    ctx.lineWidth = 3
    ctx.lineCap = 'round'
    const reach = 16 * s.swat * (s.vx < 0 ? 1 : -1)
    ctx.beginPath()
    ctx.moveTo(0, 2)
    ctx.lineTo(reach, 9)
    ctx.stroke()
    ctx.lineCap = 'butt'
  }
  ctx.restore()
}

function drawPowerMeter(side, ox) {
  const x = ox + 16
  const y = CANVAS_H - 24
  const w = 130
  const h = 12
  ctx.fillStyle = 'rgba(0,0,0,0.4)'
  roundRectPath(x, y, w, h, 6)
  ctx.fill()
  const p = side.power / 100
  ctx.fillStyle = side.power > 80 ? '#ff5d6c' : side.power > 50 ? '#ffd23f' : '#5fe6a0'
  ctx.fillRect(x, y, w * p, h)
  ctx.strokeStyle = 'rgba(255,255,255,0.25)'
  ctx.lineWidth = 1
  roundRectPath(x, y, w, h, 6)
  ctx.stroke()
  ctx.fillStyle = 'rgba(255,255,255,0.6)'
  ctx.font = '10px system-ui, sans-serif'
  ctx.textAlign = 'left'
  ctx.fillText('POWER', x, y - 4)
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

function drawBall(x, y) {
  const ballImg = g16Sprite('ball-basketball')
  if (g16ready(ballImg)) {
    if (y > FLOOR_TOP - 20) {
      const sa = Math.max(0, Math.min(0.3, (y - (FLOOR_TOP - 20)) / 300))
      ctx.fillStyle = `rgba(0,0,0,${sa})`
      ctx.beginPath()
      ctx.ellipse(x, Math.min(CANVAS_H - 6, y + 16), 12, 4, 0, 0, Math.PI * 2)
      ctx.fill()
    }
    const sz = 28
    ctx.imageSmoothingEnabled = false
    ctx.drawImage(ballImg, x - sz / 2, y - sz / 2, sz, sz)
    return
  }
  ctx.save()
  // floor shadow when low
  if (y > FLOOR_TOP - 20) {
    const sa = Math.max(0, Math.min(0.3, (y - (FLOOR_TOP - 20)) / 300))
    ctx.fillStyle = `rgba(0,0,0,${sa})`
    ctx.beginPath()
    ctx.ellipse(x, Math.min(CANVAS_H - 6, y + 16), 12, 4, 0, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.shadowColor = 'rgba(255,140,60,0.5)'
  ctx.shadowBlur = 12
  const g = ctx.createRadialGradient(x - 4, y - 5, 2, x, y, 14)
  g.addColorStop(0, '#ffb877')
  g.addColorStop(0.6, '#ff8a3a')
  g.addColorStop(1, '#d65f1f')
  ctx.fillStyle = g
  ctx.beginPath()
  ctx.arc(x, y, 13, 0, Math.PI * 2)
  ctx.fill()
  ctx.shadowBlur = 0
  // seams
  ctx.strokeStyle = 'rgba(40,15,0,0.7)'
  ctx.lineWidth = 1.5
  ctx.beginPath()
  ctx.arc(x, y, 13, 0, Math.PI * 2)
  ctx.moveTo(x - 13, y)
  ctx.lineTo(x + 13, y)
  ctx.moveTo(x, y - 13)
  ctx.lineTo(x, y + 13)
  ctx.moveTo(x - 9, y - 9)
  ctx.quadraticCurveTo(x, y, x - 9, y + 9)
  ctx.moveTo(x + 9, y - 9)
  ctx.quadraticCurveTo(x, y, x + 9, y + 9)
  ctx.stroke()
  // highlight
  ctx.fillStyle = 'rgba(255,255,255,0.35)'
  ctx.beginPath()
  ctx.arc(x - 4, y - 5, 3, 0, Math.PI * 2)
  ctx.fill()
  ctx.restore()
}

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  if (k.startsWith('arrow') || k === '/' || k === ' ' || k === ',') e.preventDefault()
  if (phase.value !== 'playing') {
    if (k === ' ' || k === 'enter') startGame()
    keys.add(k)
    return
  }
  if (!keys.has(k)) {
    if (k === 'w') shoot(game.p1)
    if (k === 'arrowup') shoot(game.p2)
    if (k === 'q') useCard(game.p1, game.p2)
    if (k === ',') useCard(game.p2, game.p1)
  }
  keys.add(k)
}
function onKeyUp(e) {
  keys.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame16Records()
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
    const store = await fetchGame16Store()
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
.game16-view { min-height: 100vh; padding: 24px clamp(16px,4vw,48px) 48px; color: #f3ece0; background: radial-gradient(circle at 50% -10%, #3a2a18, #160f08 60%); font-family: 'Segoe UI', system-ui, sans-serif; }
.topbar { display: flex; align-items: center; gap: 20px; flex-wrap: wrap; margin-bottom: 22px; }
.back-link { color: #d6b88a; text-decoration: none; font-size: 14px; padding: 8px 14px; border: 1px solid rgba(214,184,138,0.3); border-radius: 999px; transition: 0.2s; }
.back-link:hover { background: rgba(214,184,138,0.12); color: #fff; }
.title-block .eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; color: #ff9f5a; text-transform: uppercase; }
.title-block h1 { margin: 2px 0 0; font-size: 26px; background: linear-gradient(90deg,#ff9f5a,#ffd23f); -webkit-background-clip: text; background-clip: text; color: transparent; }
.time-pill { margin-left: auto; padding: 8px 18px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 16px; font-weight: 700; font-variant-numeric: tabular-nums; }
.time-pill.urgent { background: rgba(255,93,108,0.2); border-color: rgba(255,93,108,0.5); color: #ff9eaa; }
.layout { display: grid; grid-template-columns: minmax(0,1fr) 300px; gap: 22px; align-items: start; }
.stage-card { background: rgba(22,15,8,0.6); border: 1px solid rgba(214,184,138,0.16); border-radius: 20px; padding: 16px; box-shadow: 0 24px 60px rgba(0,0,0,0.5); }
.scoreband { display: flex; align-items: center; gap: 16px; margin-bottom: 14px; padding: 0 6px; }
.team { display: flex; align-items: center; gap: 12px; flex: 1; }
.team-2 { justify-content: flex-end; }
.team strong { font-size: 15px; }
.team .score { font-size: 24px; font-weight: 800; font-variant-numeric: tabular-nums; }
.team-1 .score { color: #ffd23f; }
.team-2 .score { color: #ff9f5a; }
.team .cards { font-size: 13px; color: #d6b88a; }
.vs { font-size: 13px; font-weight: 800; color: #8a6f4a; }
.stage-frame { position: relative; border-radius: 14px; overflow: hidden; }
.game-canvas { display: block; width: 100%; height: auto; border-radius: 14px; }
.overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(14,9,4,0.82); backdrop-filter: blur(4px); }
.overlay-card { text-align: center; max-width: 470px; padding: 32px; }
.overlay-eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; color: #ff9f5a; }
.overlay-card h2 { margin: 10px 0 14px; font-size: 26px; }
.winner-text { background: linear-gradient(90deg,#ff9f5a,#ffd23f); -webkit-background-clip: text; background-clip: text; color: transparent; }
.overlay-text { color: #d9c8ad; line-height: 1.7; margin: 0 0 22px; }
.primary-btn { border: none; padding: 12px 30px; border-radius: 999px; font-size: 16px; font-weight: 700; color: #2a1a08; background: linear-gradient(90deg,#ff9f5a,#ffd23f); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; box-shadow: 0 10px 26px rgba(255,159,90,0.4); }
.primary-btn:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(255,159,90,0.55); }
.sidebar { display: flex; flex-direction: column; gap: 16px; }
.panel { background: rgba(22,15,8,0.6); border: 1px solid rgba(214,184,138,0.16); border-radius: 18px; padding: 18px; }
.panel .eyebrow { margin: 0 0 12px; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #a3835a; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.controls-grid { display: grid; gap: 10px; }
.ctrl { border-radius: 12px; padding: 12px 14px; }
.ctrl strong { display: block; font-size: 14px; margin-bottom: 6px; }
.ctrl span { display: block; font-size: 13px; color: #d9c8ad; margin-bottom: 3px; }
.ctrl-1 { background: rgba(255,210,63,0.1); border: 1px solid rgba(255,210,63,0.25); }
.ctrl-2 { background: rgba(255,159,90,0.1); border: 1px solid rgba(255,159,90,0.25); }
kbd { background: rgba(255,255,255,0.12); border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 2px 7px; font-size: 12px; font-family: inherit; margin-right: 2px; }
.tips { margin: 0; padding-left: 18px; display: grid; gap: 8px; font-size: 13px; color: #d9c8ad; line-height: 1.5; }
.records { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.records li { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: center; font-size: 12px; padding: 8px 10px; border-radius: 10px; background: rgba(255,255,255,0.04); }
.rec-win { font-weight: 700; color: #ffd23f; }
.rec-score { color: #d6b88a; }
.rec-date { color: #8a6f4a; }
.empty { font-size: 13px; color: #a3835a; line-height: 1.6; margin: 0; }
.ghost-btn { background: none; border: 1px solid rgba(214,184,138,0.3); color: #d6b88a; border-radius: 999px; padding: 4px 12px; font-size: 12px; cursor: pointer; }
.ghost-btn:hover { background: rgba(214,184,138,0.12); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } }
</style>
