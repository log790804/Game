const STORAGE_KEY = 'game01.json'

function getStorage() {
  if (typeof window === 'undefined' || !window.localStorage) {
    throw new Error('localStorage is unavailable')
  }

  return window.localStorage
}

function createDefaultStore() {
  return {
    currentGame: null,
    records: [],
    updatedAt: new Date().toISOString()
  }
}

function readStore() {
  const storage = getStorage()
  const raw = storage.getItem(STORAGE_KEY)

  if (!raw) {
    const initialStore = createDefaultStore()
    storage.setItem(STORAGE_KEY, JSON.stringify(initialStore))
    return initialStore
  }

  try {
    const parsed = JSON.parse(raw)
    return {
      currentGame: parsed.currentGame ?? null,
      records: Array.isArray(parsed.records) ? parsed.records : [],
      updatedAt: parsed.updatedAt ?? new Date().toISOString()
    }
  } catch {
    const initialStore = createDefaultStore()
    storage.setItem(STORAGE_KEY, JSON.stringify(initialStore))
    return initialStore
  }
}

function writeStore(store) {
  const storage = getStorage()
  const normalizedStore = {
    currentGame: store.currentGame ?? null,
    records: Array.isArray(store.records) ? store.records : [],
    updatedAt: new Date().toISOString()
  }

  storage.setItem(STORAGE_KEY, JSON.stringify(normalizedStore))
  return normalizedStore
}

export async function fetchGame01Store() {
  return readStore()
}

export async function saveGame01State(state) {
  const store = readStore()
  store.currentGame = {
    ...state,
    updatedAt: new Date().toISOString()
  }

  return writeStore(store)
}

export async function resetGame01(requestBody) {
  const store = readStore()
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
  const store = readStore()
  const exists = store.records.some((item) => item.sessionId === record.sessionId)

  if (!exists) {
    store.records.unshift(record)
  }

  return writeStore(store)
}

export async function clearGame01Records() {
  const store = readStore()
  store.records = []
  return writeStore(store)
}
