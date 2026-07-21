<template>
  <main class="game18-view">
    <section class="topbar">
      <RouterLink to="/" class="back-link">← 返回遊戲廳</RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 18</p>
        <h1>拔槍反應對決</h1>
      </div>
      <div class="round-pill">先贏 {{ ROUNDS_TO_WIN }} 回合者勝</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <span class="dot" />
            <strong>玩家 1</strong>
            <span class="pips"><i v-for="n in ROUNDS_TO_WIN" :key="n" :class="{ on: roundWins.p1 >= n }" /></span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="pips"><i v-for="n in ROUNDS_TO_WIN" :key="n" :class="{ on: roundWins.p2 >= n }" /></span>
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
                  <p class="overlay-eyebrow">一觸即發</p>
                  <h2>看到「開槍！」搶先按鍵</h2>
                  <p class="overlay-text">
                    螢幕變綠並顯示「開槍！」的瞬間，最快按下自己按鍵者贏得回合。<br>
                    提前出手即視為偷跑，回合直接送給對手。小心假信號！
                  </p>
                  <button class="primary-btn" @click="startMatch">開始對決</button>
                </template>
                <template v-else-if="phase === 'matchover'">
                  <p class="overlay-eyebrow">對決結束</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <p class="overlay-text">回合數 {{ roundWins.p1 }} : {{ roundWins.p2 }}</p>
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
              <span>按 <kbd>F</kbd> 拔槍</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span>按 <kbd>J</kbd> 拔槍</span>
            </div>
          </div>
        </section>
        <section class="panel">
          <p class="eyebrow">規則</p>
          <ul class="tips">
            <li>只有在綠色「開槍！」出現後按下才算數。</li>
            <li>信號前按下＝偷跑，回合判給對手。</li>
            <li>過程會閃出紅色假信號，別被騙了。</li>
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
import { clearGame18Records, fetchGame18Store, saveGame18Record } from './game18Storage'
import { recordGameResult } from '@/data/lobbyScore'
import { assetUrl } from '@/utils/assetUrl'

const CANVAS_W = 880
const CANVAS_H = 460
const ROUNDS_TO_WIN = 3

// 像素素材
const G18 = {}
function g18Sprite(name) {
  if (!G18[name]) {
    const img = new Image()
    img.src = assetUrl(`/assets/G18/${name}.png`)
    G18[name] = img
  }
  return G18[name]
}
;['bg-sunset-desert', 'cowboy-p1-ready-r', 'cowboy-p1-shoot-r', 'cowboy-p1-dead-r', 'cowboy-p2-ready-l', 'cowboy-p2-shoot-l', 'cowboy-p2-dead-l', 'txt-ready', 'txt-draw', 'ui-signal'].forEach(g18Sprite)
function g18ready(img) {
  return img && img.complete && img.naturalWidth > 0
}
function g18Draw(name, cx, bottomY, w) {
  const img = g18Sprite(name)
  if (!g18ready(img)) return
  const h = w * (img.naturalHeight / img.naturalWidth)
  ctx.imageSmoothingEnabled = false
  ctx.drawImage(img, cx - w / 2, bottomY - h, w, h)
}

const canvasRef = ref(null)
const stageRef = ref(null)
const phase = ref('intro')
const roundWins = reactive({ p1: 0, p2: 0 })
const resultText = ref('')
const records = ref([])

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null

function createRound() {
  const readyDuration = 1500 + Math.random() * 3000
  const decoys = []
  const decoyCount = Math.floor(Math.random() * 3)
  for (let i = 0; i < decoyCount; i += 1) {
    decoys.push(400 + Math.random() * (readyDuration - 600))
  }
  decoys.sort((a, b) => a - b)
  return {
    state: 'ready',
    elapsed: 0,
    readyDuration,
    decoys,
    decoyIndex: 0,
    goAt: 0,
    flash: null,
    reaction: 0,
    roundWinner: null,
    reason: '',
    resultTimer: 0
  }
}

