import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game04.json')

export async function fetchGame04Store() {
  return fetchStore()
}

export async function saveGame04Record(record) {
  return saveRecord(record)
}

export async function clearGame04Records() {
  return clearRecords()
}
