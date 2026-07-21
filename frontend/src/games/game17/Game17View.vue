<template>
  <main class="game17-view">
    <section class="topbar">
      <RouterLink to="/" class="back-link">← 返回遊戲廳</RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 17</p>
        <h1>迷宮競速</h1>
      </div>
      <div class="round-pill">搶先抵達終點者勝</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <span class="dot" />
            <strong>玩家 1</strong>
            <span class="coin">🪙 {{ hud.p1Coins }}</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="coin">🪙 {{ hud.p2Coins }}</span>
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
                  <p class="overlay-eyebrow">穿梭迷宮</p>
                  <h2>搶先衝出迷宮終點</h2>
                  <p class="overlay-text">
                    兩人挑戰同一座隨機迷宮，從左上角出發衝向右下角的終點。<br>
                    沿途金幣可加成，先抵達終點者獲勝。
                  </p>
                  <button class="primary-btn" @click="startGame">開始競速</button>
                </template>
                <template v-else-if="phase === 'over'">
                  <p class="overlay-eyebrow">抵達終點</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <p class="overlay-text">金幣 {{ hud.p1Coins }} : {{ hud.p2Coins }}</p>
                  <button class="primary-btn" @click="startGame">再來一場</button>
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
              <span><kbd>W</kbd><kbd>A</kbd><kbd>S</kbd><kbd>D</kbd> 移動</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span><kbd>↑</kbd><kbd>←</kbd><kbd>↓</kbd><kbd>→</kbd> 移動</span>
            </div>
          </div>
        </section>
        <section class="panel">
          <p class="eyebrow">提示</p>
          <ul class="tips">
            <li>兩邊迷宮配置完全相同，比的是反應與記路。</li>
            <li>金幣為加成與平手判定依據。</li>
            <li>抵達右下角金色終點即獲勝。</li>
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
              <span class="rec-score">🪙 {{ r.coinP1 }} : {{ r.coinP2 }}</span>
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
import { clearGame17Records, fetchGame17Store, saveGame17Record } from './game17Storage'
import { recordGameResult } from '@/data/lobbyScore'
import { assetUrl } from '@/utils/assetUrl'

const GRID = 13
const CELL = 30
const MAZE_PX = GRID * CELL

// 像素素材
const G17 = {}
function g17Sprite(name) {
  if (!G17[name]) {
    const img = new Image()
    img.src = assetUrl(`/assets/G17/${name}.png`)
    G17[name] = img
  }
  return G17[name]
}
;['bg-dungeon', 'tile-floor', 'tile-wall', 'coin', 'flag-goal', 'player-p1', 'player-p1-hop', 'player-p2', 'player-p2-hop'].forEach(g17Sprite)
function g17ready(img) {
  return img && img.complete && img.naturalWidth > 0
}
const CANVAS_W = 920
const CANVAS_H = 460
const HALF = CANVAS_W / 2

const canvasRef = ref(null)
const stageRef = ref(null)
const phase = ref('intro')
const resultText = ref('')
const records = ref([])
const hud = reactive({ p1Coins: 0, p2Coins: 0 })

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null
const keys = new Set()

function genMaze() {
  const cells = Array.from({ length: GRID }, () =>
    Array.from({ length: GRID }, () => ({ n: true, e: true, s: true, w: true, v: false }))
  )
  const stack = [[0, 0]]
  cells[0][0].v = true
  const dirs = [
    ['n', -1, 0, 's'],
    ['s', 1, 0, 'n'],
    ['e', 0, 1, 'w'],
    ['w', 0, -1, 'e']
  ]
  while (stack.length) {
    const [r, c] = stack[stack.length - 1]
    const options = []
    for (const [d, dr, dc, opp] of dirs) {
      const nr = r + dr
      const nc = c + dc
      if (nr >= 0 && nc >= 0 && nr < GRID && nc < GRID && !cells[nr][nc].v) {
        options.push([d, nr, nc, opp])
      }
    }
    if (!options.length) {
      stack.pop()
      continue
    }
    const [d, nr, nc, opp] = options[Math.floor(Math.random() * options.length)]
    cells[r][c][d] = false
    cells[nr][nc][opp] = false
    cells[nr][nc].v = true
    stack.push([nr, nc])
  }
  return cells
}

function makePlayer(color) {
  return { r: 0, c: 0, px: CELL / 2, py: CELL / 2, target: null, coins: 0, done: false, color, lastPressed: null }
}

