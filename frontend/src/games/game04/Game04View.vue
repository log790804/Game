<template>
  <main class="game04-view">
    <section class="topbar">
      <RouterLink
        to="/"
        class="back-link"
      >
        返回遊戲廳
      </RouterLink>

      <div>
        <p class="eyebrow">Game 04</p>
        <h1>雙人分割賽車</h1>
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
            width="960"
            height="720"
          />
        </div>
      </div>

      <aside class="sidebar">
        <section class="panel">
          <p class="eyebrow">遊戲說明</p>
          <h2>雙人賽道衝刺</h2>
          <p>
            左右畫面分割，兩位玩家各自一條賽道。每位玩家先跑完 3 圈即結束；
            若時間到，則以圈數與距離決定勝負。
          </p>

          <div class="controls-grid">
            <div>
              <strong>玩家 1</strong>
              <span>加速：W</span>
              <span>左右：A / D</span>
            </div>
            <div>
              <strong>玩家 2</strong>
              <span>加速：↑</span>
              <span>左右：← / →</span>
            </div>
          </div>

          <div class="status-grid">
            <div>
              <span>目前模式</span>
              <strong>{{ modeLabel }}</strong>
            </div>
            <div>
              <span>剩餘時間</span>
              <strong>{{ remainingTime }} 秒</strong>
            </div>
            <div>
              <span>目標圈數</span>
              <strong>{{ LAP_TARGET }} 圈</strong>
            </div>
          </div>

          <div class="actions">
            <button
              type="button"
              class="primary"
              @click="startGame"
            >
              {{ state.mode === 'playing' ? '重新開始' : '開始比賽' }}
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
          <h2>最近 10 場結果</h2>
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
              <span>{{ record.playerOne.laps }} 圈 : {{ record.playerTwo.laps }} 圈</span>
              <p>{{ record.finishedAtLabel }} / {{ record.duration }} 秒</p>
            </article>
          </div>
          <p
            v-else
            class="empty-text"
          >
            還沒有對戰紀錄，先跑一場吧。
          </p>
        </section>
      </aside>
    </section>
  </main>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { clearGame04Records, fetchGame04Store, saveGame04Record } from './game04Storage'

const canvasRef = ref(null)
const stageRef = ref(null)
const records = ref([])

const keys = new Set()
const WIDTH = 960
const HEIGHT = 720
const VIEW_WIDTH = WIDTH / 2
const TRACK_WIDTH = 250
const HALF_TRACK = TRACK_WIDTH / 2
const ROAD_LENGTH = 5200
const CAR_WIDTH = 28
const CAR_HEIGHT = 52
const MAX_SPEED = 520
const ACCELERATION = 340
const BRAKE_DRAG = 250
const TURN_SPEED = 220
const ROUND_TIME = 75
const LAP_TARGET = 3
const CAR_START_Y = HEIGHT - 130

const state = reactive(createInitialState())

const modeLabel = computed(() => {
  if (state.mode === 'menu') return '待命中'
  if (state.mode === 'playing') return '比賽中'
  return '比賽結束'
})

const remainingTime = computed(() => Math.max(0, Math.ceil(ROUND_TIME - state.elapsed)))

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
  window.addEventListener('keyup', handleKeyUp)
  render(context)
  animationFrameId = window.requestAnimationFrame(loop)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleKeyDown)
  window.removeEventListener('keyup', handleKeyUp)
  window.removeEventListener('resize', resizeCanvas)
  resizeObserver?.disconnect()
  window.cancelAnimationFrame(animationFrameId)
  delete window.render_game_to_text
  delete window.advanceTime
})

async function loadRecords() {
  try {
    const store = await fetchGame04Store()
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
    trackOffset: 0,
    players: [
      createPlayer('玩家 1', '#ff9a62', 0),
      createPlayer('玩家 2', '#68b8ff', 1)
    ]
  }
}

function createPlayer(name, color, index) {
  return {
    name,
    color,
    index,
    laneX: 0,
    speed: 0,
    distance: 0,
    laps: 0,
    bestLapAt: 0,
    finishedAt: null
  }
}

function startGame() {
  Object.assign(state, createInitialState())
  state.mode = 'playing'
}

async function clearRecords() {
  try {
    const store = await clearGame04Records()
    records.value = store.records ?? []
  } catch {
    records.value = []
  }
}

function handleKeyDown(event) {
  keys.add(event.key.toLowerCase())

  if (event.key === 'Enter' && state.mode !== 'playing') {
    startGame()
  }
}

