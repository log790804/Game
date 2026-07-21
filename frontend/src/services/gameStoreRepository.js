import { doc, getDoc, setDoc } from 'firebase/firestore'
import { getGameFirestore, isFirebaseConfigured } from './firebase'

const COLLECTION_NAME = 'gameStores'

function createBaseStore() {
  return {
    records: [],
    updatedAt: new Date().toISOString()
  }
}

function normalizeStore(store, options = {}) {
  const recordLimit = options.recordLimit ?? 10
  const normalized = {
    records: Array.isArray(store?.records) ? store.records.slice(0, recordLimit) : [],
    updatedAt: store?.updatedAt ?? new Date().toISOString()
  }

  if (options.keepCurrentGame) {
    normalized.currentGame = store?.currentGame ?? null
  }

  return normalized
}

function getStorage() {
  if (typeof window === 'undefined' || !window.localStorage) {
    throw new Error('localStorage is unavailable')
  }

  return window.localStorage
}

function getDocumentId(storageKey) {
  return storageKey.replaceAll('/', '_')
}

function readLocalStore(storageKey, options) {
  const storage = getStorage()
  const raw = storage.getItem(storageKey)

  if (!raw) {
    const store = normalizeStore(createBaseStore(), options)
    storage.setItem(storageKey, JSON.stringify(store))
    return store
  }

  try {
    return normalizeStore(JSON.parse(raw), options)
  } catch {
    const store = normalizeStore(createBaseStore(), options)
    storage.setItem(storageKey, JSON.stringify(store))
    return store
  }
}

function writeLocalStore(storageKey, store, options) {
  const normalized = normalizeStore(
    {
      ...store,
      updatedAt: new Date().toISOString()
    },
    options
  )

  getStorage().setItem(storageKey, JSON.stringify(normalized))
  return normalized
}

async function readRemoteStore(storageKey, options) {
  const database = getGameFirestore()
  if (!database) return null

  const snapshot = await getDoc(doc(database, COLLECTION_NAME, getDocumentId(storageKey)))
  if (!snapshot.exists()) return null

  return normalizeStore(snapshot.data(), options)
}

async function writeRemoteStore(storageKey, store, options) {
  const database = getGameFirestore()
  if (!database) return null

  const normalized = normalizeStore(
    {
      ...store,
      updatedAt: new Date().toISOString()
    },
    options
  )

  await setDoc(doc(database, COLLECTION_NAME, getDocumentId(storageKey)), normalized)
  return normalized
}

export async function fetchGameStore(storageKey, options = {}) {
  if (isFirebaseConfigured()) {
    try {
      const remoteStore = await readRemoteStore(storageKey, options)
      if (remoteStore) {
        writeLocalStore(storageKey, remoteStore, options)
        return remoteStore
      }
    } catch (error) {
      console.warn('Firebase read failed, fallback to localStorage.', error)
    }
  }

  const localStore = readLocalStore(storageKey, options)

  if (isFirebaseConfigured()) {
    try {
      await writeRemoteStore(storageKey, localStore, options)
    } catch (error) {
      console.warn('Firebase seed failed, keeping localStorage data.', error)
    }
  }

  return localStore
}

export async function writeGameStore(storageKey, store, options = {}) {
  const localStore = writeLocalStore(storageKey, store, options)

  if (isFirebaseConfigured()) {
    try {
      return await writeRemoteStore(storageKey, localStore, options)
    } catch (error) {
      console.warn('Firebase write failed, saved to localStorage only.', error)
    }
  }

  return localStore
}

export function createRecordsStoreApi(storageKey) {
  return {
    async fetchStore() {
      return fetchGameStore(storageKey)
    },
    async saveRecord(record) {
      const store = await fetchGameStore(storageKey)
      store.records.unshift(record)
      return writeGameStore(storageKey, store)
    },
    async clearRecords() {
      return writeGameStore(storageKey, createBaseStore())
    }
  }
}
