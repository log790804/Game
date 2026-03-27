# Game

## 專案簡介

這是一個前後端分離的全端專案。

本文件的檢視範圍僅包含：

- `backend`：ASP.NET Core Web API
- `frontend`：Vue 3 + Vite 前端應用

目前專案仍接近初始化狀態：

- 後端保留預設的 `weatherforecast` API
- 前端已建立 Vue Router 與 Pinia 基礎結構
- 目前文件不納入其他非應用程式目錄

## 版本與技術

以下資訊依目前組態檔內容整理：

### Backend

- Framework：`.NET 9.0`
- 專案類型：`ASP.NET Core Web API`
- 語言：`C#`
- 專案檔：`backend/backend.csproj`
- 已使用套件：
  - `Microsoft.AspNetCore.OpenApi 9.0.14`

### Frontend

- Framework：`Vue 3`（依 `package.json` 目前為 `^3.4.0`）
- Router：`vue-router ^4.2.5`
- State 管理：`pinia ^2.1.7`
- Build Tool：`Vite ^5.0.0`
- Vite Vue Plugin：`@vitejs/plugin-vue ^5.0.0`
- 模組格式：`ESM`（`"type": "module"`）

### Node.js

- 專案內目前沒有 `engines`、`.nvmrc` 或 `package-lock.json` 可直接確認固定 Node 版本
- 依 `Vite 5` 生態，建議使用 `Node.js 18+`

## 目錄結構

```text
Game/
├─ backend/
│  ├─ Program.cs
│  ├─ appsettings.json
│  ├─ appsettings.Development.json
│  ├─ backend.csproj
│  ├─ backend.http
│  └─ Properties/
│     └─ launchSettings.json
├─ frontend/
│  ├─ public/
│  ├─ src/
│  │  ├─ assets/
│  │  ├─ components/
│  │  ├─ router/
│  │  ├─ stores/
│  │  ├─ views/
│  │  ├─ App.vue
│  │  └─ main.js
│  ├─ index.html
│  ├─ package.json
│  ├─ README.md
│  └─ vite.config.js
└─ README.md
```

## 各資料夾說明

### `backend`

後端 API 專案，目前為標準 ASP.NET Core Web API 起始結構。

主要內容：

- `Program.cs`：應用程式入口，目前註冊 OpenAPI 並提供 `/weatherforecast`
- `appsettings*.json`：環境設定檔
- `backend.http`：本機測試 API 用的 HTTP 範例
- `Properties/launchSettings.json`：本機啟動設定

### `frontend`

Vue 前端專案，目前具備基本單頁應用架構。

主要內容：

- `src/main.js`：Vue 應用程式入口
- `src/router/index.js`：前端路由設定
- `src/stores/`：Pinia 狀態管理
- `src/views/`：頁面層元件
- `src/components/`：可重用元件
- `vite.config.js`：Vite 設定與 `@` 別名

## 目前功能狀態

### 後端

- 已啟用 OpenAPI（僅 Development 環境對應 `MapOpenApi()`）
- 已啟用 HTTPS Redirect
- 已提供測試用 API：
  - `GET /weatherforecast`

### 前端

- 已建立 Vue App 啟動流程
- 已整合 Pinia
- 已建立 Vue Router
- 預設頁面包含：
  - `/`
  - `/about`

## 本機啟動方式

### 啟動 backend

```powershell
cd backend
dotnet restore
dotnet run
```

### 啟動 frontend

```powershell
cd frontend
npm install
npm run dev
```

## Docker 發行

目前已補上 Docker 發行所需檔案：

- `backend/Dockerfile`
- `frontend/Dockerfile`
- `frontend/nginx.conf`
- `docker-compose.yml`
- `.dockerignore`

### 發行架構

- `backend`：ASP.NET Core API 容器，內部監聽 `8080`
- `frontend`：Nginx 容器，提供 Vue build 後的靜態檔
- `frontend` 會將 `/api/*` 反向代理到 `backend`

### 啟動方式

在根目錄執行：

```powershell
docker compose up --build -d
```

啟動後可使用：

- Frontend：http://localhost:5173
- Backend API：http://localhost:8080/api/health

### 開發模式 API Proxy

前端開發模式下，`Vite` 已設定 `/api` 代理到：

- 預設：`http://localhost:5099`

如需變更，可在前端目錄建立 `.env`：

```env
VITE_API_PROXY_TARGET=http://localhost:5099
VITE_API_BASE_URL=/api
```

### API 路徑

目前後端已提供：

- `GET /api/health`
- `GET /api/weatherforecast`

為了相容現有測試，也保留：

- `GET /weatherforecast`

## 開發建議

- 建議補上根目錄 `docs/`，整理 API、資料流與部署方式
- 若前後端會串接，建議下一步建立：
  - API 命名規範
  - 環境變數設定方式
  - 前端 API Service 層
  - 後端 Controller / Service / Repository 分層

## 後續可擴充文件

如果專案會持續擴大，建議後續補充：

- `docs/ARCHITECTURE.md`
- `docs/API.md`
- `docs/DEPLOYMENT.md`
- `backend/Controllers.md`
- `backend/Services.md`
- `frontend/src/views/README.md`
