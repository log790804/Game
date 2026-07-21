# 動畫素材規劃

本文件定義塔防遊戲中防禦塔、敵人、技能與特效的動畫需求。第一階段先以「短幀數、辨識清楚、可循環」為原則，不追求過度複雜的全動畫。

## 動畫目標

- 讓戰鬥畫面更有生命感
- 讓玩家能看懂塔正在攻擊、敵人正在受擊或死亡
- 讓技能、升級、合成塔有明確回饋
- 保持效能穩定，避免大量敵人同時出現時掉幀

## 共通規格

| 項目 | 規格 |
|---|---|
| 風格 | 明亮 Q 版奇幻塔防 |
| 視角 | 斜俯視 3/4 top-down |
| 背景 | 透明背景 |
| Anchor | 底部中心 bottom-center |
| 一般塔幀尺寸 | 128x128 |
| 一般敵人幀尺寸 | 96x96 |
| Boss 幀尺寸 | 192x192 |
| 特效幀尺寸 | 128x128 或 256x256 |
| 格式 | PNG sprite sheet 或分幀 PNG |
| 命名 | 使用 asset-id + action + frame 編號 |

## 第一階段動畫優先順序

| 優先 | 類型 | 動畫 | 理由 |
|---:|---|---|---|
| 1 | 敵人 | walk | 敵人移動是畫面最常見動作 |
| 2 | 塔 | attack | 玩家需要知道塔正在輸出 |
| 3 | 子彈 / 投射物 | projectile | 讓攻擊目標與命中關係清楚 |
| 4 | 敵人 | hit | 受擊回饋提升打擊感 |
| 5 | 敵人 | death | 擊殺需要有爽感與結算提示 |
| 6 | 技能 | cast / impact | 主動技能需要明確視覺回饋 |
| 7 | 塔 | upgrade | 升級需要短暫特效 |
| 8 | 組合塔 | combine | 合成或共鳴觸發時使用 |

## 防禦塔動畫

塔不一定需要走路式逐幀動畫，第一版可以用「短 sprite + Phaser tween」混合。

| 塔 | idle | attack | upgrade | 說明 |
|---|---:|---:|---:|---|
| 箭塔 | 2 frames | 4 frames | 6 frames | 弓臂拉開、箭發射 |
| 砲塔 | 2 frames | 4 frames | 6 frames | 砲管後座、砲口閃光 |
| 毒塔 | 2 frames | 4 frames | 6 frames | 毒霧脈動、瓶罐發光 |
| 冰塔 | 2 frames | 4 frames | 6 frames | 冰晶閃爍、冷氣釋放 |
| 雷塔 | 2 frames | 4 frames | 6 frames | 電弧跳動、塔尖聚電 |
| 火焰塔 | 2 frames | 4 frames | 6 frames | 火焰循環、噴火瞬間 |
| 聖光塔 | 2 frames | 4 frames | 6 frames | 光環旋轉、聖光射線 |
| 時空塔 | 2 frames | 4 frames | 6 frames | 沙漏流動、時空波紋 |
| 神話核心塔 | 4 frames | 6 frames | 8 frames | 核心漂浮、能量環旋轉 |

### 塔動畫原則

- idle 動畫要很輕，不要造成畫面太吵
- attack 動畫要短，通常 120-250ms 內完成
- 攻擊動畫與 projectile 生成時間要對齊
- 升級動畫可由 sprite effect 搭配 Phaser 粒子完成
- 塔的底座位置不可跳動，避免玩家感覺塔在漂移

## 敵人動畫

敵人是最需要 sprite 動畫的對象。

### 四方向面向

正式素材需要每種敵人提供四個方向的面向圖，不能只用單一靜態圖旋轉或翻轉。

```text
enemy-normal-walk-down-sheet.png
enemy-normal-walk-up-sheet.png
enemy-normal-walk-left-sheet.png
enemy-normal-walk-right-sheet.png
enemy-normal-hit-down-sheet.png
enemy-normal-death-down-sheet.png
```

方向判定依照路徑移動向量：

- dx 絕對值大於 dy 時，用 left / right
- dy 絕對值大於 dx 時，用 up / down
- 轉彎時保留上一個方向到下一段路徑，避免短時間閃爍

### 行走分層

正式動畫建議至少分成：

- body：主體 sprite sheet
- shadow：地面陰影，可獨立縮放
- step-dust：腳步灰塵或小草擺動
- hit-flash：受擊閃白或描邊層

這樣大量怪物同時移動時，畫面會比單張圖上下跳動更順。

| 敵人 | walk | hit | death | special | 說明 |
|---|---:|---:|---:|---:|---|
| 普通怪 | 6 frames | 2 frames | 6 frames | 無 | 標準節奏 |
| 快速怪 | 6 frames | 2 frames | 4 frames | 2 frames | 步伐更快，可加速度殘影 |
| 坦克怪 | 6 frames | 2 frames | 6 frames | 2 frames | 動作慢但重量感明顯 |
| 飛行怪 | 6 frames | 2 frames | 6 frames | 2 frames | 翅膀拍動、浮空陰影 |
| 護盾怪 | 6 frames | 2 frames | 6 frames | 4 frames | 護盾閃爍或破裂 |
| Boss | 8 frames | 3 frames | 10 frames | 6 frames | 可有怒吼、召喚或護盾動作 |

