<template>
  <main class="game02-view">
    <section class="topbar">
      <RouterLink
        to="/"
        class="back-link"
      >
        返回遊戲廳
      </RouterLink>

      <div>
        <p class="eyebrow">Game 02</p>
        <h1>雙人空戰射擊</h1>
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
            width="720"
            height="960"
          />
        </div>
      </div>

      <aside class="sidebar">
        <section class="panel">
          <p class="eyebrow">遊戲說明</p>
          <h2>雙人共鬥空戰</h2>
          <p>
            難度會依照玩家火力與戰場進度慢慢提高，不再一開始就大量湧入敵機。玩家 1 與玩家 2 也都能自由橫向移動，不受中線限制。
          </p>

          <div class="controls-grid">
            <div>
              <strong>玩家 1</strong>
              <span>移動：W A S D</span>
              <span>射擊：F</span>
            </div>
            <div>
              <strong>玩家 2</strong>
              <span>移動：方向鍵</span>
              <span>射擊：L</span>
            </div>
          </div>

          <div class="status-grid">
            <div>
              <span>目前模式</span>
              <strong>{{ modeLabel }}</strong>
            </div>
            <div>
              <span>難度等級</span>
              <strong>{{ difficultyLabel }}</strong>
            </div>
            <div>
              <span>剩餘時間</span>
              <strong>{{ remainingTime }} 秒</strong>
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
              <span>{{ record.playerOne.score }} : {{ record.playerTwo.score }}</span>
              <p>{{ record.difficulty }} ｜ {{ record.duration }} 秒 ｜ {{ record.finishedAtLabel }}</p>
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
import { clearGame02Records, fetchGame02Store, saveGame02Record } from './game02Storage'

const canvasRef = ref(null)
const stageRef = ref(null)
const records = ref([])

const keys = new Set()
const WIDTH = 720
const HEIGHT = 960
const PLAYER_SPEED = 280
const BULLET_SPEED = 620
const PLAYER_SHOT_COOLDOWN = 0.12
const PLAYER_RADIUS = 24
const ROUND_TIME = 70
const BASE_ENEMY_SPEED = 118
const MAX_RECORDS = 10

const state = reactive(createInitialState())

const modeLabel = computed(() => {
  if (state.mode === 'menu') return '待命中'
  if (state.mode === 'playing') return '交戰中'
  return '回合結束'
})

const remainingTime = computed(() => Math.max(0, Math.ceil(ROUND_TIME - state.elapsed)))
const difficultyScore = computed(() => getDifficultyScore())
const difficultyLabel = computed(() => {
  if (difficultyScore.value < 1.8) return '暖身'
  if (difficultyScore.value < 2.6) return '標準'
  if (difficultyScore.value < 3.4) return '進階'
  return '高壓'
})

let animationFrameId = 0
let lastTimestamp = 0
let enemySpawnTimer = 0
let resizeObserver = null

onMounted(async () => {
  await loadRecords()
  const canvas = canvasRef.value
  const context = canvas?.getContext('2d')
  if (!canvas || !context) {
    return
  }

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
    const store = await fetchGame02Store()
    records.value = (store.records ?? []).slice(0, MAX_RECORDS)
  } catch {
    records.value = []
  }
}

function createInitialState() {
  return {
    mode: 'menu',
    elapsed: 0,
    winner: '',
    players: [
      createPlayer('玩家 1', 240, '#ffb36b'),
      createPlayer('玩家 2', 480, '#7bc6ff')
    ],
    bullets: [],
    enemies: [],
    particles: [],
    stars: createStars(),
    stageScroll: 0,
    waveMessage: '雙人空戰待命'
  }
}

function createPlayer(name, x, color) {
  return {
    name,
    x,
    y: HEIGHT - 120,
    color,
    score: 0,
    hp: 5,
    shotCooldown: 0,
    powerLevel: 1
  }
}

function createStars() {
  return Array.from({ length: 44 }, (_, index) => ({
    x: (index * 97) % WIDTH,
    y: (index * 151) % HEIGHT,
    speed: 35 + (index % 5) * 14,
    size: 1 + (index % 3)
  }))
}

function startGame() {
  Object.assign(state, createInitialState())
  state.mode = 'playing'
  state.waveMessage = '敵機雷達剛開始鎖定'
  lastTimestamp = 0
  enemySpawnTimer = 1.1
}

async function clearRecords() {
  try {
    const store = await clearGame02Records()
    records.value = store.records ?? []
  } catch {
    records.value = []
  }
}

