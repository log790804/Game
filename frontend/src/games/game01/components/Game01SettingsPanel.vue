<template>
  <section class="settings-panel">
    <div class="panel-heading">
      <div>
        <p class="eyebrow">Game 01</p>
        <h2>翻牌遊戲設定</h2>
      </div>
      <p>預設雙人輪流翻牌計分，可隨時重開新局，並把目前狀態存進 game01.json。</p>
    </div>

    <div class="settings-grid">
      <label class="field">
        <span>棋盤尺寸</span>
        <select
          :value="boardSize"
          :disabled="disabled"
          @change="$emit('update:boardSize', Number($event.target.value))"
        >
          <option
            v-for="size in boardSizes"
            :key="size"
            :value="size"
          >
            {{ size }} x {{ size }}
          </option>
        </select>
      </label>

      <label class="field">
        <span>卡背圖片網址</span>
        <input
          :value="backImage"
          :disabled="disabled"
          type="text"
          placeholder="留白則使用預設卡背"
          @input="$emit('update:backImage', $event.target.value)"
        >
      </label>

      <label class="field full">
        <span>卡面圖片網址清單</span>
        <textarea
          :value="frontImagesText"
          :disabled="disabled"
          rows="6"
          placeholder="一行一張圖片網址；若數量不足會自動補預設圖"
          @input="$emit('update:frontImagesText', $event.target.value)"
        />
      </label>
    </div>

    <div class="actions">
      <button
        type="button"
        class="primary"
        :disabled="disabled"
        @click="$emit('start')"
      >
        開始新局
      </button>

      <button
        type="button"
        :disabled="disabled"
        @click="$emit('reset')"
      >
        重置遊戲
      </button>

      <button
        type="button"
        :disabled="disabled"
        @click="$emit('clear-records')"
      >
        清空紀錄
      </button>
    </div>
  </section>
</template>

<script setup>
defineProps({
  boardSize: { type: Number, required: true },
  boardSizes: { type: Array, required: true },
  backImage: { type: String, required: true },
  frontImagesText: { type: String, required: true },
  disabled: { type: Boolean, default: false }
})

defineEmits([
  'update:boardSize',
  'update:backImage',
  'update:frontImagesText',
  'start',
  'reset',
  'clear-records'
])
</script>

<style scoped>
.settings-panel {
  display: grid;
  gap: 1.25rem;
  padding: 1.6rem;
  border-radius: 24px;
  background: rgba(18, 16, 34, 0.62);
  border: 1px solid rgba(150, 130, 220, 0.18);
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.4);
}

.panel-heading {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 1rem;
}

.eyebrow {
  color: #e7c66b;
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.16em;
  text-transform: uppercase;
}

h2 {
  margin-top: 0.35rem;
  color: #e9e6ff;
}

.panel-heading p {
  max-width: 28rem;
  color: #b3acd6;
}

.settings-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}

.field {
  display: grid;
  gap: 0.45rem;
}

.field.full {
  grid-column: 1 / -1;
}

.field span {
  color: #c7bcf0;
  font-size: 0.92rem;
  font-weight: 700;
}

select,
input,
textarea {
  width: 100%;
  border: 1px solid rgba(150, 130, 220, 0.25);
  border-radius: 14px;
  padding: 0.85rem 1rem;
  background: rgba(255, 255, 255, 0.05);
  color: #e9e6ff;
}

select:focus,
input:focus,
textarea:focus {
  outline: none;
  border-color: rgba(231, 198, 107, 0.6);
}

textarea {
  resize: vertical;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

button {
  border: 0;
  border-radius: 999px;
  padding: 0.8rem 1.15rem;
  background: rgba(255, 255, 255, 0.08);
  color: #c7bcf0;
  font-weight: 700;
  cursor: pointer;
}

button.primary {
  background: linear-gradient(135deg, #e7c66b, #b39bff);
  color: #1a1530;
}

button:disabled,
select:disabled,
input:disabled,
textarea:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

@media (max-width: 780px) {
  .settings-grid {
    grid-template-columns: 1fr;
  }
}
</style>
