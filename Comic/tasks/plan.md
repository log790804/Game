# Implementation Plan: 批次下載驗證失效復原

## Overview

沿用既有 WebView2 手動驗證與續傳機制，在下載服務、ViewModel 與主視窗之間加入明確的驗證失效結果，讓批次能安全暫停、更新工作階段並續跑。

本次增量加入預設安全的下載模式切換，標準與快速模式只在單一章節內進行 2／3 路有限併發。

## Architecture Decisions

- 以既有 `SourceAccessException` 作為模組邊界，不依賴網站錯誤訊息文字。
- 下載服務只將權限／驗證錯誤向上拋出；其他章節錯誤仍收進 `DownloadSummary`。
- ViewModel 以既有 `finally` 解除忙碌狀態並讓型別化例外傳出；主視窗負責模態驗證與最多三次續跑。
- 驗證續跑只更新工作階段，不重新套用漫畫清單，以保留原選取章節。

## Task List

### Phase 1: Contract and detection

- [x] 以失敗測試定義驗證例外必須穿透下載服務。
- [x] 將 HTTP 權限狀態與 WebView2 權限狀態統一轉為驗證例外。

### Checkpoint: Detection

- [x] 下載服務針對性測試通過。

### Phase 2: Recovery flow

- [x] ViewModel 保留選取與檔案，主視窗攔截型別化驗證例外。
- [x] 主視窗彈出手動驗證，更新工作階段後續跑，最多三次。
- [x] 手動驗證視窗加入重新整理按鈕。

### Checkpoint: Complete

- [x] 完整測試通過。
- [x] Release 建置成功。
- [x] Cookie 未新增任何持久化或日誌輸出。

### Phase 3: Download modes

- [x] 新增安全／標準／快速模式契約與併發上限測試。
- [x] 在 ViewModel 與下載管理 UI 提供模式切換，預設安全。
- [x] 標準／快速模式使用有限 HTTP 併發，安全模式維持 WebView2 單通道。

### Checkpoint: Download modes

- [x] 驗證 1／2／3 路併發上限。
- [x] 完整測試與 Release 建置通過。

### Phase 4: Check latest from offline reader

- [x] 定義本機漫畫資料夾 ID 到 HappyMH 詳情頁的安全映射。
- [x] 在離線閱讀加入「查看最新」按鈕與可用狀態。
- [x] 按下後保存進度、切換下載管理、帶入網址並載入最新章節。

### Checkpoint: Check latest

- [x] 映射與 UI 回歸測試通過。
- [x] 已下載章節排除與人工驗證流程維持不變。
- [x] 完整測試與 Release 建置通過。

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| 一般網路錯誤被誤判 | 中 | 只攔截型別化例外與明確的 401/403/429/重新導向 |
| 重試造成重複下載 | 中 | 沿用檔案存在即跳過的續傳行為 |
| 網站持續拒絕形成迴圈 | 中 | 每次按下載最多三次驗證 |
| Cookie 洩漏 | 高 | 僅匯入記憶體容器，不寫檔、不輸出內容 |

## Open Questions

無。
