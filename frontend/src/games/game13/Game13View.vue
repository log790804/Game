<template>
  <main class="game13-view">
    <section class="topbar">
      <RouterLink to="/" class="back-link">← 返回遊戲廳</RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 13</p>
        <h1>五子棋</h1>
      </div>
      <div class="round-pill">連成五子者勝</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1" :class="{ active: turn === 'p1' && phase === 'playing' }">
            <span class="stone s1" />
            <strong>玩家 1（黑）</strong>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2" :class="{ active: turn === 'p2' && phase === 'playing' }">
            <strong>玩家 2（白）</strong>
            <span class="stone s2" />
          </div>
        </div>

        <div ref="stageRef" class="stage-frame">
          <canvas ref="canvasRef" class="game-canvas" :width="SIZE" :height="SIZE" />
          <transition name="fade">
            <div v-if="phase !== 'playing'" class="overlay">
              <div class="overlay-card">
                <template v-if="phase === 'intro'">
                  <p class="overlay-eyebrow">楚河漢界</p>
                  <h2>連成五子定勝負</h2>
                  <p class="overlay-text">
                    輪流落子，先在橫、直、斜任一方向連成五子者獲勝。<br>
                    每位玩家有一次悔棋機會。
                  </p>
                  <button class="primary-btn" @click="startGame">開始對弈</button>
                </template>
                <template v-else-if="phase === 'over'">
                  <p class="overlay-eyebrow">對弈結束</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <button class="primary-btn" @click="startGame">再來一局</button>
                </template>
              </div>
            </div>
          </transition>
        </div>
      </div>

      <aside class="sidebar">
        <section class="panel turn-panel" :class="turn">
          <p class="eyebrow">目前回合</p>
          <strong class="turn-name">{{ turn === 'p1' ? '玩家 1（黑）' : '玩家 2（白）' }}</strong>
          <p class="moves-count">已落子 {{ moves.length }} 手</p>
        </section>
        <section class="panel">
          <p class="eyebrow">操作（輪到的玩家）</p>
          <ul class="tips">
            <li><kbd>↑</kbd><kbd>↓</kbd><kbd>←</kbd><kbd>→</kbd> 移動游標</li>
            <li><kbd>空白鍵</kbd> 落子</li>
          </ul>
          <button
            class="undo-btn"
            :disabled="moves.length === 0 || (lastMover && undosLeft[lastMover] === 0)"
            @click="undo"
          >
            悔棋（P1 剩 {{ undosLeft.p1 }}・P2 剩 {{ undosLeft.p2 }}）
          </button>
        </section>
        <section class="panel">
          <div class="panel-head">
            <p class="eyebrow">對戰紀錄</p>
            <button v-if="records.length" class="ghost-btn" @click="onClearRecords">清除</button>
          </div>
          <ul v-if="records.length" class="records">
            <li v-for="(r, i) in records" :key="i">
              <span class="rec-win">{{ r.winner }}</span>
              <span class="rec-score">{{ r.moves }} 手</span>
              <span class="rec-date">{{ formatDate(r.date) }}</span>
            </li>
          </ul>
          <p v-else class="empty">尚無紀錄，對弈結束後自動保存最近 10 局。</p>
        </section>
      </aside>
    </section>
  </main>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { clearGame13Records, fetchGame13Store, saveGame13Record } from './game13Storage'
import { recordGameResult } from '@/data/lobbyScore'

const N = 15
const CELL = 36
const MARGIN = 34
const SIZE = MARGIN * 2 + (N - 1) * CELL

// 像素素材
const G13 = {}
function g13Sprite(name) {
  if (!G13[name]) {
    const img = new Image()
    img.src = `/assets/G13/${name}.png`
    G13[name] = img
  }
  return G13[name]
}
;['board', 'stone-black', 'stone-white', 'ui-win-ring', 'fx-place-1', 'fx-place-2', 'fx-place-3'].forEach(g13Sprite)
function g13ready(img) {
  return img && img.complete && img.naturalWidth > 0
}

const canvasRef = ref(null)
const stageRef = ref(null)
const phase = ref('intro')
const turn = ref('p1')
const resultText = ref('')
const records = ref([])
const moves = ref([])
const undosLeft = reactive({ p1: 1, p2: 1 })

let ctx = null
let rafId = 0
let board = null
let cursor = { r: 7, c: 7 }
let winLine = null
let pulse = 0

