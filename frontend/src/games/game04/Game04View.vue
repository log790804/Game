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
        <h1>雙人偽 3D 賽車</h1>
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
            width="1200"
            height="760"
          />
        </div>
      </div>

      <aside class="sidebar">
        <section class="panel">
          <p class="eyebrow">遊戲說明</p>
          <h2>第三視角分割競速</h2>
          <p>
            左右畫面各自追蹤自己的賽車視角。賽道包含連續彎道、路障與對手碰撞，
            撞到障礙物或直接撞上對方都會減速，還會被彈開。
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
              <span>比賽模式</span>
              <strong>{{ modeLabel }}</strong>
            </div>
            <div>
              <span>目標</span>
              <strong>先完成 1 圈</strong>
            </div>
            <div>
              <span>賽道長度</span>
              <strong>{{ Math.round(trackLength / 1000) / 10 }} km</strong>
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
          <h2>最近 10 場</h2>
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
              <span>{{ record.playerOne.finishLabel }} / {{ record.playerTwo.finishLabel }}</span>
              <p>{{ record.finishedAtLabel }}</p>
            </article>
          </div>
          <p
            v-else
            class="empty-text"
          >
            還沒有紀錄，先跑一圈看看。
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
const WIDTH = 1200
const HEIGHT = 760
const VIEW_WIDTH = WIDTH / 2
const HORIZON_Y = 118
const ROAD_WORLD_WIDTH = 2200
const SEGMENT_LENGTH = 180
const DRAW_DISTANCE = 180
const CAMERA_HEIGHT = 960
const CAMERA_DEPTH = 1.02
const CAR_BASE_Y = HEIGHT - 112
const MAX_SPEED = 920
const ACCELERATION = 520
const BRAKE_DRAG = 300
const OFFROAD_DRAG = 420
const STEER_ACCELERATION = 3.4
const STEER_RECOVERY = 5.4
const COLLISION_DISTANCE = 120
const OBSTACLE_COLLISION_DISTANCE = 100
const FINISH_GRACE_TIME = 1.4

const track = createTrack()
const trackLength = track.length * SEGMENT_LENGTH

const state = reactive(createInitialState())

const modeLabel = computed(() => {
  if (state.mode === 'menu') return '待命中'
  if (state.mode === 'playing') return '競速中'
  return '比賽結束'
})

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
    settledAt: 0,
    players: [
      createPlayer('玩家 1', '#ff8f63', '#ffe9cc', 'w', 'a', 'd'),
      createPlayer('玩家 2', '#67b7ff', '#e1f4ff', 'arrowup', 'arrowleft', 'arrowright')
    ]
  }
}

