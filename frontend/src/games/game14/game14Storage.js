import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game14.json')

export async function fetchGame14Store() {
  return fetchStore()
}

export async function saveGame14Record(record) {
  return saveRecord(record)
}

export async function clearGame14Records() {
  return clearRecords()
}
