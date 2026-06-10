<template>
  <main class="game19-view">
    <section class="topbar">
      <RouterLink to="/" class="back-link">← 返回遊戲廳</RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 19</p>
        <h1>平台爭霸</h1>
      </div>
      <div v-if="phase === 'playing'" class="time-pill" :class="{ urgent: timeLeft <= 10 }">⏱ {{ timeLeft }}s</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <span class="dot" />
            <strong>玩家 1</strong>
            <span class="ko">擊落 {{ hud.p1Ko }}</span>
            <span class="dmg">{{ Math.round(hud.p1Dmg) }}%</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="dmg">{{ Math.round(hud.p2Dmg) }}%</span>
            <span class="ko">擊落 {{ hud.p2Ko }}</span>
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
                  <p class="overlay-eyebrow">站穩擂台</p>
                  <h2>把對手撞飛出場</h2>
                  <p class="overlay-text">
                    在平台間跳躍、攻擊撞飛對手，傷害越高被擊飛越遠。<br>
                    被擊出邊界就算被擊落，限時內擊落數多者獲勝。
                  </p>
                  <button class="primary-btn" @click="startGame">開始對戰</button>
                </template>
                <template v-else-if="phase === 'result'">
                  <p class="overlay-eyebrow">時間到</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <p class="overlay-text">擊落數 {{ hud.p1Ko }} : {{ hud.p2Ko }}</p>
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
              <span><kbd>A</kbd><kbd>D</kbd> 移動 · <kbd>W</kbd> 跳（可二段）</span>
              <span><kbd>F</kbd> 攻擊</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span><kbd>←</kbd><kbd>→</kbd> 移動 · <kbd>↑</kbd> 跳（可二段）</span>
              <span><kbd>/</kbd> 攻擊</span>
            </div>
          </div>
        </section>
        <section class="panel">
          <p class="eyebrow">提示</p>
          <ul class="tips">
            <li>傷害 % 越高，被打飛的距離越遠。</li>
            <li>善用二段跳回到平台避免墜落。</li>
            <li>把握對手高傷害時一擊定生死。</li>
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
import { clearGame19Records, fetchGame19Store, saveGame19Record } from './game19Storage'
import { recordGameResult } from '@/data/lobbyScore'

const CANVAS_W = 880
const CANVAS_H = 540
const GAME_SEC = 75
const GRAVITY = 0.6
const MOVE = 0.8
const MAX_RUN = 5.5
const JUMP = 12
const PW = 34
const PH = 46

const PLATFORMS = [
  { x: 190, y: 420, w: 500, h: 20 },
  { x: 110, y: 300, w: 170, h: 16 },
  { x: 600, y: 300, w: 170, h: 16 },
  { x: 360, y: 200, w: 160, h: 16 }
]

const canvasRef = ref(null)
const stageRef = ref(null)
const phase = ref('intro')
const timeLeft = ref(GAME_SEC)
const resultText = ref('')
const records = ref([])
const hud = reactive({ p1Ko: 0, p2Ko: 0, p1Dmg: 0, p2Dmg: 0 })

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null
const keys = new Set()

function makeFighter(id, x, color) {
  return { id, x, y: 120, vx: 0, vy: 0, onGround: false, jumps: 2, damage: 0, facing: 1, ko: 0, attackCd: 0, attackActive: 0, invul: 0, color, hitFlash: 0 }
}

function createGame() {
  return { p1: makeFighter('p1', 320, '#3affd0'), p2: makeFighter('p2', 540, '#ff9ec8'), elapsed: 0, particles: [] }
}

function startGame() {
  game = createGame()
  hud.p1Ko = 0
  hud.p2Ko = 0
  hud.p1Dmg = 0
  hud.p2Dmg = 0
  timeLeft.value = GAME_SEC
  phase.value = 'playing'
  lastFrame = performance.now()
  loop(lastFrame)
}

function jump(p) {
  if (p.jumps > 0) {
    p.vy = -JUMP
    p.jumps -= 1
    spawn(p.x, p.y + PH / 2, p.color, 5)
  }
}

function attack(p) {
  if (p.attackCd > 0) return
  p.attackCd = 420
  p.attackActive = 140
}