function createPlayer(name, color, accent, accelerateKey, leftKey, rightKey) {
  return {
    name,
    color,
    accent,
    accelerateKey,
    leftKey,
    rightKey,
    distance: 0,
    laneX: 0,
    laneVelocity: 0,
    speed: 0,
    lap: 0,
    progressInLap: 0,
    finishTime: null,
    hitFlash: 0,
    shake: 0
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
  if (state.mode === 'playing') {
    state.elapsed += delta

    for (const player of state.players) {
      updatePlayer(player, delta)
    }

    resolvePlayerCollision()

    for (const player of state.players) {
      resolveObstacleCollision(player)
      updateLapProgress(player)
    }

    const firstFinisher = state.players.find((player) => player.finishTime !== null)
    if (firstFinisher) {
      state.mode = 'gameover'
      state.winner = `${firstFinisher.name} 勝利`
      state.settledAt = state.elapsed
      saveRecord()
      return
    }
  }

  for (const player of state.players) {
    player.hitFlash = Math.max(0, player.hitFlash - delta * 2.2)
    player.shake = Math.max(0, player.shake - delta * 4.5)
  }
}

function updatePlayer(player, delta) {
  const accelerating = keys.has(player.accelerateKey)
  const turnLeft = keys.has(player.leftKey)
  const turnRight = keys.has(player.rightKey)
  const steering = (turnRight ? 1 : 0) - (turnLeft ? 1 : 0)
  const segment = getSegmentAt(player.distance)
  const segmentCurve = segment.curve

  if (accelerating) {
    player.speed = clamp(player.speed + ACCELERATION * delta, 0, MAX_SPEED)
  } else {
    player.speed = clamp(player.speed - BRAKE_DRAG * delta, 0, MAX_SPEED)
  }

  player.laneVelocity += steering * STEER_ACCELERATION * delta
  player.laneVelocity -= player.laneVelocity * Math.min(1, STEER_RECOVERY * delta)
  player.laneVelocity -= segmentCurve * (0.6 + player.speed / MAX_SPEED) * delta
  player.laneX = clamp(player.laneX + player.laneVelocity, -1.4, 1.4)

  const roadEdge = 0.95
  if (Math.abs(player.laneX) > roadEdge) {
    player.speed = Math.max(0, player.speed - OFFROAD_DRAG * delta * (1 + Math.abs(player.laneX) - roadEdge))
    player.laneVelocity *= 0.92
  }

  player.distance += player.speed * delta
  player.progressInLap = player.distance % trackLength
}

function resolveObstacleCollision(player) {
  const segment = getSegmentAt(player.distance)
  if (!segment.obstacles.length) return

  for (const obstacle of segment.obstacles) {
    const obstacleDistance = segment.index * SEGMENT_LENGTH + obstacle.offset
    const longitudinalDelta = shortestTrackDelta(player.distance, obstacleDistance)
    if (Math.abs(longitudinalDelta) > OBSTACLE_COLLISION_DISTANCE) continue
    if (Math.abs(player.laneX - obstacle.x) > obstacle.hitWidth) continue

    player.speed *= obstacle.slowFactor
    player.laneVelocity += (player.laneX >= obstacle.x ? 1 : -1) * obstacle.bumpForce
    player.laneX = clamp(player.laneX + (player.laneX >= obstacle.x ? 0.12 : -0.12), -1.4, 1.4)
    player.hitFlash = 1
    player.shake = Math.max(player.shake, 1)
    break
  }
}

function resolvePlayerCollision() {
  const [playerOne, playerTwo] = state.players
  const longitudinalDelta = shortestTrackDelta(playerOne.distance, playerTwo.distance)
  const lateralDelta = playerOne.laneX - playerTwo.laneX

  if (Math.abs(longitudinalDelta) > COLLISION_DISTANCE) return
  if (Math.abs(lateralDelta) > 0.24) return

  const push = lateralDelta === 0 ? 0.16 : Math.sign(lateralDelta) * 0.16
  playerOne.laneVelocity += push
  playerTwo.laneVelocity -= push
  playerOne.laneX = clamp(playerOne.laneX + push, -1.4, 1.4)
  playerTwo.laneX = clamp(playerTwo.laneX - push, -1.4, 1.4)

  const sharedSpeed = (playerOne.speed + playerTwo.speed) / 2
  playerOne.speed = Math.max(sharedSpeed * 0.72, playerOne.speed * 0.68)
  playerTwo.speed = Math.max(sharedSpeed * 0.72, playerTwo.speed * 0.68)
  playerOne.hitFlash = 0.9
  playerTwo.hitFlash = 0.9
  playerOne.shake = 1
  playerTwo.shake = 1
}

function updateLapProgress(player) {
  if (player.finishTime !== null) return
  if (player.distance < trackLength) return

  player.lap = 1
  player.finishTime = state.elapsed
}

async function saveRecord() {
  if (state.savedRecordAt === state.settledAt) return
  state.savedRecordAt = state.settledAt

  const [playerOne, playerTwo] = state.players
  const record = {
    id: `${Date.now()}-${Math.random().toString(16).slice(2)}`,
    winner: state.winner,
    finishedAt: new Date().toISOString(),
    finishedAtLabel: new Date().toLocaleString('zh-TW', { hour12: false }),
    playerOne: {
      finishLabel: formatFinishLabel(playerOne)
    },
    playerTwo: {
      finishLabel: formatFinishLabel(playerTwo)
    }
  }

  try {
    const store = await saveGame04Record(record)
    records.value = store.records ?? []
  } catch {
    records.value = records.value
  }
}

function formatFinishLabel(player) {
  if (player.finishTime !== null) return `${player.finishTime.toFixed(2)} 秒`
  return `進度 ${(player.progressInLap / trackLength * 100).toFixed(0)}%`
}

function render(context) {
  context.clearRect(0, 0, WIDTH, HEIGHT)
  drawViewport(context, state.players[0], state.players[1], 0)
  drawViewport(context, state.players[1], state.players[0], 1)
  drawDivider(context)
  drawTopHud(context)

  if (state.mode === 'menu') {
    drawOverlay(context, '雙人偽 3D 賽車', '按 Enter 開始，先完成一圈者獲勝。')
  }

  if (state.mode === 'gameover') {
    drawOverlay(context, state.winner, '按 Enter 再跑一場，障礙物與對手碰撞都會讓你減速。')
  }
}

function drawViewport(context, player, rival, viewportIndex) {
  const viewportX = viewportIndex * VIEW_WIDTH
  const centerX = viewportX + VIEW_WIDTH / 2
  const shakeX = player.shake > 0 ? Math.sin(state.elapsed * 45) * player.shake * 4 : 0

  context.save()
  context.beginPath()
  context.rect(viewportX, 0, VIEW_WIDTH, HEIGHT)
  context.clip()
  context.translate(shakeX, 0)

  drawSky(context, viewportX)
  drawMountains(context, viewportX, player.distance)
  drawRoadPerspective(context, viewportX, centerX, player)
  drawRivalInViewport(context, viewportX, centerX, player, rival)
  drawPlayerCar(context, viewportX, player)
  drawViewportHud(context, viewportX, player)

  context.restore()
}

function drawSky(context, viewportX) {
  const gradient = context.createLinearGradient(0, 0, 0, HEIGHT)
  gradient.addColorStop(0, '#8ed0ff')
  gradient.addColorStop(0.45, '#e3f5ff')
  gradient.addColorStop(1, '#f6dfac')
  context.fillStyle = gradient
  context.fillRect(viewportX, 0, VIEW_WIDTH, HEIGHT)
}

function drawMountains(context, viewportX, distance) {
  const drift = (distance * 0.02) % 200
  context.fillStyle = '#7fa1ad'
  for (let i = -1; i < 5; i += 1) {
    const x = viewportX + i * 150 - drift
    context.beginPath()
    context.moveTo(x, HORIZON_Y + 54)
    context.lineTo(x + 70, HORIZON_Y - 40)
    context.lineTo(x + 160, HORIZON_Y + 54)
    context.closePath()
    context.fill()
  }

  context.fillStyle = '#83bf6a'
  context.fillRect(viewportX, HORIZON_Y + 52, VIEW_WIDTH, HEIGHT - HORIZON_Y - 52)
}

function drawRoadPerspective(context, viewportX, centerX, player) {
  const baseIndex = Math.floor(player.distance / SEGMENT_LENGTH)
  const basePercent = (player.distance % SEGMENT_LENGTH) / SEGMENT_LENGTH
  let worldX = 0
  let dx = 0
  let lastScreenY = HEIGHT
  const renderedItems = []

  for (let n = 0; n < DRAW_DISTANCE; n += 1) {
    const currentSegment = track[(baseIndex + n) % track.length]
    const nextSegment = track[(baseIndex + n + 1) % track.length]
    const z1 = n * SEGMENT_LENGTH - basePercent * SEGMENT_LENGTH
    const z2 = z1 + SEGMENT_LENGTH

    if (z1 <= 0) {
      dx += currentSegment.curve
      worldX += dx
      continue
    }

    const scale1 = CAMERA_DEPTH / z1
    const scale2 = CAMERA_DEPTH / z2
    const x1 = centerX + (worldX - player.laneX * ROAD_WORLD_WIDTH * 0.5) * scale1 * 0.42
    const x2 = centerX + (worldX + dx - player.laneX * ROAD_WORLD_WIDTH * 0.5) * scale2 * 0.42
    const y1 = HORIZON_Y + scale1 * CAMERA_HEIGHT
    const y2 = HORIZON_Y + scale2 * CAMERA_HEIGHT
    const w1 = scale1 * ROAD_WORLD_WIDTH * 0.42
    const w2 = scale2 * ROAD_WORLD_WIDTH * 0.42

    if (y2 >= lastScreenY) {
      dx += currentSegment.curve
      worldX += dx
      continue
    }

    drawGroundStrip(context, viewportX, y2, lastScreenY, currentSegment)
    drawRoadSegment(context, x1, y1, w1, x2, y2, w2, currentSegment)
    renderedItems.push({
      segment: currentSegment,
      nextY: y2,
      centerX: x2,
      roadWidth: w2,
      z: z2
    })

    lastScreenY = y2
    dx += currentSegment.curve
    worldX += dx
  }

  drawRoadItems(context, player, renderedItems)
}

function drawGroundStrip(context, viewportX, topY, bottomY, segment) {
  context.fillStyle = segment.grassColor
  context.fillRect(viewportX, topY, VIEW_WIDTH, Math.max(0, bottomY - topY))
}

function drawRoadSegment(context, x1, y1, w1, x2, y2, w2, segment) {
  drawQuad(context, x1 - w1 * 1.16, y1, x1 - w1, y1, x2 - w2, y2, x2 - w2 * 1.16, y2, segment.rumbleColor)
  drawQuad(context, x1 + w1, y1, x1 + w1 * 1.16, y1, x2 + w2 * 1.16, y2, x2 + w2, y2, segment.rumbleColor)
  drawQuad(context, x1 - w1, y1, x1 + w1, y1, x2 + w2, y2, x2 - w2, y2, segment.roadColor)
  drawQuad(context, x1 - w1 * 0.03, y1, x1 + w1 * 0.03, y1, x2 + w2 * 0.03, y2, x2 - w2 * 0.03, y2, '#fff3d5')
}

function drawRoadItems(context, player, renderedItems) {
  for (let i = renderedItems.length - 1; i >= 0; i -= 1) {
    const item = renderedItems[i]
    if (!item.segment.obstacles.length) continue

    for (const obstacle of item.segment.obstacles) {
      const spriteX = item.centerX + obstacle.x * item.roadWidth * 0.95
      const scale = (CAMERA_DEPTH / item.z) * 1.05
      const size = Math.max(12, scale * 180)
      drawObstacle(context, obstacle, spriteX, item.nextY, size)
    }
  }
}

function drawObstacle(context, obstacle, x, baseY, size) {
  context.save()
  context.translate(x, baseY)

  if (obstacle.kind === 'cone') {
    context.fillStyle = '#ff8352'
    context.beginPath()
    context.moveTo(0, -size * 0.95)
    context.lineTo(size * 0.5, 0)
    context.lineTo(-size * 0.5, 0)
    context.closePath()
    context.fill()
    context.fillStyle = '#fff7e9'
    context.fillRect(-size * 0.26, -size * 0.42, size * 0.52, size * 0.1)
  } else if (obstacle.kind === 'barrel') {
    context.fillStyle = '#4f6074'
    context.fillRect(-size * 0.34, -size * 0.72, size * 0.68, size * 0.72)
    context.fillStyle = '#ffb25f'
    context.fillRect(-size * 0.34, -size * 0.52, size * 0.68, size * 0.12)
    context.fillRect(-size * 0.34, -size * 0.22, size * 0.68, size * 0.12)
  } else {
    context.fillStyle = '#cfd8e2'
    context.fillRect(-size * 0.48, -size * 0.6, size * 0.96, size * 0.6)
    context.fillStyle = '#ef6d5d'
    context.fillRect(-size * 0.48, -size * 0.44, size * 0.96, size * 0.12)
  }

  context.restore()
}

function drawRivalInViewport(context, viewportX, centerX, player, rival) {
  const delta = shortestTrackDelta(player.distance, rival.distance)
  if (delta <= 60 || delta > DRAW_DISTANCE * SEGMENT_LENGTH * 0.8) return

  const projected = projectCarAhead(centerX, player, rival, delta)
  if (!projected) return

  drawRivalCar(context, viewportX, rival, projected)
}

function projectCarAhead(centerX, player, rival, delta) {
  const z = Math.max(220, delta)
  const scale = CAMERA_DEPTH / z
  const screenY = HORIZON_Y + scale * CAMERA_HEIGHT
  if (screenY > HEIGHT - 60 || screenY < HORIZON_Y + 10) return null

  const screenX = centerX + (rival.laneX - player.laneX) * ROAD_WORLD_WIDTH * 0.5 * scale * 0.42
  const width = Math.max(18, scale * 210)
  const height = Math.max(28, scale * 320)
  return { screenX, screenY, width, height }
}

function drawRivalCar(context, viewportX, rival, projected) {
  context.save()
  context.translate(projected.screenX, projected.screenY)
  context.fillStyle = rival.color
  context.fillRect(-projected.width / 2, -projected.height, projected.width, projected.height)
  context.fillStyle = '#20303d'
  context.fillRect(-projected.width * 0.28, -projected.height * 0.84, projected.width * 0.56, projected.height * 0.24)
  context.fillStyle = 'rgba(255,255,255,0.45)'
  context.fillRect(-projected.width * 0.34, -projected.height * 0.14, projected.width * 0.68, projected.height * 0.12)
  context.restore()
}

function drawPlayerCar(context, viewportX, player) {
  const centerX = viewportX + VIEW_WIDTH / 2 + player.laneX * 120
  const bounce = player.shake > 0 ? Math.sin(state.elapsed * 55) * player.shake * 6 : 0

  context.save()
  context.translate(centerX, CAR_BASE_Y + bounce)
  context.fillStyle = player.color
  context.beginPath()
  context.moveTo(0, -62)
  context.lineTo(36, -18)
  context.lineTo(30, 58)
  context.lineTo(-30, 58)
  context.lineTo(-36, -18)
  context.closePath()
  context.fill()

  context.fillStyle = '#233545'
  context.beginPath()
  context.moveTo(0, -42)
  context.lineTo(20, -14)
  context.lineTo(15, 8)
  context.lineTo(-15, 8)
  context.lineTo(-20, -14)
  context.closePath()
  context.fill()

  context.fillStyle = player.hitFlash > 0 ? '#fff7da' : player.accent
  context.fillRect(-20, 18, 14, 18)
  context.fillRect(6, 18, 14, 18)
  context.fillStyle = '#171717'
  context.fillRect(-34, -10, 8, 22)
  context.fillRect(26, -10, 8, 22)
  context.fillRect(-34, 24, 8, 24)
  context.fillRect(26, 24, 8, 24)

  if (player.speed > MAX_SPEED * 0.35) {
    context.fillStyle = 'rgba(255, 224, 126, 0.78)'
    context.fillRect(-12, 60, 8, 22)
    context.fillRect(4, 60, 8, 22)
  }

  context.restore()
}

function drawViewportHud(context, viewportX, player) {
  context.fillStyle = 'rgba(255, 252, 246, 0.88)'
  context.fillRect(viewportX + 16, 16, VIEW_WIDTH - 32, 96)
  context.fillStyle = '#4d392c'
  context.font = '700 18px "Segoe UI"'
  context.fillText(`${player.name}｜進度 ${(player.progressInLap / trackLength * 100).toFixed(0)}%`, viewportX + 32, 46)

  context.fillStyle = '#89664d'
  context.font = '600 16px "Segoe UI"'
  context.fillText(`速度：${Math.round(player.speed)} km/h`, viewportX + 32, 74)
  context.fillText(`車位：${getRacePositionLabel(player)}`, viewportX + 246, 74)
}

function getRacePositionLabel(player) {
  const other = state.players.find((item) => item !== player)
  if (!other) return '1 / 2'
  return player.distance >= other.distance ? '1 / 2' : '2 / 2'
}

function drawDivider(context) {
  context.fillStyle = 'rgba(255, 255, 255, 0.92)'
  context.fillRect(VIEW_WIDTH - 5, 0, 10, HEIGHT)
  context.fillStyle = 'rgba(69, 56, 44, 0.18)'
  context.fillRect(VIEW_WIDTH - 1, 0, 2, HEIGHT)
}

function drawTopHud(context) {
  context.fillStyle = 'rgba(255, 252, 246, 0.94)'
  context.fillRect(WIDTH / 2 - 180, 18, 360, 52)
  context.fillStyle = '#4d392c'
  context.font = '700 20px "Segoe UI"'
  context.textAlign = 'center'
  context.fillText(`${modeLabel.value}｜先完成 1 圈者獲勝`, WIDTH / 2, 50)
  context.textAlign = 'start'
}

function drawOverlay(context, title, subtitle) {
  context.fillStyle = 'rgba(58, 47, 40, 0.34)'
  context.fillRect(0, 0, WIDTH, HEIGHT)
  context.fillStyle = 'rgba(255, 252, 246, 0.96)'
  context.fillRect(200, 240, WIDTH - 400, 230)
  context.strokeStyle = 'rgba(153, 110, 72, 0.45)'
  context.strokeRect(200, 240, WIDTH - 400, 230)

  context.fillStyle = '#4e3b31'
  context.textAlign = 'center'
  context.font = '800 42px "Segoe UI"'
  context.fillText(title, WIDTH / 2, 320)
  context.font = '600 22px "Segoe UI"'
  context.fillText(subtitle, WIDTH / 2, 378)
  context.font = '500 18px "Segoe UI"'
  context.fillText('玩家 1：W / A / D ｜ 玩家 2：↑ / ← / →', WIDTH / 2, 424)
  context.textAlign = 'start'
}

function resizeCanvas() {
  const canvas = canvasRef.value
  const stage = stageRef.value
  if (!canvas || !stage) return

  const availableWidth = Math.max(320, stage.clientWidth - 16)
  const availableHeight = Math.max(340, window.innerHeight - 220)
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
    winner: state.winner,
    elapsed: Number(state.elapsed.toFixed(2)),
    players: state.players.map((player) => ({
      name: player.name,
      distance: Number(player.distance.toFixed(2)),
      speed: Number(player.speed.toFixed(2)),
      laneX: Number(player.laneX.toFixed(2)),
      finishTime: player.finishTime === null ? null : Number(player.finishTime.toFixed(2))
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

function createTrack() {
  const sections = [
    makeSection(20, 0, 0.04),
    makeSection(18, 0.16, 0.12),
    makeSection(18, -0.18, -0.06),
    makeSection(22, 0, 0.18),
    makeSection(16, 0.28, 0.08),
    makeSection(18, -0.26, -0.16),
    makeSection(20, 0.08, 0.1),
    makeSection(20, -0.12, 0),
    makeSection(24, 0.22, 0.14),
    makeSection(18, -0.24, -0.18),
    makeSection(16, 0, 0.02)
  ]

  const segments = []
  let elevation = 0

  sections.forEach((section) => {
    for (let i = 0; i < section.count; i += 1) {
      elevation += section.hill
      const index = segments.length
      segments.push({
        index,
        curve: section.curve,
        hill: elevation,
        roadColor: index % 2 === 0 ? '#4a4f58' : '#454a53',
        grassColor: index % 2 === 0 ? '#84bb60' : '#7cb254',
        rumbleColor: index % 2 === 0 ? '#fdf3e6' : '#ff6e5a',
        obstacles: []
      })
    }
  })

  placeObstacles(segments)
  return segments
}

function makeSection(count, curve, hill) {
  return { count, curve, hill }
}

function placeObstacles(segments) {
  const obstaclePattern = [
    { segment: 14, x: -0.46, kind: 'cone' },
    { segment: 21, x: 0.34, kind: 'barrel' },
    { segment: 38, x: 0.08, kind: 'block' },
    { segment: 55, x: -0.3, kind: 'cone' },
    { segment: 64, x: 0.46, kind: 'barrel' },
    { segment: 82, x: -0.06, kind: 'block' },
    { segment: 101, x: 0.28, kind: 'cone' },
    { segment: 120, x: -0.44, kind: 'barrel' },
    { segment: 138, x: 0.18, kind: 'block' },
    { segment: 156, x: -0.22, kind: 'cone' },
    { segment: 173, x: 0.42, kind: 'barrel' },
    { segment: 188, x: -0.12, kind: 'block' }
  ]

  obstaclePattern.forEach((pattern, index) => {
    const segment = segments[pattern.segment % segments.length]
    segment.obstacles.push({
      kind: pattern.kind,
      x: pattern.x,
      offset: 50 + (index % 3) * 20,
      hitWidth: pattern.kind === 'cone' ? 0.12 : 0.16,
      slowFactor: pattern.kind === 'cone' ? 0.68 : pattern.kind === 'barrel' ? 0.56 : 0.48,
      bumpForce: pattern.kind === 'cone' ? 0.08 : pattern.kind === 'barrel' ? 0.12 : 0.18
    })
  })
}

function getSegmentAt(distance) {
  const normalized = normalizeDistance(distance)
  const index = Math.floor(normalized / SEGMENT_LENGTH) % track.length
  return track[index]
}

function normalizeDistance(distance) {
  return ((distance % trackLength) + trackLength) % trackLength
}

function shortestTrackDelta(fromDistance, toDistance) {
  const from = normalizeDistance(fromDistance)
  const to = normalizeDistance(toDistance)
  let delta = to - from
  if (delta > trackLength / 2) delta -= trackLength
  if (delta < -trackLength / 2) delta += trackLength
  return delta
}

function drawQuad(context, x1, y1, x2, y2, x3, y3, x4, y4, color) {
  context.fillStyle = color
  context.beginPath()
  context.moveTo(x1, y1)
  context.lineTo(x2, y2)
  context.lineTo(x3, y3)
  context.lineTo(x4, y4)
  context.closePath()
  context.fill()
}

function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max)
}
</script>

<style scoped>
.game04-view {
  width: min(1520px, calc(100% - 1rem));
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
  grid-template-columns: minmax(0, 1fr) minmax(320px, 380px);
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
  min-height: min(82vh, 760px);
  display: grid;
  place-items: center;
  overflow: hidden;
}

.game-canvas {
  display: block;
  max-width: 100%;
  max-height: min(82vh, 760px);
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
  padding: 0.85rem;
  border-radius: 18px;
  background: rgba(255, 245, 225, 0.72);
}

.controls-grid span,
.status-grid span,
.record-card span,
.record-card p,
.empty-text {
  color: #7c6858;
  font-size: 0.92rem;
}

.actions {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

button {
  border: 0;
  border-radius: 999px;
  padding: 0.9rem 1rem;
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
  max-height: 320px;
  overflow: auto;
}

@media (max-width: 1180px) {
  .layout {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 700px) {
  .controls-grid,
  .actions {
    grid-template-columns: 1fr;
  }
}
</style>