function createGame() {
  const maze = genMaze()
  const coins = new Set()
  for (let i = 0; i < 14; i += 1) {
    const r = Math.floor(Math.random() * GRID)
    const c = Math.floor(Math.random() * GRID)
    if ((r === 0 && c === 0) || (r === GRID - 1 && c === GRID - 1)) continue
    coins.add(`${r},${c}`)
  }
  return {
    maze,
    p1: makePlayer('#3affd0'),
    p1Coins: new Set(coins),
    p2: makePlayer('#ff9ec8'),
    p2Coins: new Set(coins),
    finishTime: 0
  }
}

function startGame() {
  game = createGame()
  hud.p1Coins = 0
  hud.p2Coins = 0
  phase.value = 'playing'
  lastFrame = performance.now()
  loop(lastFrame)
}

function canMove(r, c, dir) {
  const cell = game.maze[r][c]
  return !cell[dir]
}

const DIR_DELTA = { n: [-1, 0], s: [1, 0], w: [0, -1], e: [0, 1] }
function keyFor(p, d) {
  const m1 = { n: 'w', s: 's', w: 'a', e: 'd' }
  const m2 = { n: 'arrowup', s: 'arrowdown', w: 'arrowleft', e: 'arrowright' }
  return (p === game.p1 ? m1 : m2)[d]
}

function tryStartMove(p) {
  if (p.target || p.done) return
  // try the most recently pressed direction first (forgiving cornering),
  // then fall back to whatever direction is still held
  const order = []
  if (p.lastPressed) order.push(p.lastPressed)
  for (const d of ['n', 's', 'w', 'e']) if (!order.includes(d)) order.push(d)
  for (const d of order) {
    if (!keys.has(keyFor(p, d))) continue
    if (!canMove(p.r, p.c, d)) continue
    const [dr, dc] = DIR_DELTA[d]
    const nr = p.r + dr
    const nc = p.c + dc
    if (nr < 0 || nc < 0 || nr >= GRID || nc >= GRID) continue
    p.target = { r: nr, c: nc }
    return
  }
}

function updatePlayer(p, coinSet, hudKey, now) {
  tryStartMove(p)
  if (p.target) {
    const tx = p.target.c * CELL + CELL / 2
    const ty = p.target.r * CELL + CELL / 2
    const dx = tx - p.px
    const dy = ty - p.py
    const dist = Math.hypot(dx, dy)
    const speed = 3
    if (dist <= speed) {
      p.px = tx
      p.py = ty
      p.r = p.target.r
      p.c = p.target.c
      p.target = null
      const key = `${p.r},${p.c}`
      if (coinSet.has(key)) {
        coinSet.delete(key)
        p.coins += 1
        hud[hudKey] = p.coins
      }
      if (p.r === GRID - 1 && p.c === GRID - 1 && !p.done) {
        p.done = true
        finishGame(p === game.p1 ? 'p1' : 'p2', now)
      }
    } else {
      p.px += (dx / dist) * speed
      p.py += (dy / dist) * speed
    }
  }
}

let matchOver = false
async function finishGame(winnerTeam, now) {
  if (matchOver) return
  matchOver = true
  cancelAnimationFrame(rafId)
  rafId = 0
  const winner = winnerTeam === 'p1' ? '玩家 1 獲勝' : '玩家 2 獲勝'
  resultText.value = `🏁 ${winner}`
  phase.value = 'over'
  recordGameResult('/game17', winnerTeam)
  try {
    const store = await saveGame17Record({ winner, coinP1: game.p1.coins, coinP2: game.p2.coins, date: new Date().toISOString() })
    records.value = store.records
  } catch {
    /* ignore */
  }
  void now
}

function update(dt, now) {
  updatePlayer(game.p1, game.p1Coins, 'p1Coins', now)
  if (!matchOver) updatePlayer(game.p2, game.p2Coins, 'p2Coins', now)
  void dt
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
  const bgImg = g17Sprite('bg-dungeon')
  if (g17ready(bgImg)) {
    ctx.drawImage(bgImg, 0, 0, CANVAS_W, CANVAS_H)
  } else {
    ctx.fillStyle = '#0c1024'
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  }
  drawMaze(game.p1, game.p1Coins, (HALF - MAZE_PX) / 2, '玩家 1', now)
  drawMaze(game.p2, game.p2Coins, HALF + (HALF - MAZE_PX) / 2, '玩家 2', now)
  ctx.strokeStyle = 'rgba(255,255,255,0.1)'
  ctx.lineWidth = 2
  ctx.beginPath()
  ctx.moveTo(HALF, 0)
  ctx.lineTo(HALF, CANVAS_H)
  ctx.stroke()
}

