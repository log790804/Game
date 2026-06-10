<template>
  <main class="game14-view">
    <section class="topbar">
      <RouterLink to="/" class="back-link">← 返回遊戲廳</RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 14</p>
        <h1>泡泡龍對戰</h1>
      </div>
      <div
        v-if="phase === 'playing'"
        class="time-pill"
        :class="{ urgent: timeLeft <= 10 }"
      >
        ⏱ {{ timeLeft }}s
      </div>
      <div v-else class="round-pill">限時 90 秒 · 比消除數</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <span class="dot" />
            <strong>玩家 1</strong>
            <span class="pop">消除 {{ hud.p1Pop }}</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="pop">消除 {{ hud.p2Pop }}</span>
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
                  <p class="overlay-eyebrow">三消對拚</p>
                  <h2>瞄準射出，三顆同色消除</h2>
                  <p class="overlay-text">
                    跟著<b>輔助線</b>瞄準（會反彈），三顆以上同色相連即可消除，懸空的也會一併掉落。<br>
                    限時 90 秒，<b>消除數高者獲勝</b>；泡泡頂到底線會崩盤、對手加分。
                  </p>
                  <button class="primary-btn" @click="startMatch">開始對戰</button>
                </template>
                <template v-else-if="phase === 'over'">
                  <p class="overlay-eyebrow">對戰結束</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <p class="overlay-text">消除數 {{ hud.p1Pop }} : {{ hud.p2Pop }}</p>
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
              <span><kbd>A</kbd><kbd>D</kbd> 瞄準 · <kbd>W</kbd> 發射</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span><kbd>←</kbd><kbd>→</kbd> 瞄準 · <kbd>↑</kbd> 發射</span>
            </div>
          </div>
        </section>
        <section class="panel">
          <p class="eyebrow">規則</p>
          <ul class="tips">
            <li>三顆以上同色相連即消除，輔助線會顯示反彈路徑。</li>
            <li>單次消除 5 顆以上，壓一排泡泡給對手。</li>
            <li>頂到底線會崩盤清空、對手加 10 分。</li>
            <li>限時 90 秒，消除數高者獲勝。</li>
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
import { clearGame14Records, fetchGame14Store, saveGame14Record } from './game14Storage'
import { recordGameResult } from '@/data/lobbyScore'

const CANVAS_W = 920
const CANVAS_H = 640
const HALF = CANVAS_W / 2
const COLS = 8
const D = 40
const R = D / 2
const ROWH = D * 0.86
const MAXROWS = 16
const DANGER_ROW = 12
const TOPY = 16
const PAD = 14
const PUSH_EVERY = 5
const GAME_SEC = 90
const COLORS = ['#ff5d6c', '#ffd23f', '#46d0ff', '#8de96a', '#b56cff']

const canvasRef = ref(null)
const stageRef = ref(null)
const phase = ref('intro')
const resultText = ref('')
const records = ref([])
const hud = reactive({ p1Pop: 0, p2Pop: 0 })
const timeLeft = ref(GAME_SEC)

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null
const keys = new Set()

function makeSide(half) {
  const originX = half === 0 ? PAD : HALF + PAD
  const grid = Array.from({ length: MAXROWS }, () => Array(COLS).fill(null))
  for (let r = 0; r < 4; r += 1) {
    for (let c = 0; c < COLS; c += 1) {
      if (r % 2 === 1 && c === COLS - 1) continue
      grid[r][c] = Math.floor(Math.random() * COLORS.length)
    }
  }
  return {
    half,
    originX,
    centerX: originX + (COLS * D) / 2,
    grid,
    angle: Math.PI / 2,
    current: Math.floor(Math.random() * COLORS.length),
    next: Math.floor(Math.random() * COLORS.length),
    shot: null,
    shotsSincePush: 0,
    pop: 0,
    alive: true,
    fx: []
  }
}

function createGame() {
  return { p1: makeSide(0), p2: makeSide(1), elapsed: 0 }
}

