<template>
  <div class="welcome-card">
    <div class="logo">
      <svg viewBox="0 0 128 128" xmlns="http://www.w3.org/2000/svg">
        <path d="M0 8.934l49.854.158 14.167 24.47 14.432-24.47L128 8.935l-63.979 110.14z" fill="#41b883"/>
        <path d="M63.952 119.074L97.79 60.41l-33.838.05z" fill="#34495e"/>
        <path d="M0 8.934l49.854.158 14.167 24.47L49.854 57.99 16.408 57.96z" fill="#41b883"/>
        <path d="M63.979 33.562l14.432-24.47 49.854-.158-16.272 49.026-33.895.03z" fill="#41b883"/>
        <path d="M49.854 57.99l14.098 61.084L16.408 57.96z" fill="#2c3e50"/>
        <path d="M78.41 57.99l-14.431 61.084 47.544-61.054z" fill="#2c3e50"/>
      </svg>
    </div>
    <h1>歡迎使用 <span>Vue 3</span></h1>
    <p>你的預設專案已建置完成，開始開發吧！</p>
    <div class="badges">
      <span class="badge">Vue 3</span>
      <span class="badge">Vite</span>
      <span class="badge">Pinia</span>
      <span class="badge">Vue Router</span>
    </div>
  </div>
</template>

<style scoped>
.welcome-card {
  text-align: center;
  color: #e2e8f0;
}

.logo svg {
  width: 80px;
  height: 80px;
  margin-bottom: 1.5rem;
  filter: drop-shadow(0 0 20px rgba(65, 184, 131, 0.5));
  animation: float 3s ease-in-out infinite;
}

@keyframes float {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-10px); }
}

h1 {
  font-size: 2.5rem;
  font-weight: 700;
  margin-bottom: 0.8rem;
}

h1 span {
  color: #41b883;
}

p {
  color: #a0aec0;
  font-size: 1.1rem;
  margin-bottom: 1.5rem;
}

.badges {
  display: flex;
  gap: 0.6rem;
  justify-content: center;
  flex-wrap: wrap;
}

.badge {
  background: rgba(65, 184, 131, 0.15);
  border: 1px solid rgba(65, 184, 131, 0.3);
  color: #68d391;
  padding: 4px 14px;
  border-radius: 999px;
  font-size: 0.85rem;
  font-weight: 500;
}
</style>
