import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game10.json')

export async function fetchGame10Store() {
  return fetchStore()
}

export async function saveGame10Record(record) {
  return saveRecord(record)
}

export async function clearGame10Records() {
  return clearRecords()
}
