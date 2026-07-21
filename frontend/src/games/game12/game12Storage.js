import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game12.json')

export async function fetchGame12Store() {
  return fetchStore()
}

export async function saveGame12Record(record) {
  return saveRecord(record)
}

export async function clearGame12Records() {
  return clearRecords()
}
