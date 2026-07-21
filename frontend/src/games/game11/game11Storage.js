import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game11.json')

export async function fetchGame11Store() {
  return fetchStore()
}

export async function saveGame11Record(record) {
  return saveRecord(record)
}

export async function clearGame11Records() {
  return clearRecords()
}
