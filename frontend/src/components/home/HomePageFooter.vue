<template>
  <section class="footer-section">
    <div class="footer-card intro">
      <p class="eyebrow">首頁頁尾</p>
      <h2>後續擴充方向</h2>
      <p>
        目前首頁先作為遊戲大廳，後續每個卡片都可以接到不同遊戲 component，保留整體一致的入口體驗。
      </p>
    </div>

    <div class="footer-card roadmap">
      <div>
        <span>01</span>
        <strong>卡片換成真實封面</strong>
      </div>
      <div>
        <span>02</span>
        <strong>點擊後切換遊戲 component</strong>
      </div>
      <div>
        <span>03</span>
        <strong>加入收藏、人氣與分類</strong>
      </div>
    </div>
  </section>
</template>

<style scoped>
.footer-section {
  display: grid;
  grid-template-columns: minmax(0, 1.1fr) minmax(0, 1fr);
  gap: 1.25rem;
}

.footer-card {
  border-radius: 30px;
  padding: 2rem;
  border: 1px solid rgba(138, 110, 89, 0.12);
  box-shadow: 0 18px 36px rgba(125, 100, 82, 0.1);
}

.intro {
  background: linear-gradient(135deg, rgba(255, 246, 232, 0.95), rgba(255, 251, 244, 0.9));
}

.roadmap {
  display: grid;
  gap: 1rem;
  background: linear-gradient(135deg, rgba(247, 240, 228, 0.95), rgba(245, 249, 255, 0.9));
}

.eyebrow {
  color: #9e7c5d;
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

h2 {
  margin-top: 0.5rem;
  color: #4f3f31;
  font-size: 2rem;
}

p {
  margin-top: 0.9rem;
  color: #6f6258;
}

.roadmap div {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem 1.1rem;
  border-radius: 20px;
  background: rgba(255, 255, 255, 0.6);
}

.roadmap span {
  width: 2.3rem;
  height: 2.3rem;
  display: inline-grid;
  place-items: center;
  border-radius: 999px;
  background: #f3d8bc;
  color: #8a5f3b;
  font-weight: 800;
}

.roadmap strong {
  color: #5a4738;
}

@media (max-width: 900px) {
  .footer-section {
    grid-template-columns: 1fr;
  }
}
</style>
