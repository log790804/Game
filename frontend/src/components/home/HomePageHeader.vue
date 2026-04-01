<template>
  <section class="hero-section">
    <div class="hero-copy">
      <p class="eyebrow">Game Factory</p>
      <h1>溫和親民的遊戲廳首頁</h1>
      <p class="description">
        這裡是之後所有遊戲的統一入口。先用柔和、兒少皆宜的視覺風格建立大廳，之後每張卡片都可以切換到對應的遊戲 component。
      </p>

      <div class="hero-actions">
        <button type="button">開始佈局遊戲</button>
        <span>目前預留 16 個入口位置</span>
      </div>
    </div>

    <div class="hero-panel">
      <div class="panel-card">
        <span class="panel-label">首頁結構</span>
        <strong>頁首</strong>
        <strong>頁中</strong>
        <strong>頁尾</strong>
      </div>

      <div class="panel-card soft">
        <span class="panel-label">切換模式</span>
        <strong>Lobby Component</strong>
        <p>之後可以把每個遊戲入口接到獨立元件或路由。</p>
      </div>
    </div>
  </section>
</template>

<style scoped>
.hero-section {
  display: grid;
  grid-template-columns: minmax(0, 1.5fr) minmax(280px, 0.9fr);
  gap: 1.5rem;
  align-items: stretch;
}

.hero-copy,
.panel-card {
  border-radius: 32px;
  border: 1px solid rgba(127, 105, 84, 0.12);
  background: rgba(255, 252, 246, 0.84);
  box-shadow: 0 24px 60px rgba(121, 96, 74, 0.12);
  backdrop-filter: blur(14px);
}

.hero-copy {
  padding: 2.5rem;
}

.eyebrow {
  display: inline-flex;
  padding: 0.35rem 0.8rem;
  border-radius: 999px;
  background: #fff3df;
  color: #9e6f43;
  font-size: 0.85rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

h1 {
  margin-top: 1rem;
  color: #4e3d31;
  font-size: clamp(2.2rem, 4vw, 3.8rem);
  line-height: 1.08;
}

.description {
  margin-top: 1rem;
  max-width: 40rem;
  color: #6f6055;
  font-size: 1.05rem;
}

.hero-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  align-items: center;
  margin-top: 2rem;
}

button {
  border: 0;
  border-radius: 999px;
  padding: 0.95rem 1.4rem;
  background: linear-gradient(135deg, #f2b980, #eb9388);
  color: #fffaf5;
  font-weight: 700;
  box-shadow: 0 12px 24px rgba(235, 147, 136, 0.24);
}

.hero-actions span {
  color: #866c59;
  font-size: 0.95rem;
}

.hero-panel {
  display: grid;
  gap: 1rem;
}

.panel-card {
  padding: 1.5rem;
  display: grid;
  gap: 0.65rem;
  color: #5f4b3e;
}

.panel-card strong {
  font-size: 1.15rem;
}

.panel-card.soft {
  background: rgba(247, 245, 255, 0.84);
}

.panel-label {
  color: #9f886f;
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.panel-card p {
  color: #7a6c60;
}

@media (max-width: 960px) {
  .hero-section {
    grid-template-columns: 1fr;
  }

  .hero-copy {
    padding: 2rem;
  }
}
</style>
