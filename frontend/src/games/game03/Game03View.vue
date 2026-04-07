<template>
  <main class="game03-view">
    <section class="topbar">
      <RouterLink
        to="/"
        class="back-link"
      >
        返回遊戲廳
      </RouterLink>

      <div>
        <p class="eyebrow">Game 03</p>
        <h1>高樓疊疊樂</h1>
      </div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div
          ref="stageRef"
          class="stage-frame"
        >
          <canvas
            ref="canvasRef"
            class="game-canvas"
            width="900"
            height="760"
          />
        </div>
      </div>

      <aside class="sidebar">
        <section class="panel">
          <p class="eyebrow">Tower Bloxx Style</p>
          <h2>雙人同步吊車疊樓</h2>
          <p>
            兩位玩家同時操作自己的吊車，不必輪流等待。按下按鍵後，房屋樓層會從吊車落下，接上自己的高樓；偏太多就算失誤。
          </p>

          <div class="controls-grid">
            <div>
              <strong>玩家 1</strong>
              <span>釋放樓層：F</span>
              <span>左側塔</span>
            </div>
            <div>
              <strong>玩家 2</strong>
              <span>釋放樓層：L</span>
              <span>右側塔</span>
            </div>
          </div>

          <div class="status-grid">
            <div>
              <span>玩家 1</span>
              <strong>{{ playerStatus(state.players[0]) }}</strong>
            </div>
            <div>
              <span>玩家 2</span>
              <strong>{{ playerStatus(state.players[1]) }}</strong>
            </div>
            <div>
              <span>風速</span>
              <strong>{{ windLabel }}</strong>
            </div>
          </div>

          <div class="actions">
            <button
              type="button"
              class="primary"
              @click="startGame"
            >
              {{ state.mode === 'playing' ? '重新開局' : '開始遊戲' }}
            </button>
            <button
              type="button"
              @click="releaseBlock(0)"
            >
              玩家 1 釋放
            </button>
            <button
              type="button"
              @click="releaseBlock(1)"
            >
              玩家 2 釋放
            </button>
            <button
              type="button"
              @click="clearRecords"
            >
              清空紀錄
            </button>
          </div>
        </section>

        <section class="panel">
          <p class="eyebrow">本機紀錄</p>
          <h2>最近 10 局結果</h2>
          <div
            v-if="records.length"
            class="record-list"
          >
            <article
              v-for="record in records"
              :key="record.id"
              class="record-card"
            >
              <strong>{{ record.winner }}</strong>
              <span>{{ record.playerOne.floors }} : {{ record.playerTwo.floors }} 層</span>
              <p>風速 {{ record.wind }} ｜ {{ record.finishedAtLabel }}</p>
            </article>
          </div>
          <p
            v-else
            class="empty-text"
          >
            還沒有對戰紀錄，先開始第一局吧。
          </p>
        </section>
      </aside>
    </section>
  </main>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { clearGame03Records, fetchGame03Store, saveGame03Record } from './game03Storage'

const canvasRef = ref(null)
const stageRef = ref(null)
const records = ref([])

const WIDTH = 900
const HEIGHT = 760
const BLOCK_WIDTH = 118
const BLOCK_HEIGHT = 56
const MAX_MISSES = 5
const GRAVITY = 980

const state = reactive(createInitialState())
const windLabel = computed(() => `${state.windSpeed.toFixed(1)} m/s`)

let animationFrameId = 0
let lastTimestamp = 0
let resizeObserver = null

onMounted(async () => {
  await loadRecords()
  const canvas = canvasRef.value
  const context = canvas?.getContext('2d')
  if (!canvas || !context) return

  resizeCanvas()
  setupResizeHandling()
  setupTestingHooks()
  window.addEventListener('keydown', handleKeyDown)
  render(context)
  animationFrameId = window.requestAnimationFrame(loop)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleKeyDown)
  window.removeEventListener('resize', resizeCanvas)
  resizeObserver?.disconnect()
  window.cancelAnimationFrame(animationFrameId)
  delete window.render_game_to_text
  delete window.advanceTime
})

