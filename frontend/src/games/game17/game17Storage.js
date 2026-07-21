import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game17.json')

export async function fetchGame17Store() {
  return fetchStore()
}

export async function saveGame17Record(record) {
  return saveRecord(record)
}

export async function clearGame17Records() {
  return clearRecords()
}
