<template>
  <main class="game15-view">
    <section class="topbar">
      <RouterLink to="/" class="back-link">← 返回遊戲廳</RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 15</p>
        <h1>大魚吃小魚</h1>
      </div>
      <div v-if="phase === 'playing'" class="time-pill" :class="{ urgent: timeLeft <= 10 }">⏱ {{ timeLeft }}s</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <span class="dot" />
            <strong>玩家 1</strong>
            <span class="score">{{ hud.p1Score }}</span>
            <span class="size">體型 {{ Math.round(hud.p1Size) }}</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="size">體型 {{ Math.round(hud.p2Size) }}</span>
            <span class="score">{{ hud.p2Score }}</span>
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
                  <p class="overlay-eyebrow">弱肉強食</p>
                  <h2>吃掉比你小的魚成長</h2>
                  <p class="overlay-text">
                    游動吞食比自己小的魚變大，避開更大的魚。<br>
                    體型夠大時可吞掉對手！衝刺加速但會消耗體力，60 秒比分數。
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
              <span><kbd>W</kbd><kbd>A</kbd><kbd>S</kbd><kbd>D</kbd> 游動 · <kbd>F</kbd> 衝刺</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span><kbd>↑</kbd><kbd>←</kbd><kbd>↓</kbd><kbd>→</kbd> 游動 · <kbd>/</kbd> 衝刺</span>
            </div>
          </div>
        </section>
        <section class="panel">
          <p class="eyebrow">規則</p>
          <ul class="tips">
            <li>只能吃比自己小的魚，越吃越大。</li>
            <li>體型大過對手一截即可吞掉對手。</li>
            <li>⭐ 無敵星：短時間內無敵、通吃。</li>
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
import { clearGame15Records, fetchGame15Store, saveGame15Record } from './game15Storage'
import { recordGameResult } from '@/data/lobbyScore'
import { assetUrl } from '@/utils/assetUrl'

const CANVAS_W = 920
const CANVAS_H = 600
const GAME_SEC = 60
const BASE_SIZE = 22
const MAX_SIZE = 64
const AI_COUNT = 30

// 像素素材
const G15 = {}
function g15Sprite(name) {
  if (!G15[name]) {
    const img = new Image()
    img.src = assetUrl(`/assets/G15/${name}.png`)
    G15[name] = img
  }
  return G15[name]
}
const G15_BG = g15Sprite('bg-underwater')
const NPC_TIERS = ['tiny', 'small', 'mid', 'big', 'shark']
function playerTier(size) {
  return Math.max(1, Math.min(5, 1 + Math.floor(((size - BASE_SIZE) / (MAX_SIZE - BASE_SIZE)) * 5)))
}
function npcTier(size) {
  if (size < 13) return 'tiny'
  if (size < 19) return 'small'
  if (size < 26) return 'mid'
  if (size < 33) return 'big'
  return 'shark'
}
function g15ready(img) {
  return img && img.complete && img.naturalWidth > 0
}
// 海底裝飾（固定位置）
const G15_DECO = [
  { n: 'deco-seaweed-a-1', x: 80, w: 60 },
  { n: 'deco-coral-pink', x: 200, w: 80 },
  { n: 'deco-rock', x: 320, w: 90 },
  { n: 'deco-seaweed-b-1', x: 470, w: 56 },
  { n: 'deco-coral-purple', x: 600, w: 80 },
  { n: 'deco-starfish', x: 720, w: 48 },
  { n: 'deco-seaweed-a-1', x: 840, w: 60 }
]

const canvasRef = ref(null)
const stageRef = ref(null)
const phase = ref('intro')
const timeLeft = ref(GAME_SEC)
const resultText = ref('')
const records = ref([])
const hud = reactive({ p1Score: 0, p2Score: 0, p1Size: BASE_SIZE, p2Size: BASE_SIZE })

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null
const keys = new Set()

const AI_COLORS = ['#7fd8ff', '#a3e6c0', '#ffd28a', '#ff9ec8', '#c2a3ff', '#ffe66d']