async function loadRecords() {
  try {
    const store = await fetchGame03Store()
    records.value = store.records ?? []
  } catch {
    records.value = []
  }
}

function createInitialState() {
  return {
    mode: 'menu',
    elapsed: 0,
    winner: '',
    windSpeed: 1.0,
    windPhase: 0,
    players: [
      createPlayer('玩家 1', 285, '#f0a467', 0),
      createPlayer('玩家 2', 615, '#83bfff', Math.PI)
    ],
    particles: []
  }
}

function createPlayer(name, towerX, color, phaseOffset) {
  return {
    name,
    towerX,
    color,
    phaseOffset,
    floors: [],
    misses: 0,
    lean: 0,
    cranePhase: phaseOffset,
    fallingBlock: null,
    message: '等待開始'
  }
}

function startGame() {
  Object.assign(state, createInitialState())
  state.mode = 'playing'
  state.players[0].message = '按 F 釋放樓層'
  state.players[1].message = '按 L 釋放樓層'
}

async function clearRecords() {
  try {
    const store = await clearGame03Records()
    records.value = store.records ?? []
  } catch {
    records.value = []
  }
}

function handleKeyDown(event) {
  const key = event.key.toLowerCase()

  if (key === 'enter' && state.mode !== 'playing') {
    startGame()
    return
  }

  if (key === 'f') {
    releaseBlock(0)
  }

  if (key === 'l') {
    releaseBlock(1)
  }
}

function loop(timestamp) {
  const canvas = canvasRef.value
  const context = canvas?.getContext('2d')
  if (!context) return

  if (!lastTimestamp) lastTimestamp = timestamp

  const delta = Math.min((timestamp - lastTimestamp) / 1000, 0.033)
  lastTimestamp = timestamp
  step(delta)
  render(context)
  animationFrameId = window.requestAnimationFrame(loop)
}

function step(delta) {
  state.windPhase += delta * state.windSpeed
  updateParticles(delta)

  if (state.mode !== 'playing') return

  state.elapsed += delta
  state.windSpeed = getWindSpeed()

  for (const player of state.players) {
    player.cranePhase += delta * getCraneSpeed(player)
    if (player.fallingBlock) {
      updateFallingBlock(delta, player)
    }
  }
}

function releaseBlock(playerIndex) {
  if (state.mode !== 'playing') return

  const player = state.players[playerIndex]
  if (!player || player.misses >= MAX_MISSES || player.fallingBlock) return

  const crane = getCranePosition(player)
  player.fallingBlock = {
    x: crane.x - BLOCK_WIDTH / 2,
    y: crane.y + 42,
    width: BLOCK_WIDTH,
    height: BLOCK_HEIGHT,
    vy: 40,
    drift: getWindDrift(player),
    color: player.color
  }
  player.message = '樓層下落中'
}

function updateFallingBlock(delta, player) {
  const block = player.fallingBlock
  block.vy += GRAVITY * delta
  block.y += block.vy * delta
  block.x += block.drift * delta

  const landingY = getTowerTop(player)
  if (block.y + block.height < landingY) return

  block.y = landingY - block.height
  judgeLanding(block, player)
  player.fallingBlock = null
}

