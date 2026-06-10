<template>
  <header class="lobby-header">
    <div class="status-line">
      <span class="pulse" />
      <span>SYSTEM ONLINE</span>
      <span class="sep">//</span>
      <span>2P VERSUS ARENA</span>
    </div>
    <h1 class="title">
      <span class="zh">雙人對戰遊戲廳</span>
      <span class="en">VERSUS&nbsp;ARENA</span>
    </h1>
    <p class="tagline">
      20 款雙人遊戲 · 一輪定冠軍 — 每款各玩一次，勝負累積到下方比分。
    </p>
  </header>
</template>

<style scoped>
.lobby-header {
  padding: 0.5rem 0.25rem;
  animation: riseIn 0.7s cubic-bezier(0.2, 0.7, 0.2, 1) backwards;
}

.status-line {
  display: inline-flex;
  align-items: center;
  gap: 0.55rem;
  font-family: 'JetBrains Mono', ui-monospace, 'SF Mono', 'Cascadia Code', Consolas, monospace;
  font-size: 0.74rem;
  letter-spacing: 0.22em;
  text-transform: uppercase;
  color: #6fe9d6;
}
.status-line .sep {
  color: rgba(150, 200, 230, 0.4);
}
.pulse {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #34d399;
  box-shadow: 0 0 10px #34d399;
  animation: blink 1.6s ease-in-out infinite;
}

.title {
  margin-top: 0.9rem;
  line-height: 1;
}
.title .zh {
  display: block;
  font-size: clamp(2.4rem, 5.4vw, 4.2rem);
  font-weight: 800;
  letter-spacing: 0.01em;
  background: linear-gradient(100deg, #5eead4, #a78bfa 38%, #f472b6 66%, #5eead4 100%);
  background-size: 220% 100%;
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
  animation: shimmer 9s linear infinite;
}
.title .en {
  display: block;
  margin-top: 0.5rem;
  font-family: 'JetBrains Mono', ui-monospace, 'SF Mono', Consolas, monospace;
  font-size: clamp(0.7rem, 1.6vw, 1rem);
  letter-spacing: 0.55em;
  color: rgba(150, 200, 230, 0.38);
}

.tagline {
  margin-top: 1.2rem;
  max-width: 42rem;
  color: #a9a3d0;
  font-size: 1rem;
}

@keyframes shimmer {
  to {
    background-position: 220% 0;
  }
}
@keyframes blink {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.3;
  }
}
@keyframes riseIn {
  from {
    opacity: 0;
    transform: translateY(16px);
  }
  to {
    opacity: 1;
    transform: none;
  }
}

@media (prefers-reduced-motion: reduce) {
  .lobby-header,
  .title .zh,
  .pulse {
    animation: none;
  }
}
</style>