function makePlayer(id, x, color) {
  return { id, x, y: CANVAS_H / 2, vx: 0, vy: 0, size: BASE_SIZE, score: 0, stamina: 100, invulUntil: 0, color, dir: 1 }
}

function makeAI() {
  // skew strongly toward small fish; big ones are rare
  const size = 7 + Math.pow(Math.random(), 2.6) * 32
  return {
    x: Math.random() * CANVAS_W,
    y: Math.random() * CANVAS_H,
    vx: (Math.random() - 0.5) * 1.6,
    vy: (Math.random() - 0.5) * 1.2,
    size,
    color: AI_COLORS[Math.floor(Math.random() * AI_COLORS.length)],
    turnTimer: 1000 + Math.random() * 2000
  }
}

function createGame() {
  return {
    p1: makePlayer('p1', CANVAS_W * 0.25, '#3affd0'),
    p2: makePlayer('p2', CANVAS_W * 0.75, '#ff9ec8'),
    ai: Array.from({ length: AI_COUNT }, makeAI),
    star: null,
    starTimer: 8000,
    bubbles: Array.from({ length: 30 }, () => ({ x: Math.random() * CANVAS_W, y: Math.random() * CANVAS_H, r: 1 + Math.random() * 3, sp: 10 + Math.random() * 30 })),
    elapsed: 0,
    particles: []
  }
}

function startGame() {
  game = createGame()
  hud.p1Score = 0
  hud.p2Score = 0
  hud.p1Size = BASE_SIZE
  hud.p2Size = BASE_SIZE
  timeLeft.value = GAME_SEC
  phase.value = 'playing'
  lastFrame = performance.now()
  loop(lastFrame)
}

function maxSpeed(size) {
  return 4.6 - (size / MAX_SIZE) * 2.2
}

function controlPlayer(p, up, down, left, right, sprintKey, dt, now) {
  const f = dt / 16.67
  let ax = 0
  let ay = 0
  if (keys.has(left)) ax -= 1
  if (keys.has(right)) ax += 1
  if (keys.has(up)) ay -= 1
  if (keys.has(down)) ay += 1
  const sprinting = keys.has(sprintKey) && p.stamina > 0 && (ax || ay)
  const accel = 0.5 * (sprinting ? 1.8 : 1)
  p.vx += ax * accel * f
  p.vy += ay * accel * f
  if (sprinting) p.stamina = Math.max(0, p.stamina - dt / 18)
  else p.stamina = Math.min(100, p.stamina + dt / 40)
  const ms = maxSpeed(p.size) * (sprinting ? 1.8 : 1)
  const sp = Math.hypot(p.vx, p.vy)
  if (sp > ms) {
    p.vx = (p.vx / sp) * ms
    p.vy = (p.vy / sp) * ms
  }
  p.vx *= 0.92
  p.vy *= 0.92
  p.x += p.vx * f
  p.y += p.vy * f
  if (Math.abs(p.vx) > 0.2) p.dir = p.vx > 0 ? 1 : -1
  const r = p.size
  if (p.x < r) { p.x = r; p.vx = Math.abs(p.vx) }
  if (p.x > CANVAS_W - r) { p.x = CANVAS_W - r; p.vx = -Math.abs(p.vx) }
  if (p.y < r) { p.y = r; p.vy = Math.abs(p.vy) }
  if (p.y > CANVAS_H - r) { p.y = CANVAS_H - r; p.vy = -Math.abs(p.vy) }
  void now
}

function syncHud() {
  hud.p1Score = game.p1.score
  hud.p2Score = game.p2.score
  hud.p1Size = game.p1.size
  hud.p2Size = game.p2.size
}

function emit(x, y, color, n) {
  for (let i = 0; i < n; i += 1) {
    const a = Math.random() * Math.PI * 2
    const sp = 30 + Math.random() * 120
    game.particles.push({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 1, color })
  }
}

