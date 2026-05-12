Original prompt: 我有一個game02的構想 是射擊遊戲,如<STRIKERS1945> 玩家1,與玩家2可同時遊戲 ,最後看誰分數比較高

- 2026-04-01: Started implementing `game02` as a two-player vertical shooter under `frontend/src/games/game02`.
- TODO: Hook homepage card 02 to `/game02`.
- TODO: Provide canvas game loop, deterministic `advanceTime(ms)`, and `render_game_to_text`.
- TODO: If local Node tooling is available, run a browser validation loop after implementation.
- 2026-04-01: Added game02 route, lobby entry, canvas shooter MVP, localStorage record store, and testing hooks.
- 2026-04-01: Upgraded game02 with responsive canvas scaling, wave formations, elite enemies, stronger spread shots, and Strikers-style pacing.
- 2026-04-01: Tuned game02 difficulty to scale gradually with player power/time and removed center-lane movement restrictions for both players.
- 2026-04-07: Added game03 high-rise stacking game with two-player turns, 5 misses per player, wind/lean sway, route, lobby entry, localStorage records, and testing hooks.
- 2026-04-07: Reworked game03 to Tower Bloxx-style crane release gameplay: swinging crane, falling blocks, landing judgment, wind drift, tower sway, and two-player turns.
- 2026-04-07: Updated game03 to simultaneous two-player crane play and improved UI blocks into house-like modules with roofs/windows/city background.
- 2026-04-07: Removed roof shape from game03 building blocks and increased each floor height for clearer apartment-like stacking.
- 2026-04-07: Generated 10 square PNG building floor assets and wired game03 to draw tower blocks from them.
- 2026-04-07: Reworked game03 display into left/right split view with per-player camera tracking so each crane stays a fixed distance above that player's tower top.
- 2026-04-07: Tuned game03 camera tracking to keep the foundation fixed at the start and only follow once the crane nears the top of the visible play area.
- 2026-04-07: Translated game03 visible UI and canvas text from English back to Traditional Chinese.
- 2026-05-12: Added game04 as a split-screen two-player racing MVP with keyboard controls, local records, and lobby/router entry.
- 2026-05-12: Rebuilt game04 into a split-screen pseudo-3D racer with curved road segments, obstacles, player-vs-player collisions, third-person camera, and one-lap finish logic.
