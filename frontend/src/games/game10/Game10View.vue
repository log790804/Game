<template>
  <main class="game10-view">
    <section class="topbar">
      <RouterLink
        to="/"
        class="back-link"
      >
        ← 返回遊戲廳
      </RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 10</p>
        <h1>俄羅斯方塊對戰</h1>
      </div>
      <div class="round-pill">消行送垃圾 · 堆爆即敗</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <strong>玩家 1</strong>
            <span class="lines">消除 {{ hud.p1Lines }} 行</span>
            <span class="garbage" :class="{ on: hud.p1Garbage > 0 }">⚠ {{ hud.p1Garbage }}</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="garbage" :class="{ on: hud.p2Garbage > 0 }">⚠ {{ hud.p2Garbage }}</span>
            <span class="lines">消除 {{ hud.p2Lines }} 行</span>
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
                  <p class="overlay-eyebrow">堆疊對決</p>
                  <h2>消行壓垮對手</h2>
                  <p class="overlay-text">
                    一次消除多行會把垃圾行送進對方場地。<br>
                    清行可抵銷對方送來的垃圾，撐到對手堆爆者獲勝。
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
                  <p class="overlay-text">
                    玩家 1 消除 {{ hud.p1Lines }} 行 · 玩家 2 消除 {{ hud.p2Lines }} 行
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
              <span><kbd>A</kbd><kbd>D</kbd> 左右 · <kbd>S</kbd> 下降</span>
              <span><kbd>W</kbd> 旋轉 · <kbd>Q</kbd> 瞬降</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span><kbd>←</kbd><kbd>→</kbd> 左右 · <kbd>↓</kbd> 下降</span>
              <span><kbd>↑</kbd> 旋轉 · <kbd>/</kbd> 瞬降</span>
            </div>
          </div>
        </section>

        <section class="panel">
          <p class="eyebrow">垃圾行規則</p>
          <ul class="legend">
            <li><span class="num">2</span> 行 → 送出 1 行</li>
            <li><span class="num">3</span> 行 → 送出 2 行</li>
            <li><span class="num">4</span> 行（Tetris）→ 送出 4 行</li>
          </ul>
          <p class="hint">清行會先抵銷自己待落的垃圾；未清行落子時，待落垃圾才會頂上來。</p>
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
              <span class="rec-score">{{ r.lineP1 }} : {{ r.lineP2 }}</span>
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
  clearGame10Records,
  fetchGame10Store,
  saveGame10Record
} from './game10Storage'
import { recordGameResult } from '@/data/lobbyScore'

const COLS = 10
const ROWS = 20
const CELL = 26
const BOARD_W = COLS * CELL
const BOARD_H = ROWS * CELL
const BOARD_Y = 84
const BOARD1_X = 60
const CANVAS_W = 760
const CANVAS_H = BOARD_Y + BOARD_H + 16
const BOARD2_X = CANVAS_W - 60 - BOARD_W

const SHAPES = {
  I: [[0, 0, 0, 0], [1, 1, 1, 1], [0, 0, 0, 0], [0, 0, 0, 0]],
  O: [[1, 1], [1, 1]],
  T: [[0, 1, 0], [1, 1, 1], [0, 0, 0]],
  S: [[0, 1, 1], [1, 1, 0], [0, 0, 0]],
  Z: [[1, 1, 0], [0, 1, 1], [0, 0, 0]],
  J: [[1, 0, 0], [1, 1, 1], [0, 0, 0]],
  L: [[0, 0, 1], [1, 1, 1], [0, 0, 0]]
}
const COLORS = {
  I: '#36d6e6',
  O: '#ffd23f',
  T: '#b56cff',
  S: '#48e07a',
  Z: '#ff5d6c',
  J: '#4d8bff',
  L: '#ff9f43',
  G: '#5a6478'
}
const SEND_TABLE = { 0: 0, 1: 0, 2: 1, 3: 2, 4: 4 }

