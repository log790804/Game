import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game08.json')

export async function fetchGame08Store() {
  return fetchStore()
}

export async function saveGame08Record(record) {
  return saveRecord(record)
}

export async function clearGame08Records() {
  return clearRecords()
}
