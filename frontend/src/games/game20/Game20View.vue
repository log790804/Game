<template>
  <main class="game20-view">
    <section class="topbar">
      <RouterLink to="/" class="back-link">← 返回遊戲廳</RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 20</p>
        <h1>雙人格鬥</h1>
      </div>
      <div class="round-pill">三局兩勝</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="hpbar hp-1">
            <div class="hp-row">
              <strong>玩家 1</strong>
              <span class="pips"><i v-for="n in 2" :key="n" :class="{ on: roundWins.p1 >= n }" /></span>
            </div>
            <div class="hp-track"><div class="hp-fill p1" :style="{ width: hud.p1Hp + '%' }" /></div>
          </div>
          <div class="vs">VS</div>
          <div class="hpbar hp-2">
            <div class="hp-row">
              <span class="pips"><i v-for="n in 2" :key="n" :class="{ on: roundWins.p2 >= n }" /></span>
              <strong>玩家 2</strong>
            </div>
            <div class="hp-track"><div class="hp-fill p2" :style="{ width: hud.p2Hp + '%' }" /></div>
          </div>
        </div>

        <div ref="stageRef" class="stage-frame">
          <canvas ref="canvasRef" class="game-canvas" :width="CANVAS_W" :height="CANVAS_H" />
          <transition name="fade">
            <div v-if="phase !== 'playing'" class="overlay">
              <div class="overlay-card">
                <template v-if="phase === 'intro'">
                  <p class="overlay-eyebrow">擂台對決</p>
                  <h2>拳腳交鋒分高下</h2>
                  <p class="overlay-text">
                    用拳擊與踢擊壓制對手，適時防禦化解攻勢。<br>
                    把對手血量打到 0 即拿下一局，先贏 2 局者獲勝。
                  </p>
                  <button class="primary-btn" @click="startMatch">開始對戰</button>
                </template>
                <template v-else-if="phase === 'roundover'">
                  <p class="overlay-eyebrow">第 {{ roundNumber }} 局結束</p>
                  <h2>{{ roundResultText }}</h2>
                  <p class="overlay-text">目前 {{ roundWins.p1 }} : {{ roundWins.p2 }}</p>
                  <button class="primary-btn" @click="nextRound">下一局</button>
                </template>
                <template v-else-if="phase === 'matchover'">
                  <p class="overlay-eyebrow">對戰結束</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <p class="overlay-text">最終 {{ roundWins.p1 }} : {{ roundWins.p2 }}</p>
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
              <span><kbd>A</kbd><kbd>D</kbd> 移動 · <kbd>W</kbd> 跳 · <kbd>S</kbd> 防禦</span>
              <span><kbd>F</kbd> 拳擊 · <kbd>G</kbd> 踢擊</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span><kbd>←</kbd><kbd>→</kbd> 移動 · <kbd>↑</kbd> 跳 · <kbd>↓</kbd> 防禦</span>
              <span><kbd>,</kbd> 拳擊 · <kbd>.</kbd> 踢擊</span>
            </div>
          </div>
        </section>
        <section class="panel">
          <p class="eyebrow">招式</p>
          <ul class="tips">
            <li>拳擊：出招快、傷害低。</li>
            <li>踢擊：出招慢、傷害高、擊退遠。</li>
            <li>防禦：大幅減傷，但仍會被擊退一點。</li>
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
import { clearGame20Records, fetchGame20Store, saveGame20Record } from './game20Storage'
import { recordGameResult } from '@/data/lobbyScore'

const CANVAS_W = 880
const CANVAS_H = 460
const FLOOR_Y = CANVAS_H - 56
const FW = 40
const FH = 78

// 像素素材
const G20 = {}
function g20Sprite(name) {
  if (!G20[name]) {
    const img = new Image()
    img.src = `/assets/G20/${name}.png`
    G20[name] = img
  }
  return G20[name]
}
function g20ready(img) {
  return img && img.complete && img.naturalWidth > 0
}
g20Sprite('bg-dojo')
for (const pid of ['p1', 'p2']) {
  for (const pose of ['idle', 'punch', 'kick', 'crouch', 'jump', 'hit', 'ko', 'special']) {
    for (const side of ['l', 'r']) g20Sprite(`fighter-${pid}-${pose}-${side}`)
  }
}
function g20Pose(p) {
  if (game.over && p.hp <= 0) return 'ko'
  if (p.hitstun > 0) return 'hit'
  if (p.state === 'attack' && p.move) return p.move
  if (!p.onGround) return 'jump'
  if (isBlocking(p)) return 'crouch'
  return 'idle'
}
const GRAVITY = 0.7
const MOVE = 3.2
const JUMP = 13
const ROUNDS_TO_WIN = 2