// 像素素材
const G10 = {}
function g10Sprite(name) {
  if (!G10[name]) {
    const img = new Image()
    img.src = `/assets/G10/${name}.png`
    G10[name] = img
  }
  return G10[name]
}
const BLOCK_SPRITE = { I: 'block-i', O: 'block-o', T: 'block-t', S: 'block-s', Z: 'block-z', J: 'block-j', L: 'block-l', G: 'block-garbage' }
;['bg-arena', 'block-i', 'block-o', 'block-t', 'block-s', 'block-z', 'block-j', 'block-l', 'block-garbage', 'fx-line-clear-1', 'fx-line-clear-2', 'fx-line-clear-3'].forEach(g10Sprite)
function g10ready(img) {
  return img && img.complete && img.naturalWidth > 0
}

const canvasRef = ref(null)
const stageRef = ref(null)

const phase = ref('intro')
const resultText = ref('')
const records = ref([])
const hud = reactive({
  p1Lines: 0,
  p2Lines: 0,
  p1Garbage: 0,
  p2Garbage: 0
})

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null
const keys = new Set()

function newBag() {
  const bag = ['I', 'O', 'T', 'S', 'Z', 'J', 'L']
  for (let i = bag.length - 1; i > 0; i -= 1) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[bag[i], bag[j]] = [bag[j], bag[i]]
  }
  return bag
}

function makeSide() {
  const side = {
    board: Array.from({ length: ROWS }, () => Array(COLS).fill(null)),
    bag: newBag(),
    piece: null,
    next: null,
    dropTimer: 0,
    dropInterval: 800,
    lines: 0,
    pendingGarbage: 0,
    alive: true,
    softDrop: false,
    flash: [],
    lockFlash: 0
  }
  side.next = side.bag.pop()
  return side
}

function createGame() {
  const g = { p1: makeSide(), p2: makeSide() }
  spawnPiece(g.p1)
  spawnPiece(g.p2)
  return g
}

function nextType(side) {
  if (!side.bag.length) side.bag = newBag()
  return side.bag.pop()
}

function spawnPiece(side) {
  const type = side.next
  side.next = nextType(side)
  const matrix = SHAPES[type].map((row) => row.slice())
  const x = Math.floor((COLS - matrix[0].length) / 2)
  const y = type === 'I' ? -1 : 0
  const piece = { type, matrix, x, y }
  if (collides(side, matrix, x, y)) {
    side.alive = false
    side.piece = null
    return false
  }
  side.piece = piece
  side.dropTimer = 0
  return true
}

function collides(side, matrix, px, py) {
  for (let r = 0; r < matrix.length; r += 1) {
    for (let c = 0; c < matrix[r].length; c += 1) {
      if (!matrix[r][c]) continue
      const x = px + c
      const y = py + r
      if (x < 0 || x >= COLS || y >= ROWS) return true
      if (y >= 0 && side.board[y][x]) return true
    }
  }
  return false
}

function rotateCW(matrix) {
  const n = matrix.length
  const m = matrix[0].length
  const out = Array.from({ length: m }, () => Array(n).fill(0))
  for (let r = 0; r < n; r += 1) {
    for (let c = 0; c < m; c += 1) {
      out[c][n - 1 - r] = matrix[r][c]
    }
  }
  return out
}

function tryRotate(side) {
  if (!side.piece) return
  const rotated = rotateCW(side.piece.matrix)
  const kicks = [0, -1, 1, -2, 2]
  for (const dx of kicks) {
    if (!collides(side, rotated, side.piece.x + dx, side.piece.y)) {
      side.piece.matrix = rotated
      side.piece.x += dx
      return
    }
  }
}

function move(side, dx) {
  if (!side.piece) return
  if (!collides(side, side.piece.matrix, side.piece.x + dx, side.piece.y)) {
    side.piece.x += dx
  }
}

function stepDown(side) {
  if (!side.piece) return false
  if (!collides(side, side.piece.matrix, side.piece.x, side.piece.y + 1)) {
    side.piece.y += 1
    return true
  }
  lockPiece(side)
  return false
}

function hardDrop(side) {
  if (!side.piece) return
  while (!collides(side, side.piece.matrix, side.piece.x, side.piece.y + 1)) {
    side.piece.y += 1
  }
  lockPiece(side)
}