function handleKeyUp(event) {
  keys.delete(event.key.toLowerCase())
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
  if (state.mode !== 'playing') return

  state.elapsed += delta
  state.trackOffset += delta * 320

  updatePlayer(state.players[0], delta, { accelerate: 'w', left: 'a', right: 'd' })
  updatePlayer(state.players[1], delta, { accelerate: 'arrowup', left: 'arrowleft', right: 'arrowright' })

  if (state.players.some((player) => player.laps >= LAP_TARGET) || state.elapsed >= ROUND_TIME) {
    finishRace()
  }
}

function updatePlayer(player, delta, controls) {
  const accelerating = keys.has(controls.accelerate)
  const movingLeft = keys.has(controls.left)
  const movingRight = keys.has(controls.right)

  if (accelerating) {
    player.speed = clamp(player.speed + ACCELERATION * delta, 0, MAX_SPEED)
  } else {
    player.speed = clamp(player.speed - BRAKE_DRAG * delta, 0, MAX_SPEED)
  }

  if (movingLeft && !movingRight) {
    player.laneX = clamp(player.laneX - TURN_SPEED * delta * (0.7 + player.speed / MAX_SPEED), -HALF_TRACK + 26, HALF_TRACK - 26)
  }

  if (movingRight && !movingLeft) {
    player.laneX = clamp(player.laneX + TURN_SPEED * delta * (0.7 + player.speed / MAX_SPEED), -HALF_TRACK + 26, HALF_TRACK - 26)
  }

  if (!accelerating && Math.abs(player.laneX) > HALF_TRACK - 36) {
    player.speed = Math.max(0, player.speed - 140 * delta)
  }

  player.distance += player.speed * delta
  const nextLap = Math.floor(player.distance / ROAD_LENGTH)
  if (nextLap > player.laps) {
    player.laps = nextLap
    player.bestLapAt = state.elapsed
    if (player.laps >= LAP_TARGET && player.finishedAt === null) {
      player.finishedAt = state.elapsed
    }
  }
}

function finishRace() {
  state.mode = 'gameover'
  const [playerOne, playerTwo] = state.players
  state.winner = getWinnerLabel(playerOne, playerTwo)
  saveRecord()
}

function getWinnerLabel(playerOne, playerTwo) {
  if (playerOne.laps === playerTwo.laps) {
    const playerOneProgress = playerOne.distance % ROAD_LENGTH
    const playerTwoProgress = playerTwo.distance % ROAD_LENGTH

    if (Math.abs(playerOneProgress - playerTwoProgress) < 30) return '平手'
    return playerOneProgress > playerTwoProgress ? '玩家 1 勝利' : '玩家 2 勝利'
  }

  return playerOne.laps > playerTwo.laps ? '玩家 1 勝利' : '玩家 2 勝利'
}

async function saveRecord() {
  const [playerOne, playerTwo] = state.players
  const record = {
    id: `${Date.now()}-${Math.random().toString(16).slice(2)}`,
    winner: state.winner,
    duration: Math.round(state.elapsed),
    finishedAt: new Date().toISOString(),
    finishedAtLabel: new Date().toLocaleString('zh-TW', { hour12: false }),
    playerOne: {
      laps: playerOne.laps,
      distance: Math.round(playerOne.distance)
    },
    playerTwo: {
      laps: playerTwo.laps,
      distance: Math.round(playerTwo.distance)
    }
  }

  try {
    const store = await saveGame04Record(record)
    records.value = store.records ?? []
  } catch {
    records.value = records.value
  }
}

function render(context) {
  context.clearRect(0, 0, WIDTH, HEIGHT)
  drawTrackViewport(context, state.players[0], 0)
  drawTrackViewport(context, state.players[1], 1)
  drawDivider(context)
  drawTopHud(context)

  if (state.mode === 'menu') {
    drawOverlay(context, '雙人分割賽車', '按 Enter 或右側按鈕開始，先完成 3 圈者獲勝。')
  }

  if (state.mode === 'gameover') {
    drawOverlay(context, state.winner, '按 Enter 再跑一場，或回首頁選其他遊戲。')
  }
}

function drawTrackViewport(context, player, index) {
  const viewportX = index * VIEW_WIDTH

  context.save()
  context.beginPath()
  context.rect(viewportX, 0, VIEW_WIDTH, HEIGHT)
  context.clip()

  drawSkyAndField(context, viewportX)
  drawRoad(context, viewportX, player)
  drawCar(context, viewportX, player)
  drawViewportHud(context, viewportX, player)

  context.restore()
}

