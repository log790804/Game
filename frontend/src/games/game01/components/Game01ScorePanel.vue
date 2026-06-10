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
  border-radius: 22px;
  background: rgba(18, 16, 34, 0.62);
  border: 1px solid rgba(150, 130, 220, 0.18);
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.4);
}

.status-card {
  padding: 1.4rem;
}

.eyebrow {
  color: #e7c66b;
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.16em;
  text-transform: uppercase;
}

h2 {
  margin-top: 0.35rem;
  color: #e9e6ff;
}

.status-card p:last-child {
  margin-top: 0.65rem;
  color: #b3acd6;
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
  color: #b39bff;
  font-weight: 700;
}

.player-card strong {
  color: #e9e6ff;
  font-size: 1.4rem;
}

.player-card.active {
  background: linear-gradient(135deg, rgba(231, 198, 107, 0.18), rgba(179, 155, 255, 0.16));
  border-color: rgba(231, 198, 107, 0.4);
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
  color: #8f88bb;
  font-size: 0.85rem;
}

.summary-grid strong {
  color: #e9e6ff;
  font-size: 1.2rem;
}

@media (max-width: 780px) {
  .players,
  .summary-grid {
    grid-template-columns: 1fr;
  }
}
</style>
