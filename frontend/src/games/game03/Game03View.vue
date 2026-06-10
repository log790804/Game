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
          <p class="eyebrow">Tower Bloxx 風格</p>
          <h2>雙人同步吊車疊樓</h2>
          <p>
            玩家 1 和玩家 2 可以同時遊玩。左右兩側各自擁有獨立視角，
            當高樓逐漸變高時，畫面會依照各自樓層高度移動，避免看不到吊車或最新樓層。
          </p>

          <div class="controls-grid">
            <div>
              <strong>玩家 1</strong>
              <span>按 F 釋放樓層</span>
              <span>左側畫面</span>
            </div>
            <div>
              <strong>玩家 2</strong>
              <span>按 L 釋放樓層</span>
              <span>右側畫面</span>
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
              {{ state.mode === 'playing' ? '重新開始' : '開始遊戲' }}
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
              <p>風速 {{ record.wind }} / {{ record.finishedAtLabel }}</p>
            </article>
          </div>
          <p
            v-else
            class="empty-text"
          >
            目前還沒有本機紀錄，先玩一局就會儲存在這個瀏覽器中。
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
import { recordGameResult } from '@/data/lobbyScore'

const FLOOR_IMAGE_URLS = [
  new URL('./assets/floors/floor-01.png', import.meta.url).href,
  new URL('./assets/floors/floor-02.png', import.meta.url).href,
  new URL('./assets/floors/floor-03.png', import.meta.url).href,
  new URL('./assets/floors/floor-04.png', import.meta.url).href,
  new URL('./assets/floors/floor-05.png', import.meta.url).href,
  new URL('./assets/floors/floor-06.png', import.meta.url).href,
  new URL('./assets/floors/floor-07.png', import.meta.url).href,
  new URL('./assets/floors/floor-08.png', import.meta.url).href,
  new URL('./assets/floors/floor-09.png', import.meta.url).href,
  new URL('./assets/floors/floor-10.png', import.meta.url).href
]

const canvasRef = ref(null)
const stageRef = ref(null)
const records = ref([])
const floorImages = ref([])

const WIDTH = 900
const HEIGHT = 760
const VIEW_WIDTH = WIDTH / 2
const BLOCK_WIDTH = 118
const BLOCK_HEIGHT = 56
const MAX_MISSES = 5
const GRAVITY = 980
const TOWER_BASE_Y = HEIGHT - 70
const CRANE_TO_TOWER_GAP = 500
const CRANE_SCREEN_Y = 92

const state = reactive(createInitialState())
const windLabel = computed(() => `${state.windSpeed.toFixed(1)} m/s`)

let animationFrameId = 0
let lastTimestamp = 0
let resizeObserver = null

onMounted(async () => {
  await loadRecords()
  floorImages.value = await loadFloorImages()

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
      createPlayer('玩家 1', 285, '#f0a467', 0, 0),
      createPlayer('玩家 2', 615, '#83bfff', Math.PI, 1)
    ],
    particles: []
  }
}

function createPlayer(name, towerX, color, phaseOffset, index) {
  return {
    name,
    towerX,
    color,
    phaseOffset,
    index,
    floors: [],
    misses: 0,
    lean: 0,
    cranePhase: phaseOffset,
    fallingBlock: null,
    message: '準備中'
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

  if (key === 'f') releaseBlock(0)
  if (key === 'l') releaseBlock(1)
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
    if (player.fallingBlock) updateFallingBlock(delta, player)
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
    color: player.color,
    imageIndex: getNextImageIndex(player)
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
    player.message = `失誤 ${player.misses}/${MAX_MISSES}`
    createDust(player, block.x + block.width / 2, block.y, '#ff9b7a')
  } else {
    const accuracy = Math.max(0, Math.round(100 - (absOffset / tolerance) * 100))
    player.lean = clamp(player.lean + offset * 0.075, -68, 68)
    player.floors.push({
      x: targetX + offset * 0.28,
      y: block.y,
      width: BLOCK_WIDTH,
      height: BLOCK_HEIGHT,
      color: player.color,
      imageIndex: block.imageIndex,
      offset
    })
    player.message = `成功 ${player.floors.length} 層，準度 ${accuracy}%`
    createDust(player, block.x + block.width / 2, block.y, player.color)
  }

  if (state.players.every((item) => item.misses >= MAX_MISSES)) finishGame()
}