function drawSkyAndField(context, viewportX) {
  const gradient = context.createLinearGradient(0, 0, 0, HEIGHT)
  gradient.addColorStop(0, '#d4ecff')
  gradient.addColorStop(0.45, '#fef7e2')
  gradient.addColorStop(1, '#b7d99f')
  context.fillStyle = gradient
  context.fillRect(viewportX, 0, VIEW_WIDTH, HEIGHT)

  context.fillStyle = '#95c66d'
  context.fillRect(viewportX, 0, VIEW_WIDTH, HEIGHT)

  context.fillStyle = 'rgba(255, 255, 255, 0.45)'
  context.beginPath()
  context.arc(viewportX + 90, 90, 28, 0, Math.PI * 2)
  context.arc(viewportX + 122, 88, 38, 0, Math.PI * 2)
  context.arc(viewportX + 162, 92, 30, 0, Math.PI * 2)
  context.fill()
}

function drawRoad(context, viewportX, player) {
  const centerX = viewportX + VIEW_WIDTH / 2
  const roadX = centerX - HALF_TRACK
  context.fillStyle = '#404751'
  context.fillRect(roadX, 0, TRACK_WIDTH, HEIGHT)

  context.fillStyle = '#d9dbe1'
  context.fillRect(roadX - 10, 0, 10, HEIGHT)
  context.fillRect(roadX + TRACK_WIDTH, 0, 10, HEIGHT)

  const stripeOffset = (player.distance * 0.9 + state.trackOffset) % 120
  context.fillStyle = '#fff6dc'
  for (let y = -120; y < HEIGHT + 120; y += 120) {
    context.fillRect(centerX - 6, y + stripeOffset, 12, 72)
  }

  context.fillStyle = 'rgba(255,255,255,0.16)'
  context.fillRect(roadX + 20, 0, 4, HEIGHT)
  context.fillRect(roadX + TRACK_WIDTH - 24, 0, 4, HEIGHT)

  drawRoadsideMarkers(context, viewportX, stripeOffset)
}

function drawRoadsideMarkers(context, viewportX, stripeOffset) {
  for (let side = 0; side < 2; side += 1) {
    const markerX = side === 0 ? viewportX + 70 : viewportX + VIEW_WIDTH - 90
    for (let y = -80; y < HEIGHT + 80; y += 86) {
      context.fillStyle = ((Math.floor((y + stripeOffset) / 86) + side) % 2 === 0) ? '#ff725f' : '#fff7ec'
      context.fillRect(markerX, y + stripeOffset, 18, 36)
    }
  }
}

function drawCar(context, viewportX, player) {
  const carX = viewportX + VIEW_WIDTH / 2 + player.laneX
  const carY = CAR_START_Y

  context.save()
  context.translate(carX, carY)
  context.fillStyle = player.color
  context.fillRect(-CAR_WIDTH / 2, -CAR_HEIGHT / 2, CAR_WIDTH, CAR_HEIGHT)

  context.fillStyle = '#20303d'
  context.fillRect(-CAR_WIDTH / 2 + 5, -CAR_HEIGHT / 2 + 7, CAR_WIDTH - 10, 18)
  context.fillStyle = '#fefefe'
  context.fillRect(-CAR_WIDTH / 2 + 4, -CAR_HEIGHT / 2 + 24, 8, 12)
  context.fillRect(CAR_WIDTH / 2 - 12, -CAR_HEIGHT / 2 + 24, 8, 12)

  context.fillStyle = '#1e1e1e'
  context.fillRect(-CAR_WIDTH / 2 - 4, -CAR_HEIGHT / 2 + 6, 4, 14)
  context.fillRect(CAR_WIDTH / 2, -CAR_HEIGHT / 2 + 6, 4, 14)
  context.fillRect(-CAR_WIDTH / 2 - 4, CAR_HEIGHT / 2 - 20, 4, 14)
  context.fillRect(CAR_WIDTH / 2, CAR_HEIGHT / 2 - 20, 4, 14)

  if (player.speed > MAX_SPEED * 0.4) {
    context.fillStyle = 'rgba(255, 236, 150, 0.65)'
    context.fillRect(-8, CAR_HEIGHT / 2, 6, 16)
    context.fillRect(2, CAR_HEIGHT / 2, 6, 16)
  }
  context.restore()
}

function drawViewportHud(context, viewportX, player) {
  context.fillStyle = 'rgba(255, 252, 246, 0.82)'
  context.fillRect(viewportX + 18, 18, VIEW_WIDTH - 36, 88)
  context.fillStyle = '#4e3b31'
  context.font = '700 18px "Segoe UI"'
  context.fillText(`${player.name}｜${player.laps}/${LAP_TARGET} 圈`, viewportX + 34, 48)

  context.fillStyle = '#896a4f'
  context.font = '600 16px "Segoe UI"'
  context.fillText(`速度：${Math.round(player.speed)} km/h`, viewportX + 34, 72)

  const progress = Math.round((player.distance % ROAD_LENGTH) / ROAD_LENGTH * 100)
  context.fillText(`本圈進度：${progress}%`, viewportX + 220, 72)
}

