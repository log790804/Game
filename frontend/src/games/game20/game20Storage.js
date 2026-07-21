import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game20.json')

export async function fetchGame20Store() {
  return fetchStore()
}

export async function saveGame20Record(record) {
  return saveRecord(record)
}

export async function clearGame20Records() {
  return clearRecords()
}