function update(dt, now) {
  game.elapsed += dt
  timeLeft.value = Math.max(0, Math.ceil((GAME_SEC * 1000 - game.elapsed) / 1000))

  controlPlayer(game.p1, 'w', 's', 'a', 'd', 'f', dt, now)
  controlPlayer(game.p2, 'arrowup', 'arrowdown', 'arrowleft', 'arrowright', '/', dt, now)

  // AI movement
  const f = dt / 16.67
  for (const fish of game.ai) {
    fish.turnTimer -= dt
    if (fish.turnTimer <= 0) {
      fish.turnTimer = 1000 + Math.random() * 2500
      fish.vx += (Math.random() - 0.5) * 0.8
      fish.vy += (Math.random() - 0.5) * 0.6
    }
    // flee from bigger players nearby
    for (const p of [game.p1, game.p2]) {
      const d = Math.hypot(p.x - fish.x, p.y - fish.y)
      if (p.size > fish.size && d < 130) {
        fish.vx += ((fish.x - p.x) / d) * 0.4
        fish.vy += ((fish.y - p.y) / d) * 0.4
      }
    }
    const ms = maxSpeed(fish.size) * 0.7
    const sp = Math.hypot(fish.vx, fish.vy)
    if (sp > ms) { fish.vx = (fish.vx / sp) * ms; fish.vy = (fish.vy / sp) * ms }
    fish.x += fish.vx * f
    fish.y += fish.vy * f
    if (fish.x < fish.size || fish.x > CANVAS_W - fish.size) fish.vx *= -1
    if (fish.y < fish.size || fish.y > CANVAS_H - fish.size) fish.vy *= -1
    fish.x = Math.max(fish.size, Math.min(CANVAS_W - fish.size, fish.x))
    fish.y = Math.max(fish.size, Math.min(CANVAS_H - fish.size, fish.y))
  }

  // eating AI
  for (const p of [game.p1, game.p2]) {
    const invul = now < p.invulUntil
    for (let i = game.ai.length - 1; i >= 0; i -= 1) {
      const fish = game.ai[i]
      const d = Math.hypot(p.x - fish.x, p.y - fish.y)
      if (d < p.size) {
        if (invul || p.size > fish.size * 1.02) {
          p.size = Math.min(MAX_SIZE, p.size + fish.size * 0.05)
          p.score += Math.round(fish.size)
          emit(fish.x, fish.y, fish.color, 8)
          game.ai.splice(i, 1)
          game.ai.push(makeAIAtEdge())
        }
      }
    }
  }

  // player vs player
  const a = game.p1
  const b = game.p2
  const dpp = Math.hypot(a.x - b.x, a.y - b.y)
  if (dpp < Math.max(a.size, b.size)) {
    if (now > b.invulUntil && (now < a.invulUntil || a.size > b.size * 1.15)) eatPlayer(a, b, now)
    else if (now > a.invulUntil && (now < b.invulUntil || b.size > a.size * 1.15)) eatPlayer(b, a, now)
  }

  // star
  game.starTimer -= dt
  if (game.starTimer <= 0 && !game.star) {
    game.starTimer = 12000 + Math.random() * 6000
    game.star = { x: 80 + Math.random() * (CANVAS_W - 160), y: 80 + Math.random() * (CANVAS_H - 160), ttl: 9000 }
  }
  if (game.star) {
    game.star.ttl -= dt
    for (const p of [game.p1, game.p2]) {
      if (Math.hypot(p.x - game.star.x, p.y - game.star.y) < p.size + 16) {
        p.invulUntil = now + 5000
        emit(game.star.x, game.star.y, '#ffd23f', 18)
        game.star = null
        break
      }
    }
    if (game.star && game.star.ttl <= 0) game.star = null
  }

  for (const bb of game.bubbles) {
    bb.y -= bb.sp * (dt / 1000)
    if (bb.y < -5) { bb.y = CANVAS_H + 5; bb.x = Math.random() * CANVAS_W }
  }
  for (const pt of game.particles) {
    pt.x += pt.vx * (dt / 1000)
    pt.y += pt.vy * (dt / 1000)
    pt.life -= dt / 600
  }
  game.particles = game.particles.filter((p) => p.life > 0)

  syncHud()
  if (game.elapsed >= GAME_SEC * 1000) finishGame()
}