const MOVES = {
  punch: { startup: 70, active: 80, recovery: 130, dmg: 6, range: 46, kb: 3 },
  kick: { startup: 140, active: 90, recovery: 250, dmg: 13, range: 58, kb: 7 }
}

const canvasRef = ref(null)
const stageRef = ref(null)
const phase = ref('intro')
const roundNumber = ref(1)
const roundWins = reactive({ p1: 0, p2: 0 })
const roundResultText = ref('')
const resultText = ref('')
const records = ref([])
const hud = reactive({ p1Hp: 100, p2Hp: 100 })

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null
const keys = new Set()

function makeFighter(id, x, facing, color) {
  return {
    id, x, y: FLOOR_Y - FH, vx: 0, vy: 0, onGround: true, hp: 100, facing, color,
    state: 'idle', stateT: 0, move: null, hitApplied: false, hitstun: 0, blockStun: 0, hitFlash: 0
  }
}

function createGame() {
  return { p1: makeFighter('p1', 240, 1, '#3affd0'), p2: makeFighter('p2', CANVAS_W - 240, -1, '#ff9ec8'), over: false, freeze: 0, particles: [] }
}

function startMatch() {
  roundWins.p1 = 0
  roundWins.p2 = 0
  roundNumber.value = 1
  beginRound()
}
function nextRound() {
  roundNumber.value += 1
  beginRound()
}
function beginRound() {
  game = createGame()
  hud.p1Hp = 100
  hud.p2Hp = 100
  phase.value = 'playing'
  lastFrame = performance.now()
  loop(lastFrame)
}

function canAct(p) {
  return p.state !== 'attack' && p.hitstun <= 0 && p.blockStun <= 0
}

function startAttack(p, type) {
  if (!canAct(p) || !p.onGround) return
  p.state = 'attack'
  p.stateT = 0
  p.move = type
  p.hitApplied = false
}

function isBlocking(p) {
  const blockKey = p.id === 'p1' ? 's' : 'arrowdown'
  return keys.has(blockKey) && p.onGround && canAct(p)
}

function spawn(x, y, color, n) {
  for (let i = 0; i < n; i += 1) {
    const a = Math.random() * Math.PI * 2
    const sp = 40 + Math.random() * 120
    game.particles.push({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 1, color })
  }
}

function updateFighter(p, other, left, right, dt) {
  const f = dt / 16.67
  if (p.hitstun > 0) p.hitstun -= dt
  if (p.blockStun > 0) p.blockStun -= dt
  if (p.hitFlash > 0) p.hitFlash -= dt

  // facing toward opponent when actionable
  if (canAct(p)) p.facing = other.x >= p.x ? 1 : -1

  // movement
  if (canAct(p) && !isBlocking(p)) {
    let mv = 0
    if (keys.has(left)) mv -= 1
    if (keys.has(right)) mv += 1
    p.vx = mv * MOVE
  } else {
    p.vx *= 0.8
  }

  p.vy += GRAVITY * f
  p.x += p.vx * f
  p.y += p.vy * f
  if (p.y >= FLOOR_Y - FH) {
    p.y = FLOOR_Y - FH
    p.vy = 0
    p.onGround = true
  } else {
    p.onGround = false
  }
  p.x = Math.max(FW / 2, Math.min(CANVAS_W - FW / 2, p.x))

  // attack state machine
  if (p.state === 'attack') {
    p.stateT += dt
    const m = MOVES[p.move]
    if (p.stateT >= m.startup && p.stateT < m.startup + m.active && !p.hitApplied) {
      // active frames: test hit
      const hx = p.x + p.facing * (FW / 2 + m.range / 2)
      if (Math.abs(hx - other.x) < FW / 2 + m.range / 2 && Math.abs(p.y - other.y) < FH) {
        applyHit(p, other, m)
        p.hitApplied = true
      }
    }
    if (p.stateT >= m.startup + m.active + m.recovery) {
      p.state = 'idle'
      p.move = null
    }
  }
}

