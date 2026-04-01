const STORAGE_KEY = 'game02.json'

function createDefaultStore() {
  return {
    records: [],
    updatedAt: new Date().toISOString()
  }
}

function getStorage() {
  if (typeof window === 'undefined' || !window.localStorage) {
    throw new Error('localStorage is unavailable')
  }

  return window.localStorage
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
  const normalized = {
    records: Array.isArray(store.records) ? store.records : [],
    updatedAt: new Date().toISOString()
  }

  getStorage().setItem(STORAGE_KEY, JSON.stringify(normalized))
  return normalized
}

export async function fetchGame02Store() {
  return readStore()
}

export async function saveGame02Record(record) {
  const store = readStore()
  store.records.unshift(record)
  store.records = store.records.slice(0, 10)
  return writeStore(store)
}

export async function clearGame02Records() {
  return writeStore(createDefaultStore())
}
