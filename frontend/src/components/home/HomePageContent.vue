<script setup>
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { gameLobbyCards } from '@/data/gameLobby'
import {
  fetchLobbyScore,
  resetLobbyScore,
  summarizeLobby
} from '@/data/lobbyScore'
import VictoryCelebration from '@/components/home/VictoryCelebration.vue'

const lobbyStore = ref({ results: {} })
const victoryDismissed = ref(false)

const summary = computed(() => summarizeLobby(lobbyStore.value))
const showVictory = computed(() => summary.value.finished && !victoryDismissed.value)

function reload() {
  lobbyStore.value = fetchLobbyScore()
}

function resultFor(route) {
  if (!route) return null
  return summary.value.results[route] ?? null
}

function resultLabel(team) {
  if (team === 'p1') return '玩家 1 勝'
  if (team === 'p2') return '玩家 2 勝'
  if (team === 'draw') return '平手'
  return ''
}

function coverFor(no) {
  return `/assets/HOME/covers/cover-G${no}.png`
}

function handleReset() {
  if (typeof window !== 'undefined') {
    const ok = window.confirm('確定要重置本輪比分嗎？兩隊勝場會歸零，所有遊戲重新解鎖。')
    if (!ok) return
  }
  lobbyStore.value = resetLobbyScore()
  victoryDismissed.value = false
}

function dismissVictory() {
  victoryDismissed.value = true
}

const leader = computed(() => {
  const s = summary.value
  if (s.p1 > s.p2) return 'p1'
  if (s.p2 > s.p1) return 'p2'
  return 'tie'
})

onMounted(reload)
</script>

<template>
  <section class="content-section">
    <!-- 比分看板 -->
    <div class="scoreboard">
      <div
        class="team team-p1"
        :class="{ leading: leader === 'p1' && summary.played > 0 }"
      >
        <span class="team-label">PLAYER 01</span>
        <span class="team-name">玩家 1</span>
        <span class="team-wins">{{ summary.p1 }}</span>
      </div>

      <div class="score-center">
        <img
          class="vs-badge"
          src="/assets/HOME/deco/vs-badge.png"
          alt="VS"
        >
        <div class="progress-text">{{ summary.played }} / {{ summary.total }} CLEARED</div>
        <div class="progress-bar">
          <div
            class="progress-fill"
            :style="{ width: `${(summary.played / summary.total) * 100}%` }"
          />
        </div>
        <button
          class="reset-button"
          @click="handleReset"
        >
          重置比分
        </button>
      </div>

      <div
        class="team team-p2"
        :class="{ leading: leader === 'p2' && summary.played > 0 }"
      >
        <span class="team-label">PLAYER 02</span>
        <span class="team-name">玩家 2</span>
        <span class="team-wins">{{ summary.p2 }}</span>
      </div>
    </div>

    <div class="grid-label">
      <span class="gl-title">遊戲庫 <i>GAME LIBRARY</i></span>
      <span class="gl-meta">{{ summary.played }} / {{ summary.total }} 已完成</span>
    </div>

    <div class="game-grid">
      <article
        v-for="(card, index) in gameLobbyCards"
        :key="card.id"
        class="game-card"
        :class="{ done: resultFor(card.route) }"
        :style="{ '--i': index }"
      >
        <img
          class="hover-glow"
          src="/assets/HOME/fx-card-hover-glow.png"
          alt=""
          aria-hidden="true"
        >

        <div class="cover-wrap">
          <img
            class="cover"
            :src="coverFor(card.no)"
            :alt="card.title"
            loading="lazy"
          >
          <span class="gtag">G{{ card.no }}</span>
          <img
            v-if="!resultFor(card.route)"
            class="badge-pending"
            src="/assets/HOME/badge-pending.png"
            alt="待挑戰"
          >
          <span
            v-else
            class="clear-stamp"
            :class="resultFor(card.route)"
          >CLEAR</span>
        </div>

        <div class="card-body">
          <h3>{{ card.title }}</h3>
          <p class="desc">{{ card.description }}</p>
        </div>

        <div class="card-foot">
          <span
            v-if="resultFor(card.route)"
            class="winline"
            :class="resultFor(card.route)"
          >
            {{ resultFor(card.route) === 'draw' ? '🤝 平手' : '🏆 ' + resultLabel(resultFor(card.route)) }}
          </span>
          <RouterLink
            v-else
            :to="card.route"
            class="enter"
            :aria-label="`進入 ${card.title}`"
          >
            <img
              src="/assets/HOME/btn-enter-pixel.png"
              alt="PLAY"
            >
          </RouterLink>
        </div>
      </article>
    </div>

    <VictoryCelebration
      v-if="showVictory"
      :winner="summary.winner"
      :score-p1="summary.p1"
      :score-p2="summary.p2"
      @reset="handleReset"
      @close="dismissVictory"
    />
  </section>