function applyHit(attacker, target, m) {
  const blocked = isBlocking(target) && Math.sign(target.x - attacker.x) === attacker.facing * -1 ? false : isBlocking(target)
  const facingBlock = isBlocking(target) && ((attacker.facing === 1 && target.x > attacker.x) || (attacker.facing === -1 && target.x < attacker.x))
  if (facingBlock) {
    target.hp = Math.max(0, target.hp - m.dmg * 0.18)
    target.vx = attacker.facing * (m.kb * 0.4)
    target.blockStun = 120
    spawn(target.x + target.facing * 20, target.y + FH / 2, '#bcdcff', 6)
  } else {
    target.hp = Math.max(0, target.hp - m.dmg)
    target.vx = attacker.facing * m.kb
    target.vy = -m.kb * 0.4
    target.hitstun = m.dmg * 18
    target.hitFlash = 140
    spawn(target.x, target.y + FH / 2, '#ffd23f', 12)
  }
  void blocked
  hud.p1Hp = game.p1.hp
  hud.p2Hp = game.p2.hp
  if (target.hp <= 0) {
    game.over = true
    game.freeze = 1100
  }
}

function update(dt) {
  if (game.freeze > 0) {
    game.freeze -= dt
    updateParticles(dt)
    if (game.freeze <= 0) resolveRound()
    return
  }
  updateFighter(game.p1, game.p2, 'a', 'd', dt)
  updateFighter(game.p2, game.p1, 'arrowleft', 'arrowright', dt)
  // prevent overlap
  const dx = game.p2.x - game.p1.x
  const minDist = FW
  if (Math.abs(dx) < minDist) {
    const push = (minDist - Math.abs(dx)) / 2
    const s = dx >= 0 ? 1 : -1
    game.p1.x -= s * push
    game.p2.x += s * push
  }
  updateParticles(dt)
}

function updateParticles(dt) {
  for (const pt of game.particles) {
    pt.x += pt.vx * (dt / 1000)
    pt.y += pt.vy * (dt / 1000)
    pt.vy += 300 * (dt / 1000)
    pt.life -= dt / 700
  }
  game.particles = game.particles.filter((p) => p.life > 0)
}

function resolveRound() {
  if (game.p1.hp <= 0 && game.p2.hp <= 0) roundResultText.value = '雙方同時倒下，平手！'
  else if (game.p2.hp <= 0) { roundWins.p1 += 1; roundResultText.value = '玩家 1 勝出！' }
  else { roundWins.p2 += 1; roundResultText.value = '玩家 2 勝出！' }
  cancelAnimationFrame(rafId)
  rafId = 0
  if (roundWins.p1 >= ROUNDS_TO_WIN || roundWins.p2 >= ROUNDS_TO_WIN) finishMatch()
  else phase.value = 'roundover'
}

