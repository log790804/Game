# 角色動畫素材拆分

## 目的

`heros` 目錄中的角色 PNG 是 spritesheet。切割流程會把每個角色依列拆成待機、移動、攻擊、受擊、倒地與技能動畫，並輸出透明背景 frame 與橫向 sheet，供網頁預覽或遊戲引擎載入。

## 輸入

```text
TD Game/heros/*.png
```

目前支援兩種表格格式：

- 綠色格線，左側可能有動作標籤。
- 灰色格線，無動作標籤。

## 輸出

```text
TD Game/web-preview/assets/heroes/
  manifest.json
  hero-animations-data.js
  flame-swordsman/
    flame-swordsman-idle-sheet.png
    idle/
      flame-swordsman-idle-001.png
```

每個 action 都會輸出：

- `角色-action-sheet.png`：橫向 spritesheet。
- `action/*.png`：單張透明 frame。
- `manifest.json`：角色、動作、fps、frame 尺寸、frame 數與檔案路徑。
- `hero-animations-data.js`：給 `hero-animations.html` 直接載入的資料。

## 動作命名

前 7 列固定為：

| Row | Action | 說明 |
|---:|---|---|
| 1 | `idle` | 待機 |
| 2 | `walk` | 行走 |
| 3 | `run` | 跑步 |
| 4 | `jump` | 跳躍 |
| 5 | `attack` | 普攻 |
| 6 | `hurt` | 受擊 |
| 7 | `knockdown` | 倒地 |

第 8 列之後依序輸出為 `skill-01`、`skill-02`。若來源圖左側有標籤，且最後一招跨兩列，會合併為同一個 `skill-05`。

## 重跑切割

在專案根目錄執行：

```powershell
& 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' 'D:\Game\Game\TD Game\scripts\slice_hero_sprites.py'
```

若本機已安裝 Python 與 Pillow，也可在 `TD Game` 目錄執行：

```powershell
python scripts/slice_hero_sprites.py
```

## 預覽

開啟：

```text
TD Game/web-preview/hero-animations.html
```

若瀏覽器阻擋 `file://` 載入，可在 `TD Game/web-preview` 啟動本機靜態伺服器後瀏覽：

```powershell
python -m http.server 8787 --bind 127.0.0.1
```

再開啟：

```text
http://127.0.0.1:8787/hero-animations.html
```

## 目前產出

- 角色：7
- 動作與技能：88
- Frame：1065