function handleKeyDown(event) {
  keys.add(event.key.toLowerCase())

  if (event.key.toLowerCase() === 'enter' && state.mode !== 'playing') {
    startGame()
  }
}

function handleKeyUp(event) {
  keys.delete(event.key.toLowerCase())
}

function loop(timestamp) {
  const canvas = canvasRef.value
  const context = canvas?.getContext('2d')
  if (!context) {
    return
  }

  if (!lastTimestamp) {
    lastTimestamp = timestamp
  }

  const delta = Math.min((timestamp - lastTimestamp) / 1000, 0.033)
  lastTimestamp = timestamp
  step(delta)
  render(context)
  animationFrameId = window.requestAnimationFrame(loop)
}

function step(delta) {
  updateStars(delta)

  if (state.mode !== 'playing') {
    return
  }

  state.elapsed += delta
  state.stageScroll += delta * 170
  enemySpawnTimer -= delta

  if (enemySpawnTimer <= 0) {
    spawnFormation()
    const spawnInterval = Math.max(0.55, 1.45 - getDifficultyScore() * 0.2)
    enemySpawnTimer = spawnInterval
  }

  updatePlayers(delta)
  updateBullets(delta)
  updateEnemies(delta)
  updateParticles(delta)
  resolveCollisions()

  if (state.elapsed >= ROUND_TIME || state.players.every((player) => player.hp <= 0)) {
    finishGame()
  }
}

function updateStars(delta) {
  for (const star of state.stars) {
    star.y += star.speed * delta
    if (star.y > HEIGHT) {
      star.y = -8
      star.x = Math.random() * WIDTH
    }
  }
}

function updatePlayers(delta) {
  const bindings = [
    { up: 'w', down: 's', left: 'a', right: 'd', fire: 'f' },
    { up: 'arrowup', down: 'arrowdown', left: 'arrowleft', right: 'arrowright', fire: 'l' }
  ]

  state.players.forEach((player, index) => {
    if (player.hp <= 0) {
      return
    }

    const input = bindings[index]
    let moveX = 0
    let moveY = 0

    if (keys.has(input.left)) moveX -= 1
    if (keys.has(input.right)) moveX += 1
    if (keys.has(input.up)) moveY -= 1
    if (keys.has(input.down)) moveY += 1

    if (moveX !== 0 || moveY !== 0) {
      const length = Math.hypot(moveX, moveY)
      player.x += (moveX / length) * PLAYER_SPEED * delta
      player.y += (moveY / length) * PLAYER_SPEED * delta
    }

    player.x = clamp(player.x, 48, WIDTH - 48)
    player.y = clamp(player.y, 90, HEIGHT - 54)

    player.shotCooldown = Math.max(0, player.shotCooldown - delta)

    if (keys.has(input.fire) && player.shotCooldown === 0) {
      spawnBulletSpread(player)
      player.shotCooldown = PLAYER_SHOT_COOLDOWN
    }
  })
}

function spawnBulletSpread(player) {
  const patterns = {
    1: [0],
    2: [-18, 18],
    3: [-26, 0, 26]
  }

  const offsets = patterns[Math.min(player.powerLevel, 3)] ?? [0]
  offsets.forEach((offset) => {
    state.bullets.push({
      id: `${player.name}-${Math.random().toString(16).slice(2)}`,
      owner: player.name,
      x: player.x + offset,
      y: player.y - 26,
      vy: -BULLET_SPEED,
      vx: offset * 0.5,
      radius: offset === 0 ? 7 : 5,
      color: player.color
    })
  })
}

function spawnFormation() {
  const difficulty = getDifficultyScore()
  const playerPower = getAveragePowerLevel()

  if (difficulty < 1.7) {
    spawnLightFormation()
    return
  }

  if (difficulty < 2.5) {
    Math.random() > 0.35 ? spawnWideFormation() : spawnLightFormation()
    return
  }

  if (playerPower >= 2.5 && Math.random() > 0.55) {
    spawnEliteFormation()
    return
  }

  Math.random() > 0.45 ? spawnSweepFormation() : spawnWideFormation()
}

function spawnLightFormation() {
  state.waveMessage = '敵機零星靠近'
  const count = 2 + Math.floor(getDifficultyScore() * 0.8)
  for (let index = 0; index < count; index += 1) {
    spawnEnemy(140 + index * 160, -index * 50, 2, index % 2 === 0 ? 0.15 : -0.15)
  }
}

function spawnWideFormation() {
  state.waveMessage = '敵機橫列壓境'
  const positions = [90, 210, 330, 450, 570, 630]
  positions.slice(0, 3 + Math.floor(getDifficultyScore())).forEach((x, index) => {
    spawnEnemy(x, -index * 42, 2 + Math.floor(getDifficultyScore() * 0.4), index % 2 === 0 ? 0.2 : -0.2)
  })
}

