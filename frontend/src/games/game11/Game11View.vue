<template>
  <main class="game11-view">
    <section class="topbar">
      <RouterLink
        to="/"
        class="back-link"
      >
        ← 返回遊戲廳
      </RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 11</p>
        <h1>坦克大戰</h1>
      </div>
      <div class="round-pill">先擊毀對手 {{ KILLS_TO_WIN }} 次者勝</div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <span class="dot" />
            <strong>玩家 1</strong>
            <span class="kills">{{ hud.p1Kills }}</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="kills">{{ hud.p2Kills }}</span>
            <strong>玩家 2</strong>
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
                  <p class="overlay-eyebrow">鋼鐵對轟</p>
                  <h2>操控坦克殲滅對手</h2>
                  <p class="overlay-text">
                    磚牆可被砲彈打穿、鋼牆無法破壞。<br>
                    撿道具強化火力，先擊毀對手 {{ KILLS_TO_WIN }} 次者獲勝。
                  </p>
                  <button class="primary-btn" @click="startMatch">開始對戰</button>
                </template>
                <template v-else-if="phase === 'matchover'">
                  <p class="overlay-eyebrow">對戰結束</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <p class="overlay-text">擊毀數 {{ hud.p1Kills }} : {{ hud.p2Kills }}</p>
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
              <span><kbd>W</kbd><kbd>A</kbd><kbd>S</kbd><kbd>D</kbd> 移動</span>
              <span><kbd>F</kbd> 開火</span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span><kbd>↑</kbd><kbd>←</kbd><kbd>↓</kbd><kbd>→</kbd> 移動</span>
              <span><kbd>/</kbd> 開火</span>
            </div>
          </div>
        </section>
        <section class="panel">
          <p class="eyebrow">道具</p>
          <ul class="legend">
            <li><span class="ic" style="background:#ffd23f">⚡</span> 加速 8 秒</li>
            <li><span class="ic" style="background:#ff7a59">⁂</span> 散彈 8 秒（三連發）</li>
            <li><span class="ic" style="background:#46d0ff">🛡</span> 護盾 6 秒</li>
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
import { clearGame11Records, fetchGame11Store, saveGame11Record } from './game11Storage'
import { recordGameResult } from '@/data/lobbyScore'

const TILE = 40
const COLS = 23
const ROWS = 15
const CANVAS_W = COLS * TILE
const CANVAS_H = ROWS * TILE
const KILLS_TO_WIN = 5
const TANK = 30
const SPEED = 2.2
const BULLET_SPEED = 6.5
const FIRE_CD = 460

// 像素素材
const G11 = {}
function g11Sprite(name) {
  if (!G11[name]) {
    const img = new Image()
    img.src = `/assets/G11/${name}.png`
    G11[name] = img
  }
  return G11[name]
}
const POWER_SPRITE = { speed: 'item-star', spread: 'item-bomb', shield: 'item-shield' }
;['bg-battlefield', 'tank-p1', 'tank-p2', 'tile-brick', 'tile-steel', 'shell', 'item-star', 'item-bomb', 'item-shield'].forEach(g11Sprite)
function g11ready(img) {
  return img && img.complete && img.naturalWidth > 0
}

const canvasRef = ref(null)
const stageRef = ref(null)
const phase = ref('intro')
const resultText = ref('')
const records = ref([])
const hud = reactive({ p1Kills: 0, p2Kills: 0 })

let ctx = null
let rafId = 0
let lastFrame = 0
let game = null
const keys = new Set()

