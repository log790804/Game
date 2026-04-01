# Render Deployment

## Recommended Topology

- `frontend`: Render Static Site
- `game01` data: stored in browser `localStorage`

## What Changed

- `game01` no longer depends on backend APIs.
- Game state and records are stored in the browser with the key `game01.json`.
- Render only needs to host the frontend static site now.

## Files Used For Render

- `render.yaml`

## Deploy Flow

1. Push this repository to GitHub.
2. In Render, create a new Blueprint and point it to this repo.
3. Render will read `render.yaml` and create one static site:
   - `game-factory-web`
4. After deploy, open the site and test `/game01`.

## Notes

- Refreshing `/game01` will work because the static site includes a rewrite rule to `/index.html`.
- Game progress and records are stored per browser and per device.
- Clearing browser storage will remove the saved game state and records.
- If the user opens the game in another browser or device, the records will not sync automatically.