function spawnSweepFormation() {
  state.waveMessage = '左右夾擊中'
  for (let index = 0; index < 4; index += 1) {
    const fromLeft = index % 2 === 0
    spawnEnemy(fromLeft ? 70 : WIDTH - 70, -index * 56, 2 + Math.floor(getDifficultyScore() * 0.5), fromLeft ? 0.65 : -0.65)
  }
}

function spawnEliteFormation() {
  state.waveMessage = '精英敵機登場'
  spawnEnemy(WIDTH / 2, -40, 8 + Math.round(getDifficultyScore()), 0, true)
  spawnEnemy(WIDTH / 2 - 130, -110, 3, 0.28)
  spawnEnemy(WIDTH / 2 + 130, -110, 3, -0.28)
}

function spawnEnemy(x, y, hp = 2, driftFactor = 0, isElite = false) {
  const difficulty = getDifficultyScore()
  state.enemies.push({
    id: `enemy-${Math.random().toString(16).slice(2)}`,
    x,
    y,
    radius: isElite ? 34 : 24,
    hp,
    maxHp: hp,
    speed: BASE_ENEMY_SPEED + difficulty * 14 + Math.random() * 24,
    drift: driftFactor * 62,
    isElite
  })
}

function updateBullets(delta) {
  state.bullets = state.bullets
    .map((bullet) => ({
      ...bullet,
      x: bullet.x + bullet.vx * delta,
      y: bullet.y + bullet.vy * delta
    }))
    .filter((bullet) => bullet.y > -40)
}

