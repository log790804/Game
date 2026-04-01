<template>
  <section class="score-panel">
    <div class="status-card">
      <p class="eyebrow">目前狀態</p>
      <h2>{{ statusTitle }}</h2>
      <p>{{ lastAction }}</p>
    </div>

    <div class="players">
      <article
        v-for="(player, index) in players"
        :key="player.name"
        class="player-card"
        :class="{ active: currentPlayerIndex === index && !isCompleted }"
      >
        <span>{{ player.name }}</span>
        <strong>{{ player.score }} 分</strong>
      </article>
    </div>

    <div class="summary-grid">
      <div>
        <span>翻牌回合</span>
        <strong>{{ moves }}</strong>
      </div>
      <div>
        <span>完成配對</span>
        <strong>{{ matches }}</strong>
      </div>
      <div>
        <span>歷史紀錄</span>
        <strong>{{ recordCount }}</strong>
      </div>
    </div>
  </section>
</template>

<script setup>
defineProps({
  statusTitle: { type: String, required: true },
  lastAction: { type: String, required: true },
  players: { type: Array, required: true },
  currentPlayerIndex: { type: Number, required: true },
  moves: { type: Number, required: true },
  matches: { type: Number, required: true },
  isCompleted: { type: Boolean, default: false },
  recordCount: { type: Number, required: true }
})
</script>

<style scoped>
.score-panel {
  display: grid;
  gap: 1rem;
}

.status-card,
.player-card,
.summary-grid {
  border-radius: 26px;
  background: rgba(255, 252, 246, 0.88);
  border: 1px solid rgba(136, 106, 83, 0.12);
  box-shadow: 0 18px 36px rgba(112, 89, 68, 0.1);
}

.status-card {
  padding: 1.4rem;
}

.eyebrow {
  color: #9a7759;
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

h2 {
  margin-top: 0.35rem;
  color: #4f3d31;
}

.status-card p:last-child {
  margin-top: 0.65rem;
  color: #6d5f54;
}

.players {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.9rem;
}

.player-card {
  padding: 1.2rem;
  display: grid;
  gap: 0.35rem;
}

.player-card span {
  color: #8e6c52;
  font-weight: 700;
}

.player-card strong {
  color: #4d3a2f;
  font-size: 1.4rem;
}

.player-card.active {
  background: linear-gradient(135deg, rgba(255, 234, 214, 0.95), rgba(255, 248, 240, 0.96));
  border-color: rgba(226, 151, 117, 0.28);
}

.summary-grid {
  padding: 1rem 1.2rem;
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.75rem;
}

.summary-grid div {
  display: grid;
  gap: 0.35rem;
}

.summary-grid span {
  color: #8b6d57;
  font-size: 0.85rem;
}

.summary-grid strong {
  color: #4d3a2f;
  font-size: 1.2rem;
}

@media (max-width: 780px) {
  .players,
  .summary-grid {
    grid-template-columns: 1fr;
  }
}
</style>
