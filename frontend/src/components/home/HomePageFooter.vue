<template>
  <footer class="lobby-footer">
    <span class="brand">
      <img
        src="/assets/HOME/deco/joystick.png"
        alt=""
        class="ico"
      >
      Game Factory · 雙人對戰遊戲廳
    </span>
    <span class="dim">本機保存比分與紀錄，不會上傳</span>
  </footer>
</template>

<style scoped>
.lobby-footer {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  padding: 0.9rem 1.1rem;
  margin-top: 0.5rem;
  border-radius: 12px;
  background: rgba(64, 58, 82, 0.55);
  border: 2px solid rgba(253, 243, 231, 0.18);
  font-size: 0.84rem;
  color: #e7dcec;
}

.brand {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 700;
}
.ico {
  width: 22px;
  height: auto;
  image-rendering: pixelated;
}

.dim {
  color: #b7adc4;
}
</style>