function updateEnemies(delta) {
  state.enemies = state.enemies
    .map((enemy) => ({
      ...enemy,
      y: enemy.y + enemy.speed * delta,
      x: clamp(enemy.x + enemy.drift * delta, 40, WIDTH - 40)
    }))
    .filter((enemy) => enemy.y < HEIGHT + 60 && enemy.hp > 0)
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

function resolveCollisions() {
  for (const bullet of state.bullets) {
    for (const enemy of state.enemies) {
      if (distanceBetween(bullet.x, bullet.y, enemy.x, enemy.y) <= bullet.radius + enemy.radius) {
        enemy.hp -= 1
        bullet.y = -999
        bullet.x = -999

        if (enemy.hp <= 0) {
          const owner = state.players.find((player) => player.name === bullet.owner)
          if (owner) {
            owner.score += enemy.isElite ? 420 : 140
            if (owner.score >= 900) {
              owner.powerLevel = 2
            }
            if (owner.score >= 2200) {
              owner.powerLevel = 3
            }
          }
          createExplosion(enemy.x, enemy.y, enemy.isElite ? '#ffe48c' : '#ffd36f', enemy.isElite ? 18 : 12)
        }
      }
    }
  }

  state.bullets = state.bullets.filter((bullet) => bullet.y > -100)
  state.enemies = state.enemies.filter((enemy) => enemy.hp > 0)

  for (const enemy of state.enemies) {
    for (const player of state.players) {
      if (player.hp <= 0) {
        continue
      }

      if (distanceBetween(enemy.x, enemy.y, player.x, player.y) <= enemy.radius + PLAYER_RADIUS) {
        player.hp = Math.max(0, player.hp - (enemy.isElite ? 2 : 1))
        enemy.hp = 0
        createExplosion(enemy.x, enemy.y, '#ff957a', enemy.isElite ? 20 : 12)
      }
    }
  }

  state.enemies = state.enemies.filter((enemy) => enemy.hp > 0)
}

function createExplosion(x, y, color, count) {
  for (let index = 0; index < count; index += 1) {
    const angle = (Math.PI * 2 * index) / count
    state.particles.push({
      x,
      y,
      vx: Math.cos(angle) * (80 + Math.random() * 110),
      vy: Math.sin(angle) * (80 + Math.random() * 110),
      color,
      life: 0.4 + Math.random() * 0.28
    })
  }
}

function finishGame() {
  if (state.mode !== 'playing') {
    return
  }

  state.mode = 'gameover'
  const [playerOne, playerTwo] = state.players

  if (playerOne.score === playerTwo.score) {
    state.winner = '本局平手'
  } else {
    state.winner = playerOne.score > playerTwo.score ? `${playerOne.name} 勝利` : `${playerTwo.name} 勝利`
  }

  saveRecord()
}

async function saveRecord() {
  const [playerOne, playerTwo] = state.players
  const record = {
    id: `${Date.now()}-${Math.random().toString(16).slice(2)}`,
    winner: state.winner,
    difficulty: difficultyLabel.value,
    duration: Math.round(state.elapsed),
    finishedAt: new Date().toISOString(),
    finishedAtLabel: new Date().toLocaleString('zh-TW', {
      hour12: false
    }),
    playerOne: {
      name: playerOne.name,
      score: playerOne.score,
      hp: playerOne.hp
    },
    playerTwo: {
      name: playerTwo.name,
      score: playerTwo.score,
      hp: playerTwo.hp
    }
  }

  try {
    const store = await saveGame02Record(record)
    records.value = (store.records ?? []).slice(0, MAX_RECORDS)
  } catch {
    records.value = records.value
  }
}

function render(context) {
  context.clearRect(0, 0, WIDTH, HEIGHT)
  drawBackground(context)
  drawBullets(context)
  drawEnemies(context)
  drawPlayers(context)
  drawParticles(context)
  drawHud(context)

  if (state.mode === 'menu') {
    drawOverlay(context, '雙人空戰射擊', '按 Enter 或右側按鈕開始')
  }

  if (state.mode === 'gameover') {
    drawOverlay(context, state.winner || '回合結束', '按 Enter 或右側按鈕重新開局')
  }
}

function drawBackground(context) {
  const gradient = context.createLinearGradient(0, 0, 0, HEIGHT)
  gradient.addColorStop(0, '#132746')
  gradient.addColorStop(0.55, '#18375f')
  gradient.addColorStop(1, '#081524')
  context.fillStyle = gradient
  context.fillRect(0, 0, WIDTH, HEIGHT)

  context.fillStyle = 'rgba(255,255,255,0.78)'
  for (const star of state.stars) {
    context.fillRect(star.x, star.y, star.size, star.size)
  }

  const glow = context.createRadialGradient(WIDTH / 2, HEIGHT * 0.18, 40, WIDTH / 2, HEIGHT * 0.18, 260)
  glow.addColorStop(0, 'rgba(255, 221, 152, 0.22)')
  glow.addColorStop(1, 'rgba(255, 221, 152, 0)')
  context.fillStyle = glow
  context.fillRect(0, 0, WIDTH, HEIGHT)
}

function drawPlayers(context) {
  for (const player of state.players) {
    if (player.hp <= 0) {
      continue
    }

    context.save()
    context.translate(player.x, player.y)
    context.fillStyle = player.color
    context.beginPath()
    context.moveTo(0, -34)
    context.lineTo(22, 20)
    context.lineTo(8, 10)
    context.lineTo(0, 28)
    context.lineTo(-8, 10)
    context.lineTo(-22, 20)
    context.closePath()
    context.fill()

    context.fillStyle = 'rgba(255,255,255,0.85)'
    context.fillRect(-5, -6, 10, 18)
    context.fillRect(-16, 4, 8, 12)
    context.fillRect(8, 4, 8, 12)

    if (player.powerLevel >= 2) {
      context.fillStyle = 'rgba(255, 240, 180, 0.75)'
      context.fillRect(-24, -2, 6, 16)
      context.fillRect(18, -2, 6, 16)
    }

    context.restore()
  }
}

function drawBullets(context) {
  for (const bullet of state.bullets) {
    context.fillStyle = bullet.color
    context.beginPath()
    context.arc(bullet.x, bullet.y, bullet.radius, 0, Math.PI * 2)
    context.fill()
  }
}

function drawEnemies(context) {
  for (const enemy of state.enemies) {
    context.save()
    context.translate(enemy.x, enemy.y)
    context.fillStyle = enemy.isElite ? '#ffd56e' : '#ff7367'
    context.beginPath()
    context.moveTo(0, 30)
    context.lineTo(26, -12)
    context.lineTo(12, -10)
    context.lineTo(0, -34)
    context.lineTo(-12, -10)
    context.lineTo(-26, -12)
    context.closePath()
    context.fill()

    if (enemy.isElite) {
      context.fillStyle = 'rgba(255,255,255,0.72)'
      context.fillRect(-7, -4, 14, 20)
    }

    context.fillStyle = 'rgba(15, 31, 52, 0.72)'
    context.fillRect(-18, 36, 36, 5)
    context.fillStyle = enemy.isElite ? '#ffe8a2' : '#ffa598'
    context.fillRect(-18, 36, 36 * (enemy.hp / enemy.maxHp), 5)
    context.restore()
  }
}

function drawParticles(context) {
  for (const particle of state.particles) {
    context.fillStyle = particle.color
    context.globalAlpha = Math.max(0, particle.life * 1.8)
    context.fillRect(particle.x, particle.y, 4, 4)
    context.globalAlpha = 1
  }
}

function drawHud(context) {
  context.fillStyle = 'rgba(7, 15, 28, 0.62)'
  context.fillRect(20, 18, WIDTH - 40, 96)

  context.fillStyle = '#ffcf97'
  context.font = '700 22px "Segoe UI"'
  context.fillText(`玩家 1  分數 ${state.players[0].score}  HP ${state.players[0].hp}  Lv${state.players[0].powerLevel}`, 36, 48)

  context.fillStyle = '#8bd0ff'
  context.fillText(`玩家 2  分數 ${state.players[1].score}  HP ${state.players[1].hp}  Lv${state.players[1].powerLevel}`, 36, 78)

  context.fillStyle = '#f7f6f3'
  context.font = '600 18px "Segoe UI"'
  context.fillText(`剩餘時間 ${remainingTime.value}s`, WIDTH - 170, 46)
  context.fillText(`難度 ${difficultyLabel.value}`, WIDTH - 170, 76)
}

function drawOverlay(context, title, subtitle) {
  context.fillStyle = 'rgba(5, 12, 24, 0.62)'
  context.fillRect(0, 0, WIDTH, HEIGHT)

  context.fillStyle = 'rgba(255, 248, 236, 0.95)'
  context.fillRect(90, 288, WIDTH - 180, 250)
  context.strokeStyle = 'rgba(255, 191, 121, 0.65)'
  context.lineWidth = 3
  context.strokeRect(90, 288, WIDTH - 180, 250)

  context.fillStyle = '#402d23'
  context.textAlign = 'center'
  context.font = '800 42px "Segoe UI"'
  context.fillText(title, WIDTH / 2, 376)
  context.font = '600 22px "Segoe UI"'
  context.fillText(subtitle, WIDTH / 2, 430)
  context.font = '500 18px "Segoe UI"'
  context.fillText('玩家 1: WASD + F ｜ 玩家 2: 方向鍵 + L', WIDTH / 2, 474)
  context.fillText('兩位玩家都可以橫向飛過整個戰場', WIDTH / 2, 508)
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
  if (!canvas || !stage) {
    return
  }

  const availableWidth = Math.max(260, stage.clientWidth - 16)
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
    timerRemaining: remainingTime.value,
    difficulty: difficultyLabel.value,
    players: state.players.map((player) => ({
      name: player.name,
      x: Math.round(player.x),
      y: Math.round(player.y),
      hp: player.hp,
      score: player.score,
      powerLevel: player.powerLevel
    })),
    bullets: state.bullets.slice(0, 10).map((bullet) => ({
      x: Math.round(bullet.x),
      y: Math.round(bullet.y),
      owner: bullet.owner
    })),
    enemies: state.enemies.slice(0, 10).map((enemy) => ({
      x: Math.round(enemy.x),
      y: Math.round(enemy.y),
      hp: enemy.hp,
      elite: enemy.isElite
    })),
    winner: state.winner
  })

  window.advanceTime = (ms) => {
    const canvas = canvasRef.value
    const context = canvas?.getContext('2d')
    if (!context) {
      return
    }

    const steps = Math.max(1, Math.round(ms / (1000 / 60)))
    for (let index = 0; index < steps; index += 1) {
      step(1 / 60)
    }
    render(context)
  }
}

function getAveragePowerLevel() {
  return state.players.reduce((sum, player) => sum + player.powerLevel, 0) / state.players.length
}

function getDifficultyScore() {
  const powerFactor = getAveragePowerLevel() - 1
  const timeFactor = Math.min(2.2, state.elapsed / 26)
  const survivalFactor = state.players.some((player) => player.hp >= 4) ? 0.15 : 0
  return 1 + powerFactor * 0.85 + timeFactor + survivalFactor
}

function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max)
}

function distanceBetween(ax, ay, bx, by) {
  return Math.hypot(ax - bx, ay - by)
}
</script>

<style scoped>
.game02-view {
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
  min-height: min(78vh, 960px);
  display: grid;
  place-items: center;
  overflow: hidden;
}

.game-canvas {
  display: block;
  max-width: 100%;
  max-height: min(78vh, 960px);
  border-radius: 22px;
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.08);
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
    min-height: min(70vh, 860px);
  }
}

@media (max-width: 720px) {
  .controls-grid,
  .status-grid {
    grid-template-columns: 1fr;
  }

  .game02-view {
    width: min(100%, calc(100% - 0.75rem));
    padding: 0.8rem 0 1.5rem;
  }
}
</style>