// grid: 0 empty, 1 brick(hp in brickHp), 2 steel
function buildMap() {
  const grid = Array.from({ length: ROWS }, () => Array(COLS).fill(0))
  const hp = Array.from({ length: ROWS }, () => Array(COLS).fill(0))
  // border steel
  for (let r = 0; r < ROWS; r += 1) {
    for (let c = 0; c < COLS; c += 1) {
      if (r === 0 || c === 0 || r === ROWS - 1 || c === COLS - 1) {
        grid[r][c] = 2
      }
    }
  }
  // symmetric interior pattern
  for (let r = 2; r < ROWS - 2; r += 1) {
    for (let c = 2; c < COLS / 2; c += 1) {
      const roll = Math.random()
      let v = 0
      if ((r % 3 === 0 && c % 2 === 0) || roll < 0.16) v = 1
      else if (roll < 0.2) v = 2
      // keep spawn rows clear
      if (r >= 6 && r <= 8 && (c <= 3 || c >= COLS - 4)) v = 0
      grid[r][c] = v
      grid[r][COLS - 1 - c] = v
      if (v === 1) {
        hp[r][c] = 2
        hp[r][COLS - 1 - c] = 2
      }
    }
  }
  // clear spawn areas
  for (const [sr, sc] of [[7, 1], [7, COLS - 2]]) {
    for (let dr = -1; dr <= 1; dr += 1) {
      for (let dc = -1; dc <= 1; dc += 1) {
        const r = sr + dr
        const c = sc + dc
        if (r > 0 && r < ROWS - 1 && c > 0 && c < COLS - 1) {
          grid[r][c] = 0
          hp[r][c] = 0
        }
      }
    }
  }
  return { grid, hp }
}

function makeTank(id, col, dir) {
  return {
    id,
    x: col * TILE + TILE / 2,
    y: 7 * TILE + TILE / 2,
    dir,
    kills: 0,
    fireAt: 0,
    spawnCol: col,
    invulUntil: 0,
    speedUntil: 0,
    spreadUntil: 0,
    shieldUntil: 0
  }
}

function createGame() {
  const map = buildMap()
  return {
    ...map,
    p1: makeTank('p1', 1, 1),
    p2: makeTank('p2', COLS - 2, 3),
    bullets: [],
    particles: [],
    powerups: [],
    powerTimer: 5000,
    elapsed: 0
  }
}

function solidAt(col, row) {
  if (col < 0 || row < 0 || col >= COLS || row >= ROWS) return true
  return game.grid[row][col] !== 0
}

function tankBlocked(t, nx, ny) {
  const half = TANK / 2 - 2
  const corners = [
    [nx - half, ny - half],
    [nx + half, ny - half],
    [nx - half, ny + half],
    [nx + half, ny + half]
  ]
  for (const [px, py] of corners) {
    if (solidAt(Math.floor(px / TILE), Math.floor(py / TILE))) return true
  }
  return false
}

function moveTank(t, now) {
  let dir = -1
  if (t.id === 'p1') {
    if (keys.has('w')) dir = 0
    else if (keys.has('d')) dir = 1
    else if (keys.has('s')) dir = 2
    else if (keys.has('a')) dir = 3
  } else {
    if (keys.has('arrowup')) dir = 0
    else if (keys.has('arrowright')) dir = 1
    else if (keys.has('arrowdown')) dir = 2
    else if (keys.has('arrowleft')) dir = 3
  }
  if (dir === -1) return
  t.dir = dir
  const sp = now < t.speedUntil ? SPEED * 1.7 : SPEED
  let nx = t.x
  let ny = t.y
  if (dir === 0) ny -= sp
  else if (dir === 1) nx += sp
  else if (dir === 2) ny += sp
  else nx -= sp
  // block against other tank
  const other = t.id === 'p1' ? game.p2 : game.p1
  if (Math.hypot(nx - other.x, ny - other.y) < TANK - 4) return
  if (!tankBlocked(t, nx, t.y)) t.x = nx
  if (!tankBlocked(t, t.x, ny)) t.y = ny
}

function fire(t, now) {
  if (now - t.fireAt < FIRE_CD) return
  t.fireAt = now
  const dirs = [[0, -1], [1, 0], [0, 1], [-1, 0]]
  const [dx, dy] = dirs[t.dir]
  const spread = now < t.spreadUntil
  const angles = spread ? [-0.25, 0, 0.25] : [0]
  for (const a of angles) {
    const ca = Math.cos(a)
    const sa = Math.sin(a)
    const vx = (dx * ca - dy * sa) * BULLET_SPEED
    const vy = (dx * sa + dy * ca) * BULLET_SPEED
    game.bullets.push({
      x: t.x + dx * TANK * 0.5,
      y: t.y + dy * TANK * 0.5,
      vx,
      vy,
      owner: t.id
    })
  }
  spawnParticles(t.x + dx * TANK * 0.5, t.y + dy * TANK * 0.5, '#ffd23f', 5)
}

