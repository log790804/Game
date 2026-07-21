<template>
  <main class="game07-view">
    <section class="topbar">
      <RouterLink
        to="/"
        class="back-link"
      >
        ← 返回遊戲廳
      </RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 07</p>
        <h1>空氣曲棍球</h1>
      </div>
      <div class="round-pill">先進 {{ TARGET }} 球者勝</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-2">
            <span class="dot" />
            <strong>玩家 2（上）</strong>
            <span class="score">{{ score.p2 }}</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-1">
            <span class="score">{{ score.p1 }}</span>
            <strong>玩家 1（下）</strong>
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
                  <p class="overlay-eyebrow">光速對抗</p>
                  <h2>把圓盤打進對方球門</h2>
                  <p class="overlay-text">
                    操控球桿撞擊圓盤，速度越快威力越強。<br>
                    圓盤每次回合都會再加速，先進 {{ TARGET }} 球者獲勝。
                  </p>
                  <button
                    class="primary-btn"
                    @click="startMatch"
                  >
                    開始對戰
                  </button>
                </template>
                <template v-else-if="phase === 'matchover'">
                  <p class="overlay-eyebrow">對戰結束</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <p class="overlay-text">最終比數 {{ score.p1 }} : {{ score.p2 }}</p>
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
            <div class="ctrl ctrl-2">
              <strong>玩家 2（上半場）</strong>
              <span><kbd>↑</kbd><kbd>↓</kbd><kbd>←</kbd><kbd>→</kbd> 移動球桿</span>
            </div>
            <div class="ctrl ctrl-1">
              <strong>玩家 1（下半場）</strong>
              <span><kbd>W</kbd><kbd>A</kbd><kbd>S</kbd><kbd>D</kbd> 移動球桿</span>
            </div>
          </div>
        </section>

        <section class="panel">
          <p class="eyebrow">技巧</p>
          <ul class="tips">
            <li>球桿移動越快，擊出的圓盤速度越強。</li>
            <li>斜向接球可以製造刁鑽角度。</li>
            <li>每進一球，圓盤基礎速度都會提升。</li>
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
            尚無紀錄，對戰結束後自動保存最近 10 場。
          </p>
        </section>
      </aside>
    </section>
  </main>
</template>