const lastMover = computed(() => (moves.value.length ? moves.value[moves.value.length - 1].player : null))

function startGame() {
  board = Array.from({ length: N }, () => Array(N).fill(null))
  moves.value = []
  undosLeft.p1 = 1
  undosLeft.p2 = 1
  turn.value = 'p1'
  cursor = { r: 7, c: 7 }
  winLine = null
  phase.value = 'playing'
  if (!rafId) loop()
}

function place() {
  if (phase.value !== 'playing') return
  const { r, c } = cursor
  if (board[r][c]) return
  board[r][c] = turn.value
  moves.value.push({ r, c, player: turn.value })
  const line = checkWin(r, c, turn.value)
  if (line) {
    winLine = line
    finishGame(turn.value)
    return
  }
  if (moves.value.length >= N * N) {
    finishGame('draw')
    return
  }
  turn.value = turn.value === 'p1' ? 'p2' : 'p1'
}

function undo() {
  if (!moves.value.length) return
  const last = moves.value[moves.value.length - 1]
  if (undosLeft[last.player] === 0) return
  moves.value.pop()
  board[last.r][last.c] = null
  undosLeft[last.player] -= 1
  winLine = null
  turn.value = last.player
  cursor = { r: last.r, c: last.c }
}

const DIRS = [[0, 1], [1, 0], [1, 1], [1, -1]]
function checkWin(r, c, p) {
  for (const [dr, dc] of DIRS) {
    const cells = [[r, c]]
    for (let s = 1; s < 5; s += 1) {
      const nr = r + dr * s
      const nc = c + dc * s
      if (nr < 0 || nc < 0 || nr >= N || nc >= N || board[nr][nc] !== p) break
      cells.push([nr, nc])
    }
    for (let s = 1; s < 5; s += 1) {
      const nr = r - dr * s
      const nc = c - dc * s
      if (nr < 0 || nc < 0 || nr >= N || nc >= N || board[nr][nc] !== p) break
      cells.unshift([nr, nc])
    }
    if (cells.length >= 5) return cells
  }
  return null
}

async function finishGame(result) {
  let winner
  if (result === 'draw') winner = '平手'
  else winner = result === 'p1' ? '玩家 1 獲勝' : '玩家 2 獲勝'
  resultText.value = `⚫ ${winner}`
  // delay overlay so winning line is visible
  setTimeout(() => {
    phase.value = 'over'
  }, 700)
  recordGameResult('/game13', result === 'draw' ? 'draw' : result)
  try {
    const store = await saveGame13Record({ winner, moves: moves.value.length, date: new Date().toISOString() })
    records.value = store.records
  } catch {
    /* ignore */
  }
}

function loop() {
  pulse += 0.05
  render()
  rafId = requestAnimationFrame(loop)
}

function px(i) {
  return MARGIN + i * CELL
}

function render() {
  ctx.imageSmoothingEnabled = false
  const boardImg = g13Sprite('board')
  if (g13ready(boardImg)) {
    ctx.drawImage(boardImg, 0, 0, SIZE, SIZE)
  } else {
    ctx.fillStyle = '#e3b876'
    ctx.fillRect(0, 0, SIZE, SIZE)
    ctx.strokeStyle = 'rgba(60,40,15,0.7)'
    ctx.lineWidth = 1.4
    for (let i = 0; i < N; i += 1) {
      ctx.beginPath()
      ctx.moveTo(px(0), px(i))
      ctx.lineTo(px(N - 1), px(i))
      ctx.stroke()
      ctx.beginPath()
      ctx.moveTo(px(i), px(0))
      ctx.lineTo(px(i), px(N - 1))
      ctx.stroke()
    }
    ctx.fillStyle = 'rgba(60,40,15,0.8)'
    for (const [r, c] of [[3, 3], [3, 11], [11, 3], [11, 11], [7, 7]]) {
      ctx.beginPath()
      ctx.arc(px(c), px(r), 4, 0, Math.PI * 2)
      ctx.fill()
    }
  }
  // stones
  for (let r = 0; r < N; r += 1) {
    for (let c = 0; c < N; c += 1) {
      if (board[r][c]) drawStone(px(c), px(r), board[r][c])
    }
  }
  // last move marker
  if (moves.value.length && !winLine) {
    const last = moves.value[moves.value.length - 1]
    ctx.strokeStyle = '#ff5d3a'
    ctx.lineWidth = 2.5
    ctx.beginPath()
    ctx.arc(px(last.c), px(last.r), 6, 0, Math.PI * 2)
    ctx.stroke()
  }
  // win line
  if (winLine) {
    const a = winLine[0]
    const b = winLine[winLine.length - 1]
    ctx.strokeStyle = '#ff3a3a'
    ctx.lineWidth = 5
    ctx.lineCap = 'round'
    ctx.shadowColor = '#ff3a3a'
    ctx.shadowBlur = 14
    ctx.beginPath()
    ctx.moveTo(px(a[1]), px(a[0]))
    ctx.lineTo(px(b[1]), px(b[0]))
    ctx.stroke()
    ctx.shadowBlur = 0
  }
  // cursor
  if (phase.value === 'playing') {
    const glow = 0.5 + 0.5 * Math.sin(pulse)
    ctx.strokeStyle = turn.value === 'p1' ? `rgba(30,30,40,${0.5 + glow * 0.4})` : `rgba(255,255,255,${0.6 + glow * 0.4})`
    ctx.lineWidth = 3
    const cx = px(cursor.c)
    const cy = px(cursor.r)
    ctx.strokeRect(cx - 16, cy - 16, 32, 32)
  }
}