function spawnParticles(x, y, color, count) {
  for (let i = 0; i < count; i += 1) {
    const a = Math.random() * Math.PI * 2
    const sp = 40 + Math.random() * 160
    game.particles.push({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 1, color })
  }
}

function damageBrick(col, row) {
  if (col < 0 || row < 0 || col >= COLS || row >= ROWS) return
  if (game.grid[row][col] !== 1) return
  game.hp[row][col] -= 1
  spawnParticles(col * TILE + TILE / 2, row * TILE + TILE / 2, '#c2703c', 6)
  if (game.hp[row][col] <= 0) game.grid[row][col] = 0
}

function updateBullets(now) {
  for (let i = game.bullets.length - 1; i >= 0; i -= 1) {
    const b = game.bullets[i]
    b.x += b.vx
    b.y += b.vy
    const col = Math.floor(b.x / TILE)
    const row = Math.floor(b.y / TILE)
    if (col < 0 || row < 0 || col >= COLS || row >= ROWS) {
      game.bullets.splice(i, 1)
      continue
    }
    const cell = game.grid[row][col]
    if (cell === 1) {
      damageBrick(col, row)
      game.bullets.splice(i, 1)
      continue
    }
    if (cell === 2) {
      spawnParticles(b.x, b.y, '#9aa', 4)
      game.bullets.splice(i, 1)
      continue
    }
    const target = b.owner === 'p1' ? game.p2 : game.p1
    if (now > target.invulUntil && Math.hypot(b.x - target.x, b.y - target.y) < TANK / 2) {
      game.bullets.splice(i, 1)
      if (now < target.shieldUntil) {
        target.shieldUntil = 0
        spawnParticles(target.x, target.y, '#46d0ff', 14)
        continue
      }
      hitTank(target, now)
    }
  }
}

function hitTank(target, now) {
  const shooter = target.id === 'p1' ? game.p2 : game.p1
  shooter.kills += 1
  hud.p1Kills = game.p1.kills
  hud.p2Kills = game.p2.kills
  spawnParticles(target.x, target.y, '#ff5d3a', 26)
  if (shooter.kills >= KILLS_TO_WIN) {
    finishMatch()
    return
  }
  // respawn
  target.x = target.spawnCol * TILE + TILE / 2
  target.y = 7 * TILE + TILE / 2
  target.invulUntil = now + 1600
  target.speedUntil = 0
  target.spreadUntil = 0
  target.shieldUntil = 0
}

function spawnPowerup() {
  for (let attempt = 0; attempt < 30; attempt += 1) {
    const col = 2 + Math.floor(Math.random() * (COLS - 4))
    const row = 2 + Math.floor(Math.random() * (ROWS - 4))
    if (game.grid[row][col] === 0) {
      const types = ['speed', 'spread', 'shield']
      const type = types[Math.floor(Math.random() * types.length)]
      game.powerups.push({ x: col * TILE + TILE / 2, y: row * TILE + TILE / 2, type, ttl: 11000 })
      return
    }
  }
}

function checkPowerups(now) {
  for (const t of [game.p1, game.p2]) {
    for (let i = game.powerups.length - 1; i >= 0; i -= 1) {
      const p = game.powerups[i]
      if (Math.hypot(p.x - t.x, p.y - t.y) < TANK / 2 + 10) {
        if (p.type === 'speed') t.speedUntil = now + 8000
        else if (p.type === 'spread') t.spreadUntil = now + 8000
        else if (p.type === 'shield') t.shieldUntil = now + 6000
        spawnParticles(p.x, p.y, '#ffd23f', 14)
        game.powerups.splice(i, 1)
      }
    }
  }
}

