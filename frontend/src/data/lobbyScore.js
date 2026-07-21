// 大廳賽事計分：一大輪內每款遊戲只計一次，累計兩隊勝場，
// 直到點擊重置才歸零並解鎖所有遊戲。

import { fetchGameStore, writeGameStore } from '@/services/gameStoreRepository'

const STORAGE_KEY = 'lobbyScore.json'

// 目前可玩、且納入一大輪計分的遊戲路由
export const PLAYABLE_ROUTES = [
  '/game01',
  '/game02',
  '/game03',
  '/game04',
  '/game05',
  '/game06',
  '/game07',
  '/game08',
  '/game09',
  '/game10',
  '/game11',
  '/game12',
  '/game13',
  '/game14',
  '/game15',
  '/game16',
  '/game17',
  '/game18',
  '/game19',
  '/game20'
]

const VALID_TEAMS = ['p1', 'p2', 'draw']

function createDefaultStore() {
  return {
    results: {},
    updatedAt: new Date().toISOString()
  }
}

function normalizeLobbyStore(store) {
  const results = {}
  if (store?.results && typeof store.results === 'object') {
    for (const route of PLAYABLE_ROUTES) {
      const value = store.results[route]
      if (VALID_TEAMS.includes(value)) results[route] = value
    }
  }

  return {
    results,
    updatedAt: store?.updatedAt ?? new Date().toISOString()
  }
}

const storeOptions = {
  normalize: normalizeLobbyStore
}

function getStorage() {
  if (typeof window === 'undefined' || !window.localStorage) {
    throw new Error('localStorage is unavailable')
  }
  return window.localStorage
}

function readLocalStore() {
  let storage
  try {
    storage = getStorage()
  } catch {
    return createDefaultStore()
  }

  const raw = storage.getItem(STORAGE_KEY)
  if (!raw) {
    const store = createDefaultStore()
    storage.setItem(STORAGE_KEY, JSON.stringify(store))
    return store
  }

  try {
    return normalizeLobbyStore(JSON.parse(raw))
  } catch {
    const store = createDefaultStore()
    storage.setItem(STORAGE_KEY, JSON.stringify(store))
    return store
  }
}

function writeLocalStore(store) {
  const normalized = normalizeLobbyStore({
    ...store,
    updatedAt: new Date().toISOString()
  })
  try {
    getStorage().setItem(STORAGE_KEY, JSON.stringify(normalized))
  } catch {
    /* ignore storage errors */
  }
  return normalized
}

export async function fetchLobbyScore() {
  try {
    return await fetchGameStore(STORAGE_KEY, storeOptions)
  } catch (error) {
    console.warn('Lobby score remote read failed, fallback to localStorage.', error)
    return readLocalStore()
  }
}

// 紀錄一款遊戲的勝方；一大輪內同一款只會記第一次，避免重玩覆寫。
export async function recordGameResult(route, team) {
  if (!PLAYABLE_ROUTES.includes(route)) return readLocalStore()
  if (!VALID_TEAMS.includes(team)) return readLocalStore()
  const store = await fetchLobbyScore()
  if (store.results[route]) return store
  store.results[route] = team
  try {
    return await writeGameStore(STORAGE_KEY, store, storeOptions)
  } catch (error) {
    console.warn('Lobby score remote write failed, saved to localStorage only.', error)
    return writeLocalStore(store)
  }
}

export async function resetLobbyScore() {
  try {
    return await writeGameStore(STORAGE_KEY, createDefaultStore(), storeOptions)
  } catch (error) {
    console.warn('Lobby score remote reset failed, saved to localStorage only.', error)
    return writeLocalStore(createDefaultStore())
  }
}

// 從 store 推導出兩隊勝場、完成進度與最終勝方。
export function summarizeLobby(store) {
  const results = (store && store.results) || {}
  let p1 = 0
  let p2 = 0
  let draws = 0
  for (const route of PLAYABLE_ROUTES) {
    const v = results[route]
    if (v === 'p1') p1 += 1
    else if (v === 'p2') p2 += 1
    else if (v === 'draw') draws += 1
  }
  const played = p1 + p2 + draws
  const total = PLAYABLE_ROUTES.length
  const finished = played >= total
  let winner = null
  if (finished) winner = p1 > p2 ? 'p1' : p2 > p1 ? 'p2' : 'draw'
  return { results, p1, p2, draws, played, total, finished, winner }
}
