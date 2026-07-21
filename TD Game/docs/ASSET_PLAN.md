# 遊戲素材規劃

## 美術方向

建議第一版採用辨識度高的 2D Q 版奇幻塔防風格。完整美術規格請參考 [ASSET_STYLE.md](ASSET_STYLE.md)。

- 視角：2D 俯視或斜俯視
- 色調：明亮、乾淨、戰鬥資訊清楚
- 塔：不同稀有度用外框、發光、底座或材質區分
- 敵人：輪廓差異要明顯，方便快速判斷普通、快速、坦克與 Boss
- UI：按鈕尺寸需支援手機觸控

## 素材資料夾

```text
public/assets/
├─ towers/
│  ├─ common/
│  ├─ rare/
│  ├─ epic/
│  ├─ legendary/
│  └─ mythic/
├─ enemies/
│  ├─ normal/
│  ├─ fast/
│  ├─ tank/
│  ├─ flying/
│  └─ boss/
├─ skills/
├─ ui/
├─ maps/
├─ effects/
├─ achievements/
└─ audio/
   ├─ bgm/
   └─ sfx/
```

## 第一批素材清單

| 類型 | 數量 | 說明 |
|---|---:|---|
| 塔圖示 | 9 | 箭塔、砲塔、毒塔、冰塔、雷塔、火焰塔、聖光塔、時空塔、神話核心塔 |
| 敵人 | 5 | 普通、快速、坦克、飛行、Boss |
| 技能圖示 | 5 | 隕石、冰霜、金幣、時間停止、緊急修復 |
| 地圖背景 | 1-3 | 第一版關卡地圖 |
| 地圖格 | 6-10 | 路徑、草地、石地、障礙、可建造格、基地 |
| 特效 | 8-12 | 爆炸、冰凍、燃燒、毒霧、雷擊、升級、合成、擊殺 |
| UI | 10-20 | 按鈕、面板、血條、技能框、稀有度外框 |
| 成就徽章 | 8-12 | 通關、無損、擊殺、技能、每日挑戰 |
| BGM | 3 | 主選單、戰鬥、Boss |
| SFX | 10-20 | 建塔、升級、攻擊、命中、死亡、勝利、失敗 |

## 命名規則

```text
tower-arrow-common.png
tower-cannon-common.png
tower-ice-rare.png
tower-fire-epic.png
enemy-normal-01.png
enemy-boss-forest-01.png
skill-meteor.png
skill-frost-field.png
fx-explosion-01.png
ui-button-primary.png
achievement-no-damage.png
bgm-battle-01.mp3
sfx-tower-upgrade.wav
```

## MCP / 圖片生成適合項目

適合先生成：

- 塔圖示
- 敵人概念圖
- 技能圖示
- UI 面板風格
- 成就徽章
- 地圖背景概念

較不適合一次完成：

- 完整逐幀動畫 sprite sheet
- 可直接商用的完整 BGM
- 高一致性的全套音效

建議流程：

```text
先定美術風格
  -> 生成塔與敵人概念圖
  -> 挑選方向
  -> 生成正式圖示
  -> 整理命名與 manifest
  -> 實作時再接入 Phaser 載入流程
```
