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
  border-radius: 30px;
  background: rgba(255, 252, 246, 0.88);
  border: 1px solid rgba(136, 106, 83, 0.12);
  box-shadow: 0 18px 36px rgba(112, 89, 68, 0.1);
}

.panel-heading {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 1rem;
}

.eyebrow {
  color: #a07c60;
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

h2 {
  margin-top: 0.35rem;
  color: #4f3d31;
}

.panel-heading p {
  max-width: 28rem;
  color: #6f6154;
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
  color: #7e6350;
  font-size: 0.92rem;
  font-weight: 700;
}

select,
input,
textarea {
  width: 100%;
  border: 1px solid rgba(135, 106, 84, 0.18);
  border-radius: 18px;
  padding: 0.85rem 1rem;
  background: #fffdf8;
  color: #4d3a2e;
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
  background: #f3e6d5;
  color: #765941;
  font-weight: 700;
}

button.primary {
  background: linear-gradient(135deg, #f2b980, #eb9388);
  color: #fff9f2;
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