function judgeLanding(block, player) {
  const targetX = getTowerTargetX(player)
  const offset = block.x - targetX
  const absOffset = Math.abs(offset)
  const tolerance = Math.max(22, 58 - player.floors.length * 1.1 - state.windSpeed * 1.2)

  if (absOffset > tolerance) {
    player.misses += 1
    player.message = `沒接上，失誤 ${player.misses}/${MAX_MISSES}`
    createDust(block.x + block.width / 2, block.y, '#ff9b7a')
  } else {
    const accuracy = Math.max(0, Math.round(100 - (absOffset / tolerance) * 100))
    player.lean = clamp(player.lean + offset * 0.075, -68, 68)
    player.floors.push({
      x: targetX + offset * 0.28,
      y: block.y,
      width: BLOCK_WIDTH,
      height: BLOCK_HEIGHT,
      color: player.color,
      offset
    })
    player.message = `成功 ${player.floors.length} 層，準度 ${accuracy}%`
    createDust(block.x + block.width / 2, block.y, player.color)
  }

  if (state.players.every((item) => item.misses >= MAX_MISSES)) {
    finishGame()
  }
}

function finishGame() {
  state.mode = 'gameover'
  const [playerOne, playerTwo] = state.players
  if (playerOne.floors.length === playerTwo.floors.length) {
    state.winner = '本局平手'
  } else {
    state.winner = playerOne.floors.length > playerTwo.floors.length ? `${playerOne.name} 勝利` : `${playerTwo.name} 勝利`
  }
  saveRecord()
}

async function saveRecord() {
  const [playerOne, playerTwo] = state.players
  const record = {
    id: `${Date.now()}-${Math.random().toString(16).slice(2)}`,
    winner: state.winner,
    wind: state.windSpeed.toFixed(1),
    finishedAt: new Date().toISOString(),
    finishedAtLabel: new Date().toLocaleString('zh-TW', { hour12: false }),
    playerOne: {
      floors: playerOne.floors.length,
      misses: playerOne.misses
    },
    playerTwo: {
      floors: playerTwo.floors.length,
      misses: playerTwo.misses
    }
  }

  try {
    const store = await saveGame03Record(record)
    records.value = store.records ?? []
  } catch {
    records.value = records.value
  }
}

function playerStatus(player) {
  return `${player.floors.length} 層 / 失誤 ${player.misses}`
}

function getCranePosition(player) {
  const heightFactor = Math.max(1, player.floors.length)
  const amplitude = 96 + Math.min(62, heightFactor * 3.5 + state.windSpeed * 6)
  return {
    x: player.towerX + Math.sin(player.cranePhase) * amplitude,
    y: 96
  }
}

function getCraneSpeed(player) {
  return 1.25 + state.windSpeed * 0.18 + player.floors.length * 0.015
}

function getWindSpeed() {
  const totalFloors = state.players.reduce((sum, player) => sum + player.floors.length, 0)
  return 1.0 + totalFloors * 0.09
}

function getWindDrift(player) {
  return Math.sin(state.windPhase + player.phaseOffset) * state.windSpeed * 15
}

function getTowerTop(player) {
  return HEIGHT - 70 - player.floors.length * BLOCK_HEIGHT
}

function getTowerSway(player) {
  const heightFactor = Math.max(1, player.floors.length)
  return Math.sin(state.windPhase + player.phaseOffset + player.floors.length * 0.22) * state.windSpeed * heightFactor * 0.4
}

function getTowerTargetX(player) {
  return player.towerX - BLOCK_WIDTH / 2 + player.lean + getTowerSway(player)
}

function updateParticles(delta) {
  state.particles = state.particles
    .map((particle) => ({
      ...particle,
      x: particle.x + particle.vx * delta,
      y: particle.y + particle.vy * delta,
      life: particle.life - delta
    }))
    .filter((particle) => particle.life > 0)
}

function createDust(x, y, color) {
  for (let index = 0; index < 12; index += 1) {
    state.particles.push({
      x,
      y,
      vx: (Math.random() - 0.5) * 130,
      vy: -50 - Math.random() * 80,
      color,
      life: 0.45 + Math.random() * 0.3
    })
  }
}