function drawMaze(p, coinSet, ox, label, now) {
  const oy = (CANVAS_H - MAZE_PX) / 2 + 8
  ctx.save()
  ctx.translate(ox, oy)
  // floor（像素地磚平鋪）
  const floorImg = g17Sprite('tile-floor')
  if (g17ready(floorImg)) {
    for (let r = 0; r < GRID; r += 1) {
      for (let c = 0; c < GRID; c += 1) {
        ctx.drawImage(floorImg, c * CELL, r * CELL, CELL, CELL)
      }
    }
  } else {
    ctx.fillStyle = '#141a36'
    ctx.fillRect(0, 0, MAZE_PX, MAZE_PX)
  }
  // exit cell —— 終點旗
  const flag = g17Sprite('flag-goal')
  const gx = (GRID - 1) * CELL
  const gy = (GRID - 1) * CELL
  if (g17ready(flag)) {
    const fw = CELL
    const fh = fw * (flag.naturalHeight / flag.naturalWidth)
    ctx.drawImage(flag, gx, gy + CELL - fh, fw, fh)
  } else {
    ctx.fillStyle = 'rgba(255,210,63,0.25)'
    ctx.fillRect(gx, gy, CELL, CELL)
  }
  // coins
  const coinImg = g17Sprite('coin')
  ctx.font = '16px serif'
  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  for (const key of coinSet) {
    const [r, c] = key.split(',').map(Number)
    if (g17ready(coinImg)) {
      const cs = CELL * 0.66
      ctx.drawImage(coinImg, c * CELL + (CELL - cs) / 2, r * CELL + (CELL - cs) / 2, cs, cs)
    } else {
      ctx.fillText('🪙', c * CELL + CELL / 2, r * CELL + CELL / 2)
    }
  }
  // walls
  ctx.strokeStyle = '#6a5436'
  ctx.lineWidth = 2.5
  ctx.lineCap = 'round'
  for (let r = 0; r < GRID; r += 1) {
    for (let c = 0; c < GRID; c += 1) {
      const cell = game.maze[r][c]
      const x = c * CELL
      const y = r * CELL
      if (cell.n) line(x, y, x + CELL, y)
      if (cell.w) line(x, y, x, y + CELL)
      if (cell.e) line(x + CELL, y, x + CELL, y + CELL)
      if (cell.s) line(x, y + CELL, x + CELL, y + CELL)
    }
  }
  // player（移動時用跳躍幀）
  const pid = p === game.p2 ? 'p2' : 'p1'
  const moving = !!p.target
  const pImg = g17Sprite(`player-${pid}${moving && Math.floor(now / 130) % 2 ? '-hop' : ''}`)
  if (g17ready(pImg)) {
    const ps = CELL * 1.1
    ctx.drawImage(pImg, p.px - ps / 2, p.py - ps / 2, ps, ps)
  } else {
    ctx.shadowColor = p.color
    ctx.shadowBlur = 12
    ctx.fillStyle = p.color
    ctx.beginPath()
    ctx.arc(p.px, p.py, CELL * 0.3, 0, Math.PI * 2)
    ctx.fill()
    ctx.shadowBlur = 0
  }
  ctx.restore()
  // label
  ctx.fillStyle = p.color
  ctx.font = 'bold 14px system-ui, sans-serif'
  ctx.textAlign = 'center'
  ctx.fillText(label, ox + MAZE_PX / 2, oy - 6)
  void now
}

function line(x1, y1, x2, y2) {
  ctx.beginPath()
  ctx.moveTo(x1, y1)
  ctx.lineTo(x2, y2)
  ctx.stroke()
}

