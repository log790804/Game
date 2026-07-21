# 素材美術風格規格

## 核心風格

本遊戲素材建議採用「明亮 Q 版奇幻塔防」風格。

- 類型：2D 網頁塔防
- 視角：斜俯視 3/4 top-down
- 氣氛：輕鬆、魔法、冒險、清楚易讀
- 精細度：中等細節，優先確保小尺寸可辨識
- 主要用途：瀏覽器遊戲、桌機與手機皆可閱讀

## 不採用的風格

- 寫實暗黑風
- 過度細節的厚塗風
- 純像素 8-bit 風
- 科幻軍事寫實風
- 過度可愛導致戰鬥辨識度不足的幼兒風

## 視角與比例

| 素材 | 視角 | 建議尺寸 | 備註 |
|---|---|---:|---|
| 防禦塔 | 斜俯視 | 128x128 | 實際遊戲可縮到 64x64 |
| 敵人 | 斜俯視或側斜視 | 96x96 | 需保留清楚輪廓 |
| Boss | 斜俯視 | 192x192 | 可比一般敵人大 1.5-2 倍 |
| 技能圖示 | 正面 icon | 128x128 | 圖示要能放入技能列 |
| 成就徽章 | 正面 icon | 128x128 | 外框可表現成就等級 |
| UI 按鈕 | 平面 UI | 依介面設計 | 需支援手機觸控 |
| 地圖格 | 俯視或斜俯視 | 128x128 | 需可無縫拼接 |
| 特效 | 透明背景 | 128x128 或 256x256 | 爆炸、冰凍、雷擊等 |

## 色彩方向

整體色彩要明亮但不要單一色系。建議使用自然場景色搭配魔法高亮色。

| 用途 | 建議色彩 |
|---|---|
| 地圖草地 | 綠色、青綠、少量土黃 |
| 路徑 | 暖棕、石灰、淺灰 |
| 普通塔 | 木質棕、鐵灰、皮革色 |
| 稀有塔 | 藍色、青色高光 |
| 史詩塔 | 紫色、亮橘或魔法藍 |
| 傳說塔 | 金色、白色、深紅點綴 |
| 神話塔 | 白金、深紫、藍白發光 |
| 敵人 | 與地圖分離的深色輪廓 |
| 技能 | 高飽和特效色，但中心要清楚 |

## 稀有度視覺規則

| 稀有度 | 外觀表現 |
|---|---|
| 普通 | 木頭、石頭、鐵件，無發光或微弱高光 |
| 稀有 | 藍色符文、小型寶石、清楚外框 |
| 史詩 | 魔法光效、特殊材質、較複雜輪廓 |
| 傳說 | 金色裝飾、強烈輪廓、專屬底座 |
| 神話 | 獨特形狀、環形能量、漂浮核心、全場唯一感 |

## 防禦塔設計規則

每座塔在小尺寸下必須一眼看出用途。

| 塔 | 視覺關鍵 |
|---|---|
| 箭塔 | 弓、箭袋、木製高台 |
| 砲塔 | 砲管、金屬底座、火藥感 |
| 毒塔 | 綠色瓶罐、毒霧、藤蔓 |
| 冰塔 | 冰晶、藍白色核心 |
| 雷塔 | 尖塔、電弧、金屬導體 |
| 火焰塔 | 火盆、熔岩裂紋、橘紅火焰 |
| 聖光塔 | 白金色、光環、聖徽 |
| 時空塔 | 沙漏、鐘盤、紫藍空間裂縫 |
| 神話核心塔 | 漂浮核心、多層光環、獨特底座 |

## 敵人設計規則

敵人必須透過輪廓與顏色快速區分能力。

| 敵人 | 視覺關鍵 |
|---|---|
| 普通怪 | 中等體型、基本護甲 |
| 快速怪 | 瘦長、前傾、速度線或輕裝 |
| 坦克怪 | 大體型、盾牌、厚重裝甲 |
| 飛行怪 | 翅膀、漂浮影子、較小接地感 |
| 護盾怪 | 半透明護盾或發光護甲 |
| Boss | 巨大輪廓、專屬顏色、明顯弱點或核心 |

## UI 風格

UI 使用「木框 + 魔法水晶 + 清楚資訊」的奇幻冒險風格。

- HUD 不要遮住地圖中央
- 按鈕需有清楚按壓狀態
- 手機觸控按鈕建議不小於 44x44 CSS px
- 技能按鈕需要冷卻遮罩
- 塔資訊面板要顯示攻擊力、攻速、範圍、效果、升級費用
- 稀有度可使用外框顏色，但不要只靠顏色，也要有圖案或材質差異

## 特效風格

特效要短、清楚、不要長時間遮擋戰場。

| 特效 | 表現方式 |
|---|---|
| 爆炸 | 橘紅中心、煙塵外圈、短時間擴散 |
| 冰凍 | 藍白冰晶、敵人周圍霜圈 |
| 中毒 | 綠色霧氣、小氣泡 |
| 燃燒 | 橘紅火苗、地面灼燒痕 |
| 雷擊 | 藍白閃電、短暫高亮 |
| 破甲 | 裝甲碎片、金色裂痕 |
| 升級 | 向上光柱、星點、短暫外框發光 |
| 合成 | 多色能量匯聚到中心 |

## 動畫規則

第一版可先使用靜態圖搭配簡單 tween 動畫，等核心玩法穩定後再補 sprite sheet。

建議動畫：

- 敵人走路：4-6 frames
- 敵人受擊：2-3 frames
- 敵人死亡：4-6 frames
- 塔攻擊：2-4 frames 或砲口閃光
- 技能特效：6-12 frames
- 合成特效：8-12 frames

動畫素材需保持：

- 透明背景
- 同一角色比例一致
- 底部中心作為 anchor
- 同方向、同色系、同輪廓

## 圖片生成 Prompt 模板

### 防禦塔

```text
2D chibi fantasy tower defense game asset, 3/4 top-down view, [tower type], clear silhouette, readable at small size, bright adventure palette, transparent background, centered object, no text, no scenery, game-ready icon, consistent soft outline
```

### 敵人

```text
2D chibi fantasy tower defense enemy sprite, 3/4 top-down view, [enemy type], clear silhouette, readable at 64x64 game size, bright fantasy colors, transparent background, centered character, no text, no scenery, game-ready asset
```

### 技能圖示

```text
2D fantasy game skill icon, [skill effect], bold readable symbol, high contrast, circular icon composition, bright magical effect, no text, no background scenery, game UI asset
```

### 成就徽章

```text
2D fantasy achievement badge icon, [achievement theme], medal frame, clear symbol, bright readable colors, no text, transparent background, game UI asset
```

## 素材驗收標準

素材完成後需檢查：

- 64x64 時仍能辨識用途
- 不靠文字也能理解功能
- 與同類素材視角一致
- 透明背景乾淨
- 沒有多餘場景、陰影或文字
- 稀有度差異清楚
- 敵人與地圖背景有足夠對比
- UI 按鈕在手機尺寸可點擊
- 特效不會遮住重要戰鬥資訊