function cellCenter(side, row, col) {
  const offset = (row % 2) * R
  return {
    x: side.originX + col * D + R + offset,
    y: TOPY + row * ROWH + R
  }
}

function startMatch() {
  game = createGame()
  matchOver = false
  hud.p1Pop = 0
  hud.p2Pop = 0
  timeLeft.value = GAME_SEC
  phase.value = 'playing'
  lastFrame = performance.now()
  loop(lastFrame)
}

function shoot(side) {
  if (side.shot || !side.alive) return
  const sx = side.centerX
  const sy = CANVAS_H - 56
  const speed = 11
  side.shot = {
    x: sx,
    y: sy,
    vx: Math.cos(side.angle) * speed,
    vy: -Math.sin(side.angle) * speed,
    color: side.current
  }
  side.current = side.next
  side.next = Math.floor(Math.random() * COLORS.length)
}

function nearestEmptyCell(side, x, y) {
  let best = null
  let bestD = Infinity
  for (let r = 0; r < MAXROWS; r += 1) {
    for (let c = 0; c < COLS; c += 1) {
      if (r % 2 === 1 && c === COLS - 1) continue
      if (side.grid[r][c] !== null) continue
      const cc = cellCenter(side, r, c)
      const dist = Math.hypot(cc.x - x, cc.y - y)
      if (dist < bestD) {
        bestD = dist
        best = { r, c }
      }
    }
  }
  return best
}

const EVEN_NB = [[0, -1], [0, 1], [-1, -1], [-1, 0], [1, -1], [1, 0]]
const ODD_NB = [[0, -1], [0, 1], [-1, 0], [-1, 1], [1, 0], [1, 1]]
function neighbors(r, c) {
  const nb = r % 2 === 0 ? EVEN_NB : ODD_NB
  const out = []
  for (const [dr, dc] of nb) {
    const nr = r + dr
    const nc = c + dc
    if (nr < 0 || nc < 0 || nr >= MAXROWS || nc >= COLS) continue
    if (nr % 2 === 1 && nc === COLS - 1) continue
    out.push([nr, nc])
  }
  return out
}

function computeAimPath(side) {
  const speed = 11
  let x = side.centerX
  let y = CANVAS_H - 56
  let vx = Math.cos(side.angle) * speed
  let vy = -Math.sin(side.angle) * speed
  const pts = [{ x, y }]
  const leftB = side.originX + R
  const rightB = side.originX + COLS * D - R
  const gridBottom = TOPY + MAXROWS * ROWH + D
  for (let i = 0; i < 700; i += 1) {
    x += vx / 3
    y += vy / 3
    if (x < leftB) { x = leftB; vx = Math.abs(vx); pts.push({ x, y }) }
    else if (x > rightB) { x = rightB; vx = -Math.abs(vx); pts.push({ x, y }) }
    if (y <= TOPY + R) { pts.push({ x, y }); break }
    if (y < gridBottom) {
      let hit = false
      for (let r = 0; r < MAXROWS && !hit; r += 1) {
        for (let c = 0; c < COLS; c += 1) {
          if (side.grid[r][c] === null) continue
          const cc = cellCenter(side, r, c)
          if (Math.hypot(cc.x - x, cc.y - y) < D * 0.86) { hit = true; break }
        }
      }
      if (hit) { pts.push({ x, y }); break }
    }
    if (y < -40) break
  }
  return pts
}

