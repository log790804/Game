<script setup>
import { RouterLink } from 'vue-router'
import { gameLobbyCards } from '@/data/gameLobby'
</script>

<template>
  <section class="content-section">
    <div class="section-heading">
      <div>
        <p class="eyebrow">首頁頁中</p>
        <h2>遊戲入口預留區</h2>
      </div>

      <p>
        目前先規劃 4 x 4 共 16 個卡片格。每張卡片上半部保留封面圖片，下半部保留名稱與入口資訊，之後可直接替換成真正遊戲內容。
      </p>
    </div>

    <div class="game-grid">
      <article
        v-for="card in gameLobbyCards"
        :key="card.id"
        class="game-card"
      >
        <div
          class="card-cover"
          :style="card.imageStyle"
        >
          <span>{{ card.route ? '遊戲封面' : '封面預留' }}</span>
        </div>

        <div class="card-body">
          <span class="card-tag">{{ card.subtitle }}</span>
          <h3>{{ card.title }}</h3>
          <p>{{ card.description }}</p>

          <RouterLink
            v-if="card.route"
            :to="card.route"
            class="card-action is-link"
          >
            {{ card.actionLabel }}
          </RouterLink>

          <button
            v-else
            type="button"
            class="card-action"
          >
            {{ card.actionLabel }}
          </button>
        </div>
      </article>
    </div>
  </section>
</template>

<style scoped>
.content-section {
  display: grid;
  gap: 1.5rem;
}

.section-heading {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 1rem;
  align-items: end;
}

.eyebrow {
  color: #9e7d61;
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

h2 {
  margin-top: 0.45rem;
  color: #513f31;
  font-size: clamp(1.8rem, 3vw, 2.6rem);
}

.section-heading p {
  max-width: 36rem;
  color: #6e6155;
}

.game-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1.1rem;
}

.game-card {
  overflow: hidden;
  border-radius: 26px;
  background: rgba(255, 253, 249, 0.9);
  border: 1px solid rgba(137, 110, 89, 0.12);
  box-shadow: 0 18px 36px rgba(128, 101, 78, 0.12);
}

.card-cover {
  min-height: 160px;
  display: flex;
  align-items: flex-start;
  justify-content: flex-end;
  padding: 1rem;
}

.card-cover span {
  padding: 0.35rem 0.7rem;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.72);
  color: #715845;
  font-size: 0.78rem;
  font-weight: 700;
}

.card-body {
  display: grid;
  gap: 0.75rem;
  padding: 1.1rem;
}

.card-tag {
  color: #b28a64;
  font-size: 0.8rem;
  font-weight: 700;
}

h3 {
  color: #4e3a2d;
  font-size: 1.05rem;
}

.card-body p {
  color: #6f6257;
  font-size: 0.92rem;
}

.card-action {
  justify-self: start;
  border: 0;
  border-radius: 999px;
  padding: 0.7rem 1rem;
  background: #f6eadc;
  color: #7f6147;
  font-weight: 700;
}

.card-action.is-link {
  background: linear-gradient(135deg, #f2b980, #eb9388);
  color: #fff9f3;
  box-shadow: 0 12px 24px rgba(235, 147, 136, 0.22);
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
}

@media (max-width: 560px) {
  .game-grid {
    grid-template-columns: 1fr;
  }
}
</style>
