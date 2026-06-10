<template>
  <div class="victory-overlay">
    <canvas
      ref="canvasRef"
      class="fx-canvas"
    />

    <div class="victory-content">
      <p class="crown">{{ winner === 'draw' ? '🤝' : '🏆' }}</p>
      <p class="eyebrow">本輪賽事結束</p>
      <h2
        class="champion"
        :class="winner"
      >
        {{ titleText }}
      </h2>
      <p class="final-score">
        <span class="s1" :class="{ lead: winner === 'p1' }">玩家 1 · {{ scoreP1 }} 勝</span>
        <span class="dash">—</span>
        <span class="s2" :class="{ lead: winner === 'p2' }">{{ scoreP2 }} 勝 · 玩家 2</span>
      </p>

      <div class="actions">
        <button
          class="reset-btn"
          @click="$emit('reset')"
        >
          重置比分，開始新的一輪
        </button>
        <button
          class="close-btn"
          @click="$emit('close')"
        >
          先看看結果
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref, computed } from 'vue'

const props = defineProps({
  winner: { type: String, default: 'draw' },
  scoreP1: { type: Number, default: 0 },
  scoreP2: { type: Number, default: 0 }
})
defineEmits(['reset', 'close'])

const canvasRef = ref(null)

const titleText = computed(() => {
  if (props.winner === 'p1') return '玩家 1 獲勝！'
  if (props.winner === 'p2') return '玩家 2 獲勝！'
  return '勢均力敵，平手！'
})

const TEAM_COLORS = {
  p1: ['#2bd4c0', '#36d6e6', '#7dffe6', '#ffffff'],
  p2: ['#ff7ab0', '#ff9ec8', '#ffd23f', '#ffffff'],
  draw: ['#ffd23f', '#36d6e6', '#ff7ab0', '#8de96a']
}

let ctx = null
let rafId = 0
let lastTime = 0
let width = 0
let height = 0
let fireworks = []
let sparks = []
let confetti = []
let launchTimer = 0

function resize() {
  const canvas = canvasRef.value
  if (!canvas) return
  width = canvas.clientWidth
  height = canvas.clientHeight
  const dpr = Math.min(window.devicePixelRatio || 1, 2)
  canvas.width = width * dpr
  canvas.height = height * dpr
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
}

function palette() {
  return TEAM_COLORS[props.winner] || TEAM_COLORS.draw
}

function pick(arr) {
  return arr[Math.floor(Math.random() * arr.length)]
}

function spawnConfetti() {
  const colors = palette()
  for (let i = 0; i < 3; i += 1) {
    confetti.push({
      x: Math.random() * width,
      y: -20,
      vx: (Math.random() - 0.5) * 60,
      vy: 60 + Math.random() * 120,
      size: 6 + Math.random() * 8,
      rot: Math.random() * Math.PI,
      vr: (Math.random() - 0.5) * 8,
      color: pick(colors),
      sway: Math.random() * Math.PI * 2
    })
  }
}

function launchFirework() {
  fireworks.push({
    x: width * (0.2 + Math.random() * 0.6),
    y: height,
    vy: -(height * 0.012 + Math.random() * height * 0.004),
    targetY: height * (0.15 + Math.random() * 0.3),
    color: pick(palette())
  })
}

function explode(fw) {
  const count = 46 + Math.floor(Math.random() * 26)
  const colors = palette()
  for (let i = 0; i < count; i += 1) {
    const angle = (Math.PI * 2 * i) / count + Math.random() * 0.15
    const speed = 80 + Math.random() * 160
    sparks.push({
      x: fw.x,
      y: fw.y,
      vx: Math.cos(angle) * speed,
      vy: Math.sin(angle) * speed,
      life: 1,
      color: Math.random() < 0.3 ? pick(colors) : fw.color
    })
  }
}

function frame(now) {
  const dt = Math.min(48, now - lastTime) / 1000
  lastTime = now

  ctx.clearRect(0, 0, width, height)

  launchTimer -= dt
  if (launchTimer <= 0) {
    launchTimer = 0.45 + Math.random() * 0.5
    launchFirework()
  }
  if (Math.random() < 0.9) spawnConfetti()

  // fireworks rising
  for (let i = fireworks.length - 1; i >= 0; i -= 1) {
    const fw = fireworks[i]
    fw.y += fw.vy
    ctx.globalAlpha = 1
    ctx.fillStyle = fw.color
    ctx.beginPath()
    ctx.arc(fw.x, fw.y, 3, 0, Math.PI * 2)
    ctx.fill()
    if (fw.y <= fw.targetY) {
      explode(fw)
      fireworks.splice(i, 1)
    }
  }

  // sparks
  for (let i = sparks.length - 1; i >= 0; i -= 1) {
    const s = sparks[i]
    s.x += s.vx * dt
    s.y += s.vy * dt
    s.vy += 120 * dt
    s.vx *= 0.98
    s.life -= dt * 0.7
    if (s.life <= 0) {
      sparks.splice(i, 1)
      continue
    }
    ctx.globalAlpha = Math.max(0, s.life)
    ctx.fillStyle = s.color
    ctx.beginPath()
    ctx.arc(s.x, s.y, 2.4 * s.life + 0.6, 0, Math.PI * 2)
    ctx.fill()
  }

  // confetti
  for (let i = confetti.length - 1; i >= 0; i -= 1) {
    const c = confetti[i]
    c.sway += dt * 4
    c.x += (c.vx + Math.sin(c.sway) * 30) * dt
    c.y += c.vy * dt
    c.rot += c.vr * dt
    if (c.y > height + 30) {
      confetti.splice(i, 1)
      continue
    }
    ctx.globalAlpha = 1
    ctx.save()
    ctx.translate(c.x, c.y)
    ctx.rotate(c.rot)
    ctx.fillStyle = c.color
    ctx.fillRect(-c.size / 2, -c.size / 4, c.size, c.size / 2)
    ctx.restore()
  }
  ctx.globalAlpha = 1

  rafId = requestAnimationFrame(frame)
}

