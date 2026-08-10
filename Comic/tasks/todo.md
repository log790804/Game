# Tasks: 批次下載驗證失效復原

## Task 1: 定義下載中斷契約

**Acceptance criteria:**
- [x] `SourceAccessException` 不被下載服務轉成一般章節錯誤。
- [x] 圖片 HTTP 401/403/429 會轉成 `SourceAccessException`。
- [x] 一般非圖片錯誤仍留在下載摘要。

**Verification:**
- [x] `dotnet test tests/Comic.Tests/Comic.Tests.csproj --filter FullyQualifiedName~SequentialDownloadServiceTests`

**Dependencies:** None

**Files likely touched:**
- `tests/Comic.Tests/SequentialDownloadServiceTests.cs`
- `src/Comic.Infrastructure/Downloads/SequentialDownloadService.cs`

## Task 2: 串接手動驗證後續跑

**Acceptance criteria:**
- [x] ViewModel 解除忙碌狀態後讓 `SourceAccessException` 傳到主視窗。
- [x] 主視窗完成驗證後更新 Cookie/User-Agent 並重跑原批次。
- [x] 取消或三次失敗後停止且保留下載內容。

**Verification:**
- [x] `dotnet build Comic.sln --configuration Release`

**Dependencies:** Task 1

**Files likely touched:**
- `src/Comic.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/Comic.Desktop/MainWindow.xaml.cs`

## Task 3: 提供驗證頁重新整理操作

**Acceptance criteria:**
- [x] 驗證視窗可明確重新整理目前頁面。
- [x] 導航仍受既有 HappyMH allowlist 保護。

**Verification:**
- [x] `dotnet test Comic.sln`
- [x] `dotnet build Comic.sln --configuration Release`

**Dependencies:** Task 2

**Files likely touched:**
- `src/Comic.Desktop/ManualVerificationWindow.xaml`
- `src/Comic.Desktop/ManualVerificationWindow.xaml.cs`

## Task 4: 加入下載模式切換

**Acceptance criteria:**
- [x] 下拉選單提供安全、標準、快速，且每次啟動預設安全。
- [x] 三種模式的同時圖片請求上限分別為 1、2、3。
- [x] 下載期間不能切換；章節與檔案排序、續傳、人工驗證行為不變。

**Verification:**
- [x] 先確認新增併發測試在實作前失敗。
- [x] `dotnet test Comic.sln`
- [x] `dotnet build Comic.sln --configuration Release`

**Dependencies:** Task 1-3

**Files likely touched:**
- `src/Comic.Core/Models/DownloadMode.cs`
- `src/Comic.Core/Abstractions/ISequentialDownloadService.cs`
- `src/Comic.Infrastructure/Downloads/SequentialDownloadService.cs`
- `src/Comic.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/Comic.Desktop/MainWindow.xaml`
- `tests/Comic.Tests/SequentialDownloadServiceTests.cs`

## Task 5: 從離線閱讀查看最新章節

**Acceptance criteria:**
- [x] 由本機漫畫資料夾 ID 安全建立 HappyMH 漫畫網址。
- [x] 離線閱讀提供「查看最新」按鈕，無漫畫或外部資料夾模式時停用。
- [x] 按下後切換下載管理、自動載入，並只留下尚未完成章節。

**Verification:**
- [x] 先確認網址映射與 UI 測試在實作前失敗。
- [x] `dotnet test Comic.sln`
- [x] `dotnet build Comic.sln --configuration Release`

**Dependencies:** Task 1-4

**Files likely touched:**
- `src/Comic.Core/Security/SourceUrlPolicy.cs`
- `src/Comic.Desktop/ViewModels/ReaderViewModel.cs`
- `src/Comic.Desktop/MainWindow.xaml`
- `src/Comic.Desktop/MainWindow.xaml.cs`
- `tests/Comic.Tests/SourceUrlPolicyTests.cs`
- `tests/Comic.Tests/DesktopThemeTests.cs`
