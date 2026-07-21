<template>
  <main class="game09-view">
    <section class="topbar">
      <RouterLink
        to="/"
        class="back-link"
      >
        ← 返回遊戲廳
      </RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 09</p>
        <h1>打地鼠對戰</h1>
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
            <span class="combo" :class="{ hot: hud.p1Combo >= 4 }">{{ hud.p1Combo }} 連</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="combo" :class="{ hot: hud.p2Combo >= 4 }">{{ hud.p2Combo }} 連</span>
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
                  <p class="overlay-eyebrow">眼明手快</p>
                  <h2>地鼠冒頭就敲下去</h2>
                  <p class="overlay-text">
                    各自半場 3×3 的洞，地鼠探頭時按下對應按鍵擊中得分。<br>
                    敲到金鼠加倍，敲到炸彈鼠會扣分並中斷連擊。
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
          <p class="hint">每個洞口對應一個按鍵（畫面上會標示）。</p>
          <div class="controls-grid">
            <div class="ctrl ctrl-1">
              <strong>玩家 1（左）</strong>
              <span class="keypad">
                <kbd>Q</kbd><kbd>W</kbd><kbd>E</kbd>
                <kbd>A</kbd><kbd>S</kbd><kbd>D</kbd>
                <kbd>Z</kbd><kbd>X</kbd><kbd>C</kbd>
              </span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2（右）</strong>
              <span class="keypad">
                <kbd>U</kbd><kbd>I</kbd><kbd>O</kbd>
                <kbd>J</kbd><kbd>K</kbd><kbd>L</kbd>
                <kbd>M</kbd><kbd>,</kbd><kbd>.</kbd>
              </span>
            </div>
          </div>
        </section>

        <section class="panel">
          <p class="eyebrow">地鼠種類</p>
          <ul class="legend">
            <li><span class="ic">🐹</span> 一般鼠 +10</li>
            <li><span class="ic">👑</span> 黃金鼠 +30</li>
            <li><span class="ic">💣</span> 炸彈鼠 −20、連擊歸零</li>
          </ul>
          <p class="hint">連擊每 4 連提升 1 倍，最高 5 倍。</p>
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
  clearGame09Records,
  fetchGame09Store,
  saveGame09Record
} from './game09Storage'
import { recordGameResult } from '@/data/lobbyScore'
import { assetUrl } from '@/utils/assetUrl'

const CANVAS_W = 960
const CANVAS_H = 620
const HALF = CANVAS_W / 2
const GAME_SEC = 45
const GRID = 3

const P1_KEYS = ['q', 'w', 'e', 'a', 's', 'd', 'z', 'x', 'c']
const P2_KEYS = ['u', 'i', 'o', 'j', 'k', 'l', 'm', ',', '.']
const KEY_LABELS_1 = ['Q', 'W', 'E', 'A', 'S', 'D', 'Z', 'X', 'C']
const KEY_LABELS_2 = ['U', 'I', 'O', 'J', 'K', 'L', 'M', ',', '.']

// 像素素材
const G09 = {}
function g09Sprite(name) {
  if (!G09[name]) {
    const img = new Image()
    img.src = assetUrl(`/assets/G09/${name}.png`)
    G09[name] = img
  }
  return G09[name]
}
;['bg-farm', 'hole', 'mole', 'mole-hit', 'hammer', 'ui-star', 'fx-bonk-1', 'fx-bonk-2', 'fx-bonk-3'].forEach(g09Sprite)
function g09ready(img) {
  return img && img.complete && img.naturalWidth > 0
}

const canvasRef = ref(null)
const stageRef = ref(null)

const phase = ref('intro')
const timeLeft = ref(GAME_SEC)
const resultText = ref('')
const records = ref([])
const hud = reactive({ p1Score: 0, p2Score: 0, p1Combo: 0, p2Combo: 0 })

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null

function holeCenters(half) {
  const baseX = half === 0 ? 0 : HALF
  const padX = 70
  const padTop = 70
  const padBottom = 40
  const usableW = HALF - padX * 2
  const usableH = CANVAS_H - padTop - padBottom
  const centers = []
  for (let r = 0; r < GRID; r += 1) {
    for (let c = 0; c < GRID; c += 1) {
      centers.push({
        x: baseX + padX + (usableW / (GRID - 1)) * c,
        y: padTop + (usableH / (GRID - 1)) * r
      })
    }
  }
  return centers
}

