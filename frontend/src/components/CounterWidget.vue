<script setup>
import { useCounterStore } from '@/stores/counter'
const store = useCounterStore()
</script>

<template>
  <div class="counter-widget">
    <h2>Pinia 計數器範例</h2>
    <div class="count-display">
      <span class="count">{{ store.count }}</span>
      <span class="double">× 2 = {{ store.doubleCount }}</span>
    </div>
    <div class="buttons">
      <button class="btn btn-danger" @click="store.decrement">−</button>
      <button class="btn btn-secondary" @click="store.reset">重置</button>
      <button class="btn btn-success" @click="store.increment">+</button>
    </div>
  </div>
</template>

<style scoped>
.counter-widget {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  padding: 2rem 2.5rem;
  text-align: center;
  backdrop-filter: blur(10px);
  min-width: 280px;
}

h2 {
  color: #a0aec0;
  font-size: 0.9rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  margin-bottom: 1.2rem;
}

.count-display {
  margin-bottom: 1.5rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.3rem;
}

.count {
  font-size: 3.5rem;
  font-weight: 700;
  color: #63b3ed;
  line-height: 1;
}

.double {
  font-size: 0.9rem;
  color: #718096;
}

.buttons {
  display: flex;
  gap: 0.8rem;
  justify-content: center;
}

.btn {
  padding: 0.5rem 1.4rem;
  border: none;
  border-radius: 8px;
  font-size: 1.2rem;
  font-weight: 600;
  cursor: pointer;
  transition: transform 0.1s, opacity 0.2s;
}

.btn:hover { opacity: 0.85; }
.btn:active { transform: scale(0.95); }

.btn-success { background: #38a169; color: white; }
.btn-danger  { background: #e53e3e; color: white; }
.btn-secondary { background: #4a5568; color: #e2e8f0; font-size: 0.85rem; }
</style>
