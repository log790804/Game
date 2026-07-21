<template>
  <main class="game08-view">
    <section class="topbar">
      <RouterLink
        to="/"
        class="back-link"
      >
        ← 返回遊戲廳
      </RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 08</p>
        <h1>接水果大作戰</h1>
      </div>
      <div
        v-if="phase === 'playing'"
        class="time-pill"
        :class="{ urgent: timeLeft <= 10 }"
      >
        ⏱ {{ timeLeft }}s
      </div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <strong>玩家 1</strong>
            <span class="score">{{ hud.p1Score }}</span>
            <span class="combo" :class="{ hot: hud.p1Combo >= 5 }">x{{ mult(hud.p1Combo) }}</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="combo" :class="{ hot: hud.p2Combo >= 5 }">x{{ mult(hud.p2Combo) }}</span>
            <span class="score">{{ hud.p2Score }}</span>
            <strong>玩家 2</strong>
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
                  <p class="overlay-eyebrow">手忙腳亂</p>
                  <h2>接住水果，閃開炸彈</h2>
                  <p class="overlay-text">
                    左右移動籃子接住掉落的水果累積分數，<br>
                    連續接住可疊加倍率，接到炸彈會扣分並中斷連擊。
                  </p>
                  <button
                    class="primary-btn"
                    @click="startGame"
                  >
                    開始遊戲
                  </button>
                </template>
                <template v-else-if="phase === 'result'">
                  <p class="overlay-eyebrow">時間到</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <p class="overlay-text">
                    玩家 1 <strong>{{ hud.p1Score }}</strong> ：
                    <strong>{{ hud.p2Score }}</strong> 玩家 2
                  </p>
                  <button
                    class="primary-btn"
                    @click="startGame"
                  >
                    再玩一次
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
              <span><kbd>A</kbd> 左 · <kbd>D</kbd> 右</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span><kbd>←</kbd> 左 · <kbd>→</kbd> 右</span>
            </div>
          </div>
        </section>

        <section class="panel">
          <p class="eyebrow">掉落物</p>
          <ul class="legend">
            <li
              v-for="(li, i) in legendItems"
              :key="li.type"
            >
              <canvas
                class="ic-canvas"
                width="40"
                height="40"
                :ref="(el) => setLegendRef(el, i)"
              />
              {{ li.label }}
            </li>
          </ul>
          <p class="hint">水果有多種顏色，皆為一般水果。連擊每 5 顆提升 1 倍，最高 5 倍。</p>
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
            尚無紀錄，遊戲結束後自動保存最近 10 場。
          </p>
        </section>
      </aside>
    </section>
  </main>
</template>