function render(context) {
  context.clearRect(0, 0, WIDTH, HEIGHT)
  drawBackground(context)
  drawPlayerTower(context, state.players[0])
  drawPlayerTower(context, state.players[1])
  drawCrane(context, state.players[0])
  drawCrane(context, state.players[1])
  drawFallingBlock(context, state.players[0])
  drawFallingBlock(context, state.players[1])
  drawParticles(context)
  drawHud(context)

  if (state.mode === 'menu') {
    drawOverlay(context, '高樓疊疊樂', '按 Enter 或右側按鈕開始')
  }

  if (state.mode === 'gameover') {
    drawOverlay(context, state.winner, '按 Enter 或右側按鈕重新開局')
  }
}

function drawBackground(context) {
  const gradient = context.createLinearGradient(0, 0, 0, HEIGHT)
  gradient.addColorStop(0, '#cfeeff')
  gradient.addColorStop(0.55, '#fff4d8')
  gradient.addColorStop(1, '#f0c99f')
  context.fillStyle = gradient
  context.fillRect(0, 0, WIDTH, HEIGHT)

  context.fillStyle = 'rgba(255,255,255,0.72)'
  context.beginPath()
  context.arc(140, 112, 42, 0, Math.PI * 2)
  context.arc(178, 112, 55, 0, Math.PI * 2)
  context.arc(226, 112, 42, 0, Math.PI * 2)
  context.fill()

  context.fillStyle = 'rgba(126, 92, 60, 0.12)'
  for (let index = 0; index < 9; index += 1) {
    const x = index * 110
    const height = 68 + (index % 3) * 28
    context.fillRect(x, HEIGHT - 58 - height, 70, height)
  }

  context.fillStyle = 'rgba(126, 92, 60, 0.2)'
  context.fillRect(0, HEIGHT - 58, WIDTH, 58)
}

function drawCrane(context, player) {
  if (state.mode !== 'playing' || player.fallingBlock || player.misses >= MAX_MISSES) return

  const crane = getCranePosition(player)
  const lineEndY = crane.y + 42

  context.strokeStyle = '#6f5642'
  context.lineWidth = 5
  context.beginPath()
  context.moveTo(player.towerX - 210, crane.y - 30)
  context.lineTo(player.towerX + 210, crane.y - 30)
  context.stroke()

  context.fillStyle = '#8c6a4f'
  context.fillRect(crane.x - 22, crane.y - 44, 44, 26)

  context.strokeStyle = '#4f3d31'
  context.lineWidth = 3
  context.beginPath()
  context.moveTo(crane.x, crane.y - 18)
  context.lineTo(crane.x, lineEndY)
  context.stroke()

  drawHouseBlock(context, crane.x - BLOCK_WIDTH / 2, lineEndY, BLOCK_WIDTH, BLOCK_HEIGHT, player.color, true)
}

function drawPlayerTower(context, player) {
  context.fillStyle = 'rgba(80, 62, 46, 0.22)'
  context.fillRect(player.towerX - 86, HEIGHT - 70, 172, 12)

  player.floors.forEach((floor, index) => {
    const swayOffset = Math.sin(state.windPhase + player.phaseOffset + index * 0.2) * state.windSpeed * (index + 1) * 0.16
    const x = floor.x + swayOffset + player.lean * 0.18
    drawHouseBlock(context, x, floor.y, floor.width, floor.height, floor.color)
  })

  context.fillStyle = '#4f3d31'
  context.font = '700 20px "Segoe UI"'
  context.textAlign = 'center'
  context.fillText(`${player.name} ${player.floors.length} 層`, player.towerX, HEIGHT - 26)
  context.fillText(`失誤 ${player.misses}/${MAX_MISSES}`, player.towerX, HEIGHT - 2)
  context.textAlign = 'start'

  if (player.floors.length === 0) {
    context.strokeStyle = 'rgba(79, 61, 49, 0.25)'
    context.setLineDash([8, 8])
    context.strokeRect(player.towerX - BLOCK_WIDTH / 2, HEIGHT - 70 - BLOCK_HEIGHT, BLOCK_WIDTH, BLOCK_HEIGHT)
    context.setLineDash([])
  }
}

