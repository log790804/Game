# GitHub Pages + Firebase 設定

## GitHub Pages

1. 到 GitHub repository 的 `Settings` -> `Pages`。
2. `Build and deployment` 的 `Source` 選 `GitHub Actions`。
3. 推送到 `main` 後，`.github/workflows/github-pages.yml` 會自動建置 `frontend/dist` 並發布。
4. 專案頁網址通常會是 `https://<帳號>.github.io/<repository-name>/`。
5. GitHub Pages 部署時會使用 hash router，遊戲路徑會像 `https://<帳號>.github.io/<repository-name>/#/game04`，可避免重新整理時 404。

## Firebase Firestore

1. 到 Firebase Console 建立 Firebase project。
2. 新增 Web app，複製 Firebase config。
3. 建立 Cloud Firestore database。
4. 將根目錄的 `firestore.rules` 貼到 Firestore Rules 並發布。
5. 目前 `.github/workflows/github-pages.yml` 已直接帶入 Firebase Web config，push 到 `main` 後線上版會自動連到 Firestore。

## 本機測試

複製 `frontend/.env.example` 成 `frontend/.env`，填入 Firebase 設定後執行：

```powershell
cd frontend
npm run dev
```

如果 `VITE_FIREBASE_ENABLED=false` 或 Firebase 設定沒有填完整，遊戲資料會自動退回使用瀏覽器 `localStorage`。

## 注意

Firebase Web config 會被打包進前端，它不是資料庫密碼；真正的資料安全要靠 Firestore Rules。此專案目前適合公開展示遊戲紀錄，不建議存放個資或敏感內容。

如果之後想避免 Firebase config 出現在 workflow 檔案，也可以改成 GitHub Actions Secrets，再把 workflow 的 `VITE_FIREBASE_*` 改回 `${{ secrets.VITE_FIREBASE_* }}`。