function startMatch() {
  roundWins.p1 = 0
  roundWins.p2 = 0
  beginRound()
}
function beginRound() {
  game = createRound()
  phase.value = 'playing'
  lastFrame = performance.now()
  if (!rafId) loop(lastFrame)
}

function press(player) {
  if (!game) return
  if (game.state === 'ready') {
    // false start
    endRound(player === 'p1' ? 'p2' : 'p1', `${player === 'p1' ? '玩家 1' : '玩家 2'} 偷跑！`, 0)
  } else if (game.state === 'go') {
    const reaction = Math.round(performance.now() - game.goAt)
    endRound(player, `${player === 'p1' ? '玩家 1' : '玩家 2'} 拔槍！`, reaction)
  }
}

function endRound(winner, reason, reaction) {
  if (game.state === 'done') return
  game.state = 'done'
  game.roundWinner = winner
  game.reason = reason
  game.reaction = reaction
  game.resultTimer = 1700
  roundWins[winner] += 1
  game.flash = { color: winner === 'p1' ? '#3affd0' : '#ff9ec8', ttl: 400 }
}

function update(dt, now) {
  if (game.state === 'ready') {
    game.elapsed += dt
    // decoys
    while (game.decoyIndex < game.decoys.length && game.elapsed >= game.decoys[game.decoyIndex]) {
      game.decoyIndex += 1
      game.flash = { color: '#ff5d4a', ttl: 220, decoy: true }
    }
    if (game.elapsed >= game.readyDuration) {
      game.state = 'go'
      game.goAt = now
      game.flash = { color: '#39d98a', ttl: 600 }
    }
  } else if (game.state === 'done') {
    game.resultTimer -= dt
    if (game.resultTimer <= 0) {
      if (roundWins.p1 >= ROUNDS_TO_WIN || roundWins.p2 >= ROUNDS_TO_WIN) finishMatch()
      else beginRound()
    }
  }
  if (game.flash) {
    game.flash.ttl -= dt
    if (game.flash.ttl <= 0) game.flash = null
  }
}

async function finishMatch() {
  cancelAnimationFrame(rafId)
  rafId = 0
  const winner = roundWins.p1 > roundWins.p2 ? '玩家 1 獲勝' : '玩家 2 獲勝'
  resultText.value = `🔫 ${winner}`
  phase.value = 'matchover'
  recordGameResult('/game18', roundWins.p1 > roundWins.p2 ? 'p1' : 'p2')
  try {
    const store = await saveGame18Record({ winner, scoreP1: roundWins.p1, scoreP2: roundWins.p2, date: new Date().toISOString() })
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
    render()
    rafId = requestAnimationFrame(loop)
  } else {
    rafId = 0
  }
}