<script setup>
import { onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import {
  clearGame07Records,
  fetchGame07Store,
  saveGame07Record
} from './game07Storage'
import { recordGameResult } from '@/data/lobbyScore'
import { assetUrl } from '@/utils/assetUrl'

const CANVAS_W = 720
const CANVAS_H = 680
const TARGET = 7
const WALL = 14
const GOAL_W = 230
const PADDLE_R = 34
const PUCK_R = 19
const PADDLE_SPEED = 0.9
const PADDLE_FRICTION = 0.82

// 像素素材
const G07 = {}
function g07Sprite(name) {
  if (!G07[name]) {
    const img = new Image()
    img.src = assetUrl(`/assets/G07/${name}.png`)
    G07[name] = img
  }
  return G07[name]
}
;['bg-table', 'paddle-p1', 'paddle-p2', 'puck', 'txt-goal', 'fx-goal-1', 'fx-goal-2', 'fx-goal-3', 'fx-goal-4'].forEach(g07Sprite)
function g07ready(img) {
  return img && img.complete && img.naturalWidth > 0
}
const PUCK_FRICTION = 0.9975
const PUCK_MAX = 16
const BASE_PUCK_SPEED = 6

const canvasRef = ref(null)
const stageRef = ref(null)

const phase = ref('intro')
const score = reactive({ p1: 0, p2: 0 })
const resultText = ref('')
const records = ref([])

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null
const keys = new Set()

function makePaddle(x, y) {
  return { x, y, px: x, py: y, vx: 0, vy: 0 }
}

function createGame() {
  return {
    puck: { x: CANVAS_W / 2, y: CANVAS_H / 2, vx: 0, vy: 0 },
    p1: makePaddle(CANVAS_W / 2, CANVAS_H - 90),
    p2: makePaddle(CANVAS_W / 2, 90),
    serveTimer: 1200,
    serveTo: Math.random() < 0.5 ? 1 : -1,
    speedBoost: 0,
    trail: [],
    flash: 0,
    flashColor: '#ffffff',
    goalShake: 0
  }
}

function startMatch() {
  score.p1 = 0
  score.p2 = 0
  game = createGame()
  phase.value = 'playing'
  lastFrame = performance.now()
  loop(lastFrame)
}

function resetPuck(direction) {
  game.puck.x = CANVAS_W / 2
  game.puck.y = CANVAS_H / 2
  game.puck.vx = 0
  game.puck.vy = 0
  game.serveTimer = 900
  game.serveTo = direction
  game.trail = []
}

function clampPaddle(p, isBottom) {
  const minX = WALL + PADDLE_R
  const maxX = CANVAS_W - WALL - PADDLE_R
  p.x = Math.max(minX, Math.min(maxX, p.x))
  if (isBottom) {
    const minY = CANVAS_H / 2 + PADDLE_R * 0.2
    const maxY = CANVAS_H - WALL - PADDLE_R
    p.y = Math.max(minY, Math.min(maxY, p.y))
  } else {
    const minY = WALL + PADDLE_R
    const maxY = CANVAS_H / 2 - PADDLE_R * 0.2
    p.y = Math.max(minY, Math.min(maxY, p.y))
  }
}

function controlPaddle(p, up, down, left, right, dt, isBottom) {
  let ax = 0
  let ay = 0
  if (keys.has(left)) ax -= 1
  if (keys.has(right)) ax += 1
  if (keys.has(up)) ay -= 1
  if (keys.has(down)) ay += 1
  p.vx += ax * PADDLE_SPEED * dt
  p.vy += ay * PADDLE_SPEED * dt
  p.vx *= PADDLE_FRICTION
  p.vy *= PADDLE_FRICTION
  p.px = p.x
  p.py = p.y
  p.x += p.vx
  p.y += p.vy
  clampPaddle(p, isBottom)
}

function collidePaddle(puck, p) {
  const dx = puck.x - p.x
  const dy = puck.y - p.y
  const dist = Math.hypot(dx, dy)
  const min = PADDLE_R + PUCK_R
  if (dist === 0 || dist >= min) return
  const nx = dx / dist
  const ny = dy / dist
  // push out
  puck.x = p.x + nx * min
  puck.y = p.y + ny * min
  // paddle velocity
  const pvx = p.x - p.px
  const pvy = p.y - p.py
  // reflect puck velocity along normal
  const vdot = puck.vx * nx + puck.vy * ny
  puck.vx = puck.vx - 2 * vdot * nx
  puck.vy = puck.vy - 2 * vdot * ny
  // transfer paddle momentum + base kick
  puck.vx += pvx * 0.9 + nx * 2.2
  puck.vy += pvy * 0.9 + ny * 2.2
  capPuck(puck)
}

function capPuck(puck) {
  const sp = Math.hypot(puck.vx, puck.vy)
  const max = PUCK_MAX + game.speedBoost
  if (sp > max) {
    puck.vx = (puck.vx / sp) * max
    puck.vy = (puck.vy / sp) * max
  }
}

function update(dt) {
  const f = dt / 16.67
  controlPaddle(game.p1, 'w', 's', 'a', 'd', f, true)
  controlPaddle(game.p2, 'arrowup', 'arrowdown', 'arrowleft', 'arrowright', f, false)

  const puck = game.puck

  if (game.serveTimer > 0) {
    game.serveTimer -= dt
    if (game.serveTimer <= 0) {
      const angle =
        game.serveTo === 1
          ? Math.PI / 2 + (Math.random() - 0.5) * 0.8
          : -Math.PI / 2 + (Math.random() - 0.5) * 0.8
      const sp = BASE_PUCK_SPEED + game.speedBoost * 0.5
      puck.vx = Math.cos(angle) * sp
      puck.vy = Math.sin(angle) * sp
    }
  } else {
    puck.x += puck.vx * f
    puck.y += puck.vy * f
    puck.vx *= PUCK_FRICTION
    puck.vy *= PUCK_FRICTION

    // side walls
    if (puck.x < WALL + PUCK_R) {
      puck.x = WALL + PUCK_R
      puck.vx = Math.abs(puck.vx)
    } else if (puck.x > CANVAS_W - WALL - PUCK_R) {
      puck.x = CANVAS_W - WALL - PUCK_R
      puck.vx = -Math.abs(puck.vx)
    }

    const goalMinX = (CANVAS_W - GOAL_W) / 2
    const goalMaxX = (CANVAS_W + GOAL_W) / 2

    // top edge / goal
    if (puck.y < WALL + PUCK_R) {
      if (puck.x > goalMinX && puck.x < goalMaxX) {
        goal('p1')
        return
      }
      puck.y = WALL + PUCK_R
      puck.vy = Math.abs(puck.vy)
    }
    // bottom edge / goal
    if (puck.y > CANVAS_H - WALL - PUCK_R) {
      if (puck.x > goalMinX && puck.x < goalMaxX) {
        goal('p2')
        return
      }
      puck.y = CANVAS_H - WALL - PUCK_R
      puck.vy = -Math.abs(puck.vy)
    }

    collidePaddle(puck, game.p1)
    collidePaddle(puck, game.p2)

    game.trail.push({ x: puck.x, y: puck.y })
    if (game.trail.length > 14) game.trail.shift()
  }

  game.flash = Math.max(0, game.flash - dt / 400)
  game.goalShake = Math.max(0, game.goalShake - dt / 300)
}

function goal(scorer) {
  score[scorer] += 1
  game.speedBoost = Math.min(8, game.speedBoost + 0.7)
  game.flash = 1
  game.flashColor = scorer === 'p1' ? '#3affd0' : '#ff9ec8'
  game.goalShake = 1
  if (score[scorer] >= TARGET) {
    finishMatch()
    return
  }
  // serve toward the conceding side
  resetPuck(scorer === 'p1' ? -1 : 1)
}

async function finishMatch() {
  cancelAnimationFrame(rafId)
  rafId = 0
  const winner = score.p1 > score.p2 ? '玩家 1 獲勝' : '玩家 2 獲勝'
  resultText.value = `🏒 ${winner}`
  phase.value = 'matchover'
  recordGameResult('/game07', score.p1 > score.p2 ? 'p1' : score.p2 > score.p1 ? 'p2' : 'draw')
  try {
    const store = await saveGame07Record({
      winner,
      scoreP1: score.p1,
      scoreP2: score.p2,
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
    update(dt)
    render()
    rafId = requestAnimationFrame(loop)
  }
}

function render() {
  const shakeX = game.goalShake ? (Math.random() - 0.5) * 10 * game.goalShake : 0
  const shakeY = game.goalShake ? (Math.random() - 0.5) * 10 * game.goalShake : 0
  ctx.save()
  ctx.translate(shakeX, shakeY)

  // 球桌背景
  ctx.imageSmoothingEnabled = false
  const tableImg = g07Sprite('bg-table')
  const goalMinX = (CANVAS_W - GOAL_W) / 2
  if (g07ready(tableImg)) {
    ctx.drawImage(tableImg, 0, 0, CANVAS_W, CANVAS_H)
    // 球門口標示（功能對位用，淡淡一條）
    ctx.strokeStyle = 'rgba(255,158,200,0.7)'
    ctx.lineWidth = 4
    ctx.beginPath()
    ctx.moveTo(goalMinX, WALL)
    ctx.lineTo(goalMinX + GOAL_W, WALL)
    ctx.stroke()
    ctx.strokeStyle = 'rgba(58,255,208,0.7)'
    ctx.beginPath()
    ctx.moveTo(goalMinX, CANVAS_H - WALL)
    ctx.lineTo(goalMinX + GOAL_W, CANVAS_H - WALL)
    ctx.stroke()
  } else {
    ctx.fillStyle = '#0d2438'
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
    const rink = ctx.createLinearGradient(0, 0, 0, CANVAS_H)
    rink.addColorStop(0, '#123a52')
    rink.addColorStop(0.5, '#0e2c42')
    rink.addColorStop(1, '#123a52')
    ctx.fillStyle = rink
    ctx.fillRect(WALL, WALL, CANVAS_W - WALL * 2, CANVAS_H - WALL * 2)
    ctx.strokeStyle = 'rgba(120,210,255,0.4)'
    ctx.lineWidth = 3
    ctx.beginPath()
    ctx.moveTo(WALL, CANVAS_H / 2)
    ctx.lineTo(CANVAS_W - WALL, CANVAS_H / 2)
    ctx.stroke()
    ctx.beginPath()
    ctx.arc(CANVAS_W / 2, CANVAS_H / 2, 80, 0, Math.PI * 2)
    ctx.stroke()
    ctx.fillStyle = 'rgba(255,158,200,0.25)'
    ctx.fillRect(goalMinX, 0, GOAL_W, WALL)
    ctx.fillStyle = 'rgba(58,255,208,0.25)'
    ctx.fillRect(goalMinX, CANVAS_H - WALL, GOAL_W, WALL)
    ctx.strokeStyle = 'rgba(120,210,255,0.5)'
    ctx.lineWidth = 4
    ctx.strokeRect(WALL, WALL, CANVAS_W - WALL * 2, CANVAS_H - WALL * 2)
  }

  // puck trail
  for (let i = 0; i < game.trail.length; i += 1) {
    const t = game.trail[i]
    ctx.globalAlpha = (i / game.trail.length) * 0.4
    ctx.fillStyle = '#ffe66d'
    ctx.beginPath()
    ctx.arc(t.x, t.y, PUCK_R * (i / game.trail.length), 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.globalAlpha = 1

  drawPaddle(game.p2, 'paddle-p2', '#ff9ec8', '#ff6fa8')
  drawPaddle(game.p1, 'paddle-p1', '#3affd0', '#13d9aa')

  // puck
  const puckImg = g07Sprite('puck')
  if (g07ready(puckImg)) {
    const sz = PUCK_R * 2.3
    ctx.drawImage(puckImg, game.puck.x - sz / 2, game.puck.y - sz / 2, sz, sz)
  } else {
    ctx.save()
    ctx.shadowColor = '#ffe66d'
    ctx.shadowBlur = 18
    const pg = ctx.createRadialGradient(game.puck.x - 4, game.puck.y - 4, 2, game.puck.x, game.puck.y, PUCK_R)
    pg.addColorStop(0, '#fff7c2')
    pg.addColorStop(1, '#ffd23f')
    ctx.fillStyle = pg
    ctx.beginPath()
    ctx.arc(game.puck.x, game.puck.y, PUCK_R, 0, Math.PI * 2)
    ctx.fill()
    ctx.restore()
  }

  // 進球特效 + GOAL 文字
  if (game.flash > 0) {
    const frame = `fx-goal-${Math.min(4, Math.max(1, 5 - Math.ceil(game.flash * 4)))}`
    const fx = g07Sprite(frame)
    if (g07ready(fx)) {
      const fs = 180
      ctx.globalAlpha = Math.min(1, game.flash * 1.4)
      ctx.drawImage(fx, CANVAS_W / 2 - fs / 2, CANVAS_H / 2 - fs / 2, fs, fs)
      ctx.globalAlpha = 1
    }
    const goalTxt = g07Sprite('txt-goal')
    if (g07ready(goalTxt)) {
      const gw = 220
      const gh = gw * (goalTxt.naturalHeight / goalTxt.naturalWidth)
      ctx.globalAlpha = Math.min(1, game.flash * 1.6)
      ctx.drawImage(goalTxt, CANVAS_W / 2 - gw / 2, CANVAS_H / 2 - gh / 2, gw, gh)
      ctx.globalAlpha = 1
    }
  }

  if (game.serveTimer > 0) {
    ctx.fillStyle = 'rgba(255,255,255,0.85)'
    ctx.font = 'bold 40px system-ui, sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText(Math.ceil(game.serveTimer / 1000).toString(), CANVAS_W / 2, CANVAS_H / 2 - 100)
  }

  ctx.restore()

  if (game.flash > 0) {
    ctx.fillStyle = `rgba(${hexToRgb(game.flashColor)},${game.flash * 0.3})`
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  }
}

function drawPaddle(p, spriteName, light, dark) {
  const img = g07Sprite(spriteName)
  if (g07ready(img)) {
    const sz = PADDLE_R * 2.4
    ctx.drawImage(img, p.x - sz / 2, p.y - sz / 2, sz, sz)
    return
  }
  ctx.save()
  ctx.shadowColor = light
  ctx.shadowBlur = 16
  const g = ctx.createRadialGradient(p.x - 6, p.y - 6, 4, p.x, p.y, PADDLE_R)
  g.addColorStop(0, light)
  g.addColorStop(1, dark)
  ctx.fillStyle = g
  ctx.beginPath()
  ctx.arc(p.x, p.y, PADDLE_R, 0, Math.PI * 2)
  ctx.fill()
  ctx.restore()
  ctx.fillStyle = 'rgba(10,20,30,0.85)'
  ctx.beginPath()
  ctx.arc(p.x, p.y, PADDLE_R * 0.5, 0, Math.PI * 2)
  ctx.fill()
}

function hexToRgb(hex) {
  const v = hex.replace('#', '')
  return `${parseInt(v.slice(0, 2), 16)},${parseInt(v.slice(2, 4), 16)},${parseInt(
    v.slice(4, 6),
    16
  )}`
}

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  keys.add(k)
  if (k.startsWith('arrow')) e.preventDefault()
  if ((k === ' ' || k === 'enter') && phase.value !== 'playing') startMatch()
}
function onKeyUp(e) {
  keys.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame07Records()
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
  game.serveTimer = 0
  render()
}

onMounted(async () => {
  ctx = canvasRef.value.getContext('2d')
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('keyup', onKeyUp)
  try {
    const store = await fetchGame07Store()
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
.game07-view {
  min-height: 100vh;
  padding: 24px clamp(16px, 4vw, 48px) 48px;
  color: #e6f4ff;
  background: radial-gradient(circle at 50% -10%, #103048, #060f18 60%);
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
  color: #8fc6e8;
  text-decoration: none;
  font-size: 14px;
  padding: 8px 14px;
  border: 1px solid rgba(143, 198, 232, 0.3);
  border-radius: 999px;
  transition: 0.2s;
}
.back-link:hover {
  background: rgba(143, 198, 232, 0.12);
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
  background: linear-gradient(90deg, #3affd0, #ff9ec8);
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
  background: rgba(8, 18, 28, 0.6);
  border: 1px solid rgba(120, 210, 255, 0.18);
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
.team-1 {
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
  font-size: 14px;
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
.vs {
  font-size: 13px;
  font-weight: 800;
  color: #5b7a90;
}
.stage-frame {
  position: relative;
  border-radius: 14px;
  overflow: hidden;
  max-width: 540px;
  margin: 0 auto;
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
  background: rgba(4, 10, 18, 0.82);
  backdrop-filter: blur(4px);
}
.overlay-card {
  text-align: center;
  max-width: 420px;
  padding: 28px;
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
  background: linear-gradient(90deg, #3affd0, #ff9ec8);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.overlay-text {
  color: #b0cee0;
  line-height: 1.7;
  margin: 0 0 22px;
}
.primary-btn {
  border: none;
  padding: 12px 30px;
  border-radius: 999px;
  font-size: 16px;
  font-weight: 700;
  color: #042018;
  background: linear-gradient(90deg, #3affd0, #46d0ff);
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
  background: rgba(8, 18, 28, 0.6);
  border: 1px solid rgba(120, 210, 255, 0.18);
  border-radius: 18px;
  padding: 18px;
}
.panel .eyebrow {
  margin: 0 0 12px;
  font-size: 11px;
  letter-spacing: 2px;
  text-transform: uppercase;
  color: #6f95ac;
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
  margin-bottom: 8px;
}
.ctrl span {
  font-size: 13px;
  color: #b0cee0;
  display: inline-flex;
  gap: 5px;
  align-items: center;
  flex-wrap: wrap;
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
.tips {
  margin: 0;
  padding-left: 18px;
  display: grid;
  gap: 8px;
  font-size: 13px;
  color: #b0cee0;
  line-height: 1.5;
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
  color: #8fc6e8;
}
.rec-date {
  color: #5b7a90;
}
.empty {
  font-size: 13px;
  color: #6f95ac;
  line-height: 1.6;
  margin: 0;
}
.ghost-btn {
  background: none;
  border: 1px solid rgba(143, 198, 232, 0.3);
  color: #8fc6e8;
  border-radius: 999px;
  padding: 4px 12px;
  font-size: 12px;
  cursor: pointer;
}
.ghost-btn:hover {
  background: rgba(143, 198, 232, 0.12);
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
