<template>
  <footer class="lobby-footer">
    <span>Game Factory · 雙人對戰遊戲廳</span>
    <span class="dim">本機保存比分與紀錄，不會上傳</span>
  </footer>
</template>

<style scoped>
.lobby-footer {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  padding: 1rem 1.25rem;
  border-top: 1px solid rgba(150, 130, 220, 0.16);
  margin-top: 0.5rem;
  font-size: 0.85rem;
  color: #8f88bb;
}

.dim {
  color: #6d6796;
}
</style>