function spawn(x, y, color, n) {
  for (let i = 0; i < n; i += 1) {
    const a = Math.random() * Math.PI * 2
    const sp = 40 + Math.random() * 120
    game.particles.push({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 1, color })
  }
}

function updateFighter(p, left, right, dt, now) {
  const f = dt / 16.67
  if (keys.has(left)) { p.vx -= MOVE * f; p.facing = -1 }
  if (keys.has(right)) { p.vx += MOVE * f; p.facing = 1 }
  p.vx = Math.max(-MAX_RUN, Math.min(MAX_RUN, p.vx))
  if (!keys.has(left) && !keys.has(right)) p.vx *= 0.86
  p.vy += GRAVITY * f
  p.x += p.vx * f
  p.y += p.vy * f

  // platform collision (land on top)
  p.onGround = false
  for (const pl of PLATFORMS) {
    if (p.x + PW / 2 > pl.x && p.x - PW / 2 < pl.x + pl.w) {
      const feet = p.y + PH / 2
      if (p.vy >= 0 && feet >= pl.y && feet <= pl.y + pl.h + 14 && p.y < pl.y) {
        p.y = pl.y - PH / 2
        p.vy = 0
        p.onGround = true
        p.jumps = 2
      }
    }
  }

  if (p.attackCd > 0) p.attackCd -= dt
  if (p.attackActive > 0) p.attackActive -= dt
  if (p.invul > 0) p.invul -= dt
  if (p.hitFlash > 0) p.hitFlash -= dt

  // KO check (blast zones)
  if (p.x < -70 || p.x > CANVAS_W + 70 || p.y > CANVAS_H + 90 || p.y < -160) {
    ko(p, now)
  }
}

function ko(p, now) {
  const other = p.id === 'p1' ? game.p2 : game.p1
  other.ko += 1
  hud.p1Ko = game.p1.ko
  hud.p2Ko = game.p2.ko
  spawn(Math.max(20, Math.min(CANVAS_W - 20, p.x)), Math.max(20, Math.min(CANVAS_H - 20, p.y)), p.color, 22)
  p.x = CANVAS_W / 2 + (p.id === 'p1' ? -40 : 40)
  p.y = 80
  p.vx = 0
  p.vy = 0
  p.damage = 0
  p.invul = 1500
  void now
  if (p.id === 'p1') hud.p1Dmg = 0
  else hud.p2Dmg = 0
}

function resolveAttacks() {
  const a = game.p1
  const b = game.p2
  tryHit(a, b)
  tryHit(b, a)
}

function tryHit(attacker, target) {
  if (attacker.attackActive <= 0 || target.invul > 0) return
  const hx = attacker.x + attacker.facing * (PW / 2 + 14)
  const hy = attacker.y
  if (Math.abs(hx - target.x) < PW / 2 + 16 && Math.abs(hy - target.y) < PH / 2 + 8) {
    attacker.attackActive = 0
    const kb = 4 + target.damage * 0.09
    target.vx = attacker.facing * (kb + 2)
    target.vy = -(kb * 0.7 + 3)
    target.damage += 9
    target.invul = 300
    target.hitFlash = 200
    if (target.id === 'p1') hud.p1Dmg = target.damage
    else hud.p2Dmg = target.damage
    spawn(target.x, target.y, '#ffd23f', 12)
  }
}

function update(dt, now) {
  game.elapsed += dt
  timeLeft.value = Math.max(0, Math.ceil((GAME_SEC * 1000 - game.elapsed) / 1000))
  updateFighter(game.p1, 'a', 'd', dt, now)
  updateFighter(game.p2, 'arrowleft', 'arrowright', dt, now)
  resolveAttacks()
  for (const pt of game.particles) {
    pt.x += pt.vx * (dt / 1000)
    pt.y += pt.vy * (dt / 1000)
    pt.vy += 300 * (dt / 1000)
    pt.life -= dt / 700
  }
  game.particles = game.particles.filter((p) => p.life > 0)
  if (game.elapsed >= GAME_SEC * 1000) finishGame()
}

