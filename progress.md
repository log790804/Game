Original prompt: 我有一個game02的構想 是射擊遊戲,如<STRIKERS1945> 玩家1,與玩家2可同時遊戲 ,最後看誰分數比較高

- 2026-04-01: Started implementing `game02` as a two-player vertical shooter under `frontend/src/games/game02`.
- TODO: Hook homepage card 02 to `/game02`.
- TODO: Provide canvas game loop, deterministic `advanceTime(ms)`, and `render_game_to_text`.
- TODO: If local Node tooling is available, run a browser validation loop after implementation.
- 2026-04-01: Added game02 route, lobby entry, canvas shooter MVP, localStorage record store, and testing hooks.
- 2026-04-01: Upgraded game02 with responsive canvas scaling, wave formations, elite enemies, stronger spread shots, and Strikers-style pacing.
- 2026-04-01: Tuned game02 difficulty to scale gradually with player power/time and removed center-lane movement restrictions for both players.
