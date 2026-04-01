# Render Deployment

## Recommended Topology

- `frontend`: Render Static Site
- `backend`: Render Web Service using Docker
- `game01.json`: stored on a Render Persistent Disk

## Why This Setup

- The Vue app is a static build, so Render Static Site is the simplest fit.
- The ASP.NET Core API already has a Dockerfile, so Render Web Service can deploy it directly.
- `game01.json` must survive restarts and redeploys, so the backend needs a persistent disk.

## Files Added For Render

- `render.yaml`
- `frontend/.env.production.example`

## Important Runtime Variables

### Backend

- `PORT=10000`
- `ASPNETCORE_ENVIRONMENT=Production`
- `GAME_DATA_ROOT=/app/storage`

`GAME_DATA_ROOT` is important because `game01.json` must be written under the mounted disk path.

## Persistent Data

The backend now supports a configurable storage root.

On Render, `game01.json` will be stored at:

```text
/app/storage/game01/game01.json
```

## Deploy Flow

1. Push this repository to GitHub.
2. In Render, create a new Blueprint and point it to this repo.
3. Render will read `render.yaml` and create:
   - `game-factory-api`
   - `game-factory-web`
4. Confirm the backend disk is attached at `/app/storage`.
5. After the first deploy, open the frontend and confirm it can call:

```text
https://game-factory-api.onrender.com/api/health
```

## Notes

- The backend uses a persistent disk, so it should stay on a paid plan that supports disks.
- If you later rename the backend service in Render, update `VITE_API_BASE_URL` to match the new public URL.
- The frontend static site includes a rewrite rule so Vue Router routes such as `/game01` can be refreshed directly.
