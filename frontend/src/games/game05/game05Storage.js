import { createRecordsStoreApi } from '../../services/gameStoreRepository'

const { fetchStore, saveRecord, clearRecords } = createRecordsStoreApi('game05.json')

export async function fetchGame05Store() {
  return fetchStore()
}

export async function saveGame05Record(record) {
  return saveRecord(record)
}

export async function clearGame05Records() {
  return clearRecords()
}