function settleShot(side) {
  const s = side.shot
  const cell = nearestEmptyCell(side, s.x, s.y)
  side.shot = null
  if (!cell) return
  side.grid[cell.r][cell.c] = s.color
  // match flood
  const color = s.color
  const seen = new Set()
  const stack = [[cell.r, cell.c]]
  const group = []
  while (stack.length) {
    const [r, c] = stack.pop()
    const key = r * COLS + c
    if (seen.has(key)) continue
    seen.add(key)
    if (side.grid[r][c] !== color) continue
    group.push([r, c])
    for (const [nr, nc] of neighbors(r, c)) stack.push([nr, nc])
  }
  if (group.length >= 3) {
    for (const [r, c] of group) {
      const cc = cellCenter(side, r, c)
      spawnFx(side, cc.x, cc.y, COLORS[color])
      side.grid[r][c] = null
    }
    side.pop += group.length
    if (side.half === 0) hud.p1Pop = side.pop
    else hud.p2Pop = side.pop
    removeFloating(side)
    if (group.length >= 5) addRow(side === game.p1 ? game.p2 : game.p1)
  }
  side.shotsSincePush += 1
  if (side.shotsSincePush >= PUSH_EVERY) {
    side.shotsSincePush = 0
    addRow(side)
  }
  checkOverflow(side)
}

function removeFloating(side) {
  const connected = new Set()
  const stack = []
  for (let c = 0; c < COLS; c += 1) {
    if (side.grid[0][c] !== null) stack.push([0, c])
  }
  while (stack.length) {
    const [r, c] = stack.pop()
    const key = r * COLS + c
    if (connected.has(key)) continue
    if (side.grid[r][c] === null) continue
    connected.add(key)
    for (const [nr, nc] of neighbors(r, c)) stack.push([nr, nc])
  }
  for (let r = 0; r < MAXROWS; r += 1) {
    for (let c = 0; c < COLS; c += 1) {
      if (side.grid[r][c] !== null && !connected.has(r * COLS + c)) {
        const cc = cellCenter(side, r, c)
        spawnFx(side, cc.x, cc.y, COLORS[side.grid[r][c]])
        side.pop += 1
        if (side.half === 0) hud.p1Pop = side.pop
        else hud.p2Pop = side.pop
        side.grid[r][c] = null
      }
    }
  }
}

function addRow(side) {
  // shift down
  for (let r = MAXROWS - 1; r > 0; r -= 1) {
    side.grid[r] = side.grid[r - 1].slice()
  }
  side.grid[0] = Array.from({ length: COLS }, (_, c) => {
    if (Math.random() < 0.82) return Math.floor(Math.random() * COLORS.length)
    return null
  })
  checkOverflow(side)
}

function checkOverflow(side) {
  let over = false
  for (let c = 0; c < COLS && !over; c += 1) {
    for (let r = DANGER_ROW; r < MAXROWS; r += 1) {
      if (side.grid[r][c] !== null) { over = true; break }
    }
  }
  if (!over) return
  // board collapses: clear it (no score), opponent gets a bonus, then refill a few rows
  for (let r = 0; r < MAXROWS; r += 1) {
    for (let c = 0; c < COLS; c += 1) {
      if (side.grid[r][c] !== null) {
        const cc = cellCenter(side, r, c)
        spawnFx(side, cc.x, cc.y, COLORS[side.grid[r][c]])
        side.grid[r][c] = null
      }
    }
  }
  side.shotsSincePush = 0
  const opp = side === game.p1 ? game.p2 : game.p1
  opp.pop += 10
  if (opp.half === 0) hud.p1Pop = opp.pop
  else hud.p2Pop = opp.pop
  for (let r = 0; r < 3; r += 1) {
    for (let c = 0; c < COLS; c += 1) {
      if (r % 2 === 1 && c === COLS - 1) continue
      side.grid[r][c] = Math.floor(Math.random() * COLORS.length)
    }
  }
}

function isCleared(side) {
  for (let r = 0; r < MAXROWS; r += 1) {
    for (let c = 0; c < COLS; c += 1) {
      if (side.grid[r][c] !== null) return false
    }
  }
  return true
}

function spawnFx(side, x, y, color) {
  for (let i = 0; i < 8; i += 1) {
    const a = Math.random() * Math.PI * 2
    const sp = 40 + Math.random() * 120
    side.fx.push({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 1, color })
  }
}