function lockPiece(side) {
  const { matrix, x, y, type } = side.piece
  for (let r = 0; r < matrix.length; r += 1) {
    for (let c = 0; c < matrix[r].length; c += 1) {
      if (matrix[r][c] && y + r >= 0) {
        side.board[y + r][x + c] = type
      }
    }
  }
  side.lockFlash = 0.4
  const cleared = clearLines(side)
  const other = side === game.p1 ? game.p2 : game.p1

  if (cleared > 0) {
    side.lines += cleared
    let sent = SEND_TABLE[cleared] || 0
    // cancel own pending garbage first
    if (side.pendingGarbage > 0) {
      const cancel = Math.min(side.pendingGarbage, sent)
      side.pendingGarbage -= cancel
      sent -= cancel
    }
    if (sent > 0) other.pendingGarbage += sent
  } else if (side.pendingGarbage > 0) {
    applyGarbage(side, side.pendingGarbage)
    side.pendingGarbage = 0
  }

  side.dropInterval = Math.max(140, 800 - Math.floor(side.lines / 8) * 65)
  syncHud()

  if (!spawnPiece(side)) {
    endMatch()
  }
}

function clearLines(side) {
  let cleared = 0
  for (let r = ROWS - 1; r >= 0; r -= 1) {
    if (side.board[r].every((cell) => cell)) {
      side.board.splice(r, 1)
      side.board.unshift(Array(COLS).fill(null))
      side.flash.push({ row: r, life: 1 })
      cleared += 1
      r += 1
    }
  }
  return cleared
}

function applyGarbage(side, n) {
  for (let i = 0; i < n; i += 1) {
    const top = side.board.shift()
    // if top row had blocks, they overflow (lost)
    void top
    const hole = Math.floor(Math.random() * COLS)
    const row = Array(COLS).fill('G')
    row[hole] = null
    side.board.push(row)
  }
  // if current piece now overlaps, push it up
  if (side.piece && collides(side, side.piece.matrix, side.piece.x, side.piece.y)) {
    side.piece.y -= n
  }
}

function syncHud() {
  hud.p1Lines = game.p1.lines
  hud.p2Lines = game.p2.lines
  hud.p1Garbage = game.p1.pendingGarbage
  hud.p2Garbage = game.p2.pendingGarbage
}

function startMatch() {
  game = createGame()
  syncHud()
  phase.value = 'playing'
  lastFrame = performance.now()
  loop(lastFrame)
}

async function endMatch() {
  cancelAnimationFrame(rafId)
  rafId = 0
  let winner
  if (!game.p1.alive && !game.p2.alive) winner = '平手'
  else if (!game.p1.alive) winner = '玩家 2 獲勝'
  else winner = '玩家 1 獲勝'
  resultText.value = `🧱 ${winner}`
  phase.value = 'matchover'
  recordGameResult(
    '/game10',
    !game.p1.alive && !game.p2.alive ? 'draw' : !game.p1.alive ? 'p2' : 'p1'
  )
  try {
    const store = await saveGame10Record({
      winner,
      lineP1: game.p1.lines,
      lineP2: game.p2.lines,
      date: new Date().toISOString()
    })
    records.value = store.records
  } catch {
    /* ignore */
  }
}

function updateSide(side, dt) {
  if (!side.alive || !side.piece) return
  const interval = side.softDrop ? Math.min(60, side.dropInterval) : side.dropInterval
  side.dropTimer += dt
  while (side.dropTimer >= interval) {
    side.dropTimer -= interval
    if (!stepDown(side)) break
  }
  side.lockFlash = Math.max(0, side.lockFlash - dt / 400)
  for (const f of side.flash) f.life -= dt / 300
  side.flash = side.flash.filter((f) => f.life > 0)
}

function update(dt) {
  updateSide(game.p1, dt)
  updateSide(game.p2, dt)
}

function loop(now) {
  const dt = Math.min(50, now - lastFrame)
  lastFrame = now
  if (phase.value === 'playing') {
    update(dt)
    render()
    rafId = requestAnimationFrame(loop)
  }
}

