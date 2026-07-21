import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game07.json')

export async function fetchGame07Store() {
  return fetchStore()
}

export async function saveGame07Record(record) {
  return saveRecord(record)
}

export async function clearGame07Records() {
  return clearRecords()
}