async function finishMatch() {
  let winner
  if (roundWins.p1 > roundWins.p2) winner = '玩家 1 獲勝'
  else if (roundWins.p2 > roundWins.p1) winner = '玩家 2 獲勝'
  else winner = '平手'
  resultText.value = `🥋 ${winner}`
  phase.value = 'matchover'
  recordGameResult('/game20', roundWins.p1 > roundWins.p2 ? 'p1' : roundWins.p2 > roundWins.p1 ? 'p2' : 'draw')
  try {
    const store = await saveGame20Record({ winner, scoreP1: roundWins.p1, scoreP2: roundWins.p2, date: new Date().toISOString() })
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
  const bgImg = g20Sprite('bg-dojo')
  if (g20ready(bgImg)) {
    ctx.drawImage(bgImg, 0, 0, CANVAS_W, CANVAS_H)
  } else {
    const bg = ctx.createLinearGradient(0, 0, 0, CANVAS_H)
    bg.addColorStop(0, '#2a1830')
    bg.addColorStop(1, '#120a18')
    ctx.fillStyle = bg
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
    ctx.fillStyle = '#3a2a44'
    ctx.fillRect(0, FLOOR_Y, CANVAS_W, CANVAS_H - FLOOR_Y)
  }

  drawFighter(game.p1)
  drawFighter(game.p2)

  for (const pt of game.particles) {
    ctx.globalAlpha = Math.max(0, pt.life)
    ctx.fillStyle = pt.color
    ctx.beginPath()
    ctx.arc(pt.x, pt.y, 3 * pt.life + 1, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.globalAlpha = 1

  if (game.over && game.freeze > 0) {
    const ko = g20Sprite('txt-ko')
    if (g20ready(ko)) {
      const w = 260
      const h = w * (ko.naturalHeight / ko.naturalWidth)
      ctx.drawImage(ko, CANVAS_W / 2 - w / 2, CANVAS_H / 2 - h / 2 - 20, w, h)
    } else {
      ctx.fillStyle = '#fff'
      ctx.font = 'bold 60px system-ui, sans-serif'
      ctx.textAlign = 'center'
      ctx.textBaseline = 'middle'
      ctx.fillText('K.O.', CANVAS_W / 2, CANVAS_H / 2 - 20)
    }
  }
}

function drawFighter(p) {
  const blocking = isBlocking(p)
  // 像素鬥士精靈
  const side = p.facing > 0 ? 'r' : 'l'
  const img = g20Sprite(`fighter-${p.id}-${g20Pose(p)}-${side}`)
  if (g20ready(img)) {
    ctx.save()
    if (p.hitFlash > 0 && Math.floor(p.hitFlash / 40) % 2 === 0) ctx.globalAlpha = 0.8
    const h = 132
    const w = h * (img.naturalWidth / img.naturalHeight)
    ctx.imageSmoothingEnabled = false
    ctx.drawImage(img, p.x - w / 2, FLOOR_Y - h, w, h)
    ctx.restore()
    return
  }
  ctx.save()
  ctx.fillStyle = p.hitFlash > 0 ? '#fff' : p.color
  roundRect(p.x - FW / 2, p.y, FW, FH, 8)
  ctx.fill()
  // head
  ctx.beginPath()
  ctx.arc(p.x, p.y - 8, 14, 0, Math.PI * 2)
  ctx.fill()
  // eye
  ctx.fillStyle = '#10141c'
  ctx.beginPath()
  ctx.arc(p.x + p.facing * 6, p.y - 9, 3, 0, Math.PI * 2)
  ctx.fill()
  // arm / attack
  if (p.state === 'attack' && p.move) {
    const m = MOVES[p.move]
    const active = p.stateT >= m.startup && p.stateT < m.startup + m.active
    ctx.fillStyle = p.move === 'kick' ? '#ffd23f' : '#ffffff'
    if (p.move === 'punch') {
      const ext = active ? m.range : m.range * 0.4
      ctx.fillRect(p.x + (p.facing > 0 ? FW / 2 : -FW / 2 - ext), p.y + 18, ext, 10)
    } else {
      const ext = active ? m.range : m.range * 0.4
      ctx.fillRect(p.x + (p.facing > 0 ? FW / 2 : -FW / 2 - ext), p.y + FH - 24, ext, 12)
    }
  }
  if (blocking) {
    ctx.strokeStyle = '#7fc0ff'
    ctx.lineWidth = 3
    ctx.globalAlpha = 0.8
    ctx.beginPath()
    ctx.arc(p.x + p.facing * (FW / 2), p.y + FH / 2, FH * 0.5, -Math.PI / 2, Math.PI / 2)
    ctx.stroke()
    ctx.globalAlpha = 1
  }
  ctx.restore()
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
  if (k.startsWith('arrow') || k === ' ' || k === ',' || k === '.') e.preventDefault()
  if (phase.value !== 'playing') {
    if (k === ' ' || k === 'enter') {
      if (phase.value === 'roundover') nextRound()
      else startMatch()
    }
    keys.add(k)
    return
  }
  if (!e.repeat && game.freeze <= 0) {
    if (k === 'w' && game.p1.onGround && canAct(game.p1)) game.p1.vy = -JUMP
    if (k === 'arrowup' && game.p2.onGround && canAct(game.p2)) game.p2.vy = -JUMP
    if (k === 'f') startAttack(game.p1, 'punch')
    if (k === 'g') startAttack(game.p1, 'kick')
    if (k === ',') startAttack(game.p2, 'punch')
    if (k === '.') startAttack(game.p2, 'kick')
  }
  keys.add(k)
}
function onKeyUp(e) {
  keys.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame20Records()
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
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('keyup', onKeyUp)
  try {
    const store = await fetchGame20Store()
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
.game20-view { min-height: 100vh; padding: 24px clamp(16px,4vw,48px) 48px; color: #f1e8f4; background: radial-gradient(circle at 50% -10%, #341a3e, #120814 60%); font-family: 'Segoe UI', system-ui, sans-serif; }
.topbar { display: flex; align-items: center; gap: 20px; flex-wrap: wrap; margin-bottom: 22px; }
.back-link { color: #d3a8d0; text-decoration: none; font-size: 14px; padding: 8px 14px; border: 1px solid rgba(211,168,208,0.3); border-radius: 999px; transition: 0.2s; }
.back-link:hover { background: rgba(211,168,208,0.12); color: #fff; }
.title-block .eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; color: #ff7ab0; text-transform: uppercase; }
.title-block h1 { margin: 2px 0 0; font-size: 26px; background: linear-gradient(90deg,#3affd0,#ff7ab0); -webkit-background-clip: text; background-clip: text; color: transparent; }
.round-pill { margin-left: auto; padding: 8px 16px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 13px; }
.layout { display: grid; grid-template-columns: minmax(0,1fr) 300px; gap: 22px; align-items: start; }
.stage-card { background: rgba(18,8,20,0.6); border: 1px solid rgba(200,140,190,0.16); border-radius: 20px; padding: 16px; box-shadow: 0 24px 60px rgba(0,0,0,0.5); }
.scoreband { display: flex; align-items: center; gap: 16px; margin-bottom: 14px; padding: 0 6px; }
.hpbar { flex: 1; }
.hp-row { display: flex; align-items: center; justify-content: space-between; margin-bottom: 5px; }
.hp-2 .hp-row { flex-direction: row; }
.hp-row strong { font-size: 14px; }
.pips { display: inline-flex; gap: 5px; }
.pips i { width: 11px; height: 11px; border-radius: 50%; background: rgba(255,255,255,0.15); display: block; }
.hp-1 .pips i.on { background: #3affd0; box-shadow: 0 0 8px #3affd0; }
.hp-2 .pips i.on { background: #ff9ec8; box-shadow: 0 0 8px #ff9ec8; }
.hp-track { height: 14px; border-radius: 999px; background: rgba(255,255,255,0.12); overflow: hidden; }
.hp-2 .hp-track { transform: scaleX(-1); }
.hp-fill { height: 100%; border-radius: 999px; transition: width 0.2s; }
.hp-fill.p1 { background: linear-gradient(90deg,#13d9aa,#3affd0); }
.hp-fill.p2 { background: linear-gradient(90deg,#ff6fa8,#ff9ec8); }
.vs { font-size: 13px; font-weight: 800; color: #8a5d80; }
.stage-frame { position: relative; border-radius: 14px; overflow: hidden; }
.game-canvas { display: block; width: 100%; height: auto; border-radius: 14px; }
.overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(10,5,12,0.82); backdrop-filter: blur(4px); }
.overlay-card { text-align: center; max-width: 460px; padding: 32px; }
.overlay-eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; color: #ff7ab0; }
.overlay-card h2 { margin: 10px 0 14px; font-size: 26px; }
.winner-text { background: linear-gradient(90deg,#3affd0,#ff7ab0); -webkit-background-clip: text; background-clip: text; color: transparent; }
.overlay-text { color: #d8c2d6; line-height: 1.7; margin: 0 0 22px; }
.primary-btn { border: none; padding: 12px 30px; border-radius: 999px; font-size: 16px; font-weight: 700; color: #1a0814; background: linear-gradient(90deg,#3affd0,#ff7ab0); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; box-shadow: 0 10px 26px rgba(255,122,176,0.4); }
.primary-btn:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(255,122,176,0.55); }
.sidebar { display: flex; flex-direction: column; gap: 16px; }
.panel { background: rgba(18,8,20,0.6); border: 1px solid rgba(200,140,190,0.16); border-radius: 18px; padding: 18px; }
.panel .eyebrow { margin: 0 0 12px; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #a87ca0; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.controls-grid { display: grid; gap: 10px; }
.ctrl { border-radius: 12px; padding: 12px 14px; }
.ctrl strong { display: block; font-size: 14px; margin-bottom: 6px; }
.ctrl span { display: block; font-size: 13px; color: #d8c2d6; margin-bottom: 3px; }
.ctrl-1 { background: rgba(58,255,208,0.1); border: 1px solid rgba(58,255,208,0.25); }
.ctrl-2 { background: rgba(255,158,200,0.1); border: 1px solid rgba(255,158,200,0.25); }
kbd { background: rgba(255,255,255,0.12); border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 2px 7px; font-size: 12px; font-family: inherit; margin-right: 2px; }
.tips { margin: 0; padding-left: 18px; display: grid; gap: 8px; font-size: 13px; color: #d8c2d6; line-height: 1.5; }
.records { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.records li { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: center; font-size: 12px; padding: 8px 10px; border-radius: 10px; background: rgba(255,255,255,0.04); }
.rec-win { font-weight: 700; color: #ff9ec8; }
.rec-score { color: #d3a8d0; }
.rec-date { color: #8a5d80; }
.empty { font-size: 13px; color: #a87ca0; line-height: 1.6; margin: 0; }
.ghost-btn { background: none; border: 1px solid rgba(211,168,208,0.3); color: #d3a8d0; border-radius: 999px; padding: 4px 12px; font-size: 12px; cursor: pointer; }
.ghost-btn:hover { background: rgba(211,168,208,0.12); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } }
</style>
