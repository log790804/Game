<template>
  <header class="lobby-header">
    <img
      class="mascot"
      src="/assets/HOME/home-mascot-pixel-girl.png"
      alt="看板娘"
    >

    <div class="head-text">
      <span class="online-pill">
        <span class="dot" />
        2P VERSUS ARENA
      </span>

      <img
        class="logo"
        src="/assets/HOME/logo-game-factory-pixel.png"
        alt="Game Factory"
      >
      <h1 class="zh-title">雙人對戰遊戲廳</h1>
      <p class="tagline">
        20 款雙人遊戲・一輪定冠軍 — 每款各玩一次，勝負累積到下方比分。
      </p>
    </div>
  </header>
</template>

<style scoped>
.lobby-header {
  display: flex;
  align-items: flex-end;
  gap: 1.4rem;
  padding: 0.5rem 0.5rem 0.25rem;
  animation: riseIn 0.7s cubic-bezier(0.2, 0.7, 0.2, 1) backwards;
}

.mascot {
  width: 120px;
  height: auto;
  image-rendering: pixelated;
  flex-shrink: 0;
  filter: drop-shadow(0 8px 0 rgba(40, 36, 60, 0.3));
  animation: mascotBob 4.5s ease-in-out infinite;
}

.head-text {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.online-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  align-self: flex-start;
  padding: 0.28rem 0.8rem;
  border-radius: 999px;
  background: rgba(253, 243, 231, 0.92);
  border: 2px solid #5b4a54;
  font-family: ui-monospace, 'SF Mono', Consolas, monospace;
  font-size: 0.66rem;
  font-weight: 700;
  letter-spacing: 0.18em;
  color: #5b4a54;
}
.online-pill .dot {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  background: #a8d8b9;
  border: 1px solid #5b4a54;
  animation: blink 1.6s ease-in-out infinite;
}

.logo {
  width: clamp(220px, 32vw, 320px);
  height: auto;
  image-rendering: pixelated;
  filter: drop-shadow(0 4px 0 rgba(40, 36, 60, 0.35));
}

.zh-title {
  font-size: clamp(1.4rem, 3vw, 2rem);
  font-weight: 800;
  letter-spacing: 0.08em;
  color: #fdf3e7;
  text-shadow: 0 2px 0 #5b4a54, 0 0 18px rgba(255, 183, 197, 0.45);
}

.tagline {
  max-width: 40rem;
  color: #e7dcec;
  font-size: 0.96rem;
  text-shadow: 0 1px 4px rgba(28, 24, 44, 0.7);
}

@keyframes mascotBob {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-8px); }
}
@keyframes blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.3; }
}
@keyframes riseIn {
  from { opacity: 0; transform: translateY(16px); }
  to { opacity: 1; transform: none; }
}

@media (prefers-reduced-motion: reduce) {
  .lobby-header,
  .mascot,
  .online-pill .dot {
    animation: none;
  }
}

@media (max-width: 640px) {
  .lobby-header {
    gap: 0.8rem;
  }
  .mascot {
    width: 84px;
  }
}
</style>
