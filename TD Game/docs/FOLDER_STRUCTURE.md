# 未來實作資料夾規劃

以下是實作階段建議採用的資料夾結構。現階段只作為規劃，不代表已建立程式碼。

```text
TD Game/
├─ README.md
├─ docs/
│  ├─ GAME_DESIGN.md
│  ├─ FOLDER_STRUCTURE.md
│  ├─ TOWER_SYSTEM.md
│  ├─ SYSTEM_FLOW.md
│  └─ ASSET_PLAN.md
│
├─ public/
│  └─ assets/
│     ├─ towers/
│     ├─ enemies/
│     ├─ skills/
│     ├─ ui/
│     ├─ maps/
│     ├─ effects/
│     ├─ achievements/
│     └─ audio/
│        ├─ bgm/
│        └─ sfx/
│
├─ src/
│  ├─ app/
│  │  ├─ App.vue
│  │  └─ main.ts
│  │
│  ├─ game/
│  │  ├─ config/
│  │  ├─ scenes/
│  │  ├─ entities/
│  │  ├─ systems/
│  │  ├─ state/
│  │  └─ utils/
│  │
│  ├─ components/
│  ├─ styles/
│  └─ types/
│
├─ package.json
└─ vite.config.ts
```

## src/game/config

放置遊戲設定資料。

- game.config.ts
- tower.config.ts
- enemy.config.ts
- level.config.ts
- wave.config.ts
- skill.config.ts
- achievement.config.ts
- tech-tree.config.ts
- tower-combo.config.ts
- daily-challenge.config.ts
- difficulty.config.ts

## src/game/scenes

Phaser 場景。

- BootScene.ts
- PreloadScene.ts
- MenuScene.ts
- LevelSelectScene.ts
- GameScene.ts
- ResultScene.ts

## src/game/entities

遊戲物件。

- Enemy.ts
- Tower.ts
- Projectile.ts
- SkillEffect.ts
- MapTile.ts

## src/game/systems

遊戲系統邏輯。

- EnemySystem.ts
- TowerSystem.ts
- WaveSystem.ts
- CombatSystem.ts
- SkillSystem.ts
- AchievementSystem.ts
- SaveSystem.ts
- TechTreeSystem.ts
- TowerComboSystem.ts
- DailyChallengeSystem.ts
- AudioSystem.ts
- DifficultySystem.ts

## src/components

Vue UI 元件。

- GameCanvas.vue
- GameHud.vue
- TowerPanel.vue
- TowerInfoPanel.vue
- SkillBar.vue
- LevelSelect.vue
- AchievementPanel.vue
- TechTreePanel.vue
- DailyChallengePanel.vue
- DifficultySelect.vue
- SettingsMenu.vue
- PauseMenu.vue
