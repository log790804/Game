import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game02.json')

export async function fetchGame02Store() {
  return fetchStore()
}

export async function saveGame02Record(record) {
  return saveRecord(record)
}

export async function clearGame02Records() {
  return clearRecords()
}