function drawDivider(context) {
  context.fillStyle = 'rgba(255,255,255,0.9)'
  context.fillRect(VIEW_WIDTH - 4, 0, 8, HEIGHT)
  context.fillStyle = 'rgba(126, 92, 60, 0.2)'
  context.fillRect(VIEW_WIDTH - 1, 0, 2, HEIGHT)
}

function drawTopHud(context) {
  context.fillStyle = 'rgba(255, 252, 246, 0.92)'
  context.fillRect(WIDTH / 2 - 140, 20, 280, 52)
  context.fillStyle = '#4e3b31'
  context.font = '700 20px "Segoe UI"'
  context.textAlign = 'center'
  context.fillText(`${modeLabel.value}｜剩餘 ${remainingTime.value} 秒`, WIDTH / 2, 52)
  context.textAlign = 'start'
}

function drawOverlay(context, title, subtitle) {
  context.fillStyle = 'rgba(58, 47, 40, 0.36)'
  context.fillRect(0, 0, WIDTH, HEIGHT)
  context.fillStyle = 'rgba(255, 252, 246, 0.96)'
  context.fillRect(180, 250, WIDTH - 360, 220)
  context.strokeStyle = 'rgba(153, 110, 72, 0.45)'
  context.strokeRect(180, 250, WIDTH - 360, 220)

  context.fillStyle = '#4e3b31'
  context.textAlign = 'center'
  context.font = '800 40px "Segoe UI"'
  context.fillText(title, WIDTH / 2, 330)
  context.font = '600 22px "Segoe UI"'
  context.fillText(subtitle, WIDTH / 2, 388)
  context.font = '500 18px "Segoe UI"'
  context.fillText('玩家 1：W A D ｜ 玩家 2：方向鍵上 左 右', WIDTH / 2, 430)
  context.textAlign = 'start'
}

function resizeCanvas() {
  const canvas = canvasRef.value
  const stage = stageRef.value
  if (!canvas || !stage) return

  const availableWidth = Math.max(300, stage.clientWidth - 16)
  const availableHeight = Math.max(320, window.innerHeight - 220)
  const scale = Math.min(availableWidth / WIDTH, availableHeight / HEIGHT)
  canvas.style.width = `${Math.floor(WIDTH * scale)}px`
  canvas.style.height = `${Math.floor(HEIGHT * scale)}px`
}

function setupResizeHandling() {
  window.addEventListener('resize', resizeCanvas)

  if (typeof ResizeObserver !== 'undefined' && stageRef.value) {
    resizeObserver = new ResizeObserver(() => resizeCanvas())
    resizeObserver.observe(stageRef.value)
  }
}

function setupTestingHooks() {
  window.render_game_to_text = () => JSON.stringify({
    coordinateSystem: {
      origin: 'top-left',
      xDirection: 'right',
      yDirection: 'down'
    },
    mode: state.mode,
    elapsed: Number(state.elapsed.toFixed(2)),
    winner: state.winner,
    players: state.players.map((player) => ({
      name: player.name,
      laneX: Number(player.laneX.toFixed(2)),
      speed: Number(player.speed.toFixed(2)),
      laps: player.laps,
      progress: Number((player.distance % ROAD_LENGTH).toFixed(2))
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
.game04-view {
  width: min(1440px, calc(100% - 1rem));
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
  min-height: min(78vh, 720px);
  display: grid;
  place-items: center;
  overflow: hidden;
}

.game-canvas {
  display: block;
  max-width: 100%;
  max-height: min(78vh, 720px);
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
  border-radius: 18px;
  background: rgba(255, 245, 225, 0.72);
}

.controls-grid span,
.status-grid span,
.record-card span,
.record-card p,
.empty-text {
  color: #7c6858;
  font-size: 0.9rem;
}

.actions {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

button {
  border: 0;
  border-radius: 999px;
  padding: 0.85rem 1rem;
  background: #fff3df;
  color: #6a4e3b;
  font-weight: 800;
  cursor: pointer;
  box-shadow: inset 0 0 0 1px rgba(118, 89, 68, 0.14);
}

button.primary {
  background: linear-gradient(135deg, #f3a65d, #ef7d62);
  color: #fff;
  box-shadow: 0 14px 28px rgba(204, 112, 70, 0.24);
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
