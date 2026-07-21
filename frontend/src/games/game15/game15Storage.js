import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game15.json')

export async function fetchGame15Store() {
  return fetchStore()
}

export async function saveGame15Record(record) {
  return saveRecord(record)
}

export async function clearGame15Records() {
  return clearRecords()
}