function render() {
  ctx.imageSmoothingEnabled = false
  const bgImg = g10Sprite('bg-arena')
  if (g10ready(bgImg)) {
    ctx.drawImage(bgImg, 0, 0, CANVAS_W, CANVAS_H)
  } else {
    ctx.fillStyle = '#0a0d1c'
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  }
  drawBoard(game.p1, BOARD1_X, '玩家 1', '#36d6e6')
  drawBoard(game.p2, BOARD2_X, '玩家 2', '#ff7ab0')
}

function drawBoard(side, ox, label, accent) {
  // label + next preview
  ctx.fillStyle = accent
  ctx.font = 'bold 16px system-ui, sans-serif'
  ctx.textAlign = 'left'
  ctx.textBaseline = 'middle'
  ctx.fillText(label, ox, 26)

  drawNext(side, ox + BOARD_W - 86, 8, accent)

  // playfield bg（半透明，露出競技場背景）
  ctx.fillStyle = 'rgba(14, 19, 48, 0.72)'
  ctx.fillRect(ox, BOARD_Y, BOARD_W, BOARD_H)
  // grid
  ctx.strokeStyle = 'rgba(120,140,200,0.07)'
  ctx.lineWidth = 1
  for (let c = 0; c <= COLS; c += 1) {
    ctx.beginPath()
    ctx.moveTo(ox + c * CELL, BOARD_Y)
    ctx.lineTo(ox + c * CELL, BOARD_Y + BOARD_H)
    ctx.stroke()
  }
  for (let r = 0; r <= ROWS; r += 1) {
    ctx.beginPath()
    ctx.moveTo(ox, BOARD_Y + r * CELL)
    ctx.lineTo(ox + BOARD_W, BOARD_Y + r * CELL)
    ctx.stroke()
  }

  // settled blocks
  for (let r = 0; r < ROWS; r += 1) {
    for (let c = 0; c < COLS; c += 1) {
      if (side.board[r][c]) drawCell(ox + c * CELL, BOARD_Y + r * CELL, side.board[r][c])
    }
  }

  // ghost + piece
  if (side.piece && side.alive) {
    let gy = side.piece.y
    while (!collides(side, side.piece.matrix, side.piece.x, gy + 1)) gy += 1
    drawPiece(side.piece, ox, gy, side.piece.type, true)
    drawPiece(side.piece, ox, side.piece.y, side.piece.type, false)
  }

  // clear flash
  for (const f of side.flash) {
    ctx.fillStyle = `rgba(255,255,255,${f.life * 0.6})`
    ctx.fillRect(ox, BOARD_Y + f.row * CELL, BOARD_W, CELL)
  }

  // border
  ctx.strokeStyle = side.alive ? accent : '#5a6478'
  ctx.lineWidth = 3
  ctx.strokeRect(ox - 1.5, BOARD_Y - 1.5, BOARD_W + 3, BOARD_H + 3)

  // pending garbage bar
  if (side.pendingGarbage > 0) {
    const barH = Math.min(BOARD_H, side.pendingGarbage * CELL)
    ctx.fillStyle = 'rgba(255,93,108,0.85)'
    ctx.fillRect(ox - 10, BOARD_Y + BOARD_H - barH, 6, barH)
  }

  if (!side.alive) {
    ctx.fillStyle = 'rgba(8,10,25,0.7)'
    ctx.fillRect(ox, BOARD_Y, BOARD_W, BOARD_H)
    ctx.fillStyle = '#ff5d6c'
    ctx.font = 'bold 26px system-ui, sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText('堆爆', ox + BOARD_W / 2, BOARD_Y + BOARD_H / 2)
  }
}

