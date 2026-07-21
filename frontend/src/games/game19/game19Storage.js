import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game19.json')

export async function fetchGame19Store() {
  return fetchStore()
}

export async function saveGame19Record(record) {
  return saveRecord(record)
}

export async function clearGame19Records() {
  return clearRecords()
}
