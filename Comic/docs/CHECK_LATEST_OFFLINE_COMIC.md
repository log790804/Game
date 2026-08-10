# Spec: 離線漫畫查看最新章節

## Objective

在「離線閱讀」選定一本本機漫畫後，提供「查看最新」按鈕。按下後依漫畫資料夾名稱建立 HappyMH 詳情頁網址，自動切換至「下載管理」、帶入網址並載入最新章節清單；現有下載歷史機制繼續排除已完成章節，只顯示可下載更新。

### Assumptions

1. 漫畫資料夾名稱是 HappyMH 漫畫 ID，例如 `butiange` 對應 `https://m.happymh.com/manga/butiange`。
2. 此功能只檢查與列出更新，不自動勾選或下載章節。
3. 外部章節資料夾模式沒有漫畫 ID，因此不提供查看最新。
4. 網站要求驗證時沿用既有手動驗證與 Cookie 更新流程。
5. `comic-info.json` 不需升級；顯示名稱仍由關聯檔提供，來源網址由資料夾 ID 安全推導。

## Tech Stack

- .NET 10 / C# / WPF
- xUnit 2.9.3

## Commands

- Mapping test: `dotnet test tests/Comic.Tests/Comic.Tests.csproj --filter FullyQualifiedName~CreateHappyMhMangaUriFromComicId`
- UI test: `dotnet test tests/Comic.Tests/Comic.Tests.csproj --filter FullyQualifiedName~ReaderView_ProvidesCheckLatestAction`
- Full test: `dotnet test Comic.sln`
- Release build: `dotnet build Comic.sln --configuration Release`

## Project Structure

- `src/Comic.Core/Security/SourceUrlPolicy.cs`：資料夾 ID 到 HappyMH URL 的安全映射契約
- `src/Comic.Desktop/ViewModels/ReaderViewModel.cs`：所選離線漫畫與按鈕可用狀態
- `src/Comic.Desktop/MainWindow.xaml`：查看最新按鈕
- `src/Comic.Desktop/MainWindow.xaml.cs`：切換分頁、帶入網址與載入漫畫
- `tests/Comic.Tests/`：網址與 UI 回歸測試

## Code Style

網址只能透過安全策略建立，不在 UI 直接串接任意路徑：

```csharp
var sourceUri = SourceUrlPolicy.CreateHappyMhMangaUriFromComicId(comic.Id);
```

## Testing Strategy

- 單元測試驗證合法 ID 的精確映射，以及路徑穿越、URL 字元與過長 ID 被拒絕。
- 靜態 WPF 測試驗證按鈕文字、事件、可用狀態與輔助功能名稱。
- 完整測試確認下載排除、閱讀歷史與人工驗證未回歸。

## Boundaries

- Always：只建立 `https://m.happymh.com/manga/{id}`；以 allowlist 驗證 ID；切換前保存閱讀位置。
- Ask first：升級中繼資料格式、自動下載更新、新增其他漫畫網站。
- Never：把資料夾文字當成完整 URL、允許路徑分隔符、略過 HappyMH URL 驗證、按下後自動下載。

## Success Criteria

- 選定離線漫畫時「查看最新」可用；外部資料夾模式或沒有漫畫時停用。
- 按下後切換到下載管理，網址欄顯示由資料夾 ID 建立的 HappyMH 詳情頁。
- 自動載入最新章節，已完成章節仍被下載歷史排除。
- 無更新時下載管理明確顯示可下載章節為 0。
- 不接受可造成任意路徑或任意主機的資料夾 ID。
- 全部測試通過且 Release 建置零錯誤。

## Open Questions

無。