function drawNext(side, x, y, accent) {
  ctx.fillStyle = 'rgba(255,255,255,0.04)'
  ctx.fillRect(x, y, 78, 54)
  ctx.strokeStyle = 'rgba(255,255,255,0.12)'
  ctx.lineWidth = 1
  ctx.strokeRect(x, y, 78, 54)
  const m = SHAPES[side.next]
  const s = 13
  const w = m[0].length * s
  const h = m.length * s
  const px = x + (78 - w) / 2
  const py = y + (54 - h) / 2
  for (let r = 0; r < m.length; r += 1) {
    for (let c = 0; c < m[r].length; c += 1) {
      if (m[r][c]) drawCellSized(px + c * s, py + r * s, s, side.next)
    }
  }
  void accent
}

function drawPiece(piece, ox, py, type, ghost) {
  const m = piece.matrix
  for (let r = 0; r < m.length; r += 1) {
    for (let c = 0; c < m[r].length; c += 1) {
      if (!m[r][c]) continue
      const y = py + r
      if (y < 0) continue
      const x = piece.x + c
      if (ghost) {
        ctx.strokeStyle = COLORS[type] || '#fff'
        ctx.globalAlpha = 0.4
        ctx.lineWidth = 2
        ctx.strokeRect(ox + x * CELL + 2, BOARD_Y + y * CELL + 2, CELL - 4, CELL - 4)
        ctx.globalAlpha = 1
      } else {
        drawCell(ox + x * CELL, BOARD_Y + y * CELL, type)
      }
    }
  }
}

function drawCell(x, y, type) {
  drawCellSized(x, y, CELL, type)
}

function drawCellSized(x, y, size, type) {
  const img = g10Sprite(BLOCK_SPRITE[type])
  if (g10ready(img)) {
    ctx.drawImage(img, x, y, size, size)
    return
  }
  const color = COLORS[type] || '#888'
  ctx.fillStyle = color
  ctx.fillRect(x + 1, y + 1, size - 2, size - 2)
  ctx.fillStyle = 'rgba(255,255,255,0.28)'
  ctx.fillRect(x + 1, y + 1, size - 2, 3)
  ctx.fillRect(x + 1, y + 1, 3, size - 2)
  ctx.fillStyle = 'rgba(0,0,0,0.28)'
  ctx.fillRect(x + 1, y + size - 4, size - 2, 3)
  ctx.fillRect(x + size - 4, y + 1, 3, size - 2)
}

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  if ((k === ' ' || k === 'enter') && phase.value !== 'playing') {
    startMatch()
    return
  }
  if (phase.value !== 'playing') return
  if (k.startsWith('arrow') || k === '/') e.preventDefault()

  const p1 = game.p1
  const p2 = game.p2
  if (keys.has(k)) {
    // allow repeat only for movement via OS repeat; ignore re-trigger for rotate/drop
  }

  switch (k) {
    case 'a': if (p1.alive) move(p1, -1); break
    case 'd': if (p1.alive) move(p1, 1); break
    case 'w': if (p1.alive && !keys.has(k)) tryRotate(p1); break
    case 's': p1.softDrop = true; break
    case 'q': if (p1.alive && !keys.has(k)) hardDrop(p1); break
    case 'arrowleft': if (p2.alive) move(p2, -1); break
    case 'arrowright': if (p2.alive) move(p2, 1); break
    case 'arrowup': if (p2.alive && !keys.has(k)) tryRotate(p2); break
    case 'arrowdown': p2.softDrop = true; break
    case '/': if (p2.alive && !keys.has(k)) hardDrop(p2); break
    default: break
  }
  keys.add(k)
}

function onKeyUp(e) {
  const k = e.key.toLowerCase()
  keys.delete(k)
  if (!game) return
  if (k === 's') game.p1.softDrop = false
  if (k === 'arrowdown') game.p2.softDrop = false
}