function drawStone(x, y, p) {
  const img = g13Sprite(p === 'p1' ? 'stone-black' : 'stone-white')
  if (g13ready(img)) {
    const sz = 32
    ctx.drawImage(img, x - sz / 2, y - sz / 2, sz, sz)
    return
  }
  ctx.save()
  ctx.shadowColor = 'rgba(0,0,0,0.4)'
  ctx.shadowBlur = 6
  ctx.shadowOffsetY = 2
  const g = ctx.createRadialGradient(x - 5, y - 5, 2, x, y, 15)
  if (p === 'p1') {
    g.addColorStop(0, '#5a6470')
    g.addColorStop(1, '#10141c')
  } else {
    g.addColorStop(0, '#ffffff')
    g.addColorStop(1, '#c8cdd6')
  }
  ctx.fillStyle = g
  ctx.beginPath()
  ctx.arc(x, y, 15, 0, Math.PI * 2)
  ctx.fill()
  ctx.restore()
}

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  if ((k === 'enter' || k === ' ') && phase.value !== 'playing') {
    if (k === 'enter' || phase.value === 'intro' || phase.value === 'over') startGame()
    e.preventDefault()
    return
  }
  if (phase.value !== 'playing') return
  if (k === 'arrowup') cursor.r = Math.max(0, cursor.r - 1)
  else if (k === 'arrowdown') cursor.r = Math.min(N - 1, cursor.r + 1)
  else if (k === 'arrowleft') cursor.c = Math.max(0, cursor.c - 1)
  else if (k === 'arrowright') cursor.c = Math.min(N - 1, cursor.c + 1)
  else if (k === ' ') place()
  else if (k === 'z' || k === 'backspace') undo()
  if (k.startsWith('arrow') || k === ' ' || k === 'backspace') e.preventDefault()
}

async function onClearRecords() {
  const store = await clearGame13Records()
  records.value = store.records
}
function formatDate(iso) {
  const d = new Date(iso)
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

onMounted(async () => {
  ctx = canvasRef.value.getContext('2d')
  window.addEventListener('keydown', onKeyDown)
  try {
    const store = await fetchGame13Store()
    records.value = store.records
  } catch {
    /* ignore */
  }
  board = Array.from({ length: N }, () => Array(N).fill(null))
  loop()
})
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeyDown)
  if (rafId) cancelAnimationFrame(rafId)
})
</script>