</template>

<style scoped>
.content-section {
  display: grid;
  gap: 1.6rem;
  --ink: #5b4a54;
  --cream: #fdf3e7;
  --pink: #ffb7c5;
  --blue: #a2cffe;
  --mono: ui-monospace, 'SF Mono', 'Cascadia Code', Consolas, monospace;
}

@keyframes riseIn {
  from { opacity: 0; transform: translateY(18px); }
  to { opacity: 1; transform: none; }
}

/* 比分看板 —— 奶油色像素面板 */
.scoreboard {
  position: relative;
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
  gap: 1rem;
  padding: 1.3rem 1.6rem;
  border-radius: 16px;
  background: rgba(253, 243, 231, 0.94);
  border: 3px solid var(--ink);
  box-shadow: 0 10px 0 rgba(40, 36, 60, 0.35), 0 22px 48px rgba(0, 0, 0, 0.4);
  animation: riseIn 0.6s cubic-bezier(0.2, 0.7, 0.2, 1) 0.08s backwards;
}

.team {
  display: grid;
  gap: 0.15rem;
  padding: 0.6rem 1.1rem;
  border-radius: 12px;
  transition: box-shadow 0.25s, transform 0.25s;
}
.team-p2 {
  text-align: right;
  justify-items: end;
}
.team-label {
  font-family: var(--mono);
  font-size: 0.66rem;
  letter-spacing: 0.2em;
  color: #a08a92;
}
.team-name {
  font-size: 0.95rem;
  font-weight: 800;
  color: var(--ink);
}
.team-wins {
  font-family: var(--mono);
  font-size: 2.8rem;
  font-weight: 800;
  line-height: 1;
}
.team-p1 .team-wins {
  color: #ef88a8;
  text-shadow: 2px 2px 0 rgba(91, 74, 84, 0.18);
}
.team-p2 .team-wins {
  color: #5a9be6;
  text-shadow: 2px 2px 0 rgba(91, 74, 84, 0.18);
}
.team.leading {
  transform: translateY(-2px);
}
.team-p1.leading {
  box-shadow: inset 0 0 0 2px var(--pink), 0 6px 0 rgba(239, 136, 168, 0.25);
}
.team-p2.leading {
  box-shadow: inset 0 0 0 2px var(--blue), 0 6px 0 rgba(90, 155, 230, 0.25);
}

.score-center {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  min-width: 190px;
}
.vs-badge {
  width: 64px;
  height: auto;
  image-rendering: pixelated;
}
.progress-text {
  font-family: var(--mono);
  font-size: 0.72rem;
  letter-spacing: 0.12em;
  color: #8a7680;
}
.progress-bar {
  width: 170px;
  height: 12px;
  border-radius: 999px;
  background: #efe2d4;
  border: 2px solid var(--ink);
  overflow: hidden;
}
.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--pink), #d8b4f8 55%, var(--blue));
  transition: width 0.4s ease;
}
.reset-button {
  margin-top: 0.2rem;
  border: 2px solid var(--ink);
  border-radius: 999px;
  padding: 0.4rem 1.2rem;
  font-size: 0.8rem;
  font-weight: 800;
  color: var(--ink);
  background: var(--pink);
  box-shadow: 0 3px 0 rgba(40, 36, 60, 0.3);
  transition: transform 0.12s, box-shadow 0.12s;
}
.reset-button:hover {
  transform: translateY(-2px);
  box-shadow: 0 5px 0 rgba(40, 36, 60, 0.3);
}
.reset-button:active {
  transform: translateY(1px);
  box-shadow: 0 1px 0 rgba(40, 36, 60, 0.3);
}

