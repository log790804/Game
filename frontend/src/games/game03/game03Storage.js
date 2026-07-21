import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game03.json')

export async function fetchGame03Store() {
  return fetchStore()
}

export async function saveGame03Record(record) {
  return saveRecord(record)
}

export async function clearGame03Records() {
  return clearRecords()
}