function drawFallingBlock(context, player) {
  const block = player.fallingBlock
  if (!block) return

  drawHouseBlock(context, block.x, block.y, block.width, block.height, block.color, true)
}

function drawHouseBlock(context, x, y, width, height, color, highlight = false) {
  context.fillStyle = color
  context.fillRect(x, y, width, height)

  context.fillStyle = 'rgba(255, 248, 210, 0.92)'
  for (let row = 0; row < 2; row += 1) {
    for (let column = 0; column < 4; column += 1) {
      context.fillRect(x + 15 + column * 24, y + 12 + row * 20, 12, 12)
    }
  }

  context.fillStyle = 'rgba(75, 54, 39, 0.18)'
  context.fillRect(x, y + height - 6, width, 6)
  context.fillStyle = 'rgba(255,255,255,0.25)'
  context.fillRect(x, y + 4, width, 5)

  if (highlight) {
    context.strokeStyle = 'rgba(255,255,255,0.8)'
    context.lineWidth = 2
    context.strokeRect(x + 2, y + 2, width - 4, height - 4)
  }
}

function drawParticles(context) {
  state.particles.forEach((particle) => {
    context.globalAlpha = Math.max(0, particle.life * 1.8)
    context.fillStyle = particle.color
    context.fillRect(particle.x, particle.y, 5, 5)
    context.globalAlpha = 1
  })
}

function drawHud(context) {
  context.fillStyle = 'rgba(255, 252, 246, 0.82)'
  context.fillRect(24, 22, WIDTH - 48, 96)

  context.fillStyle = '#4f3d31'
  context.font = '700 21px "Segoe UI"'
  context.fillText(`玩家 1：${playerStatus(state.players[0])}`, 44, 56)
  context.fillText(`玩家 2：${playerStatus(state.players[1])}`, 44, 88)

  context.fillStyle = '#8a684d'
  context.font = '600 18px "Segoe UI"'
  context.fillText(`風速：${windLabel.value}`, WIDTH - 190, 56)
}

function drawOverlay(context, title, subtitle) {
  context.fillStyle = 'rgba(85, 66, 49, 0.35)'
  context.fillRect(0, 0, WIDTH, HEIGHT)
  context.fillStyle = 'rgba(255, 252, 246, 0.96)'
  context.fillRect(190, 260, WIDTH - 380, 220)
  context.strokeStyle = 'rgba(153, 110, 72, 0.45)'
  context.strokeRect(190, 260, WIDTH - 380, 220)

  context.fillStyle = '#4f3d31'
  context.textAlign = 'center'
  context.font = '800 42px "Segoe UI"'
  context.fillText(title, WIDTH / 2, 344)
  context.font = '600 22px "Segoe UI"'
  context.fillText(subtitle, WIDTH / 2, 398)
  context.font = '500 18px "Segoe UI"'
  context.fillText('玩家 1：F 釋放 ｜ 玩家 2：L 釋放 ｜ 兩人可以同步遊玩', WIDTH / 2, 438)
  context.textAlign = 'start'
}

function setupResizeHandling() {
  window.addEventListener('resize', resizeCanvas)

  if (typeof ResizeObserver !== 'undefined' && stageRef.value) {
    resizeObserver = new ResizeObserver(() => resizeCanvas())
    resizeObserver.observe(stageRef.value)
  }
}

function resizeCanvas() {
  const canvas = canvasRef.value
  const stage = stageRef.value
  if (!canvas || !stage) return

  const availableWidth = Math.max(280, stage.clientWidth - 16)
  const availableHeight = Math.max(360, window.innerHeight - 220)
  const scale = Math.min(availableWidth / WIDTH, availableHeight / HEIGHT)
  canvas.style.width = `${Math.floor(WIDTH * scale)}px`
  canvas.style.height = `${Math.floor(HEIGHT * scale)}px`
}

