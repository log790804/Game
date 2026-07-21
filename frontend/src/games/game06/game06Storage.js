import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game06.json')

export async function fetchGame06Store() {
  return fetchStore()
}

export async function saveGame06Record(record) {
  return saveRecord(record)
}

export async function clearGame06Records() {
  return clearRecords()
}