function render() {
  ctx.imageSmoothingEnabled = false
  const bgImg = g18Sprite('bg-sunset-desert')
  if (g18ready(bgImg)) {
    ctx.drawImage(bgImg, 0, 0, CANVAS_W, CANVAS_H)
  } else {
    ctx.fillStyle = '#161a26'
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  }
  // 訊號色調覆蓋（核心玩法回饋）
  if (game.flash) {
    ctx.fillStyle = game.flash.color
    ctx.globalAlpha = 0.42
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
    ctx.globalAlpha = 1
  } else if (game.state === 'go') {
    ctx.fillStyle = '#39d98a'
    ctx.globalAlpha = 0.38
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
    ctx.globalAlpha = 1
  }

  // 牛仔對峙
  const groundY = CANVAS_H - 24
  const shooting = game.state === 'go' || game.state === 'done'
  g18Draw(`cowboy-p1-${shooting ? 'shoot' : 'ready'}-r`, CANVAS_W * 0.22, groundY, 96)
  g18Draw(`cowboy-p2-${shooting ? 'shoot' : 'ready'}-l`, CANVAS_W * 0.78, groundY, 96)

  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  if (game.state === 'ready') {
    if (g18ready(g18Sprite('txt-ready'))) {
      const t = g18Sprite('txt-ready')
      const w = 200
      ctx.drawImage(t, CANVAS_W / 2 - w / 2, CANVAS_H / 2 - 50, w, w * (t.naturalHeight / t.naturalWidth))
    } else {
      ctx.fillStyle = 'rgba(255,255,255,0.92)'
      ctx.font = 'bold 46px system-ui, sans-serif'
      ctx.fillText('準備…', CANVAS_W / 2, CANVAS_H / 2 - 10)
    }
    ctx.font = '18px system-ui, sans-serif'
    ctx.fillStyle = 'rgba(255,255,255,0.78)'
    ctx.fillText('看到綠色「開槍！」再按，別偷跑', CANVAS_W / 2, CANVAS_H / 2 + 30)
  } else if (game.state === 'go') {
    const t = g18Sprite('txt-draw')
    if (g18ready(t)) {
      const w = 280
      ctx.drawImage(t, CANVAS_W / 2 - w / 2, CANVAS_H / 2 - 60, w, w * (t.naturalHeight / t.naturalWidth))
    } else {
      ctx.fillStyle = '#05241a'
      ctx.font = 'bold 76px system-ui, sans-serif'
      ctx.fillText('開槍！', CANVAS_W / 2, CANVAS_H / 2)
    }
  } else if (game.state === 'done') {
    ctx.fillStyle = '#fff'
    ctx.font = 'bold 44px system-ui, sans-serif'
    ctx.fillText(game.reason, CANVAS_W / 2, 80)
    ctx.font = '22px system-ui, sans-serif'
    if (game.reaction > 0) ctx.fillText(`反應時間 ${game.reaction} ms`, CANVAS_W / 2, 124)
  }

  // player key hints
  ctx.font = 'bold 18px system-ui, sans-serif'
  ctx.fillStyle = '#3affd0'
  ctx.fillText('玩家 1： F', CANVAS_W * 0.25, CANVAS_H - 8)
  ctx.fillStyle = '#ff9ec8'
  ctx.fillText('玩家 2： J', CANVAS_W * 0.75, CANVAS_H - 8)
}

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  if ((k === ' ' || k === 'enter') && phase.value !== 'playing') {
    startMatch()
    e.preventDefault()
    return
  }
  if (phase.value !== 'playing') return
  if (e.repeat) return
  if (k === 'f') press('p1')
  else if (k === 'j') press('p2')
}

async function onClearRecords() {
  const store = await clearGame18Records()
  records.value = store.records
}
function formatDate(iso) {
  const d = new Date(iso)
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}
function idleRender() {
  ctx.fillStyle = '#161a26'
  ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  ctx.fillStyle = 'rgba(255,255,255,0.5)'
  ctx.font = 'bold 28px system-ui, sans-serif'
  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  ctx.fillText('按開始對決', CANVAS_W / 2, CANVAS_H / 2)
}