function setupTestingHooks() {
  window.render_game_to_text = () => JSON.stringify({
    coordinateSystem: {
      origin: 'top-left',
      xDirection: 'right',
      yDirection: 'down'
    },
    mode: state.mode,
    windSpeed: Number(state.windSpeed.toFixed(2)),
    winner: state.winner,
    players: state.players.map((player) => ({
      name: player.name,
      floors: player.floors.length,
      misses: player.misses,
      falling: Boolean(player.fallingBlock),
      lean: Number(player.lean.toFixed(2))
    }))
  })

  window.advanceTime = (ms) => {
    const canvas = canvasRef.value
    const context = canvas?.getContext('2d')
    if (!context) return

    const steps = Math.max(1, Math.round(ms / (1000 / 60)))
    for (let index = 0; index < steps; index += 1) {
      step(1 / 60)
    }
    render(context)
  }
}

function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max)
}
</script>

<style scoped>
.game03-view {
  width: min(1400px, calc(100% - 1rem));
  margin: 0 auto;
  display: grid;
  gap: 1.2rem;
  padding: 1rem 0 2rem;
}

.topbar {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 1rem;
  align-items: center;
}

.back-link {
  display: inline-flex;
  align-items: center;
  padding: 0.8rem 1rem;
  border-radius: 999px;
  background: rgba(255, 252, 246, 0.88);
  border: 1px solid rgba(136, 106, 83, 0.12);
  box-shadow: 0 18px 36px rgba(112, 89, 68, 0.08);
  color: #715746;
  font-weight: 700;
}

.eyebrow {
  color: #9f7c61;
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

h1 {
  margin-top: 0.3rem;
  color: #4e3d31;
  font-size: clamp(2rem, 4vw, 3rem);
}

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(300px, 360px);
  gap: 1rem;
  align-items: start;
}

.stage-card,
.panel {
  border-radius: 28px;
  background: rgba(255, 252, 246, 0.9);
  border: 1px solid rgba(137, 110, 89, 0.12);
  box-shadow: 0 18px 36px rgba(112, 89, 68, 0.12);
}

.stage-card {
  padding: 0.8rem;
}

.stage-frame {
  min-height: min(78vh, 760px);
  display: grid;
  place-items: center;
  overflow: hidden;
}

.game-canvas {
  display: block;
  max-width: 100%;
  max-height: min(78vh, 760px);
  border-radius: 22px;
}

.sidebar {
  display: grid;
  gap: 1rem;
}

.panel {
  padding: 1.25rem;
  display: grid;
  gap: 0.9rem;
}

.panel h2 {
  color: #4b392e;
}

.panel p {
  color: #6c5e52;
}

.controls-grid,
.status-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
}

.controls-grid div,
.status-grid div,
.record-card {
  display: grid;
  gap: 0.25rem;
  padding: 0.9rem 1rem;
  border-radius: 18px;
  background: rgba(247, 240, 226, 0.82);
}

.controls-grid strong,
.status-grid strong,
.record-card strong {
  color: #5a4537;
}

.controls-grid span,
.status-grid span,
.record-card span,
.record-card p,
.empty-text {
  color: #715f51;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.7rem;
}

button {
  border: 0;
  border-radius: 999px;
  padding: 0.8rem 1.15rem;
  background: #f3e6d5;
  color: #765941;
  font-weight: 700;
  cursor: pointer;
}

button.primary {
  background: linear-gradient(135deg, #f2b980, #eb9388);
  color: #fff9f2;
}

.record-list {
  display: grid;
  gap: 0.7rem;
}

@media (max-width: 1100px) {
  .layout {
    grid-template-columns: 1fr;
  }

  .stage-frame {
    min-height: min(70vh, 760px);
  }
}

@media (max-width: 720px) {
  .controls-grid,
  .status-grid {
    grid-template-columns: 1fr;
  }

  .game03-view {
    width: min(100%, calc(100% - 0.75rem));
    padding: 0.8rem 0 1.5rem;
  }
}
</style>
