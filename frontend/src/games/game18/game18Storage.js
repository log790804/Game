import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game18.json')

export async function fetchGame18Store() {
  return fetchStore()
}

export async function saveGame18Record(record) {
  return saveRecord(record)
}

export async function clearGame18Records() {
  return clearRecords()
}