async function onClearRecords() {
  const store = await clearGame10Records()
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
  window.addEventListener('keyup', onKeyUp)
  try {
    const store = await fetchGame10Store()
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
.game10-view {
  min-height: 100vh;
  padding: 24px clamp(16px, 4vw, 48px) 48px;
  color: #e8ecff;
  background: radial-gradient(circle at 50% -10%, #1a1f44, #07091a 60%);
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
  color: #9aa8e8;
  text-decoration: none;
  font-size: 14px;
  padding: 8px 14px;
  border: 1px solid rgba(154, 168, 232, 0.3);
  border-radius: 999px;
  transition: 0.2s;
}
.back-link:hover {
  background: rgba(154, 168, 232, 0.12);
  color: #fff;
}
.title-block .eyebrow {
  margin: 0;
  font-size: 12px;
  letter-spacing: 3px;
  color: #36d6e6;
  text-transform: uppercase;
}
.title-block h1 {
  margin: 2px 0 0;
  font-size: 26px;
  background: linear-gradient(90deg, #36d6e6, #ff7ab0);
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
  background: rgba(7, 9, 26, 0.6);
  border: 1px solid rgba(120, 140, 220, 0.18);
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
.team-1 strong {
  color: #36d6e6;
}
.team-2 strong {
  color: #ff7ab0;
}
.team .lines {
  font-size: 13px;
  color: #aab5e0;
  font-variant-numeric: tabular-nums;
}
.team .garbage {
  font-size: 13px;
  color: #5d6796;
  font-weight: 700;
}
.team .garbage.on {
  color: #ff5d6c;
}
.vs {
  font-size: 13px;
  font-weight: 800;
  color: #5d6796;
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
  background: rgba(4, 6, 16, 0.84);
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
  color: #36d6e6;
}
.overlay-card h2 {
  margin: 10px 0 14px;
  font-size: 28px;
}
.winner-text {
  background: linear-gradient(90deg, #36d6e6, #ff7ab0);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.overlay-text {
  color: #b4bee8;
  line-height: 1.7;
  margin: 0 0 22px;
}
.primary-btn {
  border: none;
  padding: 12px 30px;
  border-radius: 999px;
  font-size: 16px;
  font-weight: 700;
  color: #04121a;
  background: linear-gradient(90deg, #36d6e6, #4d8bff);
  cursor: pointer;
  transition: transform 0.15s, box-shadow 0.15s;
  box-shadow: 0 10px 26px rgba(54, 214, 230, 0.35);
}
.primary-btn:hover {
  transform: translateY(-2px);
  box-shadow: 0 14px 32px rgba(54, 214, 230, 0.5);
}
.sidebar {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.panel {
  background: rgba(7, 9, 26, 0.6);
  border: 1px solid rgba(120, 140, 220, 0.18);
  border-radius: 18px;
  padding: 18px;
}
.panel .eyebrow {
  margin: 0 0 12px;
  font-size: 11px;
  letter-spacing: 2px;
  text-transform: uppercase;
  color: #6f7cab;
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
  display: block;
  font-size: 13px;
  color: #b4bee8;
  margin-bottom: 4px;
}
.ctrl-1 {
  background: rgba(54, 214, 230, 0.1);
  border: 1px solid rgba(54, 214, 230, 0.25);
}
.ctrl-2 {
  background: rgba(255, 122, 176, 0.1);
  border: 1px solid rgba(255, 122, 176, 0.25);
}
kbd {
  background: rgba(255, 255, 255, 0.12);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 6px;
  padding: 2px 7px;
  font-size: 12px;
  font-family: inherit;
  margin-right: 2px;
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
  color: #c0caec;
}
.num {
  width: 24px;
  height: 24px;
  border-radius: 7px;
  display: grid;
  place-items: center;
  font-size: 13px;
  font-weight: 800;
  color: #04121a;
  background: #36d6e6;
  flex-shrink: 0;
}
.hint {
  font-size: 12px;
  color: #6f7cab;
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
  color: #36d6e6;
}
.rec-score {
  color: #aab5e0;
}
.rec-date {
  color: #5d6796;
}
.empty {
  font-size: 13px;
  color: #6f7cab;
  line-height: 1.6;
  margin: 0;
}
.ghost-btn {
  background: none;
  border: 1px solid rgba(154, 168, 232, 0.3);
  color: #9aa8e8;
  border-radius: 999px;
  padding: 4px 12px;
  font-size: 12px;
  cursor: pointer;
}
.ghost-btn:hover {
  background: rgba(154, 168, 232, 0.12);
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