let matchOver = false
async function finishByScore() {
  if (matchOver) return
  matchOver = true
  cancelAnimationFrame(rafId)
  rafId = 0
  let winnerTeam
  if (hud.p1Pop > hud.p2Pop) winnerTeam = 'p1'
  else if (hud.p2Pop > hud.p1Pop) winnerTeam = 'p2'
  else winnerTeam = 'draw'
  const winner = winnerTeam === 'p1' ? '玩家 1 獲勝' : winnerTeam === 'p2' ? '玩家 2 獲勝' : '平手'
  resultText.value = `🫧 ${winner}`
  phase.value = 'over'
  recordGameResult('/game14', winnerTeam)
  try {
    const store = await saveGame14Record({ winner, scoreP1: hud.p1Pop, scoreP2: hud.p2Pop, date: new Date().toISOString() })
    records.value = store.records
  } catch {
    /* ignore */
  }
}

function updateSide(side, dt) {
  // aim
  const leftK = side.half === 0 ? 'a' : 'arrowleft'
  const rightK = side.half === 0 ? 'd' : 'arrowright'
  if (keys.has(leftK)) side.angle = Math.min(Math.PI - 0.25, side.angle + 0.035)
  if (keys.has(rightK)) side.angle = Math.max(0.25, side.angle - 0.035)
  // shot
  if (side.shot) {
    const s = side.shot
    for (let step = 0; step < 3; step += 1) {
      s.x += s.vx / 3
      s.y += s.vy / 3
      if (s.x < side.originX + R) {
        s.x = side.originX + R
        s.vx = Math.abs(s.vx)
      } else if (s.x > side.originX + COLS * D - R) {
        s.x = side.originX + COLS * D - R
        s.vx = -Math.abs(s.vx)
      }
      if (s.y <= TOPY + R) {
        settleShot(side)
        break
      }
      // collide with existing
      let hit = false
      for (let r = 0; r < MAXROWS && !hit; r += 1) {
        for (let c = 0; c < COLS; c += 1) {
          if (side.grid[r][c] === null) continue
          const cc = cellCenter(side, r, c)
          if (Math.hypot(cc.x - s.x, cc.y - s.y) < D * 0.86) {
            settleShot(side)
            hit = true
            break
          }
        }
      }
      if (hit) break
    }
  }
  for (const f of side.fx) {
    f.x += f.vx * (dt / 1000)
    f.y += f.vy * (dt / 1000)
    f.vy += 200 * (dt / 1000)
    f.life -= dt / 600
  }
  side.fx = side.fx.filter((f) => f.life > 0)
}

function update(dt) {
  game.elapsed += dt
  timeLeft.value = Math.max(0, Math.ceil((GAME_SEC * 1000 - game.elapsed) / 1000))
  updateSide(game.p1, dt)
  if (!matchOver) updateSide(game.p2, dt)
  if (!matchOver && game.elapsed >= GAME_SEC * 1000) finishByScore()
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
  ctx.fillStyle = '#0c1226'
  ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  drawSide(game.p1)
  drawSide(game.p2)
  // divider
  ctx.strokeStyle = 'rgba(255,255,255,0.12)'
  ctx.lineWidth = 2
  ctx.beginPath()
  ctx.moveTo(HALF, 0)
  ctx.lineTo(HALF, CANVAS_H)
  ctx.stroke()
}

