import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game16.json')

export async function fetchGame16Store() {
  return fetchStore()
}

export async function saveGame16Record(record) {
  return saveRecord(record)
}

export async function clearGame16Records() {
  return clearRecords()
}
