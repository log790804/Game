import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game09.json')

export async function fetchGame09Store() {
  return fetchStore()
}

export async function saveGame09Record(record) {
  return saveRecord(record)
}

export async function clearGame09Records() {
  return clearRecords()
}
