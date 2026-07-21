import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game13.json')

export async function fetchGame13Store() {
  return fetchStore()
}

export async function saveGame13Record(record) {
  return saveRecord(record)
}

export async function clearGame13Records() {
  return clearRecords()
}