### 敵人動畫原則

- walk 動畫必須可無縫循環
- hit 動畫不要太長，避免大量敵人受擊時畫面混亂
- death 動畫可以稍微誇張，但要快速清除碰撞與目標鎖定
- 飛行怪需要保留浮空感和地面陰影
- Boss 可以比一般敵人多一個 special 動作

## 投射物與命中特效

| 攻擊類型 | 投射物 | 命中特效 |
|---|---|---|
| 箭 | 飛行箭矢 2-4 frames | 小型火花 |
| 砲彈 | 砲彈旋轉 2-4 frames | 爆炸 6-8 frames |
| 毒 | 綠色液滴 2-4 frames | 毒霧 6 frames |
| 冰 | 冰晶碎片 2-4 frames | 冰花 6 frames |
| 雷 | 閃電鏈 4 frames | 電擊閃光 4 frames |
| 火 | 火球 4 frames | 火焰爆開 6 frames |
| 聖光 | 光束或光彈 4 frames | 金白光圈 6 frames |
| 時空 | 紫藍能量球 4 frames | 波紋扭曲 6 frames |

## 箭塔攻擊動畫規格

箭塔攻擊需要拆成三個時間點：

```text
1. windup：拉弓，弓弦後拉，持續約攻擊間隔的 20%-30%
2. release：箭矢生成，從塔的發射點飛出
3. impact：箭矢命中怪物後才扣血，播放受擊動畫與扣血數字
```

箭矢飛行：

- 使用拋物線或二次貝茲曲線
- 飛行時箭頭要朝向速度方向旋轉
- 目標移動時可追蹤目標目前位置
- 不可在發射瞬間扣血
- 命中判定成立後才套用傷害、血條變化、hit 動畫

## 技能動畫

| 技能 | cast | impact | 持續效果 | 說明 |
|---|---:|---:|---:|---|
| 隕石轟炸 | 6 frames | 8 frames | 無 | 隕石落下與爆炸 |
| 冰霜領域 | 6 frames | 6 frames | 8 frames loop | 範圍緩速圈 |
| 金幣祝福 | 6 frames | 無 | 8 frames loop | 金色收益光效 |
| 時間停止 | 8 frames | 6 frames | 8 frames loop | 紫藍時鐘波紋 |
| 緊急修復 | 6 frames | 6 frames | 無 | 基地回血光效 |

## 組合塔與合成動畫

組合塔動畫不一定每次都播放，避免干擾戰鬥。建議只在首次觸發、合成完成或強化狀態變更時播放。

| 動畫 | 幀數 | 用途 |
|---|---:|---|
| combo-activate | 8 frames | 共鳴啟動 |
| combo-link | 6 frames loop | 多塔之間的連線提示 |
| tower-fusion | 10-12 frames | 合成塔生成 |
| mythic-awaken | 12 frames | 神話塔終極效果解鎖 |

## 命名規則

### Sprite sheet

```text
tower-arrow-attack-sheet.png
tower-cannon-idle-sheet.png
enemy-normal-walk-sheet.png
enemy-fast-hit-sheet.png
enemy-boss-forest-death-sheet.png
skill-meteor-impact-sheet.png
fx-combo-activate-sheet.png
```

### 分幀 PNG

```text
enemy-normal-walk-001.png
enemy-normal-walk-002.png
enemy-normal-walk-003.png
tower-arrow-attack-001.png
tower-arrow-attack-002.png
```

## Phaser 實作備註

未來實作時建議：

- 使用 texture atlas 或 sprite sheet 管理動畫
- 同一種敵人的 walk 動畫共用，不要每隻敵人載入獨立圖檔
- hit 動畫可用 tint + scale tween 輔助，減少 sprite 數量
- 大量小怪死亡時可降低死亡動畫播放數量，避免效能問題
- 使用 animation key 統一命名，例如 `enemy-normal-walk`
- 塔的 idle 動畫可用 Phaser tween 補足，不一定全部做成 sprite

## 圖片生成流程

動畫素材不要逐張獨立生成，容易造成角色比例與輪廓漂移。建議流程：

```text
先確認單張 seed frame
  -> 以 seed frame 生成完整 animation strip
  -> 固定每格尺寸與底部中心 anchor
  -> 檢查角色比例是否穩定
  -> 產出 preview sheet
  -> 通過後才切成正式 sprite sheet 或 atlas
```

## 動畫驗收標準

- 每一幀角色比例一致
- 動作在 64x64 時仍看得懂
- 透明背景乾淨
- 底座或腳底位置不亂跳
- loop 動畫首尾銜接自然
- attack 動畫能對齊子彈生成時間
- hit 與 death 不會遮擋過多戰場資訊
- Boss 動畫可讀性高，但不應佔滿整個畫面