function makeAIAtEdge() {
  const fish = makeAI()
  const edge = Math.floor(Math.random() * 4)
  if (edge === 0) { fish.x = 0; fish.y = Math.random() * CANVAS_H }
  else if (edge === 1) { fish.x = CANVAS_W; fish.y = Math.random() * CANVAS_H }
  else if (edge === 2) { fish.y = 0; fish.x = Math.random() * CANVAS_W }
  else { fish.y = CANVAS_H; fish.x = Math.random() * CANVAS_W }
  return fish
}

function eatPlayer(winner, loser, now) {
  winner.size = Math.min(MAX_SIZE, winner.size + loser.size * 0.25)
  winner.score += 60
  emit(loser.x, loser.y, loser.color, 22)
  loser.size = BASE_SIZE
  loser.x = winner.id === 'p1' ? CANVAS_W * 0.85 : CANVAS_W * 0.15
  loser.y = CANVAS_H / 2
  loser.vx = 0
  loser.vy = 0
  loser.invulUntil = now + 2500
}

async function finishGame() {
  cancelAnimationFrame(rafId)
  rafId = 0
  let winner
  if (game.p1.score > game.p2.score) winner = '玩家 1 獲勝'
  else if (game.p2.score > game.p1.score) winner = '玩家 2 獲勝'
  else winner = '平手'
  resultText.value = `🐟 ${winner}`
  phase.value = 'result'
  recordGameResult('/game15', game.p1.score > game.p2.score ? 'p1' : game.p2.score > game.p1.score ? 'p2' : 'draw')
  try {
    const store = await saveGame15Record({ winner, scoreP1: game.p1.score, scoreP2: game.p2.score, date: new Date().toISOString() })
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
  ctx.imageSmoothingEnabled = false
  if (g15ready(G15_BG)) {
    ctx.drawImage(G15_BG, 0, 0, CANVAS_W, CANVAS_H)
  } else {
    const sea = ctx.createLinearGradient(0, 0, 0, CANVAS_H)
    sea.addColorStop(0, '#0e3b5c')
    sea.addColorStop(1, '#07263c')
    ctx.fillStyle = sea
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  }
  // 海底裝飾
  for (const d of G15_DECO) {
    const img = g15Sprite(d.n)
    if (g15ready(img)) {
      const h = d.w * (img.naturalHeight / img.naturalWidth)
      ctx.drawImage(img, d.x - d.w / 2, CANVAS_H - h + 2, d.w, h)
    }
  }
  const bub = g15Sprite('bubble')
  for (const bb of game.bubbles) {
    if (g15ready(bub)) {
      ctx.drawImage(bub, bb.x - bb.r, bb.y - bb.r, bb.r * 2, bb.r * 2)
    } else {
      ctx.fillStyle = 'rgba(255,255,255,0.12)'
      ctx.beginPath()
      ctx.arc(bb.x, bb.y, bb.r, 0, Math.PI * 2)
      ctx.fill()
    }
  }
  for (const fish of game.ai) drawFish(fish.x, fish.y, fish.size, fish.color, fish.vx >= 0 ? 1 : -1, false, now)
  if (game.star) {
    ctx.save()
    ctx.translate(game.star.x, game.star.y)
    ctx.rotate(now / 600)
    ctx.shadowColor = '#ffd23f'
    ctx.shadowBlur = 18
    ctx.font = '30px serif'
    ctx.textAlign = 'center'
    ctx.textBaseline = 'middle'
    ctx.fillText('⭐', 0, 0)
    ctx.restore()
  }
  drawFish(game.p1.x, game.p1.y, game.p1.size, game.p1.color, game.p1.dir, now < game.p1.invulUntil, now, 'p1')
  drawFish(game.p2.x, game.p2.y, game.p2.size, game.p2.color, game.p2.dir, now < game.p2.invulUntil, now, 'p2')
  for (const pt of game.particles) {
    ctx.globalAlpha = Math.max(0, pt.life)
    ctx.fillStyle = pt.color
    ctx.beginPath()
    ctx.arc(pt.x, pt.y, 3 * pt.life + 1, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.globalAlpha = 1
  // stamina bars
  drawStamina(game.p1, 14)
  drawStamina(game.p2, CANVAS_W - 134)
}

function drawStamina(p, x) {
  ctx.fillStyle = 'rgba(0,0,0,0.3)'
  ctx.fillRect(x, CANVAS_H - 16, 120, 8)
  ctx.fillStyle = p.color
  ctx.fillRect(x, CANVAS_H - 16, 120 * (p.stamina / 100), 8)
}

function mix(hex, target, f) {
  const n = parseInt(hex.slice(1), 16)
  const r = (n >> 16) & 255
  const g = (n >> 8) & 255
  const b = n & 255
  const R = Math.round(r + (target - r) * f)
  const G = Math.round(g + (target - g) * f)
  const B = Math.round(b + (target - b) * f)
  return `rgb(${R},${G},${B})`
}

function drawFish(x, y, size, color, dir, glow, now, isPlayer) {
  // 像素魚精靈
  const side = dir > 0 ? 'r' : 'l'
  const frame = Math.floor(now / 160) % 2 ? 'b' : 'a'
  const spriteName = isPlayer
    ? `fish-${isPlayer}-L${playerTier(size)}-${frame}-${side}`
    : `npc-${npcTier(size)}-${frame}-${side}`
  const fimg = g15Sprite(spriteName)
  if (g15ready(fimg)) {
    ctx.save()
    if (glow) {
      ctx.shadowColor = '#ffd23f'
      ctx.shadowBlur = 22
    }
    const w = size * 2.7
    const h = w * (fimg.naturalHeight / fimg.naturalWidth)
    ctx.imageSmoothingEnabled = false
    ctx.drawImage(fimg, x - w / 2, y - h / 2, w, h)
    ctx.restore()
    return
  }

  ctx.save()
  ctx.translate(x, y)
  ctx.scale(dir, 1)
  if (glow) {
    ctx.shadowColor = '#ffd23f'
    ctx.shadowBlur = 22
  }
  const dark = mix(color, 0, 0.42)
  const light = mix(color, 255, 0.42)
  const wig = Math.sin(now / 150 + x * 0.05) * size * 0.16

  // tail (forked, wiggling)
  ctx.fillStyle = dark
  ctx.beginPath()
  ctx.moveTo(-size * 0.66, 0)
  ctx.quadraticCurveTo(-size * 1.2, -size * 0.2 + wig, -size * 1.5, -size * 0.55 + wig)
  ctx.lineTo(-size * 1.32, 0)
  ctx.lineTo(-size * 1.5, size * 0.55 - wig)
  ctx.quadraticCurveTo(-size * 1.2, size * 0.2 - wig, -size * 0.66, 0)
  ctx.closePath()
  ctx.fill()

  // dorsal + pelvic fins
  ctx.fillStyle = mix(color, 0, 0.25)
  ctx.beginPath()
  ctx.moveTo(-size * 0.15, -size * 0.6)
  ctx.quadraticCurveTo(size * 0.15, -size * 1.02, size * 0.55, -size * 0.46)
  ctx.closePath()
  ctx.fill()
  ctx.beginPath()
  ctx.moveTo(-size * 0.05, size * 0.5)
  ctx.quadraticCurveTo(-size * 0.1, size * 0.95, size * 0.4, size * 0.46)
  ctx.closePath()
  ctx.fill()

  // body
  const g = ctx.createLinearGradient(0, -size * 0.75, 0, size * 0.75)
  g.addColorStop(0, light)
  g.addColorStop(0.5, color)
  g.addColorStop(1, dark)
  ctx.fillStyle = g
  ctx.beginPath()
  ctx.ellipse(0, 0, size, size * 0.7, 0, 0, Math.PI * 2)
  ctx.fill()

  // belly sheen
  ctx.fillStyle = 'rgba(255,255,255,0.16)'
  ctx.beginPath()
  ctx.ellipse(size * 0.12, size * 0.2, size * 0.66, size * 0.36, 0, 0, Math.PI * 2)
  ctx.fill()

  // gill
  ctx.strokeStyle = 'rgba(0,0,0,0.2)'
  ctx.lineWidth = Math.max(1, size * 0.05)
  ctx.beginPath()
  ctx.arc(size * 0.18, 0, size * 0.5, -0.95, 0.95)
  ctx.stroke()

  // pectoral fin
  ctx.fillStyle = 'rgba(0,0,0,0.16)'
  ctx.beginPath()
  ctx.moveTo(size * 0.16, size * 0.08)
  ctx.quadraticCurveTo(size * 0.05, size * 0.62, size * 0.52, size * 0.34)
  ctx.closePath()
  ctx.fill()

  // eye
  ctx.fillStyle = '#fff'
  ctx.beginPath()
  ctx.arc(size * 0.5, -size * 0.22, size * 0.2, 0, Math.PI * 2)
  ctx.fill()
  ctx.fillStyle = '#10141c'
  ctx.beginPath()
  ctx.arc(size * 0.55, -size * 0.22, size * 0.1, 0, Math.PI * 2)
  ctx.fill()
  ctx.fillStyle = 'rgba(255,255,255,0.9)'
  ctx.beginPath()
  ctx.arc(size * 0.5, -size * 0.27, size * 0.045, 0, Math.PI * 2)
  ctx.fill()

  // mouth
  ctx.strokeStyle = 'rgba(0,0,0,0.32)'
  ctx.lineWidth = Math.max(1, size * 0.05)
  ctx.beginPath()
  ctx.arc(size * 0.82, size * 0.04, size * 0.16, 2.4, 3.9)
  ctx.stroke()

  if (isPlayer) {
    ctx.shadowBlur = 0
    ctx.strokeStyle = 'rgba(255,255,255,0.7)'
    ctx.lineWidth = 2.2
    ctx.beginPath()
    ctx.ellipse(0, 0, size + 1, size * 0.7 + 1, 0, 0, Math.PI * 2)
    ctx.stroke()
  }
  ctx.restore()
}

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  keys.add(k)
  if (k.startsWith('arrow') || k === '/' || k === ' ') e.preventDefault()
  if ((k === ' ' || k === 'enter') && phase.value !== 'playing') startGame()
}
function onKeyUp(e) {
  keys.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame15Records()
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
    const store = await fetchGame15Store()
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
.game15-view { min-height: 100vh; padding: 24px clamp(16px,4vw,48px) 48px; color: #e6f3fb; background: radial-gradient(circle at 50% -10%, #0e3b5c, #04161f 60%); font-family: 'Segoe UI', system-ui, sans-serif; }
.topbar { display: flex; align-items: center; gap: 20px; flex-wrap: wrap; margin-bottom: 22px; }
.back-link { color: #8fcbe8; text-decoration: none; font-size: 14px; padding: 8px 14px; border: 1px solid rgba(143,203,232,0.3); border-radius: 999px; transition: 0.2s; }
.back-link:hover { background: rgba(143,203,232,0.12); color: #fff; }
.title-block .eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; color: #3affd0; text-transform: uppercase; }
.title-block h1 { margin: 2px 0 0; font-size: 26px; background: linear-gradient(90deg,#3affd0,#ff9ec8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.time-pill { margin-left: auto; padding: 8px 18px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 16px; font-weight: 700; font-variant-numeric: tabular-nums; }
.time-pill.urgent { background: rgba(255,93,108,0.2); border-color: rgba(255,93,108,0.5); color: #ff9eaa; }
.layout { display: grid; grid-template-columns: minmax(0,1fr) 300px; gap: 22px; align-items: start; }
.stage-card { background: rgba(4,18,28,0.6); border: 1px solid rgba(120,180,210,0.16); border-radius: 20px; padding: 16px; box-shadow: 0 24px 60px rgba(0,0,0,0.5); }
.scoreband { display: flex; align-items: center; gap: 16px; margin-bottom: 14px; padding: 0 6px; }
.team { display: flex; align-items: center; gap: 10px; flex: 1; }
.team-2 { justify-content: flex-end; }
.team .dot { width: 14px; height: 14px; border-radius: 50%; }
.team-1 .dot { background: #3affd0; box-shadow: 0 0 12px #3affd0; }
.team-2 .dot { background: #ff9ec8; box-shadow: 0 0 12px #ff9ec8; }
.team strong { font-size: 15px; }
.team .score { font-size: 22px; font-weight: 800; font-variant-numeric: tabular-nums; }
.team-1 .score { color: #3affd0; }
.team-2 .score { color: #ff9ec8; }
.team .size { font-size: 12px; color: #8fb4c8; }
.vs { font-size: 13px; font-weight: 800; color: #5a7a8c; }
.stage-frame { position: relative; border-radius: 14px; overflow: hidden; }
.game-canvas { display: block; width: 100%; height: auto; border-radius: 14px; }
.overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(3,16,24,0.82); backdrop-filter: blur(4px); }
.overlay-card { text-align: center; max-width: 460px; padding: 32px; }
.overlay-eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; color: #3affd0; }
.overlay-card h2 { margin: 10px 0 14px; font-size: 26px; }
.winner-text { background: linear-gradient(90deg,#3affd0,#ff9ec8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.overlay-text { color: #aecddd; line-height: 1.7; margin: 0 0 22px; }
.primary-btn { border: none; padding: 12px 30px; border-radius: 999px; font-size: 16px; font-weight: 700; color: #04201a; background: linear-gradient(90deg,#3affd0,#46d0ff); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; box-shadow: 0 10px 26px rgba(58,255,208,0.35); }
.primary-btn:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(58,255,208,0.5); }
.sidebar { display: flex; flex-direction: column; gap: 16px; }
.panel { background: rgba(4,18,28,0.6); border: 1px solid rgba(120,180,210,0.16); border-radius: 18px; padding: 18px; }
.panel .eyebrow { margin: 0 0 12px; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #6f97aa; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.controls-grid { display: grid; gap: 10px; }
.ctrl { border-radius: 12px; padding: 12px 14px; }
.ctrl strong { display: block; font-size: 14px; margin-bottom: 6px; }
.ctrl span { font-size: 13px; color: #aecddd; }
.ctrl-1 { background: rgba(58,255,208,0.1); border: 1px solid rgba(58,255,208,0.25); }
.ctrl-2 { background: rgba(255,158,200,0.1); border: 1px solid rgba(255,158,200,0.25); }
kbd { background: rgba(255,255,255,0.12); border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 2px 7px; font-size: 12px; font-family: inherit; margin-right: 2px; }
.tips { margin: 0; padding-left: 18px; display: grid; gap: 8px; font-size: 13px; color: #aecddd; line-height: 1.5; }
.records { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.records li { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: center; font-size: 12px; padding: 8px 10px; border-radius: 10px; background: rgba(255,255,255,0.04); }
.rec-win { font-weight: 700; color: #ffd23f; }
.rec-score { color: #8fcbe8; }
.rec-date { color: #5a7a8c; }
.empty { font-size: 13px; color: #6f97aa; line-height: 1.6; margin: 0; }
.ghost-btn { background: none; border: 1px solid rgba(143,203,232,0.3); color: #8fcbe8; border-radius: 999px; padding: 4px 12px; font-size: 12px; cursor: pointer; }
.ghost-btn:hover { background: rgba(143,203,232,0.12); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } }
</style>
