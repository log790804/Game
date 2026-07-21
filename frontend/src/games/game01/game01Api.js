import { fetchGameStore, writeGameStore } from '../../services/gameStoreRepository'

const STORAGE_KEY = 'game01.json'

function normalizeStore(store) {
  return {
    currentGame: store.currentGame ?? null,
    records: Array.isArray(store.records) ? store.records : [],
    updatedAt: store.updatedAt ?? new Date().toISOString()
  }
}

async function readStore() {
  const store = await fetchGameStore(STORAGE_KEY, { keepCurrentGame: true, recordLimit: 30 })
  return normalizeStore(store)
}

async function writeStore(store) {
  return writeGameStore(STORAGE_KEY, normalizeStore(store), { keepCurrentGame: true, recordLimit: 30 })
}

export async function fetchGame01Store() {
  return readStore()
}

export async function saveGame01State(state) {
  const store = await readStore()
  store.currentGame = {
    ...state,
    updatedAt: new Date().toISOString()
  }

  return writeStore(store)
}

export async function resetGame01(requestBody) {
  const store = await readStore()
  store.currentGame = {
    sessionId: '',
    boardSize: requestBody.boardSize ?? 4,
    backImage: requestBody.backImage ?? '',
    frontImages: requestBody.frontImages ?? [],
    cards: [],
    players: [
      { name: '玩家 1', score: 0 },
      { name: '玩家 2', score: 0 }
    ],
    currentPlayerIndex: 0,
    moves: 0,
    matches: 0,
    isCompleted: false,
    recordCommitted: false,
    lastAction: '已重置遊戲，等待開始新局',
    startedAt: new Date().toISOString(),
    completedAt: null,
    updatedAt: new Date().toISOString()
  }

  return writeStore(store)
}

export async function appendGame01Record(record) {
  const store = await readStore()
  const exists = store.records.some((item) => item.sessionId === record.sessionId)

  if (!exists) {
    store.records.unshift(record)
  }

  return writeStore(store)
}

export async function clearGame01Records() {
  const store = await readStore()
  store.records = []
  return writeStore(store)
}