<style scoped>
.game13-view { min-height: 100vh; padding: 24px clamp(16px,4vw,48px) 48px; color: #efe6d6; background: #2a1d12 url('/assets/G13/bg-wood-sakura.png') center / cover fixed no-repeat; image-rendering: pixelated; font-family: 'Segoe UI', system-ui, sans-serif; }
.topbar { display: flex; align-items: center; gap: 20px; flex-wrap: wrap; margin-bottom: 22px; }
.back-link { color: #cbb084; text-decoration: none; font-size: 14px; padding: 8px 14px; border: 1px solid rgba(203,176,132,0.3); border-radius: 999px; transition: 0.2s; }
.back-link:hover { background: rgba(203,176,132,0.12); color: #fff; }
.title-block .eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; color: #e0b154; text-transform: uppercase; }
.title-block h1 { margin: 2px 0 0; font-size: 26px; color: #f5e7cf; }
.round-pill { margin-left: auto; padding: 8px 16px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 13px; }
.layout { display: grid; grid-template-columns: minmax(0,1fr) 300px; gap: 22px; align-items: start; }
.stage-card { background: rgba(20,16,10,0.6); border: 1px solid rgba(203,176,132,0.18); border-radius: 20px; padding: 16px; box-shadow: 0 24px 60px rgba(0,0,0,0.5); }
.scoreband { display: flex; align-items: center; gap: 16px; margin-bottom: 14px; padding: 0 6px; }
.team { display: flex; align-items: center; gap: 10px; flex: 1; padding: 6px 10px; border-radius: 12px; transition: 0.2s; }
.team-2 { justify-content: flex-end; }
.team.active { background: rgba(224,177,84,0.16); box-shadow: 0 0 0 1px rgba(224,177,84,0.4) inset; }
.team strong { font-size: 14px; }
.stone { width: 18px; height: 18px; border-radius: 50%; }
.stone.s1 { background: radial-gradient(circle at 35% 35%, #5a6470, #10141c); }
.stone.s2 { background: radial-gradient(circle at 35% 35%, #fff, #c8cdd6); }
.vs { font-size: 13px; font-weight: 800; color: #8a7350; }
.stage-frame { position: relative; border-radius: 14px; overflow: hidden; max-width: 580px; margin: 0 auto; }
.game-canvas { display: block; width: 100%; height: auto; border-radius: 14px; }
.overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(20,14,6,0.84); backdrop-filter: blur(4px); }
.overlay-card { text-align: center; max-width: 420px; padding: 30px; }
.overlay-eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; color: #e0b154; }
.overlay-card h2 { margin: 10px 0 14px; font-size: 28px; }
.winner-text { color: #ffd98a; }
.overlay-text { color: #d3c3a4; line-height: 1.7; margin: 0 0 22px; }
.primary-btn { border: none; padding: 12px 30px; border-radius: 999px; font-size: 16px; font-weight: 700; color: #2a1d08; background: linear-gradient(90deg,#f0c878,#e0a94e); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; box-shadow: 0 10px 26px rgba(224,169,78,0.4); }
.primary-btn:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(224,169,78,0.55); }
.sidebar { display: flex; flex-direction: column; gap: 16px; }
.panel { background: rgba(20,16,10,0.6); border: 1px solid rgba(203,176,132,0.18); border-radius: 18px; padding: 18px; }
.panel .eyebrow { margin: 0 0 10px; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #9c845c; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.turn-panel { border-width: 2px; }
.turn-panel.p1 { border-color: rgba(120,130,150,0.5); }
.turn-panel.p2 { border-color: rgba(255,255,255,0.4); }
.turn-name { font-size: 16px; }
.moves-count { margin: 8px 0 0; font-size: 13px; color: #b8a47e; }
.tips { margin: 0 0 14px; padding: 0; list-style: none; display: grid; gap: 8px; font-size: 13px; color: #d3c3a4; }
kbd { background: rgba(255,255,255,0.12); border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 2px 7px; font-size: 12px; font-family: inherit; margin-right: 3px; }
.undo-btn { width: 100%; border: 1px solid rgba(224,177,84,0.4); background: rgba(224,177,84,0.12); color: #f0d49a; border-radius: 12px; padding: 10px; font-size: 13px; font-weight: 700; cursor: pointer; }
.undo-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.undo-btn:not(:disabled):hover { background: rgba(224,177,84,0.22); }
.records { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.records li { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: center; font-size: 12px; padding: 8px 10px; border-radius: 10px; background: rgba(255,255,255,0.04); }
.rec-win { font-weight: 700; color: #ffd98a; }
.rec-score { color: #cbb084; }
.rec-date { color: #8a7350; }
.empty { font-size: 13px; color: #9c845c; line-height: 1.6; margin: 0; }
.ghost-btn { background: none; border: 1px solid rgba(203,176,132,0.3); color: #cbb084; border-radius: 999px; padding: 4px 12px; font-size: 12px; cursor: pointer; }
.ghost-btn:hover { background: rgba(203,176,132,0.12); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } }
</style>
