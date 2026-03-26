# My Vue App

Vue 3 預設專案，使用 Vite 建置。

## 技術棧

- **Vue 3** — Composition API + `<script setup>`
- **Vite 5** — 開發伺服器 & 打包
- **Vue Router 4** — 路由管理（含懶加載）
- **Pinia** — 狀態管理

## 專案結構

```
my-vue-app/
├── public/
├── src/
│   ├── assets/        # 全域樣式
│   ├── components/    # 可重用元件
│   ├── router/        # Vue Router 設定
│   ├── stores/        # Pinia stores
│   ├── views/         # 頁面元件
│   ├── App.vue
│   └── main.js
├── index.html
├── vite.config.js
└── package.json
```

## 開始使用

```bash
# 安裝相依套件
npm install

# 啟動開發伺服器
npm run dev

# 打包正式版本
npm run build

# 預覽打包結果
npm run preview
```
