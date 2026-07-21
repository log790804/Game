<template>
  <section class="board-shell">
    <div class="board-heading">
      <div>
        <p class="eyebrow">遊戲棋盤</p>
        <h2>{{ boardSize }} x {{ boardSize }} 翻牌盤面</h2>
      </div>
      <p>翻牌時會保留卡背設定；新局開始時有洗牌動畫，完成配對後會保留已翻開狀態。</p>
    </div>

    <div
      class="game-board"
      :class="{ 'is-shuffling': isShuffling }"
      :style="boardStyle"
    >
      <button
        v-for="(card, index) in cards"
        :key="card.id"
        type="button"
        class="memory-card"
        :class="{
          'is-flipped': card.isRevealed || card.isMatched,
          'is-matched': card.isMatched
        }"
        :style="{ '--shuffle-delay': index }"
        :disabled="disabled || card.isMatched || card.isRevealed"
        @click="$emit('flip', card.id)"
      >
        <span class="memory-card__inner">
          <span class="memory-card__face memory-card__face--back">
            <img
              :src="backImage"
              alt="卡背"
            >
          </span>

          <span class="memory-card__face memory-card__face--front">
            <img
              :src="card.faceImage"
              alt="卡面"
            >
            <span
              v-if="card.isMatched"
              class="memory-card__glow"
              aria-hidden="true"
            />
          </span>
        </span>
      </button>
    </div>
  </section>
</template>

<script setup>
import { computed } from 'vue'
import { assetUrl } from '@/utils/assetUrl'

const props = defineProps({
  cards: { type: Array, required: true },
  boardSize: { type: Number, required: true },
  backImage: { type: String, required: true },
  disabled: { type: Boolean, default: false },
  isShuffling: { type: Boolean, default: false }
})

const boardStyle = computed(() => ({
  '--board-columns': props.boardSize,
  '--match-glow-1': `url("${assetUrl('/assets/G01/fx-match-glow-1.png')}")`,
  '--match-glow-2': `url("${assetUrl('/assets/G01/fx-match-glow-2.png')}")`,
  '--match-glow-3': `url("${assetUrl('/assets/G01/fx-match-glow-3.png')}")`,
  '--match-glow-4': `url("${assetUrl('/assets/G01/fx-match-glow-4.png')}")`
}))

defineEmits(['flip'])
</script>

<style scoped>
.board-shell {
  display: grid;
  gap: 1rem;
  overflow-x: auto;
}

.board-heading {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 1rem;
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

.board-heading p:last-child {
  max-width: 28rem;
  color: #b3acd6;
}

.game-board {
  display: grid;
  grid-template-columns: repeat(var(--board-columns), minmax(0, 1fr));
  gap: 0.8rem;
}

.memory-card {
  aspect-ratio: 3 / 4;
  border: 0;
  padding: 0;
  background: transparent;
  perspective: 1200px;
}

.memory-card__inner {
  position: relative;
  display: block;
  width: 100%;
  height: 100%;
  transform-style: preserve-3d;
  transition: transform 0.6s ease, filter 0.4s ease;
}

.memory-card.is-flipped .memory-card__inner {
  transform: rotateY(180deg);
}

.memory-card.is-matched .memory-card__inner {
  filter: drop-shadow(0 14px 22px rgba(120, 156, 110, 0.24));
}

.memory-card__face {
  position: absolute;
  inset: 0;
  display: block;
  overflow: hidden;
  border-radius: 22px;
  backface-visibility: hidden;
  box-shadow: 0 14px 30px rgba(113, 90, 70, 0.14);
}

.memory-card__face img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
  image-rendering: pixelated;
}

/* 配對成功光環：4 幀 steps 動畫 */
.memory-card__glow {
  position: absolute;
  inset: -14%;
  pointer-events: none;
  background-position: center;
  background-size: contain;
  background-repeat: no-repeat;
  image-rendering: pixelated;
  animation: matchGlow 0.52s steps(1, end) 2;
  opacity: 0;
}

@keyframes matchGlow {
  0% { background-image: var(--match-glow-1); opacity: 1; }
  25% { background-image: var(--match-glow-2); opacity: 1; }
  50% { background-image: var(--match-glow-3); opacity: 1; }
  75% { background-image: var(--match-glow-4); opacity: 1; }
  100% { opacity: 0; }
}

.memory-card__face--front {
  transform: rotateY(180deg);
}

.game-board.is-shuffling .memory-card:not(.is-flipped) .memory-card__inner {
  animation: shuffleWave 0.72s ease;
  animation-delay: calc(var(--shuffle-delay) * 40ms);
}

@keyframes shuffleWave {
  0% {
    transform: translateY(0) rotateY(0deg) scale(1);
  }
  30% {
    transform: translateY(-10px) rotateY(22deg) scale(1.03);
  }
  60% {
    transform: translateY(6px) rotateY(-16deg) scale(0.98);
  }
  100% {
    transform: translateY(0) rotateY(0deg) scale(1);
  }
}
</style>
