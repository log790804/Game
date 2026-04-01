<template>
  <main class="about">
    <div class="card">
      <h1>遊戲工廠規劃中</h1>
      <p>這個頁面先保留作為後續的遊戲分類、活動公告或工廠設定說明。</p>
      <ul>
        <li>可以放遊戲分類與主題清單</li>
        <li>可以放活動公告與最新上架資訊</li>
        <li>可以放家長模式或年齡分級說明</li>
        <li>可以放遊戲 component 切換規則</li>
      </ul>
    </div>
  </main>
</template>

<style scoped>
main.about {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 2rem;
}

.card {
  background: rgba(255, 252, 246, 0.88);
  border: 1px solid rgba(132, 106, 82, 0.12);
  border-radius: 24px;
  padding: 2.5rem;
  max-width: 500px;
  width: 100%;
  box-shadow: 0 20px 40px rgba(111, 87, 66, 0.12);
  color: #5b493d;
}

h1 {
  font-size: 1.8rem;
  margin-bottom: 1rem;
  color: #8a684d;
}

p {
  color: #7b695c;
  margin-bottom: 1.2rem;
}

ul {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 0.8rem;
}

li {
  padding: 0.8rem 1rem;
  background: rgba(244, 232, 215, 0.68);
  border-radius: 12px;
  font-size: 0.95rem;
}
</style>
