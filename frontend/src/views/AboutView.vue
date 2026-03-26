<template>
  <main class="about">
    <div class="card">
      <h1>關於此專案</h1>
      <p>這是一個使用以下技術建置的 Vue 3 預設專案：</p>
      <ul>
        <li>⚡ <strong>Vite</strong> — 極速開發伺服器與打包工具</li>
        <li>💚 <strong>Vue 3</strong> — Composition API + <code>&lt;script setup&gt;</code></li>
        <li>🗂️ <strong>Vue Router 4</strong> — SPA 路由管理（含懶加載）</li>
        <li>🍍 <strong>Pinia</strong> — 輕量狀態管理</li>
      </ul>
    </div>
  </main>
</template>

<style scoped>
main.about {
  min-height: calc(100vh - 60px);
  background: linear-gradient(135deg, #0f0c29, #302b63, #24243e);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 2rem;
}

.card {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  padding: 2.5rem;
  max-width: 500px;
  width: 100%;
  backdrop-filter: blur(10px);
  color: #e2e8f0;
}

h1 {
  font-size: 1.8rem;
  margin-bottom: 1rem;
  color: #63b3ed;
}

p {
  color: #a0aec0;
  margin-bottom: 1.2rem;
}

ul {
  list-style: none;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.8rem;
}

li {
  padding: 0.6rem 1rem;
  background: rgba(99, 179, 237, 0.08);
  border-radius: 8px;
  font-size: 0.95rem;
}

code {
  background: rgba(99, 179, 237, 0.15);
  padding: 2px 6px;
  border-radius: 4px;
  font-family: monospace;
  color: #90cdf4;
}
</style>
