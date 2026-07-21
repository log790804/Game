<template>
  <main class="game06-view">
    <section class="topbar">
      <RouterLink
        to="/"
        class="back-link"
      >
        ← 返回遊戲廳
      </RouterLink>
      <div class="title-block">
        <p class="eyebrow">Game 06</p>
        <h1>節奏大師對戰</h1>
      </div>
      <div
        v-if="phase === 'playing'"
        class="song-pill"
      >
        {{ Math.max(0, Math.ceil((SONG_MS - songTime) / 1000)) }}s
      </div>
    </section>

    <section class="layout">
      <div class="stage-card">
        <div class="scoreband">
          <div class="team team-1">
            <strong>玩家 1</strong>
            <span class="score">{{ hud.p1Score }}</span>
            <span class="combo" :class="{ hot: hud.p1Combo >= 10 }">{{ hud.p1Combo }} combo</span>
          </div>
          <div class="vs">VS</div>
          <div class="team team-2">
            <span class="combo" :class="{ hot: hud.p2Combo >= 10 }">{{ hud.p2Combo }} combo</span>
            <span class="score">{{ hud.p2Score }}</span>
            <strong>玩家 2</strong>
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
                  <p class="overlay-eyebrow">同步律動</p>
                  <h2>跟著節拍敲擊</h2>
                  <p class="overlay-text">
                    音符落到判定線時按下對應按鍵。<br>
                    越準分數越高，連續命中累積 Combo 倍率。
                  </p>
                  <button
                    class="primary-btn"
                    @click="startSong"
                  >
                    開始演奏
                  </button>
                </template>

                <template v-else-if="phase === 'result'">
                  <p class="overlay-eyebrow">演奏結束</p>
                  <h2 class="winner-text">{{ resultText }}</h2>
                  <div class="result-grid">
                    <div class="rcol">
                      <span class="rname">玩家 1</span>
                      <span class="rscore">{{ hud.p1Score }}</span>
                      <span class="rdetail">
                        Perfect {{ stats.p1.perfect }} · Good {{ stats.p1.good }} · Miss
                        {{ stats.p1.miss }}
                      </span>
                      <span class="rdetail">最高 Combo {{ stats.p1.maxCombo }}</span>
                    </div>
                    <div class="rcol">
                      <span class="rname">玩家 2</span>
                      <span class="rscore">{{ hud.p2Score }}</span>
                      <span class="rdetail">
                        Perfect {{ stats.p2.perfect }} · Good {{ stats.p2.good }} · Miss
                        {{ stats.p2.miss }}
                      </span>
                      <span class="rdetail">最高 Combo {{ stats.p2.maxCombo }}</span>
                    </div>
                  </div>
                  <button
                    class="primary-btn"
                    @click="startSong"
                  >
                    再來一首
                  </button>
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
              <span class="keys"><kbd>D</kbd><kbd>F</kbd><kbd>G</kbd></span>
            </div>
            <div class="ctrl ctrl-2">
              <strong>玩家 2</strong>
              <span class="keys"><kbd>J</kbd><kbd>K</kbd><kbd>L</kbd></span>
            </div>
          </div>
        </section>

        <section class="panel">
          <p class="eyebrow">判定</p>
          <ul class="legend">
            <li><span class="tag perfect">PERFECT</span> ±55ms · 100 分</li>
            <li><span class="tag good">GOOD</span> ±115ms · 50 分</li>
            <li><span class="tag miss">MISS</span> 漏接 · Combo 歸零</li>
          </ul>
          <p class="hint">Combo 每 10 連段提升一級倍率（最高 4 倍）。</p>
        </section>

        <section class="panel">
          <div class="panel-head">
            <p class="eyebrow">對戰紀錄</p>
            <button
              v-if="records.length"
              class="ghost-btn"
              @click="onClearRecords"
            >
              清除
            </button>
          </div>
          <ul
            v-if="records.length"
            class="records"
          >
            <li
              v-for="(r, i) in records"
              :key="i"
            >
              <span class="rec-win">{{ r.winner }}</span>
              <span class="rec-score">{{ r.scoreP1 }} : {{ r.scoreP2 }}</span>
              <span class="rec-date">{{ formatDate(r.date) }}</span>
            </li>
          </ul>
          <p
            v-else
            class="empty"
          >
            尚無紀錄，演奏結束後自動保存最近 10 場。
          </p>
        </section>
      </aside>
    </section>
  </main>