async function finishGame() {
  cancelAnimationFrame(rafId)
  rafId = 0
  let winner
  if (game.p1.ko > game.p2.ko) winner = '玩家 1 獲勝'
  else if (game.p2.ko > game.p1.ko) winner = '玩家 2 獲勝'
  else winner = '平手'
  resultText.value = `🥊 ${winner}`
  phase.value = 'result'
  recordGameResult('/game19', game.p1.ko > game.p2.ko ? 'p1' : game.p2.ko > game.p1.ko ? 'p2' : 'draw')
  try {
    const store = await saveGame19Record({ winner, scoreP1: game.p1.ko, scoreP2: game.p2.ko, date: new Date().toISOString() })
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
  const bg = ctx.createLinearGradient(0, 0, 0, CANVAS_H)
  bg.addColorStop(0, '#1a1430')
  bg.addColorStop(1, '#0c0a18')
  ctx.fillStyle = bg
  ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  // platforms
  for (const pl of PLATFORMS) {
    ctx.fillStyle = '#46407a'
    roundRect(pl.x, pl.y, pl.w, pl.h, 6)
    ctx.fill()
    ctx.fillStyle = 'rgba(255,255,255,0.18)'
    ctx.fillRect(pl.x, pl.y, pl.w, 3)
  }
  drawFighter(game.p1, now)
  drawFighter(game.p2, now)
  for (const pt of game.particles) {
    ctx.globalAlpha = Math.max(0, pt.life)
    ctx.fillStyle = pt.color
    ctx.beginPath()
    ctx.arc(pt.x, pt.y, 3 * pt.life + 1, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.globalAlpha = 1
}

function drawFighter(p, now) {
  ctx.save()
  if (p.invul > 0 && Math.floor(now / 100) % 2 === 0) ctx.globalAlpha = 0.45
  // body
  ctx.fillStyle = p.hitFlash > 0 ? '#fff' : p.color
  roundRect(p.x - PW / 2, p.y - PH / 2, PW, PH, 8)
  ctx.fill()
  // eyes (facing)
  ctx.fillStyle = '#10141c'
  ctx.beginPath()
  ctx.arc(p.x + p.facing * 7, p.y - 8, 3.5, 0, Math.PI * 2)
  ctx.fill()
  // attack arc
  if (p.attackActive > 0) {
    ctx.fillStyle = 'rgba(255,210,63,0.6)'
    ctx.beginPath()
    ctx.arc(p.x + p.facing * (PW / 2 + 10), p.y, 18, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.restore()
  // damage label
  ctx.fillStyle = p.color
  ctx.font = 'bold 13px system-ui, sans-serif'
  ctx.textAlign = 'center'
  ctx.fillText(`${Math.round(p.damage)}%`, p.x, p.y - PH / 2 - 8)
}

function roundRect(x, y, w, h, r) {
  ctx.beginPath()
  ctx.moveTo(x + r, y)
  ctx.arcTo(x + w, y, x + w, y + h, r)
  ctx.arcTo(x + w, y + h, x, y + h, r)
  ctx.arcTo(x, y + h, x, y, r)
  ctx.arcTo(x, y, x + w, y, r)
  ctx.closePath()
}

function onKeyDown(e) {
  const k = e.key.toLowerCase()
  if (k.startsWith('arrow') || k === '/' || k === ' ') e.preventDefault()
  if (phase.value !== 'playing') {
    if (k === ' ' || k === 'enter') startGame()
    keys.add(k)
    return
  }
  if (!e.repeat) {
    if (k === 'w') jump(game.p1)
    if (k === 'arrowup') jump(game.p2)
    if (k === 'f') attack(game.p1)
    if (k === '/') attack(game.p2)
  }
  keys.add(k)
}
function onKeyUp(e) {
  keys.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame19Records()
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
    const store = await fetchGame19Store()
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
.game19-view { min-height: 100vh; padding: 24px clamp(16px,4vw,48px) 48px; color: #ece9f8; background: radial-gradient(circle at 50% -10%, #241a44, #0b0816 60%); font-family: 'Segoe UI', system-ui, sans-serif; }
.topbar { display: flex; align-items: center; gap: 20px; flex-wrap: wrap; margin-bottom: 22px; }
.back-link { color: #ada0d8; text-decoration: none; font-size: 14px; padding: 8px 14px; border: 1px solid rgba(173,160,216,0.3); border-radius: 999px; transition: 0.2s; }
.back-link:hover { background: rgba(173,160,216,0.12); color: #fff; }
.title-block .eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; color: #b58bff; text-transform: uppercase; }
.title-block h1 { margin: 2px 0 0; font-size: 26px; background: linear-gradient(90deg,#3affd0,#ff9ec8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.time-pill { margin-left: auto; padding: 8px 18px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 16px; font-weight: 700; font-variant-numeric: tabular-nums; }
.time-pill.urgent { background: rgba(255,93,108,0.2); border-color: rgba(255,93,108,0.5); color: #ff9eaa; }
.layout { display: grid; grid-template-columns: minmax(0,1fr) 300px; gap: 22px; align-items: start; }
.stage-card { background: rgba(11,8,22,0.6); border: 1px solid rgba(160,140,210,0.16); border-radius: 20px; padding: 16px; box-shadow: 0 24px 60px rgba(0,0,0,0.5); }
.scoreband { display: flex; align-items: center; gap: 16px; margin-bottom: 14px; padding: 0 6px; }
.team { display: flex; align-items: center; gap: 10px; flex: 1; }
.team-2 { justify-content: flex-end; }
.team .dot { width: 14px; height: 14px; border-radius: 50%; }
.team-1 .dot { background: #3affd0; box-shadow: 0 0 12px #3affd0; }
.team-2 .dot { background: #ff9ec8; box-shadow: 0 0 12px #ff9ec8; }
.team strong { font-size: 14px; }
.team .ko { font-size: 14px; font-weight: 800; color: #ffd23f; }
.team .dmg { font-size: 13px; color: #ff8a8a; font-weight: 700; font-variant-numeric: tabular-nums; }
.vs { font-size: 13px; font-weight: 800; color: #6a5d96; }
.stage-frame { position: relative; border-radius: 14px; overflow: hidden; }
.game-canvas { display: block; width: 100%; height: auto; border-radius: 14px; }
.overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(7,5,14,0.82); backdrop-filter: blur(4px); }
.overlay-card { text-align: center; max-width: 460px; padding: 32px; }
.overlay-eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; color: #b58bff; }
.overlay-card h2 { margin: 10px 0 14px; font-size: 26px; }
.winner-text { background: linear-gradient(90deg,#3affd0,#ff9ec8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.overlay-text { color: #c2b8e0; line-height: 1.7; margin: 0 0 22px; }
.primary-btn { border: none; padding: 12px 30px; border-radius: 999px; font-size: 16px; font-weight: 700; color: #05140f; background: linear-gradient(90deg,#3affd0,#b58bff); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; box-shadow: 0 10px 26px rgba(181,139,255,0.4); }
.primary-btn:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(181,139,255,0.55); }
.sidebar { display: flex; flex-direction: column; gap: 16px; }
.panel { background: rgba(11,8,22,0.6); border: 1px solid rgba(160,140,210,0.16); border-radius: 18px; padding: 18px; }
.panel .eyebrow { margin: 0 0 12px; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #897cac; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.controls-grid { display: grid; gap: 10px; }
.ctrl { border-radius: 12px; padding: 12px 14px; }
.ctrl strong { display: block; font-size: 14px; margin-bottom: 6px; }
.ctrl span { display: block; font-size: 13px; color: #c2b8e0; margin-bottom: 3px; }
.ctrl-1 { background: rgba(58,255,208,0.1); border: 1px solid rgba(58,255,208,0.25); }
.ctrl-2 { background: rgba(255,158,200,0.1); border: 1px solid rgba(255,158,200,0.25); }
kbd { background: rgba(255,255,255,0.12); border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 2px 7px; font-size: 12px; font-family: inherit; margin-right: 2px; }
.tips { margin: 0; padding-left: 18px; display: grid; gap: 8px; font-size: 13px; color: #c2b8e0; line-height: 1.5; }
.records { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.records li { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: center; font-size: 12px; padding: 8px 10px; border-radius: 10px; background: rgba(255,255,255,0.04); }
.rec-win { font-weight: 700; color: #ffd23f; }
.rec-score { color: #ada0d8; }
.rec-date { color: #6a5d96; }
.empty { font-size: 13px; color: #897cac; line-height: 1.6; margin: 0; }
.ghost-btn { background: none; border: 1px solid rgba(173,160,216,0.3); color: #ada0d8; border-radius: 999px; padding: 4px 12px; font-size: 12px; cursor: pointer; }
.ghost-btn:hover { background: rgba(173,160,216,0.12); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } }
</style>