const P1_DIR = { w: 'n', s: 's', a: 'w', d: 'e' }
const P2_DIR = { arrowup: 'n', arrowdown: 's', arrowleft: 'w', arrowright: 'e' }
function onKeyDown(e) {
  const k = e.key.toLowerCase()
  keys.add(k)
  if (k.startsWith('arrow') || k === ' ') e.preventDefault()
  if (game) {
    if (P1_DIR[k]) game.p1.lastPressed = P1_DIR[k]
    else if (P2_DIR[k]) game.p2.lastPressed = P2_DIR[k]
  }
  if ((k === ' ' || k === 'enter') && phase.value !== 'playing') startGame()
}
function onKeyUp(e) {
  keys.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame17Records()
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
  matchOver = false
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('keyup', onKeyUp)
  try {
    const store = await fetchGame17Store()
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

const _s = startGame
void _s
</script>

<style scoped>
.game17-view { min-height: 100vh; padding: 24px clamp(16px,4vw,48px) 48px; color: #e7ecfb; background: radial-gradient(circle at 50% -10%, #1a2150, #080b1c 60%); font-family: 'Segoe UI', system-ui, sans-serif; }
.topbar { display: flex; align-items: center; gap: 20px; flex-wrap: wrap; margin-bottom: 22px; }
.back-link { color: #9fb0e8; text-decoration: none; font-size: 14px; padding: 8px 14px; border: 1px solid rgba(159,176,232,0.3); border-radius: 999px; transition: 0.2s; }
.back-link:hover { background: rgba(159,176,232,0.12); color: #fff; }
.title-block .eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; color: #8ea2ff; text-transform: uppercase; }
.title-block h1 { margin: 2px 0 0; font-size: 26px; background: linear-gradient(90deg,#3affd0,#ff9ec8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.round-pill { margin-left: auto; padding: 8px 16px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 13px; }
.layout { display: grid; grid-template-columns: minmax(0,1fr) 300px; gap: 22px; align-items: start; }
.stage-card { background: rgba(8,11,28,0.6); border: 1px solid rgba(120,140,220,0.16); border-radius: 20px; padding: 16px; box-shadow: 0 24px 60px rgba(0,0,0,0.5); }
.scoreband { display: flex; align-items: center; gap: 16px; margin-bottom: 14px; padding: 0 6px; }
.team { display: flex; align-items: center; gap: 12px; flex: 1; }
.team-2 { justify-content: flex-end; }
.team .dot { width: 14px; height: 14px; border-radius: 50%; }
.team-1 .dot { background: #3affd0; box-shadow: 0 0 12px #3affd0; }
.team-2 .dot { background: #ff9ec8; box-shadow: 0 0 12px #ff9ec8; }
.team strong { font-size: 15px; }
.team .coin { font-size: 14px; color: #ffd23f; font-weight: 700; }
.vs { font-size: 13px; font-weight: 800; color: #5d6a96; }
.stage-frame { position: relative; border-radius: 14px; overflow: hidden; }
.game-canvas { display: block; width: 100%; height: auto; border-radius: 14px; }
.overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(5,8,20,0.82); backdrop-filter: blur(4px); }
.overlay-card { text-align: center; max-width: 460px; padding: 32px; }
.overlay-eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; color: #8ea2ff; }
.overlay-card h2 { margin: 10px 0 14px; font-size: 26px; }
.winner-text { background: linear-gradient(90deg,#3affd0,#ff9ec8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.overlay-text { color: #b6c2e8; line-height: 1.7; margin: 0 0 22px; }
.primary-btn { border: none; padding: 12px 30px; border-radius: 999px; font-size: 16px; font-weight: 700; color: #05140f; background: linear-gradient(90deg,#3affd0,#46d0ff); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; box-shadow: 0 10px 26px rgba(58,255,208,0.35); }
.primary-btn:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(58,255,208,0.5); }
.sidebar { display: flex; flex-direction: column; gap: 16px; }
.panel { background: rgba(8,11,28,0.6); border: 1px solid rgba(120,140,220,0.16); border-radius: 18px; padding: 18px; }
.panel .eyebrow { margin: 0 0 12px; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #7585b0; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.controls-grid { display: grid; gap: 10px; }
.ctrl { border-radius: 12px; padding: 12px 14px; }
.ctrl strong { display: block; font-size: 14px; margin-bottom: 6px; }
.ctrl span { font-size: 13px; color: #b6c2e8; }
.ctrl-1 { background: rgba(58,255,208,0.1); border: 1px solid rgba(58,255,208,0.25); }
.ctrl-2 { background: rgba(255,158,200,0.1); border: 1px solid rgba(255,158,200,0.25); }
kbd { background: rgba(255,255,255,0.12); border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 2px 7px; font-size: 12px; font-family: inherit; margin-right: 2px; }
.tips { margin: 0; padding-left: 18px; display: grid; gap: 8px; font-size: 13px; color: #b6c2e8; line-height: 1.5; }
.records { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.records li { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: center; font-size: 12px; padding: 8px 10px; border-radius: 10px; background: rgba(255,255,255,0.04); }
.rec-win { font-weight: 700; color: #ffd23f; }
.rec-score { color: #9fb0e8; }
.rec-date { color: #5d6a96; }
.empty { font-size: 13px; color: #7585b0; line-height: 1.6; margin: 0; }
.ghost-btn { background: none; border: 1px solid rgba(159,176,232,0.3); color: #9fb0e8; border-radius: 999px; padding: 4px 12px; font-size: 12px; cursor: pointer; }
.ghost-btn:hover { background: rgba(159,176,232,0.12); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } }
</style>