</template>

<script setup>
import { onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import {
  clearGame06Records,
  fetchGame06Store,
  saveGame06Record
} from './game06Storage'
import { recordGameResult } from '@/data/lobbyScore'
import { assetUrl } from '@/utils/assetUrl'

const CANVAS_W = 960
const CANVAS_H = 660
const LANES = 3
const HALF_W = CANVAS_W / 2
const LANE_W = HALF_W / LANES
const HIT_Y = CANVAS_H - 90
const APPROACH = 1700
const BPM = 104
const BEAT = (60 / BPM) * 1000
const SONG_MS = 62000
const PERFECT_MS = 55
const GOOD_MS = 115

const P1_KEYS = { d: 0, f: 1, g: 2 }
const P2_KEYS = { j: 0, k: 1, l: 2 }
const LANE_COLORS = ['#46d0ff', '#a78bfa', '#ff7ab0']

// 像素素材
const G06 = {}
function g06Sprite(name) {
  if (!G06[name]) {
    const img = new Image()
    img.src = assetUrl(`/assets/G06/${name}.png`)
    G06[name] = img
  }
  return G06[name]
}
const LANE_HUE = ['blue', 'purple', 'pink']
const POPUP_SPRITE = { PERFECT: 'txt-perfect', GOOD: 'txt-good', MISS: 'txt-miss' }
;['bg-lanes', 'judgeline', 'note-blue', 'note-purple', 'note-pink', 'target-blue', 'target-purple', 'target-pink', 'txt-perfect', 'txt-good', 'txt-miss', 'fx-hit-1', 'fx-hit-2', 'fx-hit-3'].forEach(g06Sprite)
function g06ready(img) {
  return img && img.complete && img.naturalWidth > 0
}

const canvasRef = ref(null)
const stageRef = ref(null)

const phase = ref('intro')
const songTime = ref(0)
const resultText = ref('')
const records = ref([])
const hud = reactive({ p1Score: 0, p2Score: 0, p1Combo: 0, p2Combo: 0 })
const stats = reactive({
  p1: { perfect: 0, good: 0, miss: 0, maxCombo: 0 },
  p2: { perfect: 0, good: 0, miss: 0, maxCombo: 0 }
})

let ctx = null
let rafId = 0
let startStamp = 0
let chart = null
let lastBeatIndex = -1
let beatPulse = 0
let audioCtx = null
const laneFlash = { p1: [0, 0, 0], p2: [0, 0, 0] }
const judgePopups = []

function buildChart() {
  // shared chart for both players, density ramps up over time
  const notes = []
  const totalBeats = Math.floor(SONG_MS / BEAT)
  let lastLane = -1
  for (let b = 4; b < totalBeats; b += 1) {
    const t = b * BEAT
    const progress = t / SONG_MS
    const density = 0.55 + progress * 0.4
    if (Math.random() < density) {
      let lane = Math.floor(Math.random() * LANES)
      if (lane === lastLane && Math.random() < 0.6) lane = (lane + 1) % LANES
      lastLane = lane
      notes.push({ time: t, lane })
    }
    // off-beat extra notes later in the song
    if (progress > 0.35 && Math.random() < progress * 0.45) {
      notes.push({ time: t + BEAT / 2, lane: Math.floor(Math.random() * LANES) })
    }
  }
  notes.sort((a, b) => a.time - b.time)
  return notes.map((n, i) => ({ ...n, id: i }))
}

function makeNoteSet() {
  return chart.map((n) => ({ ...n, judged: false }))
}

function ensureAudio() {
  if (audioCtx) return
  try {
    const AC = window.AudioContext || window.webkitAudioContext
    audioCtx = new AC()
  } catch {
    audioCtx = null
  }
}

function tone(freq, dur, type, gainPeak) {
  if (!audioCtx) return
  const now = audioCtx.currentTime
  const osc = audioCtx.createOscillator()
  const g = audioCtx.createGain()
  osc.type = type
  osc.frequency.setValueAtTime(freq, now)
  g.gain.setValueAtTime(0.0001, now)
  g.gain.exponentialRampToValueAtTime(gainPeak, now + 0.008)
  g.gain.exponentialRampToValueAtTime(0.0001, now + dur)
  osc.connect(g)
  g.connect(audioCtx.destination)
  osc.start(now)
  osc.stop(now + dur + 0.02)
}

function playKick() {
  if (!audioCtx) return
  const now = audioCtx.currentTime
  const osc = audioCtx.createOscillator()
  const g = audioCtx.createGain()
  osc.frequency.setValueAtTime(150, now)
  osc.frequency.exponentialRampToValueAtTime(50, now + 0.12)
  g.gain.setValueAtTime(0.32, now)
  g.gain.exponentialRampToValueAtTime(0.0001, now + 0.18)
  osc.connect(g)
  g.connect(audioCtx.destination)
  osc.start(now)
  osc.stop(now + 0.2)
}

function playHat() {
  tone(8000, 0.04, 'square', 0.05)
}

function playHit(lane) {
  const freqs = [523, 659, 784]
  tone(freqs[lane] * 2, 0.12, 'triangle', 0.16)
}

function startSong() {
  ensureAudio()
  if (audioCtx && audioCtx.state === 'suspended') audioCtx.resume()
  chart = buildChart()
  hud.p1Score = 0
  hud.p2Score = 0
  hud.p1Combo = 0
  hud.p2Combo = 0
  stats.p1 = { perfect: 0, good: 0, miss: 0, maxCombo: 0 }
  stats.p2 = { perfect: 0, good: 0, miss: 0, maxCombo: 0 }
  judgePopups.length = 0
  lastBeatIndex = -1
  notesP1 = makeNoteSet()
  notesP2 = makeNoteSet()
  phase.value = 'playing'
  startStamp = performance.now()
  songTime.value = 0
  lastFrame = startStamp
  loop(startStamp)
}

let notesP1 = []
let notesP2 = []
let lastFrame = 0

function multiplier(combo) {
  if (combo >= 30) return 4
  if (combo >= 20) return 3
  if (combo >= 10) return 2
  return 1
}

function judgeKey(player, lane) {
  const notes = player === 'p1' ? notesP1 : notesP2
  const st = stats[player]
  const now = songTime.value
  let best = null
  let bestDt = Infinity
  for (const n of notes) {
    if (n.judged || n.lane !== lane) continue
    const dt = Math.abs(n.time - now)
    if (dt < bestDt) {
      bestDt = dt
      best = n
    }
  }
  laneFlash[player][lane] = 1

  if (!best || bestDt > GOOD_MS) {
    return
  }
  best.judged = true
  playHit(lane)
  const comboKey = player === 'p1' ? 'p1Combo' : 'p2Combo'
  const scoreKey = player === 'p1' ? 'p1Score' : 'p2Score'

  if (bestDt <= PERFECT_MS) {
    st.perfect += 1
    hud[comboKey] += 1
    hud[scoreKey] += 100 * multiplier(hud[comboKey])
    pushPopup(player, lane, 'PERFECT', '#46d0ff')
  } else {
    st.good += 1
    hud[comboKey] += 1
    hud[scoreKey] += 50 * multiplier(hud[comboKey])
    pushPopup(player, lane, 'GOOD', '#ffd23f')
  }
  st.maxCombo = Math.max(st.maxCombo, hud[comboKey])
}

function pushPopup(player, lane, text, color) {
  const baseX = player === 'p1' ? 0 : HALF_W
  judgePopups.push({
    x: baseX + lane * LANE_W + LANE_W / 2,
    y: HIT_Y - 30,
    text,
    color,
    life: 1
  })
}

function autoMiss() {
  const now = songTime.value
  for (const [player, notes] of [['p1', notesP1], ['p2', notesP2]]) {
    const st = stats[player]
    const comboKey = player === 'p1' ? 'p1Combo' : 'p2Combo'
    for (const n of notes) {
      if (!n.judged && now - n.time > GOOD_MS) {
        n.judged = true
        st.miss += 1
        hud[comboKey] = 0
        pushPopup(player, n.lane, 'MISS', '#ff5d6c')
      }
    }
  }
}

function update(dt) {
  songTime.value = performance.now() - startStamp

  // beat audio + pulse
  const beatIndex = Math.floor(songTime.value / BEAT)
  if (beatIndex !== lastBeatIndex && songTime.value < SONG_MS) {
    lastBeatIndex = beatIndex
    beatPulse = 1
    if (beatIndex % 2 === 0) playKick()
    else playHat()
  }
  beatPulse = Math.max(0, beatPulse - dt / 220)

  autoMiss()

  for (const p of ['p1', 'p2']) {
    for (let i = 0; i < LANES; i += 1) {
      laneFlash[p][i] = Math.max(0, laneFlash[p][i] - dt / 160)
    }
  }
  for (const pop of judgePopups) {
    pop.y -= dt / 22
    pop.life -= dt / 700
  }
  for (let i = judgePopups.length - 1; i >= 0; i -= 1) {
    if (judgePopups[i].life <= 0) judgePopups.splice(i, 1)
  }

  if (songTime.value > SONG_MS + APPROACH) {
    finishSong()
  }
}

async function finishSong() {
  cancelAnimationFrame(rafId)
  rafId = 0
  let winner
  if (hud.p1Score > hud.p2Score) winner = '玩家 1 獲勝'
  else if (hud.p2Score > hud.p1Score) winner = '玩家 2 獲勝'
  else winner = '平手'
  resultText.value = `🎵 ${winner}`
  phase.value = 'result'
  recordGameResult(
    '/game06',
    hud.p1Score > hud.p2Score ? 'p1' : hud.p2Score > hud.p1Score ? 'p2' : 'draw'
  )
  try {
    const store = await saveGame06Record({
      winner,
      scoreP1: hud.p1Score,
      scoreP2: hud.p2Score,
      date: new Date().toISOString()
    })
    records.value = store.records
  } catch {
    /* ignore */
  }
}

function loop(now) {
  const dt = Math.min(48, now - lastFrame)
  lastFrame = now
  if (phase.value === 'playing') {
    update(dt)
    render()
    rafId = requestAnimationFrame(loop)
  }
}

function render() {
  ctx.clearRect(0, 0, CANVAS_W, CANVAS_H)
  const bgGlow = 0.04 + beatPulse * 0.08
  const bg = ctx.createLinearGradient(0, 0, 0, CANVAS_H)
  bg.addColorStop(0, `rgba(40,30,80,${0.5 + beatPulse * 0.25})`)
  bg.addColorStop(1, '#0a0a18')
  ctx.fillStyle = '#0a0a18'
  ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  ctx.fillStyle = bg
  ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)

  drawField('p1', 0, notesP1)
  drawField('p2', HALF_W, notesP2)

  // center divider
  ctx.strokeStyle = `rgba(255,255,255,${0.08 + bgGlow})`
  ctx.lineWidth = 2
  ctx.beginPath()
  ctx.moveTo(HALF_W, 0)
  ctx.lineTo(HALF_W, CANVAS_H)
  ctx.stroke()

  for (const pop of judgePopups) {
    ctx.globalAlpha = Math.max(0, pop.life)
    const sprName = POPUP_SPRITE[pop.text]
    const spr = sprName ? g06Sprite(sprName) : null
    if (g06ready(spr)) {
      const h = 30
      const w = h * (spr.naturalWidth / spr.naturalHeight)
      ctx.drawImage(spr, pop.x - w / 2, pop.y - h, w, h)
    } else {
      ctx.fillStyle = pop.color
      ctx.font = 'bold 22px system-ui, sans-serif'
      ctx.textAlign = 'center'
      ctx.fillText(pop.text, pop.x, pop.y)
    }
  }
  ctx.globalAlpha = 1
}