function finishGame() {
  state.mode = 'gameover'
  const [playerOne, playerTwo] = state.players

  if (playerOne.floors.length === playerTwo.floors.length) {
    state.winner = '平手'
  } else {
    state.winner = playerOne.floors.length > playerTwo.floors.length ? '玩家 1 勝利' : '玩家 2 勝利'
  }

  recordGameResult(
    '/game03',
    playerOne.floors.length === playerTwo.floors.length
      ? 'draw'
      : playerOne.floors.length > playerTwo.floors.length
        ? 'p1'
        : 'p2'
  )

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
  return `${player.floors.length} 層 / ${player.misses} 次失誤`
}

function getCranePosition(player) {
  const heightFactor = Math.max(1, player.floors.length)
  const amplitude = 96 + Math.min(62, heightFactor * 3.5 + state.windSpeed * 6)
  return {
    x: player.towerX + Math.sin(player.cranePhase) * amplitude,
    y: getCraneY(player)
  }
}

function getCraneY(player) {
  return getTowerTop(player) - CRANE_TO_TOWER_GAP
}

function getPlayerCameraY(player) {
  // 起始時先固定地基位置，等吊車接近可視畫面上緣時再開始追蹤。
  return Math.min(0, getCraneY(player) - CRANE_SCREEN_Y)
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

function getNextImageIndex(player) {
  const playerOffset = player.index * 5
  return (player.floors.length + playerOffset) % FLOOR_IMAGE_URLS.length
}

function loadFloorImages() {
  return Promise.all(FLOOR_IMAGE_URLS.map((url) => new Promise((resolve) => {
    const image = new Image()
    image.onload = () => resolve(image)
    image.onerror = () => resolve(null)
    image.src = url
  })))
}

function getTowerTop(player) {
  return TOWER_BASE_Y - player.floors.length * BLOCK_HEIGHT
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

function createDust(player, x, y, color) {
  for (let index = 0; index < 12; index += 1) {
    state.particles.push({
      ownerIndex: player.index,
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
  drawPlayerViewport(context, state.players[0], 0)
  drawPlayerViewport(context, state.players[1], 1)
  drawSplitDivider(context)
  drawHud(context)

  if (state.mode === 'menu') {
    drawOverlay(context, '高樓疊疊樂', '按 Enter 開始，玩家 1 和玩家 2 可以同步疊樓。')
  }

  if (state.mode === 'gameover') {
    drawOverlay(context, state.winner, '按 Enter 重新開始，挑戰更高的樓層。')
  }
}

function drawPlayerViewport(context, player, playerIndex) {
  const viewportX = playerIndex * VIEW_WIDTH
  const cameraY = getPlayerCameraY(player)

  context.save()
  context.beginPath()
  context.rect(viewportX, 0, VIEW_WIDTH, HEIGHT)
  context.clip()

  drawBackground(context, viewportX, playerIndex)

  context.save()
  context.translate(viewportX + VIEW_WIDTH / 2 - player.towerX, -cameraY)
  drawPlayerTower(context, player)
  drawCrane(context, player)
  drawFallingBlock(context, player)
  drawParticles(context, player)
  context.restore()

  drawViewportLabel(context, player, viewportX)
  context.restore()
}

function drawBackground(context, viewportX = 0, playerIndex = 0) {
  const gradient = context.createLinearGradient(0, 0, 0, HEIGHT)
  gradient.addColorStop(0, '#cfeeff')
  gradient.addColorStop(0.55, '#fff4d8')
  gradient.addColorStop(1, '#f0c99f')
  context.fillStyle = gradient
  context.fillRect(viewportX, 0, VIEW_WIDTH, HEIGHT)

  context.fillStyle = 'rgba(255, 255, 255, 0.72)'
  context.beginPath()
  context.arc(viewportX + 98 + playerIndex * 24, 112, 42, 0, Math.PI * 2)
  context.arc(viewportX + 136 + playerIndex * 24, 112, 55, 0, Math.PI * 2)
  context.arc(viewportX + 184 + playerIndex * 24, 112, 42, 0, Math.PI * 2)
  context.fill()

  context.fillStyle = 'rgba(126, 92, 60, 0.12)'
  for (let index = 0; index < 5; index += 1) {
    const x = viewportX + index * 96
    const height = 68 + (index % 3) * 28
    context.fillRect(x, HEIGHT - 58 - height, 70, height)
  }

  context.fillStyle = 'rgba(126, 92, 60, 0.2)'
  context.fillRect(viewportX, HEIGHT - 58, VIEW_WIDTH, 58)
}

function drawSplitDivider(context) {
  context.fillStyle = 'rgba(255, 252, 246, 0.92)'
  context.fillRect(VIEW_WIDTH - 3, 0, 6, HEIGHT)
  context.fillStyle = 'rgba(95, 72, 54, 0.22)'
  context.fillRect(VIEW_WIDTH - 1, 0, 2, HEIGHT)
}

function drawViewportLabel(context, player, viewportX) {
  context.fillStyle = 'rgba(255, 252, 246, 0.78)'
  context.fillRect(viewportX + 18, HEIGHT - 82, VIEW_WIDTH - 36, 54)
  context.fillStyle = '#4f3d31'
  context.font = '700 18px "Segoe UI"'
  context.textAlign = 'center'
  context.fillText(`${player.name} ${player.floors.length} 層 / 失誤 ${player.misses}/${MAX_MISSES}`, viewportX + VIEW_WIDTH / 2, HEIGHT - 49)
  context.textAlign = 'start'
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

  drawHouseBlock(context, crane.x - BLOCK_WIDTH / 2, lineEndY, BLOCK_WIDTH, BLOCK_HEIGHT, player.color, true, getNextImageIndex(player))
}

function drawPlayerTower(context, player) {
  context.fillStyle = 'rgba(80, 62, 46, 0.22)'
  context.fillRect(player.towerX - 86, TOWER_BASE_Y, 172, 12)

  player.floors.forEach((floor, index) => {
    const swayOffset = Math.sin(state.windPhase + player.phaseOffset + index * 0.2) * state.windSpeed * (index + 1) * 0.16
    const x = floor.x + swayOffset + player.lean * 0.18
    drawHouseBlock(context, x, floor.y, floor.width, floor.height, floor.color, false, floor.imageIndex)
  })

  if (player.floors.length === 0) {
    context.strokeStyle = 'rgba(79, 61, 49, 0.25)'
    context.setLineDash([8, 8])
    context.strokeRect(player.towerX - BLOCK_WIDTH / 2, TOWER_BASE_Y - BLOCK_HEIGHT, BLOCK_WIDTH, BLOCK_HEIGHT)
    context.setLineDash([])
  }
}

function drawFallingBlock(context, player) {
  const block = player.fallingBlock
  if (!block) return

  drawHouseBlock(context, block.x, block.y, block.width, block.height, block.color, true, block.imageIndex)
}

function drawHouseBlock(context, x, y, width, height, color, highlight = false, imageIndex = 0) {
  const image = floorImages.value[imageIndex]
  if (image) {
    context.drawImage(image, x, y, width, height)
  } else {
    context.fillStyle = color
    context.fillRect(x, y, width, height)
  }

  if (highlight) {
    context.strokeStyle = 'rgba(255, 255, 255, 0.8)'
    context.lineWidth = 2
    context.strokeRect(x + 2, y + 2, width - 4, height - 4)
  }
}

function drawParticles(context, player) {
  state.particles
    .filter((particle) => particle.ownerIndex === player.index)
    .forEach((particle) => {
      context.globalAlpha = Math.max(0, particle.life * 1.8)
      context.fillStyle = particle.color
      context.fillRect(particle.x, particle.y, 5, 5)
      context.globalAlpha = 1
    })
}

function drawHud(context) {
  state.players.forEach((player, index) => {
    const viewportX = index * VIEW_WIDTH
    context.fillStyle = 'rgba(255, 252, 246, 0.82)'
    context.fillRect(viewportX + 18, 20, VIEW_WIDTH - 36, 78)

    context.fillStyle = '#4f3d31'
    context.font = '700 19px "Segoe UI"'
    context.fillText(`玩家 ${index + 1}：${playerStatus(player)}`, viewportX + 34, 52)

    context.fillStyle = '#8a684d'
    context.font = '600 16px "Segoe UI"'
    context.fillText(`風速：${windLabel.value} / 視角依樓高調整`, viewportX + 34, 78)
  })
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
  context.fillText('玩家 1：F 釋放 / 玩家 2：L 釋放 / 兩位玩家可同步遊玩', WIDTH / 2, 438)
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
      lean: Number(player.lean.toFixed(2)),
      cameraY: Number(getPlayerCameraY(player).toFixed(2)),
      craneY: Number(getCraneY(player).toFixed(2)),
      towerTop: Number(getTowerTop(player).toFixed(2))
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
  position: relative;
  min-height: 100vh;
  width: min(1400px, calc(100% - 1rem));
  margin: 0 auto;
  display: grid;
  gap: 1.2rem;
  padding: 1rem 0 2rem;
  color: #eaf2ff;
  font-family: 'Segoe UI', system-ui, sans-serif;
}

.game03-view::before {
  content: '';
  position: fixed;
  inset: 0;
  z-index: -1;
  background: radial-gradient(circle at 50% -10%, #163a63, #07172b 60%);
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
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(111, 183, 255, 0.3);
  color: #9cc5f0;
  font-weight: 700;
  text-decoration: none;
  transition: 0.2s;
}

.back-link:hover {
  background: rgba(111, 183, 255, 0.14);
  color: #fff;
}

.eyebrow {
  color: #ffd36f;
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.16em;
  text-transform: uppercase;
}

h1 {
  margin-top: 0.3rem;
  font-size: clamp(2rem, 4vw, 3rem);
  background: linear-gradient(90deg, #ffd36f, #6fb7ff);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(300px, 360px);
  gap: 1rem;
  align-items: start;
}

.stage-card,
.panel {
  border-radius: 20px;
  background: rgba(8, 20, 38, 0.6);
  border: 1px solid rgba(111, 160, 220, 0.18);
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.45);
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
  border-radius: 14px;
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
  color: #eaf2ff;
}

.panel p {
  color: #9fb6dc;
}

.controls-grid,
.status-grid,
.actions {
  display: grid;
  gap: 0.75rem;
}

.controls-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.controls-grid div,
.status-grid div,
.record-card {
  display: grid;
  gap: 0.25rem;
  padding: 0.8rem;
  border-radius: 14px;
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(111, 160, 220, 0.12);
}

.controls-grid strong,
.status-grid strong,
.record-card strong {
  color: #ffd36f;
}

.controls-grid span,
.status-grid span,
.record-card span,
.record-card p,
.empty-text {
  color: #9fb6dc;
  font-size: 0.9rem;
}

.actions {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

button {
  border: 0;
  border-radius: 999px;
  padding: 0.85rem 1rem;
  background: rgba(255, 255, 255, 0.08);
  color: #9cc5f0;
  font-weight: 800;
  cursor: pointer;
}

button.primary {
  background: linear-gradient(135deg, #ffd36f, #6fb7ff);
  color: #0a1c30;
  box-shadow: 0 14px 28px rgba(111, 183, 255, 0.24);
}

.record-list {
  display: grid;
  gap: 0.75rem;
  max-height: 300px;
  overflow: auto;
}

@media (max-width: 1120px) {
  .layout {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .controls-grid,
  .actions {
    grid-template-columns: 1fr;
  }
}
</style>
