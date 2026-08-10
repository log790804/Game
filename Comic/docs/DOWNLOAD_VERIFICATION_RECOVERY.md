# Spec: 批次下載驗證失效復原

## Objective

當批次下載途中因 HappyMH 權限或真人驗證失效而無法繼續時，暫停下載並自動開啟既有的手動驗證視窗。使用者完成驗證後，程式匯入新的 Cookie 與 User-Agent，然後以原本勾選的章節重新進入下載流程；既有圖片由續傳機制跳過。

下載管理提供可切換的下載模式，預設為「安全」：

- 安全：每次 1 張、800ms 間隔、優先使用 WebView2。
- 標準：每次最多 2 張、350ms 間隔、使用已驗證的 HTTP 工作階段。
- 快速：每次最多 3 張、150ms 間隔、使用已驗證的 HTTP 工作階段。

### Assumptions

1. `SourceAccessException` 只代表需要更新來源網站工作階段的存取失敗；一般逾時、斷線、格式錯誤仍走原本的單章錯誤摘要。
2. HTTP 401、403、429 與驗證頁重新導向視為需要手動驗證。
3. Cookie 僅保存在目前執行中的 `CookieContainer` 與 WebView2 使用者資料，不寫入漫畫中繼資料、下載紀錄或應用程式日誌。
4. 使用者取消驗證時停止本次批次，保留已下載圖片與未完成章節的選取狀態。
5. 每次按下下載最多自動要求三次驗證，避免網站持續拒絕時形成無限重試。
6. 下載模式只能在未執行下載時切換，且不跨應用程式重啟保存；每次啟動都回到安全模式。

## Tech Stack

- .NET 10 / C# / WPF
- Microsoft WebView2 `1.0.4078.44`
- xUnit `2.9.3`

## Commands

- Targeted test: `dotnet test tests/Comic.Tests/Comic.Tests.csproj --filter FullyQualifiedName~SequentialDownloadServiceTests`
- Full test: `dotnet test Comic.sln`
- Release build: `dotnet build Comic.sln --configuration Release`
- Run: `dotnet run --project src/Comic.Desktop/Comic.Desktop.csproj`

## Project Structure

- `src/Comic.Core/Exceptions/`：來源存取例外契約
- `src/Comic.Infrastructure/Downloads/`：順序下載、續傳與錯誤分類
- `src/Comic.Desktop/`：驗證視窗與下載復原協調
- `src/Comic.Desktop/ViewModels/`：下載結果狀態與畫面訊息
- `tests/Comic.Tests/`：下載服務回歸測試

## Code Style

使用型別化例外，不以錯誤訊息字串判斷流程；主視窗是模態 UI 的唯一協調者：

```csharp
catch (SourceAccessException exception)
{
    await RecoverVerifiedSessionAndResumeAsync(exception);
}
```

## Testing Strategy

- 先新增下載服務回歸測試，證明驗證例外不再被一般章節錯誤吞掉。
- 新增 HTTP 權限狀態測試，證明圖片請求會轉成驗證例外。
- 保留現有非圖片與一般錯誤測試，證明它們不會誤觸人工驗證。
- 最後執行全部 xUnit 測試與 Release 建置。

## Boundaries

- Always：限制 HappyMH URL、限制內容大小、保留 `.partial` 清理、使用型別化例外、驗證後重跑原選取批次。
- Ask first：新增依賴、改變 Library 格式、永久保存 Cookie、嘗試自動破解 CAPTCHA。
- Never：記錄或顯示 Cookie 值、繞過真人驗證、無上限自動重試、因驗證失效刪除已下載圖片。

## Success Criteria

- 批次下載在章節清單或圖片請求收到驗證失效時立即暫停，不繼續後續章節。
- 下載模式下拉選單預設安全，並可切換標準或快速；模式在單次批次開始時固定。
- 安全／標準／快速的同時圖片請求上限分別為 1／2／3。
- 主視窗自動顯示手動驗證視窗，且視窗提供重新整理與返回漫畫頁功能。
- 完成驗證後更新 Cookie/User-Agent，重新執行原批次；既有圖片不重複寫入。
- 取消驗證或連續三次仍失敗時安全停止，已下載內容保留。
- 一般網路、格式或單張圖片錯誤維持原本錯誤摘要，不彈出驗證視窗。
- 測試全部通過，Release 建置零錯誤。

## Open Questions

無；需求與既有手動驗證流程足以決定此版本行為。