function drawField(player, baseX, notes) {
  const now = songTime.value
  ctx.imageSmoothingEnabled = false

  // 軌道背景
  const bgImg = g06Sprite('bg-lanes')
  if (g06ready(bgImg)) {
    ctx.drawImage(bgImg, baseX, 0, HALF_W, CANVAS_H)
  }

  // lanes（柔光 + 打擊閃光）
  for (let i = 0; i < LANES; i += 1) {
    const x = baseX + i * LANE_W
    if (!g06ready(bgImg)) {
      ctx.fillStyle = i % 2 === 0 ? 'rgba(255,255,255,0.02)' : 'rgba(255,255,255,0.035)'
      ctx.fillRect(x, 0, LANE_W, CANVAS_H)
    }
    if (laneFlash[player][i] > 0) {
      ctx.fillStyle = `rgba(${hexToRgb(LANE_COLORS[i])},${laneFlash[player][i] * 0.22})`
      ctx.fillRect(x, 0, LANE_W, CANVAS_H)
    }
  }

  // 判定線
  const jl = g06Sprite('judgeline')
  if (g06ready(jl)) {
    const h = 22
    ctx.drawImage(jl, baseX, HIT_Y - h / 2, HALF_W, h)
  } else {
    ctx.save()
    ctx.strokeStyle = 'rgba(255,255,255,0.7)'
    ctx.lineWidth = 3
    ctx.beginPath()
    ctx.moveTo(baseX, HIT_Y)
    ctx.lineTo(baseX + HALF_W, HIT_Y)
    ctx.stroke()
    ctx.restore()
  }

  // 打擊目標圈
  for (let i = 0; i < LANES; i += 1) {
    const x = baseX + i * LANE_W + LANE_W / 2
    const tgt = g06Sprite(`target-${LANE_HUE[i]}`)
    const flash = laneFlash[player][i]
    if (g06ready(tgt)) {
      const sz = 50 + flash * 10
      ctx.globalAlpha = 0.7 + flash * 0.3
      ctx.drawImage(tgt, x - sz / 2, HIT_Y - sz / 2, sz, sz)
      ctx.globalAlpha = 1
    } else {
      ctx.strokeStyle = LANE_COLORS[i]
      ctx.globalAlpha = 0.5 + flash * 0.5
      ctx.lineWidth = 3
      ctx.beginPath()
      ctx.arc(x, HIT_Y, 22, 0, Math.PI * 2)
      ctx.stroke()
      ctx.globalAlpha = 1
    }
    // 打擊命中火花
    if (flash > 0.55) {
      const frame = flash > 0.85 ? 'fx-hit-1' : flash > 0.7 ? 'fx-hit-2' : 'fx-hit-3'
      const fx = g06Sprite(frame)
      if (g06ready(fx)) {
        const fs = 60
        ctx.globalAlpha = flash
        ctx.drawImage(fx, x - fs / 2, HIT_Y - fs / 2, fs, fs)
        ctx.globalAlpha = 1
      }
    }
  }

  // notes
  for (const n of notes) {
    if (n.judged) continue
    const appearAt = n.time - APPROACH
    if (now < appearAt) continue
    const t = (now - appearAt) / APPROACH
    const y = t * HIT_Y
    if (y > CANVAS_H) continue
    const noteImg = g06Sprite(`note-${LANE_HUE[n.lane]}`)
    const x = baseX + n.lane * LANE_W + 8
    const w = LANE_W - 16
    if (g06ready(noteImg)) {
      const h = w * (noteImg.naturalHeight / noteImg.naturalWidth)
      ctx.drawImage(noteImg, x, y - h / 2, w, h)
    } else {
      ctx.save()
      ctx.shadowColor = LANE_COLORS[n.lane]
      ctx.shadowBlur = 14
      ctx.fillStyle = LANE_COLORS[n.lane]
      roundRect(x, y - 11, w, 22, 8)
      ctx.fill()
      ctx.restore()
    }
  }
}