onMounted(async () => {
  ctx = canvasRef.value.getContext('2d')
  window.addEventListener('keydown', onKeyDown)
  try {
    const store = await fetchGame18Store()
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
.game18-view { min-height: 100vh; padding: 24px clamp(16px,4vw,48px) 48px; color: #e9ecf6; background: radial-gradient(circle at 50% -10%, #2a1f3a, #0d0a14 60%); font-family: 'Segoe UI', system-ui, sans-serif; }
.topbar { display: flex; align-items: center; gap: 20px; flex-wrap: wrap; margin-bottom: 22px; }
.back-link { color: #b7a8d8; text-decoration: none; font-size: 14px; padding: 8px 14px; border: 1px solid rgba(183,168,216,0.3); border-radius: 999px; transition: 0.2s; }
.back-link:hover { background: rgba(183,168,216,0.12); color: #fff; }
.title-block .eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; color: #39d98a; text-transform: uppercase; }
.title-block h1 { margin: 2px 0 0; font-size: 26px; background: linear-gradient(90deg,#39d98a,#ff9ec8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.round-pill { margin-left: auto; padding: 8px 16px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 13px; }
.layout { display: grid; grid-template-columns: minmax(0,1fr) 300px; gap: 22px; align-items: start; }
.stage-card { background: rgba(13,10,20,0.6); border: 1px solid rgba(160,140,200,0.16); border-radius: 20px; padding: 16px; box-shadow: 0 24px 60px rgba(0,0,0,0.5); }
.scoreband { display: flex; align-items: center; gap: 16px; margin-bottom: 14px; padding: 0 6px; }
.team { display: flex; align-items: center; gap: 12px; flex: 1; }
.team-2 { justify-content: flex-end; }
.team .dot { width: 14px; height: 14px; border-radius: 50%; }
.team-1 .dot { background: #3affd0; box-shadow: 0 0 12px #3affd0; }
.team-2 .dot { background: #ff9ec8; box-shadow: 0 0 12px #ff9ec8; }
.team strong { font-size: 15px; }
.pips { display: inline-flex; gap: 5px; }
.pips i { width: 12px; height: 12px; border-radius: 50%; background: rgba(255,255,255,0.15); display: block; }
.team-1 .pips i.on { background: #3affd0; box-shadow: 0 0 8px #3affd0; }
.team-2 .pips i.on { background: #ff9ec8; box-shadow: 0 0 8px #ff9ec8; }
.vs { font-size: 13px; font-weight: 800; color: #6a5d8a; }
.stage-frame { position: relative; border-radius: 14px; overflow: hidden; }
.game-canvas { display: block; width: 100%; height: auto; border-radius: 14px; }
.overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(8,6,14,0.82); backdrop-filter: blur(4px); }
.overlay-card { text-align: center; max-width: 460px; padding: 32px; }
.overlay-eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; color: #39d98a; }
.overlay-card h2 { margin: 10px 0 14px; font-size: 26px; }
.winner-text { background: linear-gradient(90deg,#39d98a,#ff9ec8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.overlay-text { color: #c0b6dc; line-height: 1.7; margin: 0 0 22px; }
.primary-btn { border: none; padding: 12px 30px; border-radius: 999px; font-size: 16px; font-weight: 700; color: #05241a; background: linear-gradient(90deg,#39d98a,#46d0ff); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; box-shadow: 0 10px 26px rgba(57,217,138,0.35); }
.primary-btn:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(57,217,138,0.5); }
.sidebar { display: flex; flex-direction: column; gap: 16px; }
.panel { background: rgba(13,10,20,0.6); border: 1px solid rgba(160,140,200,0.16); border-radius: 18px; padding: 18px; }
.panel .eyebrow { margin: 0 0 12px; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #8979ac; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.controls-grid { display: grid; gap: 10px; }
.ctrl { border-radius: 12px; padding: 12px 14px; }
.ctrl strong { display: block; font-size: 14px; margin-bottom: 6px; }
.ctrl span { font-size: 13px; color: #c0b6dc; }
.ctrl-1 { background: rgba(58,255,208,0.1); border: 1px solid rgba(58,255,208,0.25); }
.ctrl-2 { background: rgba(255,158,200,0.1); border: 1px solid rgba(255,158,200,0.25); }
kbd { background: rgba(255,255,255,0.12); border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 2px 8px; font-size: 13px; font-family: inherit; }
.tips { margin: 0; padding-left: 18px; display: grid; gap: 8px; font-size: 13px; color: #c0b6dc; line-height: 1.5; }
.records { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.records li { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: center; font-size: 12px; padding: 8px 10px; border-radius: 10px; background: rgba(255,255,255,0.04); }
.rec-win { font-weight: 700; color: #39d98a; }
.rec-score { color: #b7a8d8; }
.rec-date { color: #6a5d8a; }
.empty { font-size: 13px; color: #8979ac; line-height: 1.6; margin: 0; }
.ghost-btn { background: none; border: 1px solid rgba(183,168,216,0.3); color: #b7a8d8; border-radius: 999px; padding: 4px 12px; font-size: 12px; cursor: pointer; }
.ghost-btn:hover { background: rgba(183,168,216,0.12); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } }
</style>