function startMatch() {
  game = createGame()
  hud.p1Kills = 0
  hud.p2Kills = 0
  phase.value = 'playing'
  lastFrame = performance.now()
  loop(lastFrame)
}

async function finishMatch() {
  cancelAnimationFrame(rafId)
  rafId = 0
  const winner = game.p1.kills > game.p2.kills ? '玩家 1 獲勝' : '玩家 2 獲勝'
  resultText.value = `💥 ${winner}`
  phase.value = 'matchover'
  recordGameResult('/game11', game.p1.kills > game.p2.kills ? 'p1' : 'p2')
  try {
    const store = await saveGame11Record({
      winner,
      scoreP1: game.p1.kills,
      scoreP2: game.p2.kills,
      date: new Date().toISOString()
    })
    records.value = store.records
  } catch {
    /* ignore */
  }
}

function update(dt, now) {
  game.elapsed += dt
  moveTank(game.p1, now)
  moveTank(game.p2, now)
  if (keys.has('f')) fire(game.p1, now)
  if (keys.has('/')) fire(game.p2, now)
  updateBullets(now)
  checkPowerups(now)
  game.powerTimer -= dt
  if (game.powerTimer <= 0) {
    game.powerTimer = 9000 + Math.random() * 5000
    if (game.powerups.length < 2) spawnPowerup()
  }
  for (const p of game.powerups) p.ttl -= dt
  game.powerups = game.powerups.filter((p) => p.ttl > 0)
  for (const pt of game.particles) {
    pt.x += pt.vx * (dt / 1000)
    pt.y += pt.vy * (dt / 1000)
    pt.vx *= 0.92
    pt.vy *= 0.92
    pt.life -= dt / 600
  }
  game.particles = game.particles.filter((p) => p.life > 0)
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
  const bgImg = g11Sprite('bg-battlefield')
  if (g11ready(bgImg)) {
    ctx.drawImage(bgImg, 0, 0, CANVAS_W, CANVAS_H)
  } else {
    ctx.fillStyle = '#11161f'
    ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  }
  // tiles
  const brickImg = g11Sprite('tile-brick')
  const steelImg = g11Sprite('tile-steel')
  for (let r = 0; r < ROWS; r += 1) {
    for (let c = 0; c < COLS; c += 1) {
      const v = game.grid[r][c]
      if (!v) continue
      const x = c * TILE
      const y = r * TILE
      const img = v === 1 ? brickImg : steelImg
      if (g11ready(img)) {
        ctx.drawImage(img, x, y, TILE, TILE)
      } else if (v === 1) {
        ctx.fillStyle = '#a45a2c'
        ctx.fillRect(x + 2, y + 2, TILE - 4, TILE - 4)
      } else {
        ctx.fillStyle = '#9aa6b5'
        ctx.fillRect(x + 3, y + 3, TILE - 6, TILE - 6)
      }
    }
  }
  for (const p of game.powerups) drawPowerup(p, now)
  drawTank(game.p1, '#3affd0', '#13d9aa', now)
  drawTank(game.p2, '#ff9ec8', '#ff6fa8', now)
  const shellImg = g11Sprite('shell')
  for (const b of game.bullets) {
    if (g11ready(shellImg)) {
      ctx.save()
      ctx.translate(b.x, b.y)
      ctx.rotate(Math.atan2(b.vy, b.vx) + Math.PI / 2)
      ctx.drawImage(shellImg, -8, -10, 16, 20)
      ctx.restore()
    } else {
      ctx.save()
      ctx.shadowColor = '#ffe66d'
      ctx.shadowBlur = 10
      ctx.fillStyle = '#fff3b0'
      ctx.beginPath()
      ctx.arc(b.x, b.y, 4, 0, Math.PI * 2)
      ctx.fill()
      ctx.restore()
    }
  }
  for (const pt of game.particles) {
    ctx.globalAlpha = Math.max(0, pt.life)
    ctx.fillStyle = pt.color
    ctx.beginPath()
    ctx.arc(pt.x, pt.y, 3 * pt.life + 1, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.globalAlpha = 1
}

function drawPowerup(p, now) {
  const colors = { speed: '#ffd23f', spread: '#ff7a59', shield: '#46d0ff' }
  const icons = { speed: '⚡', spread: '⁂', shield: '🛡' }
  const bob = Math.sin(now / 250) * 3
  const img = g11Sprite(POWER_SPRITE[p.type])
  if (g11ready(img)) {
    const sz = 34
    ctx.drawImage(img, p.x - sz / 2, p.y - sz / 2 + bob, sz, sz)
    return
  }
  ctx.save()
  ctx.shadowColor = colors[p.type]
  ctx.shadowBlur = 14
  ctx.fillStyle = 'rgba(0,0,0,0.5)'
  roundRect(p.x - 15, p.y - 15 + bob, 30, 30, 8)
  ctx.fill()
  ctx.restore()
  ctx.font = '18px serif'
  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  ctx.fillText(icons[p.type], p.x, p.y + bob)
}

function drawTank(t, light, dark, now) {
  const flicker = now < t.invulUntil && Math.floor(now / 120) % 2 === 0
  ctx.save()
  ctx.translate(t.x, t.y)
  ctx.rotate((t.dir * Math.PI) / 2)
  if (now < t.shieldUntil) {
    ctx.strokeStyle = '#46d0ff'
    ctx.lineWidth = 3
    ctx.globalAlpha = 0.7
    ctx.beginPath()
    ctx.arc(0, 0, TANK / 2 + 6, 0, Math.PI * 2)
    ctx.stroke()
    ctx.globalAlpha = 1
  }
  ctx.globalAlpha = flicker ? 0.4 : 1
  const img = g11Sprite(`tank-${t.id}`)
  if (g11ready(img)) {
    const sz = TANK + 12
    ctx.drawImage(img, -sz / 2, -sz / 2, sz, sz)
  } else {
    ctx.fillStyle = dark
    roundRect(-TANK / 2, -TANK / 2, TANK, TANK, 6)
    ctx.fill()
    ctx.fillStyle = light
    ctx.beginPath()
    ctx.arc(0, 0, TANK / 3, 0, Math.PI * 2)
    ctx.fill()
    ctx.fillStyle = light
    ctx.fillRect(-3, -TANK / 2 - 8, 6, TANK / 2)
  }
  ctx.restore()
  ctx.globalAlpha = 1
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
  keys.add(k)
  if (k.startsWith('arrow') || k === '/' || k === ' ') e.preventDefault()
  if ((k === ' ' || k === 'enter') && phase.value !== 'playing') startMatch()
}
function onKeyUp(e) {
  keys.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame11Records()
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
    const store = await fetchGame11Store()
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
.game11-view {
  min-height: 100vh;
  padding: 24px clamp(16px, 4vw, 48px) 48px;
  color: #e7ecf5;
  background: radial-gradient(circle at 50% -10%, #28323f, #0a0e15 60%);
  font-family: 'Segoe UI', system-ui, sans-serif;
}
.topbar { display: flex; align-items: center; gap: 20px; flex-wrap: wrap; margin-bottom: 22px; }
.back-link { color: #9fb0c8; text-decoration: none; font-size: 14px; padding: 8px 14px; border: 1px solid rgba(159,176,200,0.3); border-radius: 999px; transition: 0.2s; }
.back-link:hover { background: rgba(159,176,200,0.12); color: #fff; }
.title-block .eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; color: #ffd23f; text-transform: uppercase; }
.title-block h1 { margin: 2px 0 0; font-size: 26px; background: linear-gradient(90deg,#3affd0,#ff9ec8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.round-pill { margin-left: auto; padding: 8px 16px; border-radius: 999px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); font-size: 13px; }
.layout { display: grid; grid-template-columns: minmax(0,1fr) 300px; gap: 22px; align-items: start; }
.stage-card { background: rgba(10,14,21,0.6); border: 1px solid rgba(150,170,200,0.16); border-radius: 20px; padding: 16px; box-shadow: 0 24px 60px rgba(0,0,0,0.5); }
.scoreband { display: flex; align-items: center; gap: 16px; margin-bottom: 14px; padding: 0 6px; }
.team { display: flex; align-items: center; gap: 12px; flex: 1; }
.team-2 { justify-content: flex-end; }
.team .dot { width: 14px; height: 14px; border-radius: 50%; }
.team-1 .dot { background: #3affd0; box-shadow: 0 0 12px #3affd0; }
.team-2 .dot { background: #ff9ec8; box-shadow: 0 0 12px #ff9ec8; }
.team strong { font-size: 15px; }
.team .kills { font-size: 24px; font-weight: 800; font-variant-numeric: tabular-nums; }
.team-1 .kills { color: #3affd0; }
.team-2 .kills { color: #ff9ec8; }
.vs { font-size: 13px; font-weight: 800; color: #61708a; }
.stage-frame { position: relative; border-radius: 14px; overflow: hidden; }
.game-canvas { display: block; width: 100%; height: auto; border-radius: 14px; }
.overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(6,9,14,0.82); backdrop-filter: blur(4px); }
.overlay-card { text-align: center; max-width: 440px; padding: 32px; }
.overlay-eyebrow { margin: 0; font-size: 12px; letter-spacing: 3px; text-transform: uppercase; color: #ffd23f; }
.overlay-card h2 { margin: 10px 0 14px; font-size: 28px; }
.winner-text { background: linear-gradient(90deg,#3affd0,#ff9ec8); -webkit-background-clip: text; background-clip: text; color: transparent; }
.overlay-text { color: #b3c0d4; line-height: 1.7; margin: 0 0 22px; }
.primary-btn { border: none; padding: 12px 30px; border-radius: 999px; font-size: 16px; font-weight: 700; color: #05140f; background: linear-gradient(90deg,#3affd0,#46d0ff); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; box-shadow: 0 10px 26px rgba(58,255,208,0.35); }
.primary-btn:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(58,255,208,0.5); }
.sidebar { display: flex; flex-direction: column; gap: 16px; }
.panel { background: rgba(10,14,21,0.6); border: 1px solid rgba(150,170,200,0.16); border-radius: 18px; padding: 18px; }
.panel .eyebrow { margin: 0 0 12px; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #7787a0; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.controls-grid { display: grid; gap: 10px; }
.ctrl { border-radius: 12px; padding: 12px 14px; }
.ctrl strong { display: block; font-size: 14px; margin-bottom: 6px; }
.ctrl span { display: block; font-size: 13px; color: #b3c0d4; margin-bottom: 3px; }
.ctrl-1 { background: rgba(58,255,208,0.1); border: 1px solid rgba(58,255,208,0.25); }
.ctrl-2 { background: rgba(255,158,200,0.1); border: 1px solid rgba(255,158,200,0.25); }
kbd { background: rgba(255,255,255,0.12); border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 2px 7px; font-size: 12px; font-family: inherit; margin-right: 2px; }
.legend { list-style: none; margin: 0; padding: 0; display: grid; gap: 9px; }
.legend li { display: flex; align-items: center; gap: 10px; font-size: 13px; color: #c2cee2; }
.ic { width: 26px; height: 26px; border-radius: 7px; display: grid; place-items: center; font-size: 14px; flex-shrink: 0; }
.records { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.records li { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: center; font-size: 12px; padding: 8px 10px; border-radius: 10px; background: rgba(255,255,255,0.04); }
.rec-win { font-weight: 700; color: #ffd23f; }
.rec-score { color: #9fb0c8; }
.rec-date { color: #61708a; }
.empty { font-size: 13px; color: #7787a0; line-height: 1.6; margin: 0; }
.ghost-btn { background: none; border: 1px solid rgba(159,176,200,0.3); color: #9fb0c8; border-radius: 999px; padding: 4px 12px; font-size: 12px; cursor: pointer; }
.ghost-btn:hover { background: rgba(159,176,200,0.12); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } }
</style>