function hexToRgb(hex) {
  const v = hex.replace('#', '')
  const r = parseInt(v.slice(0, 2), 16)
  const g = parseInt(v.slice(2, 4), 16)
  const b = parseInt(v.slice(4, 6), 16)
  return `${r},${g},${b}`
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

const keysDown = new Set()
function onKeyDown(e) {
  const k = e.key.toLowerCase()
  if (keysDown.has(k)) return
  keysDown.add(k)

  if ((k === ' ' || k === 'enter') && phase.value !== 'playing') {
    startSong()
    return
  }
  if (phase.value !== 'playing') return
  if (k in P1_KEYS) judgeKey('p1', P1_KEYS[k])
  else if (k in P2_KEYS) judgeKey('p2', P2_KEYS[k])
}
function onKeyUp(e) {
  keysDown.delete(e.key.toLowerCase())
}

async function onClearRecords() {
  const store = await clearGame06Records()
  records.value = store.records
}

function formatDate(iso) {
  const d = new Date(iso)
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(
    d.getMinutes()
  ).padStart(2, '0')}`
}

function idleRender() {
  ctx.clearRect(0, 0, CANVAS_W, CANVAS_H)
  ctx.fillStyle = '#0a0a18'
  ctx.fillRect(0, 0, CANVAS_W, CANVAS_H)
  songTime.value = 0
  notesP1 = []
  notesP2 = []
  drawField('p1', 0, [])
  drawField('p2', HALF_W, [])
  ctx.strokeStyle = 'rgba(255,255,255,0.1)'
  ctx.beginPath()
  ctx.moveTo(HALF_W, 0)
  ctx.lineTo(HALF_W, CANVAS_H)
  ctx.stroke()
}

onMounted(async () => {
  ctx = canvasRef.value.getContext('2d')
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('keyup', onKeyUp)
  try {
    const store = await fetchGame06Store()
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
  if (audioCtx) audioCtx.close()
})
</script>

<style scoped>
.game06-view {
  min-height: 100vh;
  padding: 24px clamp(16px, 4vw, 48px) 48px;
  color: #ece9ff;
  background: radial-gradient(circle at 50% -10%, #2a1f55, #08060f 60%);
  font-family: 'Segoe UI', system-ui, sans-serif;
}

.topbar {
  display: flex;
  align-items: center;
  gap: 20px;
  flex-wrap: wrap;
  margin-bottom: 22px;
}
.back-link {
  color: #b3a8e8;
  text-decoration: none;
  font-size: 14px;
  padding: 8px 14px;
  border: 1px solid rgba(179, 168, 232, 0.3);
  border-radius: 999px;
  transition: 0.2s;
}
.back-link:hover {
  background: rgba(179, 168, 232, 0.12);
  color: #fff;
}
.title-block .eyebrow {
  margin: 0;
  font-size: 12px;
  letter-spacing: 3px;
  color: #a78bfa;
  text-transform: uppercase;
}
.title-block h1 {
  margin: 2px 0 0;
  font-size: 26px;
  background: linear-gradient(90deg, #46d0ff, #ff7ab0);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.song-pill {
  margin-left: auto;
  padding: 8px 18px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.06);
  border: 1px solid rgba(255, 255, 255, 0.12);
  font-size: 16px;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 300px;
  gap: 22px;
  align-items: start;
}

.stage-card {
  background: rgba(12, 9, 24, 0.6);
  border: 1px solid rgba(167, 139, 250, 0.2);
  border-radius: 20px;
  padding: 16px;
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.5);
}

.scoreband {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 14px;
  padding: 0 6px;
}
.team {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
}
.team-2 {
  justify-content: flex-end;
}
.team strong {
  font-size: 15px;
}
.team .score {
  font-size: 22px;
  font-weight: 800;
  font-variant-numeric: tabular-nums;
}
.team-1 .score {
  color: #46d0ff;
}
.team-2 .score {
  color: #ff7ab0;
}
.team .combo {
  font-size: 13px;
  color: #8d83b8;
  transition: color 0.2s;
}
.team .combo.hot {
  color: #ffd23f;
  font-weight: 700;
}
.vs {
  font-size: 13px;
  font-weight: 800;
  color: #6b5fa0;
}

.stage-frame {
  position: relative;
  border-radius: 14px;
  overflow: hidden;
}
.game-canvas {
  display: block;
  width: 100%;
  height: auto;
  border-radius: 14px;
}

.overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(8, 6, 16, 0.82);
  backdrop-filter: blur(4px);
}
.overlay-card {
  text-align: center;
  max-width: 480px;
  padding: 32px;
}
.overlay-eyebrow {
  margin: 0;
  font-size: 12px;
  letter-spacing: 3px;
  text-transform: uppercase;
  color: #a78bfa;
}
.overlay-card h2 {
  margin: 10px 0 14px;
  font-size: 30px;
}
.winner-text {
  background: linear-gradient(90deg, #46d0ff, #ff7ab0);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.overlay-text {
  color: #c2bce8;
  line-height: 1.7;
  margin: 0 0 22px;
}
.result-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  margin: 0 0 24px;
}
.rcol {
  background: rgba(255, 255, 255, 0.05);
  border-radius: 14px;
  padding: 16px;
  display: grid;
  gap: 6px;
}
.rname {
  font-size: 13px;
  color: #a78bfa;
}
.rscore {
  font-size: 28px;
  font-weight: 800;
}
.rdetail {
  font-size: 12px;
  color: #9d96c4;
}
.primary-btn {
  border: none;
  padding: 12px 30px;
  border-radius: 999px;
  font-size: 16px;
  font-weight: 700;
  color: #0a0618;
  background: linear-gradient(90deg, #46d0ff, #a78bfa);
  cursor: pointer;
  transition: transform 0.15s, box-shadow 0.15s;
  box-shadow: 0 10px 26px rgba(167, 139, 250, 0.4);
}
.primary-btn:hover {
  transform: translateY(-2px);
  box-shadow: 0 14px 32px rgba(167, 139, 250, 0.55);
}

.sidebar {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.panel {
  background: rgba(12, 9, 24, 0.6);
  border: 1px solid rgba(167, 139, 250, 0.18);
  border-radius: 18px;
  padding: 18px;
}
.panel .eyebrow {
  margin: 0 0 12px;
  font-size: 11px;
  letter-spacing: 2px;
  text-transform: uppercase;
  color: #8d83b8;
}
.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.controls-grid {
  display: grid;
  gap: 10px;
}
.ctrl {
  border-radius: 12px;
  padding: 12px 14px;
}
.ctrl strong {
  display: block;
  font-size: 14px;
  margin-bottom: 8px;
}
.ctrl-1 {
  background: rgba(70, 208, 255, 0.1);
  border: 1px solid rgba(70, 208, 255, 0.25);
}
.ctrl-2 {
  background: rgba(255, 122, 176, 0.1);
  border: 1px solid rgba(255, 122, 176, 0.25);
}
.keys {
  display: inline-flex;
  gap: 6px;
}
kbd {
  background: rgba(255, 255, 255, 0.12);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 6px;
  padding: 3px 9px;
  font-size: 13px;
  font-family: inherit;
}
.legend {
  list-style: none;
  margin: 0 0 10px;
  padding: 0;
  display: grid;
  gap: 10px;
}
.legend li {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 13px;
  color: #c2bce8;
}
.tag {
  font-size: 10px;
  font-weight: 800;
  padding: 3px 8px;
  border-radius: 6px;
  letter-spacing: 1px;
}
.tag.perfect {
  background: #46d0ff;
  color: #052233;
}
.tag.good {
  background: #ffd23f;
  color: #3a2c00;
}
.tag.miss {
  background: #ff5d6c;
  color: #380008;
}
.hint {
  font-size: 12px;
  color: #8d83b8;
  margin: 0;
  line-height: 1.6;
}
.records {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 8px;
}
.records li {
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 8px;
  align-items: center;
  font-size: 12px;
  padding: 8px 10px;
  border-radius: 10px;
  background: rgba(255, 255, 255, 0.04);
}
.rec-win {
  font-weight: 700;
  color: #ffd23f;
}
.rec-score {
  color: #b3a8e8;
}
.rec-date {
  color: #6b5fa0;
}
.empty {
  font-size: 13px;
  color: #8d83b8;
  line-height: 1.6;
  margin: 0;
}
.ghost-btn {
  background: none;
  border: 1px solid rgba(179, 168, 232, 0.3);
  color: #b3a8e8;
  border-radius: 999px;
  padding: 4px 12px;
  font-size: 12px;
  cursor: pointer;
}
.ghost-btn:hover {
  background: rgba(179, 168, 232, 0.12);
}
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.25s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
@media (max-width: 920px) {
  .layout {
    grid-template-columns: 1fr;
  }
}
</style>