/* grid label */
.grid-label {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 1rem;
  padding: 0 0.25rem 0.6rem;
  border-bottom: 2px dashed rgba(253, 243, 231, 0.35);
}
.gl-title {
  font-size: 1.1rem;
  font-weight: 800;
  color: #fdf3e7;
  text-shadow: 0 2px 0 #5b4a54;
}
.gl-title i {
  font-style: normal;
  font-family: var(--mono);
  font-size: 0.72rem;
  letter-spacing: 0.2em;
  color: #ffd9a0;
  margin-left: 0.5rem;
}
.gl-meta {
  font-family: var(--mono);
  font-size: 0.74rem;
  letter-spacing: 0.1em;
  color: #e7dcec;
}

.game-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1.1rem;
}

/* 卡片 —— 用像素卡框 9-slice */
.game-card {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 0.55rem;
  padding: 16px;
  background: var(--cream);
  border: 16px solid transparent;
  border-image-source: url('/assets/HOME/ui-game-card-frame.png');
  border-image-slice: 20 fill;
  border-image-width: 16px;
  border-image-repeat: stretch;
  image-rendering: pixelated;
  transition: transform 0.18s ease;
  animation: riseIn 0.5s ease calc(var(--i, 0) * 0.03s + 0.15s) backwards;
}
.game-card:hover {
  transform: translateY(-6px);
}
.game-card.done {
  filter: saturate(0.85);
}

.hover-glow {
  position: absolute;
  inset: -22px;
  width: calc(100% + 44px);
  height: calc(100% + 44px);
  image-rendering: pixelated;
  opacity: 0;
  pointer-events: none;
  z-index: -1;
  transition: opacity 0.25s ease;
}
.game-card:hover .hover-glow {
  opacity: 0.9;
}

.cover-wrap {
  position: relative;
  border: 2px solid var(--ink);
  border-radius: 8px;
  overflow: hidden;
  background: #2a2440;
  aspect-ratio: 16 / 9;
}
.cover {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
  image-rendering: pixelated;
}
.gtag {
  position: absolute;
  left: 6px;
  top: 6px;
  font-family: var(--mono);
  font-size: 0.66rem;
  font-weight: 800;
  letter-spacing: 0.08em;
  color: var(--ink);
  background: rgba(253, 243, 231, 0.92);
  border: 1.5px solid var(--ink);
  border-radius: 6px;
  padding: 0.1rem 0.4rem;
}
.badge-pending {
  position: absolute;
  right: 5px;
  top: 5px;
  width: 34px;
  height: 34px;
  image-rendering: pixelated;
}
.clear-stamp {
  position: absolute;
  right: 5px;
  top: 6px;
  font-family: var(--mono);
  font-size: 0.62rem;
  font-weight: 800;
  letter-spacing: 0.12em;
  color: var(--cream);
  border: 2px solid var(--cream);
  border-radius: 6px;
  padding: 0.12rem 0.36rem;
  transform: rotate(-8deg);
}
.clear-stamp.p1 { background: #ef88a8; }
.clear-stamp.p2 { background: #5a9be6; }
.clear-stamp.draw { background: #b9a0d8; }

.card-body {
  display: grid;
  gap: 0.2rem;
}
h3 {
  color: var(--ink);
  font-size: 1.02rem;
  font-weight: 800;
}
.desc {
  color: #8a7680;
  font-size: 0.78rem;
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.card-foot {
  margin-top: auto;
  display: flex;
  align-items: center;
  min-height: 32px;
}
.enter {
  display: inline-flex;
  transition: transform 0.15s ease;
}
.enter img {
  height: 36px;
  width: auto;
  image-rendering: pixelated;
}
.game-card:hover .enter {
  transform: translateY(-3px);
}
.enter:active {
  transform: translateY(1px);
}
.winline {
  font-size: 0.84rem;
  font-weight: 800;
}
.winline.p1 { color: #ef88a8; }
.winline.p2 { color: #5a9be6; }
.winline.draw { color: #8a7680; }

@media (prefers-reduced-motion: reduce) {
  .scoreboard,
  .game-card {
    animation: none;
  }
}

@media (max-width: 1180px) {
  .game-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 860px) {
  .game-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .scoreboard {
    grid-template-columns: 1fr;
    text-align: center;
  }
  .team-p2 {
    text-align: center;
    justify-items: center;
  }
}

@media (max-width: 560px) {
  .game-grid {
    grid-template-columns: 1fr;
  }
}
</style>