<script setup>
import { onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import {
  clearGame08Records,
  fetchGame08Store,
  saveGame08Record
} from './game08Storage'
import { recordGameResult } from '@/data/lobbyScore'
import { assetUrl } from '@/utils/assetUrl'

const CANVAS_W = 960
const CANVAS_H = 620
const HALF = CANVAS_W / 2
const GAME_SEC = 60
const BASKET_W = 96
const BASKET_Y = CANVAS_H - 54
const BASKET_SPEED = 8

const canvasRef = ref(null)
const stageRef = ref(null)

const legendItems = [
  { type: 'fruit', fruit: '🍎', label: '一般水果 +10' },
  { type: 'golden', label: '黃金果 +30' },
  { type: 'bomb', label: '炸彈 −20、連擊歸零' },
  { type: 'magnet', label: '磁鐵 5 秒自動吸取' },
  { type: 'freeze', label: '冰凍 5 秒落速減半' }
]
const legendCanvases = []
function setLegendRef(el, i) {
  if (el) legendCanvases[i] = el
}
function renderLegend() {
  for (let i = 0; i < legendItems.length; i += 1) {
    const el = legendCanvases[i]
    if (!el) continue
    const c = el.getContext('2d')
    c.clearRect(0, 0, el.width, el.height)
    c.save()
    c.translate(el.width / 2, el.height / 2)
    drawItemShape(c, legendItems[i].type, legendItems[i].fruit, 14)
    c.restore()
  }
}

const phase = ref('intro')
const timeLeft = ref(GAME_SEC)
const resultText = ref('')
const records = ref([])
const hud = reactive({ p1Score: 0, p2Score: 0, p1Combo: 0, p2Combo: 0 })

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null
const keys = new Set()

const FRUITS = ['🍎', '🍊', '🍇', '🍓', '🍑', '🍉']
const FRUIT_COLORS = {
  '🍎': '#ff4d4d',
  '🍊': '#ff9f43',
  '🍇': '#b06bff',
  '🍓': '#ff5d8f',
  '🍑': '#ffb142',
  '🍉': '#2bd66e'
}

// 像素素材
const G08 = {}
function g08Sprite(name) {
  if (!G08[name]) {
    const img = new Image()
    img.src = assetUrl(`/assets/G08/${name}.png`)
    G08[name] = img
  }
  return G08[name]
}
const FRUIT_SPRITE = {
  '🍎': 'fruit-apple',
  '🍊': 'fruit-orange',
  '🍇': 'fruit-grape',
  '🍓': 'fruit-cherry',
  '🍑': 'fruit-peach',
  '🍉': 'fruit-grape'
}
;['bg-orchard', 'basket-cat', 'bomb', 'fruit-apple', 'fruit-orange', 'fruit-grape', 'fruit-cherry', 'fruit-peach', 'fx-catch-1', 'fx-catch-2', 'fx-catch-3'].forEach(g08Sprite)
function g08ready(img) {
  return img && img.complete && img.naturalWidth > 0
}

function darken(hex, f) {
  const n = parseInt(hex.slice(1), 16)
  const r = Math.round(((n >> 16) & 255) * f)
  const g = Math.round(((n >> 8) & 255) * f)
  const b = Math.round((n & 255) * f)
  return `rgb(${r},${g},${b})`
}

function makeSide(half) {
  return {
    half,
    x: half === 0 ? HALF / 2 : HALF + HALF / 2,
    score: 0,
    combo: 0,
    items: [],
    spawnTimer: 600,
    magnetUntil: 0,
    freezeUntil: 0,
    popups: []
  }
}

function createGame() {
  return {
    elapsed: 0,
    p1: makeSide(0),
    p2: makeSide(1),
    particles: []
  }
}

function mult(combo) {
  return Math.min(5, 1 + Math.floor(combo / 5))
}

function startGame() {
  game = createGame()
  hud.p1Score = 0
  hud.p2Score = 0
  hud.p1Combo = 0
  hud.p2Combo = 0
  timeLeft.value = GAME_SEC
  phase.value = 'playing'
  lastFrame = performance.now()
  loop(lastFrame)
}

function spawnItem(side, now) {
  const progress = game.elapsed / (GAME_SEC * 1000)
  const baseX =
    side.half === 0
      ? 40 + Math.random() * (HALF - 80)
      : HALF + 40 + Math.random() * (HALF - 80)
  const roll = Math.random()
  let type
  if (roll < 0.1) type = 'bomb'
  else if (roll < 0.17) type = 'golden'
  else if (roll < 0.2) type = 'magnet'
  else if (roll < 0.23) type = 'freeze'
  else type = 'fruit'

  const fruit = FRUITS[Math.floor(Math.random() * FRUITS.length)]
  const speed = 2.2 + progress * 2.6 + Math.random() * 1.2
  side.items.push({
    x: baseX,
    y: -30,
    vy: speed,
    type,
    fruit,
    r: type === 'bomb' ? 22 : 20,
    rot: Math.random() * Math.PI,
    vr: (Math.random() - 0.5) * 0.1
  })
}

function pushPopup(side, x, y, text, color) {
  side.popups.push({ x, y, text, color, life: 1 })
}

function emitParticles(x, y, color, count) {
  for (let i = 0; i < count; i += 1) {
    const a = Math.random() * Math.PI * 2
    const sp = 60 + Math.random() * 160
    game.particles.push({
      x,
      y,
      vx: Math.cos(a) * sp,
      vy: Math.sin(a) * sp - 60,
      life: 1,
      color
    })
  }
}

function updateSide(side, leftKey, rightKey, dt, now, hudScore, hudCombo) {
  const f = dt / 16.67
  if (keys.has(leftKey)) side.x -= BASKET_SPEED * f
  if (keys.has(rightKey)) side.x += BASKET_SPEED * f
  const minX = side.half === 0 ? BASKET_W / 2 : HALF + BASKET_W / 2
  const maxX = side.half === 0 ? HALF - BASKET_W / 2 : CANVAS_W - BASKET_W / 2
  side.x = Math.max(minX, Math.min(maxX, side.x))

  side.spawnTimer -= dt
  if (side.spawnTimer <= 0) {
    const progress = game.elapsed / (GAME_SEC * 1000)
    side.spawnTimer = 720 - progress * 320 + Math.random() * 240
    spawnItem(side, now)
  }

  const freeze = now < side.freezeUntil
  const magnet = now < side.magnetUntil

  for (const it of side.items) {
    let vy = it.vy
    if (freeze) vy *= 0.5
    it.y += vy * f
    it.rot += it.vr * f
    if (magnet && it.type !== 'bomb') {
      const dx = side.x - it.x
      it.x += dx * 0.06 * f
    }
  }

  for (let i = side.items.length - 1; i >= 0; i -= 1) {
    const it = side.items[i]
    // catch
    if (
      it.y > BASKET_Y - 24 &&
      it.y < BASKET_Y + 26 &&
      Math.abs(it.x - side.x) < BASKET_W / 2 + it.r * 0.4
    ) {
      side.items.splice(i, 1)
      handleCatch(side, it, hudScore, hudCombo, now)
      continue
    }
    if (it.y > CANVAS_H + 40) {
      side.items.splice(i, 1)
      if (it.type === 'fruit' || it.type === 'golden') {
        side.combo = 0
        syncCombo(hudCombo, 0)
      }
    }
  }

  for (const p of side.popups) {
    p.y -= dt / 18
    p.life -= dt / 800
  }
  side.popups = side.popups.filter((p) => p.life > 0)
}

function syncCombo(key, value) {
  hud[key] = value
}

function handleCatch(side, it, hudScore, hudCombo, now) {
  if (it.type === 'bomb') {
    side.score = Math.max(0, side.score - 20)
    side.combo = 0
    hud[hudScore] = side.score
    hud[hudCombo] = 0
    pushPopup(side, it.x, BASKET_Y - 20, '-20', '#ff5d6c')
    emitParticles(it.x, BASKET_Y, '#ff5d6c', 16)
    return
  }
  if (it.type === 'magnet') {
    side.magnetUntil = now + 5000
    pushPopup(side, it.x, BASKET_Y - 20, '磁鐵!', '#a78bfa')
    emitParticles(it.x, BASKET_Y, '#a78bfa', 14)
    return
  }
  if (it.type === 'freeze') {
    side.freezeUntil = now + 5000
    pushPopup(side, it.x, BASKET_Y - 20, '冰凍!', '#4dd0ff')
    emitParticles(it.x, BASKET_Y, '#4dd0ff', 14)
    return
  }
  // fruit or golden
  side.combo += 1
  const base = it.type === 'golden' ? 30 : 10
  const gain = base * mult(side.combo)
  side.score += gain
  hud[hudScore] = side.score
  hud[hudCombo] = side.combo
  pushPopup(side, it.x, BASKET_Y - 20, `+${gain}`, it.type === 'golden' ? '#ffd23f' : '#7CFFb0')
  emitParticles(it.x, BASKET_Y, it.type === 'golden' ? '#ffd23f' : FRUIT_COLORS[it.fruit], 12)
}

function update(dt, now) {
  game.elapsed += dt
  timeLeft.value = Math.max(0, Math.ceil((GAME_SEC * 1000 - game.elapsed) / 1000))

  updateSide(game.p1, 'a', 'd', dt, now, 'p1Score', 'p1Combo')
  updateSide(game.p2, 'arrowleft', 'arrowright', dt, now, 'p2Score', 'p2Combo')

  for (const p of game.particles) {
    p.x += p.vx * (dt / 1000)
    p.y += p.vy * (dt / 1000)
    p.vy += 400 * (dt / 1000)
    p.life -= dt / 700
  }
  game.particles = game.particles.filter((p) => p.life > 0)

  if (game.elapsed >= GAME_SEC * 1000) {
    finishGame()
  }
}

async function finishGame() {
  cancelAnimationFrame(rafId)
  rafId = 0
  let winner
  if (hud.p1Score > hud.p2Score) winner = '玩家 1 獲勝'
  else if (hud.p2Score > hud.p1Score) winner = '玩家 2 獲勝'
  else winner = '平手'
  resultText.value = `🧺 ${winner}`
  phase.value = 'result'
  recordGameResult(
    '/game08',
    hud.p1Score > hud.p2Score ? 'p1' : hud.p2Score > hud.p1Score ? 'p2' : 'draw'
  )
  try {
    const store = await saveGame08Record({
      winner,
      scoreP1: hud.p1Score,
      scoreP2: hud.p2Score,
      date: new Date().toISOString()
    })
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
  // background
  ctx.imageSmoothingEnabled = false
  const bgImg = g08Sprite('bg-orchard')
  if (g08ready(bgImg)) {
    ctx.drawImage(bgImg, 0, 0, CANVAS_W, CANVAS_H)
  } else {
    ctx.fillStyle = '#0c1530'
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
    const sky = ctx.createLinearGradient(0, 0, 0, CANVAS_H)
    sky.addColorStop(0, '#1a2a52')
    sky.addColorStop(1, '#0c1530')
    ctx.fillStyle = sky
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  }

  // halves tint
  drawSideBg(game.p1, 0, 'rgba(58,255,176,0.04)')
  drawSideBg(game.p2, HALF, 'rgba(255,158,200,0.04)')

  // divider
  ctx.strokeStyle = 'rgba(255,255,255,0.1)'
  ctx.setLineDash([8, 10])
  ctx.lineWidth = 2
  ctx.beginPath()
  ctx.moveTo(HALF, 0)
  ctx.lineTo(HALF, CANVAS_H)
  ctx.stroke()
  ctx.setLineDash([])

  drawItems(game.p1, now)
  drawItems(game.p2, now)
  drawBasket(game.p1, '#3affd0', now)
  drawBasket(game.p2, '#ff9ec8', now)

  // particles
  for (const p of game.particles) {
    ctx.globalAlpha = Math.max(0, p.life)
    ctx.fillStyle = p.color
    ctx.beginPath()
    ctx.arc(p.x, p.y, 3 * p.life + 1, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.globalAlpha = 1

  drawPopups(game.p1)
  drawPopups(game.p2)
}

function drawSideBg(side, baseX, tint) {
  ctx.fillStyle = tint
  ctx.fillRect(baseX, 0, HALF, CANVAS_H)
  const now = performance.now()
  if (now < side.freezeUntil) {
    ctx.fillStyle = 'rgba(77,208,255,0.08)'
    ctx.fillRect(baseX, 0, HALF, CANVAS_H)
  }
  if (now < side.magnetUntil) {
    ctx.fillStyle = 'rgba(142,123,255,0.07)'
    ctx.fillRect(baseX, 0, HALF, CANVAS_H)
  }
}

/* ---- shared item shapes (used by both the canvas game and the sidebar legend) ---- */
function drawFruitShape(c, col, R) {
  c.save()
  c.shadowColor = col
  c.shadowBlur = R * 0.7
  const g = c.createRadialGradient(-R * 0.35, -R * 0.4, R * 0.2, 0, 0, R)
  g.addColorStop(0, 'rgba(255,255,255,0.95)')
  g.addColorStop(0.32, col)
  g.addColorStop(1, darken(col, 0.62))
  c.fillStyle = g
  c.beginPath()
  c.arc(0, 0, R, 0, Math.PI * 2)
  c.fill()
  c.shadowBlur = 0
  c.lineWidth = 1.5
  c.strokeStyle = 'rgba(0,0,0,0.35)'
  c.stroke()
  c.fillStyle = 'rgba(255,255,255,0.6)'
  c.beginPath()
  c.arc(-R * 0.32, -R * 0.4, R * 0.2, 0, Math.PI * 2)
  c.fill()
  c.strokeStyle = '#6b4a2b'
  c.lineWidth = R * 0.14
  c.beginPath()
  c.moveTo(0, -R * 0.78)
  c.lineTo(0, -R * 1.05)
  c.stroke()
  c.fillStyle = '#3fae5a'
  c.beginPath()
  c.ellipse(R * 0.28, -R * 0.95, R * 0.26, R * 0.14, -0.6, 0, Math.PI * 2)
  c.fill()
  c.restore()
}

function starPath(c, outer, inner, points) {
  c.beginPath()
  for (let i = 0; i < points * 2; i += 1) {
    const r = i % 2 === 0 ? outer : inner
    const a = (i / (points * 2)) * Math.PI * 2 - Math.PI / 2
    const x = Math.cos(a) * r
    const y = Math.sin(a) * r
    if (i === 0) c.moveTo(x, y)
    else c.lineTo(x, y)
  }
  c.closePath()
}

function drawGoldenShape(c, R) {
  c.save()
  c.shadowColor = '#ffd23f'
  c.shadowBlur = R
  c.fillStyle = '#ffd23f'
  starPath(c, R, R * 0.45, 5)
  c.fill()
  c.shadowBlur = 0
  c.lineWidth = 1.5
  c.strokeStyle = '#b8860b'
  c.stroke()
  c.fillStyle = 'rgba(255,255,255,0.55)'
  c.beginPath()
  c.arc(-R * 0.18, -R * 0.2, R * 0.16, 0, Math.PI * 2)
  c.fill()
  c.restore()
}

function drawBombShape(c, R) {
  c.save()
  const g = c.createRadialGradient(-R * 0.3, -R * 0.3, R * 0.2, 0, R * 0.1, R)
  g.addColorStop(0, '#5a5f70')
  g.addColorStop(1, '#15171f')
  c.fillStyle = g
  c.beginPath()
  c.arc(0, R * 0.15, R * 0.82, 0, Math.PI * 2)
  c.fill()
  c.lineWidth = 1.5
  c.strokeStyle = 'rgba(0,0,0,0.5)'
  c.stroke()
  c.fillStyle = 'rgba(255,255,255,0.3)'
  c.beginPath()
  c.arc(-R * 0.28, -R * 0.05, R * 0.16, 0, Math.PI * 2)
  c.fill()
  c.fillStyle = '#2a2d38'
  c.fillRect(-R * 0.2, -R * 0.78, R * 0.4, R * 0.26)
  c.strokeStyle = '#c9a06b'
  c.lineWidth = 2
  c.beginPath()
  c.moveTo(0, -R * 0.72)
  c.quadraticCurveTo(R * 0.5, -R * 1.05, R * 0.55, -R * 0.66)
  c.stroke()
  c.fillStyle = '#ff7a3a'
  c.beginPath()
  c.arc(R * 0.55, -R * 0.66, R * 0.16, 0, Math.PI * 2)
  c.fill()
  c.fillStyle = '#ffe27a'
  c.beginPath()
  c.arc(R * 0.55, -R * 0.66, R * 0.08, 0, Math.PI * 2)
  c.fill()
  c.restore()
}

function drawMagnetShape(c, R) {
  c.save()
  c.lineCap = 'butt'
  // red horseshoe (opening downward)
  c.strokeStyle = '#e23b3b'
  c.lineWidth = R * 0.46
  c.beginPath()
  c.arc(0, 0, R * 0.6, Math.PI, 0)
  c.stroke()
  c.beginPath()
  c.moveTo(-R * 0.6, 0)
  c.lineTo(-R * 0.6, R * 0.7)
  c.moveTo(R * 0.6, 0)
  c.lineTo(R * 0.6, R * 0.7)
  c.stroke()
  // grey poles
  c.strokeStyle = '#d3d8e2'
  c.beginPath()
  c.moveTo(-R * 0.6, R * 0.5)
  c.lineTo(-R * 0.6, R * 0.78)
  c.moveTo(R * 0.6, R * 0.5)
  c.lineTo(R * 0.6, R * 0.78)
  c.stroke()
  c.restore()
}

function drawFreezeShape(c, R) {
  c.save()
  c.strokeStyle = '#bfeeff'
  c.lineWidth = R * 0.12
  c.lineCap = 'round'
  c.shadowColor = '#4dd0ff'
  c.shadowBlur = R * 0.7
  for (let i = 0; i < 6; i += 1) {
    c.save()
    c.rotate((i * Math.PI) / 3)
    c.beginPath()
    c.moveTo(0, 0)
    c.lineTo(0, -R)
    c.stroke()
    c.beginPath()
    c.moveTo(0, -R * 0.58)
    c.lineTo(R * 0.24, -R * 0.78)
    c.moveTo(0, -R * 0.58)
    c.lineTo(-R * 0.24, -R * 0.78)
    c.stroke()
    c.restore()
  }
  c.shadowBlur = 0
  c.fillStyle = '#e6fbff'
  c.beginPath()
  c.arc(0, 0, R * 0.16, 0, Math.PI * 2)
  c.fill()
  c.restore()
}

function drawItemShape(c, type, fruit, R) {
  if (type === 'bomb') {
    const img = g08Sprite('bomb')
    if (g08ready(img)) { c.imageSmoothingEnabled = false; c.drawImage(img, -R * 1.2, -R * 1.2, R * 2.4, R * 2.4); return }
    drawBombShape(c, R)
  } else if (type === 'golden') drawGoldenShape(c, R)
  else if (type === 'magnet') drawMagnetShape(c, R)
  else if (type === 'freeze') drawFreezeShape(c, R)
  else {
    const img = g08Sprite(FRUIT_SPRITE[fruit])
    if (g08ready(img)) { c.imageSmoothingEnabled = false; c.drawImage(img, -R * 1.2, -R * 1.2, R * 2.4, R * 2.4); return }
    drawFruitShape(c, FRUIT_COLORS[fruit] || '#ff4d4d', R)
  }
}

function drawItems(side, now) {
  for (const it of side.items) {
    ctx.save()
    ctx.translate(it.x, it.y)
    if (it.type === 'golden' || it.type === 'freeze' || it.type === 'fruit') ctx.rotate(it.rot)
    drawItemShape(ctx, it.type, it.fruit, 17)
    ctx.restore()
  }
  void now
}

function drawBasket(side, color, now) {
  const x = side.x
  const y = BASKET_Y
  const w = BASKET_W
  const glow = now < side.magnetUntil ? '#a78bfa' : now < side.freezeUntil ? '#4dd0ff' : color
  const img = g08Sprite('basket-cat')
  if (g08ready(img)) {
    const bw = w + 24
    const bh = bw * (img.naturalHeight / img.naturalWidth)
    if (now < side.magnetUntil || now < side.freezeUntil) {
      ctx.save()
      ctx.shadowColor = glow
      ctx.shadowBlur = 18
    }
    ctx.imageSmoothingEnabled = false
    ctx.drawImage(img, x - bw / 2, y + 24 - bh, bw, bh)
    if (now < side.magnetUntil || now < side.freezeUntil) ctx.restore()
    return
  }
  ctx.save()
  ctx.shadowColor = glow
  ctx.shadowBlur = 16
  ctx.fillStyle = color
  ctx.beginPath()
  ctx.moveTo(x - w / 2, y - 14)
  ctx.lineTo(x + w / 2, y - 14)
  ctx.lineTo(x + w / 2 - 12, y + 22)
  ctx.lineTo(x - w / 2 + 12, y + 22)
  ctx.closePath()
  ctx.fill()
  ctx.restore()
  ctx.fillStyle = 'rgba(255,255,255,0.85)'
  ctx.fillRect(x - w / 2, y - 18, w, 6)
}

function drawPopups(side) {
  for (const p of side.popups) {
    ctx.globalAlpha = Math.max(0, p.life)
    ctx.fillStyle = p.color
    ctx.font = 'bold 22px system-ui, sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText(p.text, p.x, p.y)
  }
  ctx.globalAlpha = 1
}

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  keys.add(k)
  if (k.startsWith('arrow')) e.preventDefault()
  if ((k === ' ' || k === 'enter') && phase.value !== 'playing') startGame()
}
function onKeyUp(e) {
  keys.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame08Records()
  records.value = store.records
}

function formatDate(iso) {
  const d = new Date(iso)
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(
    d.getMinutes()
  ).padStart(2, '0')}`
}

function idleRender() {
  game = createGame()
  render(performance.now())
}

onMounted(async () => {
  ctx = canvasRef.value.getContext('2d')
  renderLegend()
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('keyup', onKeyUp)
  try {
    const store = await fetchGame08Store()
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
.game08-view {
  min-height: 100vh;
  padding: 24px clamp(16px, 4vw, 48px) 48px;
  color: #e9f0ff;
  background: radial-gradient(circle at 50% -10%, #1d2c54, #080d1d 60%);
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
  color: #9fb6e8;
  text-decoration: none;
  font-size: 14px;
  padding: 8px 14px;
  border: 1px solid rgba(159, 182, 232, 0.3);
  border-radius: 999px;
  transition: 0.2s;
}
.back-link:hover {
  background: rgba(159, 182, 232, 0.12);
  color: #fff;
}
.title-block .eyebrow {
  margin: 0;
  font-size: 12px;
  letter-spacing: 3px;
  color: #3affd0;
  text-transform: uppercase;
}
.title-block h1 {
  margin: 2px 0 0;
  font-size: 26px;
  background: linear-gradient(90deg, #3affd0, #ffd23f);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.time-pill {
  margin-left: auto;
  padding: 8px 18px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.06);
  border: 1px solid rgba(255, 255, 255, 0.12);
  font-size: 16px;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}
.time-pill.urgent {
  background: rgba(255, 93, 108, 0.2);
  border-color: rgba(255, 93, 108, 0.5);
  color: #ff9eaa;
}
.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 300px;
  gap: 22px;
  align-items: start;
}
.stage-card {
  background: rgba(8, 13, 29, 0.6);
  border: 1px solid rgba(120, 150, 220, 0.18);
  border-radius: 20px;
  padding: 16px;
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.5);
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
  gap: 12px;
  flex: 1;
}
.team-2 {
  justify-content: flex-end;
}
.team strong {
  font-size: 15px;
}
.team .score {
  font-size: 24px;
  font-weight: 800;
  font-variant-numeric: tabular-nums;
}
.team-1 .score {
  color: #3affd0;
}
.team-2 .score {
  color: #ff9ec8;
}
.team .combo {
  font-size: 14px;
  color: #8493bc;
  font-weight: 700;
}
.team .combo.hot {
  color: #ffd23f;
}
.vs {
  font-size: 13px;
  font-weight: 800;
  color: #5d6a96;
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
  background: rgba(5, 9, 20, 0.82);
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
  color: #3affd0;
}
.overlay-card h2 {
  margin: 10px 0 14px;
  font-size: 28px;
}
.winner-text {
  background: linear-gradient(90deg, #3affd0, #ffd23f);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.overlay-text {
  color: #b6c4e8;
  line-height: 1.7;
  margin: 0 0 22px;
}
.primary-btn {
  border: none;
  padding: 12px 30px;
  border-radius: 999px;
  font-size: 16px;
  font-weight: 700;
  color: #052018;
  background: linear-gradient(90deg, #3affd0, #ffd23f);
  cursor: pointer;
  transition: transform 0.15s, box-shadow 0.15s;
  box-shadow: 0 10px 26px rgba(58, 255, 208, 0.35);
}
.primary-btn:hover {
  transform: translateY(-2px);
  box-shadow: 0 14px 32px rgba(58, 255, 208, 0.5);
}
.sidebar {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.panel {
  background: rgba(8, 13, 29, 0.6);
  border: 1px solid rgba(120, 150, 220, 0.18);
  border-radius: 18px;
  padding: 18px;
}
.panel .eyebrow {
  margin: 0 0 12px;
  font-size: 11px;
  letter-spacing: 2px;
  text-transform: uppercase;
  color: #7585b0;
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
  margin-bottom: 6px;
}
.ctrl span {
  font-size: 13px;
  color: #b6c4e8;
}
.ctrl-1 {
  background: rgba(58, 255, 208, 0.1);
  border: 1px solid rgba(58, 255, 208, 0.25);
}
.ctrl-2 {
  background: rgba(255, 158, 200, 0.1);
  border: 1px solid rgba(255, 158, 200, 0.25);
}
kbd {
  background: rgba(255, 255, 255, 0.12);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 6px;
  padding: 2px 7px;
  font-size: 12px;
  font-family: inherit;
}
.legend {
  list-style: none;
  margin: 0 0 10px;
  padding: 0;
  display: grid;
  gap: 9px;
}
.legend li {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 13px;
  color: #c2cef0;
}
.ic-canvas {
  width: 30px;
  height: 30px;
  flex-shrink: 0;
}
.hint {
  font-size: 12px;
  color: #7585b0;
  margin: 0;
  line-height: 1.6;
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
  color: #ffd23f;
}
.rec-score {
  color: #9fb6e8;
}
.rec-date {
  color: #5d6a96;
}
.empty {
  font-size: 13px;
  color: #7585b0;
  line-height: 1.6;
  margin: 0;
}
.ghost-btn {
  background: none;
  border: 1px solid rgba(159, 182, 232, 0.3);
  color: #9fb6e8;
  border-radius: 999px;
  padding: 4px 12px;
  font-size: 12px;
  cursor: pointer;
}
.ghost-btn:hover {
  background: rgba(159, 182, 232, 0.12);
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