function drawSide(side) {
  const fieldX = side.originX
  const fieldW = COLS * D
  ctx.fillStyle = 'rgba(255,255,255,0.025)'
  ctx.fillRect(fieldX, 0, fieldW, CANVAS_H)
  // danger line
  const dy = TOPY + DANGER_ROW * ROWH
  ctx.strokeStyle = 'rgba(255,93,108,0.5)'
  ctx.setLineDash([6, 8])
  ctx.lineWidth = 2
  ctx.beginPath()
  ctx.moveTo(fieldX, dy)
  ctx.lineTo(fieldX + fieldW, dy)
  ctx.stroke()
  ctx.setLineDash([])

  for (let r = 0; r < MAXROWS; r += 1) {
    for (let c = 0; c < COLS; c += 1) {
      if (side.grid[r][c] === null) continue
      const cc = cellCenter(side, r, c)
      drawBubble(cc.x, cc.y, COLORS[side.grid[r][c]])
    }
  }
  // extended aim guide (bounces off walls, stops at first bubble / ceiling)
  const sx = side.centerX
  const sy = CANVAS_H - 56
  if (!side.shot) {
    const path = computeAimPath(side)
    ctx.strokeStyle = side.half === 0 ? 'rgba(70,208,255,0.55)' : 'rgba(255,93,108,0.55)'
    ctx.setLineDash([4, 8])
    ctx.lineWidth = 2
    ctx.lineCap = 'round'
    ctx.beginPath()
    path.forEach((p, i) => (i === 0 ? ctx.moveTo(p.x, p.y) : ctx.lineTo(p.x, p.y)))
    ctx.stroke()
    ctx.setLineDash([])
    ctx.lineCap = 'butt'
    const end = path[path.length - 1]
    ctx.save()
    ctx.globalAlpha = 0.5
    drawBubble(end.x, end.y, COLORS[side.current])
    ctx.restore()
  }
  // launcher bubbles
  drawBubble(sx, sy, COLORS[side.current])
  drawBubble(side.half === 0 ? fieldX + 18 : fieldX + fieldW - 18, CANVAS_H - 24, COLORS[side.next], 12)
  // shot
  if (side.shot) drawBubble(side.shot.x, side.shot.y, COLORS[side.shot.color])
  // fx
  for (const f of side.fx) {
    ctx.globalAlpha = Math.max(0, f.life)
    ctx.fillStyle = f.color
    ctx.beginPath()
    ctx.arc(f.x, f.y, 4 * f.life + 1, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.globalAlpha = 1
}

function drawBubble(x, y, color, radius = R - 2) {
  ctx.save()
  const g = ctx.createRadialGradient(x - radius * 0.3, y - radius * 0.3, 2, x, y, radius)
  g.addColorStop(0, '#ffffff')
  g.addColorStop(0.25, color)
  g.addColorStop(1, color)
  ctx.fillStyle = g
  ctx.beginPath()
  ctx.arc(x, y, radius, 0, Math.PI * 2)
  ctx.fill()
  ctx.strokeStyle = 'rgba(0,0,0,0.15)'
  ctx.lineWidth = 1
  ctx.stroke()
  ctx.restore()
}

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  keys.add(k)
  if (k.startsWith('arrow') || k === ' ') e.preventDefault()
  if (phase.value !== 'playing') {
    if (k === ' ' || k === 'enter') startMatch()
    return
  }
  if (k === 'w') shoot(game.p1)
  if (k === 'arrowup') shoot(game.p2)
}
function onKeyUp(e) {
  keys.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame14Records()
  records.value = store.records
}
function formatDate(iso) {
  const d = new Date(iso)
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}
function idleRender() {
  game = createGame()
  render()
}

onMounted(async () => {
  ctx = canvasRef.value.getContext('2d')
  matchOver = false
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('keyup', onKeyUp)
  try {
    const store = await fetchGame14Store()
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
.game14-view { min-height: 100vh; padding: 24px clamp(16px,4vw,48px) 48px; color: #e9eefb; background: radial-gradient(circle at 50% -10%, #1c2550, #080b1c 60%); font-family: 'Segoe UI', system-ui, sans-serif; }
.topbar { display: flex; align-items: center; gap: 20px; flex-wrap: wrap; margin-bottom: 22px; }
.back-link { color: #9fb0e8; text-decoration: none; font-size: 14px; padding: 8px 14px; border: 1px solid rgba(159,176,232,0.3); border-radius: 999px; transition: 0.2s; }
.back-link:hover { background: rgba(159,176,232,0.12); color: #fff; }
.title-block .eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; color: #46d0ff; text-transform: uppercase; }
.title-block h1 { margin: 2px 0 0; font-size: 26px; background: linear-gradient(90deg,#46d0ff,#ff5d6c); -webkit-background-clip: text; background-clip: text; color: transparent; }
.round-pill { margin-left: auto; padding: 8px 16px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 13px; }
.time-pill { margin-left: auto; padding: 8px 18px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 16px; font-weight: 700; font-variant-numeric: tabular-nums; }
.time-pill.urgent { background: rgba(255,93,108,0.2); border-color: rgba(255,93,108,0.5); color: #ff9eaa; }
.layout { display: grid; grid-template-columns: minmax(0,1fr) 300px; gap: 22px; align-items: start; }
.stage-card { background: rgba(8,11,28,0.6); border: 1px solid rgba(120,150,220,0.16); border-radius: 20px; padding: 16px; box-shadow: 0 24px 60px rgba(0,0,0,0.5); }
.scoreband { display: flex; align-items: center; gap: 16px; margin-bottom: 14px; padding: 0 6px; }
.team { display: flex; align-items: center; gap: 12px; flex: 1; }
.team-2 { justify-content: flex-end; }
.team .dot { width: 14px; height: 14px; border-radius: 50%; }
.team-1 .dot { background: #46d0ff; box-shadow: 0 0 12px #46d0ff; }
.team-2 .dot { background: #ff5d6c; box-shadow: 0 0 12px #ff5d6c; }
.team strong { font-size: 15px; }
.team .pop { font-size: 13px; color: #9fb0e8; font-variant-numeric: tabular-nums; }
.vs { font-size: 13px; font-weight: 800; color: #5d6a96; }
.stage-frame { position: relative; border-radius: 14px; overflow: hidden; }
.game-canvas { display: block; width: 100%; height: auto; border-radius: 14px; }
.overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(5,8,20,0.82); backdrop-filter: blur(4px); }
.overlay-card { text-align: center; max-width: 460px; padding: 32px; }
.overlay-eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; color: #46d0ff; }
.overlay-card h2 { margin: 10px 0 14px; font-size: 26px; }
.winner-text { background: linear-gradient(90deg,#46d0ff,#ff5d6c); -webkit-background-clip: text; background-clip: text; color: transparent; }
.overlay-text { color: #b6c2e0; line-height: 1.7; margin: 0 0 22px; }
.primary-btn { border: none; padding: 12px 30px; border-radius: 999px; font-size: 16px; font-weight: 700; color: #04121a; background: linear-gradient(90deg,#46d0ff,#8de96a); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; box-shadow: 0 10px 26px rgba(70,208,255,0.35); }
.primary-btn:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(70,208,255,0.5); }
.sidebar { display: flex; flex-direction: column; gap: 16px; }
.panel { background: rgba(8,11,28,0.6); border: 1px solid rgba(120,150,220,0.16); border-radius: 18px; padding: 18px; }
.panel .eyebrow { margin: 0 0 12px; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #7585b0; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.controls-grid { display: grid; gap: 10px; }
.ctrl { border-radius: 12px; padding: 12px 14px; }
.ctrl strong { display: block; font-size: 14px; margin-bottom: 6px; }
.ctrl span { font-size: 13px; color: #b6c2e0; }
.ctrl-1 { background: rgba(70,208,255,0.1); border: 1px solid rgba(70,208,255,0.25); }
.ctrl-2 { background: rgba(255,93,108,0.1); border: 1px solid rgba(255,93,108,0.25); }
kbd { background: rgba(255,255,255,0.12); border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 2px 7px; font-size: 12px; font-family: inherit; margin-right: 2px; }
.tips { margin: 0; padding-left: 18px; display: grid; gap: 8px; font-size: 13px; color: #b6c2e0; line-height: 1.5; }
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