function makeSide(half) {
  return {
    half,
    score: 0,
    combo: 0,
    centers: holeCenters(half),
    holes: Array.from({ length: 9 }, () => ({ mole: null })),
    spawnTimer: 500,
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
  return Math.min(5, 1 + Math.floor(combo / 4))
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

function spawnMole(side) {
  const empty = []
  side.holes.forEach((h, i) => {
    if (!h.mole) empty.push(i)
  })
  if (!empty.length) return
  const idx = empty[Math.floor(Math.random() * empty.length)]
  const roll = Math.random()
  let type = 'normal'
  if (roll < 0.12) type = 'bomb'
  else if (roll < 0.24) type = 'golden'
  const progress = game.elapsed / (GAME_SEC * 1000)
  const life = 1150 - progress * 480 + Math.random() * 250
  side.holes[idx].mole = {
    type,
    life,
    maxLife: life,
    rise: 0,
    state: 'up'
  }
}

function pushPopup(side, c, text, color) {
  side.popups.push({ x: c.x, y: c.y - 30, text, color, life: 1 })
}

function emitParticles(c, color, count) {
  for (let i = 0; i < count; i += 1) {
    const a = Math.random() * Math.PI * 2
    const sp = 50 + Math.random() * 150
    game.particles.push({
      x: c.x,
      y: c.y,
      vx: Math.cos(a) * sp,
      vy: Math.sin(a) * sp - 40,
      life: 1,
      color
    })
  }
}

function hit(side, idx, hudScore, hudCombo) {
  const hole = side.holes[idx]
  if (!hole.mole || hole.mole.state === 'down') return
  const mole = hole.mole
  const c = side.centers[idx]
  if (mole.type === 'bomb') {
    side.score = Math.max(0, side.score - 20)
    side.combo = 0
    hud[hudScore] = side.score
    hud[hudCombo] = 0
    pushPopup(side, c, '-20', '#ff5d6c')
    emitParticles(c, '#ff5d6c', 18)
  } else {
    side.combo += 1
    const base = mole.type === 'golden' ? 30 : 10
    const gain = base * mult(side.combo)
    side.score += gain
    hud[hudScore] = side.score
    hud[hudCombo] = side.combo
    pushPopup(side, c, `+${gain}`, mole.type === 'golden' ? '#ffd23f' : '#7CFFb0')
    emitParticles(c, mole.type === 'golden' ? '#ffd23f' : '#caa472', 12)
  }
  hole.mole.state = 'down'
  hole.mole.life = 160
}

function updateSide(side, dt) {
  side.spawnTimer -= dt
  if (side.spawnTimer <= 0) {
    const progress = game.elapsed / (GAME_SEC * 1000)
    side.spawnTimer = 640 - progress * 300 + Math.random() * 320
    spawnMole(side)
  }
  for (const hole of side.holes) {
    const m = hole.mole
    if (!m) continue
    if (m.state === 'up') {
      m.rise = Math.min(1, m.rise + dt / 130)
      m.life -= dt
      if (m.life <= 0) {
        m.state = 'down'
        m.life = 160
      }
    } else {
      m.rise = Math.max(0, m.rise - dt / 120)
      m.life -= dt
      if (m.life <= 0 || m.rise <= 0) hole.mole = null
    }
  }
  for (const p of side.popups) {
    p.y -= dt / 18
    p.life -= dt / 800
  }
  side.popups = side.popups.filter((p) => p.life > 0)
}

function update(dt) {
  game.elapsed += dt
  timeLeft.value = Math.max(0, Math.ceil((GAME_SEC * 1000 - game.elapsed) / 1000))
  updateSide(game.p1, dt)
  updateSide(game.p2, dt)
  for (const p of game.particles) {
    p.x += p.vx * (dt / 1000)
    p.y += p.vy * (dt / 1000)
    p.vy += 400 * (dt / 1000)
    p.life -= dt / 700
  }
  game.particles = game.particles.filter((p) => p.life > 0)
  if (game.elapsed >= GAME_SEC * 1000) finishGame()
}

async function finishGame() {
  cancelAnimationFrame(rafId)
  rafId = 0
  let winner
  if (hud.p1Score > hud.p2Score) winner = '玩家 1 獲勝'
  else if (hud.p2Score > hud.p1Score) winner = '玩家 2 獲勝'
  else winner = '平手'
  resultText.value = `🔨 ${winner}`
  phase.value = 'result'
  recordGameResult(
    '/game09',
    hud.p1Score > hud.p2Score ? 'p1' : hud.p2Score > hud.p1Score ? 'p2' : 'draw'
  )
  try {
    const store = await saveGame09Record({
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
    update(dt)
    render()
    rafId = requestAnimationFrame(loop)
  }
}

function render() {
  ctx.imageSmoothingEnabled = false
  const bgImg = g09Sprite('bg-farm')
  if (g09ready(bgImg)) {
    ctx.drawImage(bgImg, 0, 0, CANVAS_W, CANVAS_H)
  } else {
    ctx.fillStyle = '#10240f'
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
    drawFieldBg(0, '#1f4d24', '#143618')
    drawFieldBg(HALF, '#1f4d24', '#143618')
  }

  ctx.strokeStyle = 'rgba(255,255,255,0.18)'
  ctx.setLineDash([8, 10])
  ctx.lineWidth = 2
  ctx.beginPath()
  ctx.moveTo(HALF, 0)
  ctx.lineTo(HALF, CANVAS_H)
  ctx.stroke()
  ctx.setLineDash([])

  drawSide(game.p1, KEY_LABELS_1)
  drawSide(game.p2, KEY_LABELS_2)

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

function drawFieldBg(baseX, c1, c2) {
  const g = ctx.createLinearGradient(0, 0, 0, CANVAS_H)
  g.addColorStop(0, c1)
  g.addColorStop(1, c2)
  ctx.fillStyle = g
  ctx.fillRect(baseX, 0, HALF, CANVAS_H)
}

function drawSide(side, labels) {
  const holeImg = g09Sprite('hole')
  side.centers.forEach((c, i) => {
    const hole = side.holes[i]
    // mole（在洞口後緣冒出，先畫）
    if (hole.mole) {
      drawMole(c, hole.mole)
    }

    // hole（畫在地鼠前緣，蓋住下半身製造從洞冒出的效果）
    if (g09ready(holeImg)) {
      const hw = 104
      const hh = hw * (holeImg.naturalHeight / holeImg.naturalWidth)
      ctx.drawImage(holeImg, c.x - hw / 2, c.y + 18 - hh * 0.42, hw, hh)
    } else {
      ctx.save()
      ctx.fillStyle = '#3a2a18'
      ctx.beginPath()
      ctx.ellipse(c.x, c.y + 18, 46, 20, 0, 0, Math.PI * 2)
      ctx.fill()
      ctx.restore()
    }

    // key label
    ctx.fillStyle = 'rgba(255,255,255,0.6)'
    ctx.font = 'bold 14px system-ui, sans-serif'
    ctx.textAlign = 'center'
    ctx.textBaseline = 'middle'
    ctx.fillText(labels[i], c.x, c.y + 50)
  })
}

function drawMole(c, mole) {
  const rise = mole.rise
  const y = c.y + 18 - rise * 50
  ctx.save()
  // clip to hole so mole emerges
  ctx.beginPath()
  ctx.rect(c.x - 48, 0, 96, c.y + 20)
  ctx.clip()

  const hit = mole.state === 'down'
  const sprName = hit ? 'mole-hit' : 'mole'
  const img = g09Sprite(sprName)
  if (mole.type !== 'bomb' && g09ready(img)) {
    if (mole.type === 'golden') {
      ctx.shadowColor = '#ffd23f'
      ctx.shadowBlur = 18
    }
    const sz = 76
    ctx.drawImage(img, c.x - sz / 2, y - sz * 0.55, sz, sz)
    if (mole.type === 'golden') {
      const star = g09Sprite('ui-star')
      if (g09ready(star)) ctx.drawImage(star, c.x - 16, y - sz * 0.55 - 22, 32, 32)
    }
  } else {
    let emoji = mole.type === 'bomb' ? '💣' : '🐹'
    if (mole.type === 'golden') {
      ctx.shadowColor = '#ffd23f'
      ctx.shadowBlur = 18
    }
    ctx.font = '46px serif'
    ctx.textAlign = 'center'
    ctx.textBaseline = 'middle'
    ctx.fillText(emoji, c.x, y)
  }
  ctx.restore()

  // 敲擊火花
  if (hit) {
    const f = Math.max(0, Math.min(1, mole.life / 160))
    const frame = f > 0.66 ? 'fx-bonk-1' : f > 0.33 ? 'fx-bonk-2' : 'fx-bonk-3'
    const fx = g09Sprite(frame)
    if (g09ready(fx)) {
      const fs = 64
      ctx.drawImage(fx, c.x - fs / 2, y - fs * 0.6, fs, fs)
    }
  }
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
  if ((k === ' ' || k === 'enter') && phase.value !== 'playing') {
    startGame()
    return
  }
  if (phase.value !== 'playing') return
  const i1 = P1_KEYS.indexOf(k)
  if (i1 >= 0) {
    hit(game.p1, i1, 'p1Score', 'p1Combo')
    return
  }
  const i2 = P2_KEYS.indexOf(k)
  if (i2 >= 0) hit(game.p2, i2, 'p2Score', 'p2Combo')
}

async function onClearRecords() {
  const store = await clearGame09Records()
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
  render()
}

onMounted(async () => {
  ctx = canvasRef.value.getContext('2d')
  window.addEventListener('keydown', onKeyDown)
  try {
    const store = await fetchGame09Store()
    records.value = store.records
  } catch {
    /* ignore */
  }
  idleRender()
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeyDown)
  if (rafId) cancelAnimationFrame(rafId)
})
</script>

<style scoped>
.game09-view {
  min-height: 100vh;
  padding: 24px clamp(16px, 4vw, 48px) 48px;
  color: #eaf5e6;
  background: radial-gradient(circle at 50% -10%, #1c3a1f, #07120a 60%);
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
  color: #9bd6a0;
  text-decoration: none;
  font-size: 14px;
  padding: 8px 14px;
  border: 1px solid rgba(155, 214, 160, 0.3);
  border-radius: 999px;
  transition: 0.2s;
}
.back-link:hover {
  background: rgba(155, 214, 160, 0.12);
  color: #fff;
}
.title-block .eyebrow {
  margin: 0;
  font-size: 12px;
  letter-spacing: 3px;
  color: #ffd23f;
  text-transform: uppercase;
}
.title-block h1 {
  margin: 2px 0 0;
  font-size: 26px;
  background: linear-gradient(90deg, #8de96a, #ffd23f);
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
  background: rgba(7, 18, 10, 0.6);
  border: 1px solid rgba(141, 233, 106, 0.18);
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
  color: #8de96a;
}
.team-2 .score {
  color: #ffd23f;
}
.team .combo {
  font-size: 14px;
  color: #7fa583;
  font-weight: 700;
}
.team .combo.hot {
  color: #ffd23f;
}
.vs {
  font-size: 13px;
  font-weight: 800;
  color: #5e7d62;
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
  background: rgba(4, 12, 7, 0.82);
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
  color: #ffd23f;
}
.overlay-card h2 {
  margin: 10px 0 14px;
  font-size: 28px;
}
.winner-text {
  background: linear-gradient(90deg, #8de96a, #ffd23f);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.overlay-text {
  color: #bcd6bf;
  line-height: 1.7;
  margin: 0 0 22px;
}
.primary-btn {
  border: none;
  padding: 12px 30px;
  border-radius: 999px;
  font-size: 16px;
  font-weight: 700;
  color: #13280c;
  background: linear-gradient(90deg, #8de96a, #ffd23f);
  cursor: pointer;
  transition: transform 0.15s, box-shadow 0.15s;
  box-shadow: 0 10px 26px rgba(141, 233, 106, 0.35);
}
.primary-btn:hover {
  transform: translateY(-2px);
  box-shadow: 0 14px 32px rgba(141, 233, 106, 0.5);
}
.sidebar {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.panel {
  background: rgba(7, 18, 10, 0.6);
  border: 1px solid rgba(141, 233, 106, 0.18);
  border-radius: 18px;
  padding: 18px;
}
.panel .eyebrow {
  margin: 0 0 12px;
  font-size: 11px;
  letter-spacing: 2px;
  text-transform: uppercase;
  color: #6e9472;
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
.ctrl-1 {
  background: rgba(141, 233, 106, 0.1);
  border: 1px solid rgba(141, 233, 106, 0.25);
}
.ctrl-2 {
  background: rgba(255, 210, 63, 0.1);
  border: 1px solid rgba(255, 210, 63, 0.25);
}
.keypad {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 5px;
  width: 110px;
}
kbd {
  background: rgba(255, 255, 255, 0.12);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 6px;
  padding: 4px 0;
  font-size: 12px;
  font-family: inherit;
  text-align: center;
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
  color: #c5dcc7;
}
.ic {
  width: 26px;
  text-align: center;
  font-size: 18px;
  flex-shrink: 0;
}
.hint {
  font-size: 12px;
  color: #6e9472;
  margin: 0 0 10px;
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
  color: #9bd6a0;
}
.rec-date {
  color: #5e7d62;
}
.empty {
  font-size: 13px;
  color: #6e9472;
  line-height: 1.6;
  margin: 0;
}
.ghost-btn {
  background: none;
  border: 1px solid rgba(155, 214, 160, 0.3);
  color: #9bd6a0;
  border-radius: 999px;
  padding: 4px 12px;
  font-size: 12px;
  cursor: pointer;
}
.ghost-btn:hover {
  background: rgba(155, 214, 160, 0.12);
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