onMounted(() => {
  ctx = canvasRef.value.getContext('2d')
  resize()
  window.addEventListener('resize', resize)
  // initial burst
  for (let i = 0; i < 4; i += 1) launchFirework()
  lastTime = performance.now()
  rafId = requestAnimationFrame(frame)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', resize)
  if (rafId) cancelAnimationFrame(rafId)
})
</script>

<style scoped>
.victory-overlay {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: flex;
  align-items: center;
  justify-content: center;
  background: radial-gradient(circle at 50% 30%, rgba(20, 24, 48, 0.86), rgba(6, 8, 20, 0.95));
  backdrop-filter: blur(6px);
  animation: overlay-in 0.4s ease;
}
@keyframes overlay-in {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}
.fx-canvas {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}
.victory-content {
  position: relative;
  text-align: center;
  padding: 2rem;
  color: #fff;
  animation: pop-in 0.6s cubic-bezier(0.22, 1.4, 0.36, 1);
}
@keyframes pop-in {
  from {
    transform: scale(0.6);
    opacity: 0;
  }
  to {
    transform: scale(1);
    opacity: 1;
  }
}
.crown {
  font-size: 4.5rem;
  margin: 0;
  filter: drop-shadow(0 6px 18px rgba(255, 210, 63, 0.5));
  animation: bob 2s ease-in-out infinite;
}
@keyframes bob {
  0%,
  100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-10px);
  }
}
.eyebrow {
  margin: 0.4rem 0 0.6rem;
  letter-spacing: 0.35em;
  font-size: 0.8rem;
  color: rgba(255, 255, 255, 0.7);
  text-transform: uppercase;
}
.champion {
  margin: 0;
  font-size: clamp(2.4rem, 6vw, 4rem);
  font-weight: 900;
  letter-spacing: 0.04em;
}
.champion.p1 {
  background: linear-gradient(90deg, #2bd4c0, #36d6e6, #7dffe6);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
  text-shadow: 0 0 40px rgba(54, 214, 230, 0.4);
}
.champion.p2 {
  background: linear-gradient(90deg, #ff7ab0, #ff9ec8, #ffd23f);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
  text-shadow: 0 0 40px rgba(255, 122, 176, 0.4);
}
.champion.draw {
  background: linear-gradient(90deg, #ffd23f, #36d6e6, #ff7ab0);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.final-score {
  margin: 1.4rem 0 2rem;
  display: inline-flex;
  align-items: center;
  gap: 0.9rem;
  font-size: clamp(1rem, 2.2vw, 1.3rem);
  font-weight: 700;
  color: rgba(255, 255, 255, 0.85);
}
.final-score .lead {
  color: #fff;
  text-shadow: 0 0 16px rgba(255, 255, 255, 0.6);
}
.final-score .dash {
  color: rgba(255, 255, 255, 0.4);
}
.actions {
  display: flex;
  gap: 0.9rem;
  justify-content: center;
  flex-wrap: wrap;
}
.reset-btn {
  border: none;
  padding: 0.9rem 1.8rem;
  border-radius: 999px;
  font-size: 1rem;
  font-weight: 800;
  color: #122;
  background: linear-gradient(90deg, #ffd23f, #ffb142);
  cursor: pointer;
  box-shadow: 0 12px 30px rgba(255, 178, 66, 0.4);
  transition: transform 0.15s, box-shadow 0.15s;
}
.reset-btn:hover {
  transform: translateY(-2px);
  box-shadow: 0 16px 36px rgba(255, 178, 66, 0.55);
}
.close-btn {
  border: 1px solid rgba(255, 255, 255, 0.3);
  background: rgba(255, 255, 255, 0.06);
  color: #fff;
  padding: 0.9rem 1.6rem;
  border-radius: 999px;
  font-size: 1rem;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.15s;
}
.close-btn:hover {
  background: rgba(255, 255, 255, 0.16);
}
</style>
