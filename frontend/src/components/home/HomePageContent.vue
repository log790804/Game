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
        <span class="vs">VS</span>
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
        :style="{ '--accent': card.accent, '--i': index }"
      >
        <span class="ghost-no">{{ card.no }}</span>

        <div class="card-head">
          <span class="tag">G{{ card.no }}</span>
          <span
            class="state"
            :class="resultFor(card.route) ? 'is-done' : 'is-ready'"
          >
            {{ resultFor(card.route) ? '已通關' : '待挑戰' }}
          </span>
        </div>

        <h3>{{ card.title }}</h3>
        <p class="desc">{{ card.description }}</p>

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
          >
            進入 <span class="arrow">→</span>
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
  --mono: 'JetBrains Mono', ui-monospace, 'SF Mono', 'Cascadia Code', Consolas, monospace;
}

@keyframes riseIn {
  from {
    opacity: 0;
    transform: translateY(18px);
  }
  to {
    opacity: 1;
    transform: none;
  }
}

/* 比分看板 */
.scoreboard {
  position: relative;
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
  gap: 1rem;
  padding: 1.5rem 1.8rem;
  border-radius: 18px;
  background: linear-gradient(160deg, rgba(32, 30, 60, 0.55), rgba(12, 10, 26, 0.55));
  border: 1px solid rgba(150, 140, 220, 0.18);
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(8px);
  animation: riseIn 0.6s cubic-bezier(0.2, 0.7, 0.2, 1) 0.08s backwards;
}

.team {
  display: grid;
  gap: 0.15rem;
  padding: 0.6rem 1.1rem;
  border-radius: 14px;
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
  color: #7c76aa;
}
.team-name {
  font-size: 0.95rem;
  font-weight: 700;
  color: #d8d4f5;
}
.team-wins {
  font-family: var(--mono);
  font-size: 2.8rem;
  font-weight: 800;
  line-height: 1;
}
.team-p1 .team-wins {
  color: #5eead4;
  text-shadow: 0 0 22px rgba(94, 234, 212, 0.45);
}
.team-p2 .team-wins {
  color: #f472b6;
  text-shadow: 0 0 22px rgba(244, 114, 182, 0.45);
}
.team.leading {
  transform: translateY(-2px);
}
.team-p1.leading {
  box-shadow: inset 0 0 0 1px rgba(94, 234, 212, 0.4), 0 10px 30px rgba(94, 234, 212, 0.18);
}
.team-p2.leading {
  box-shadow: inset 0 0 0 1px rgba(244, 114, 182, 0.4), 0 10px 30px rgba(244, 114, 182, 0.18);
}

.score-center {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.55rem;
  min-width: 190px;
}
.vs {
  font-family: var(--mono);
  font-size: 1.1rem;
  font-weight: 800;
  color: #cfcaf0;
  letter-spacing: 0.18em;
}
.progress-text {
  font-family: var(--mono);
  font-size: 0.72rem;
  letter-spacing: 0.12em;
  color: #8f88bb;
}
.progress-bar {
  width: 170px;
  height: 5px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.1);
  overflow: hidden;
}
.progress-fill {
  height: 100%;
  border-radius: 999px;
  background: linear-gradient(90deg, #5eead4, #a78bfa 55%, #f472b6);
  box-shadow: 0 0 12px rgba(94, 234, 212, 0.5);
  transition: width 0.4s ease;
}
.reset-button {
  margin-top: 0.2rem;
  border: 1px solid rgba(94, 234, 212, 0.45);
  border-radius: 999px;
  padding: 0.5rem 1.3rem;
  font-size: 0.82rem;
  font-weight: 700;
  color: #5eead4;
  background: rgba(94, 234, 212, 0.08);
  cursor: pointer;
  transition: background 0.2s, box-shadow 0.2s, transform 0.15s;
}
.reset-button:hover {
  background: rgba(94, 234, 212, 0.16);
  box-shadow: 0 0 22px rgba(94, 234, 212, 0.3);
  transform: translateY(-1px);
}

/* grid label */
.grid-label {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 1rem;
  padding: 0 0.25rem;
  border-bottom: 1px solid rgba(150, 140, 220, 0.14);
  padding-bottom: 0.7rem;
}
.gl-title {
  font-size: 1.05rem;
  font-weight: 800;
  color: #e9e6ff;
}
.gl-title i {
  font-style: normal;
  font-family: var(--mono);
  font-size: 0.72rem;
  letter-spacing: 0.2em;
  color: #6fe9d6;
  margin-left: 0.5rem;
}
.gl-meta {
  font-family: var(--mono);
  font-size: 0.74rem;
  letter-spacing: 0.1em;
  color: #8f88bb;
}

.game-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1rem;
}

.game-card {
  position: relative;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-height: 196px;
  padding: 1.1rem 1.15rem 1.15rem;
  border-radius: 16px;
  background: linear-gradient(165deg, rgba(30, 28, 56, 0.62), rgba(13, 11, 27, 0.62));
  border: 1px solid rgba(150, 140, 220, 0.16);
  box-shadow: 0 16px 40px rgba(0, 0, 0, 0.4);
  transition: transform 0.2s, border-color 0.2s, box-shadow 0.2s;
  animation: riseIn 0.5s ease calc(var(--i, 0) * 0.03s + 0.15s) backwards;
}
.game-card::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0;
  height: 2px;
  width: 100%;
  background: linear-gradient(90deg, var(--accent), transparent 72%);
  opacity: 0.7;
}
.game-card:hover {
  transform: translateY(-6px);
  border-color: color-mix(in srgb, var(--accent) 55%, transparent);
  box-shadow: 0 22px 50px rgba(0, 0, 0, 0.55),
    0 0 26px color-mix(in srgb, var(--accent) 28%, transparent);
}
.game-card.done {
  opacity: 0.62;
}

.ghost-no {
  position: absolute;
  right: -4px;
  bottom: -22px;
  font-family: var(--mono);
  font-weight: 800;
  font-size: 5.4rem;
  line-height: 1;
  color: var(--accent);
  opacity: 0.1;
  pointer-events: none;
}

.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.tag {
  font-family: var(--mono);
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.16em;
  color: var(--accent);
}
.state {
  font-size: 0.66rem;
  letter-spacing: 0.08em;
  padding: 0.2rem 0.55rem;
  border-radius: 999px;
  border: 1px solid;
}
.state.is-ready {
  color: #cdd6ff;
  border-color: rgba(150, 140, 220, 0.3);
}
.state.is-done {
  color: #8f88bb;
  border-color: rgba(150, 140, 220, 0.2);
}

h3 {
  color: #f0eeff;
  font-size: 1.06rem;
  position: relative;
  z-index: 1;
}

.desc {
  color: #9a93c4;
  font-size: 0.82rem;
  line-height: 1.45;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  position: relative;
  z-index: 1;
}

.card-foot {
  margin-top: auto;
  position: relative;
  z-index: 1;
}
.enter {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  font-weight: 700;
  font-size: 0.88rem;
  color: var(--accent);
  text-decoration: none;
}
.enter .arrow {
  transition: transform 0.2s;
}
.game-card:hover .enter .arrow {
  transform: translateX(5px);
}
.winline {
  font-size: 0.82rem;
  font-weight: 700;
}
.winline.p1 {
  color: #5eead4;
}
.winline.p2 {
  color: #f472b6;
}
.winline.draw {
  color: #cdd6ff;
}

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
