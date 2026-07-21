import { initializeApp } from 'firebase/app'
import { getFirestore } from 'firebase/firestore'

const firebaseConfig = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID,
  storageBucket: import.meta.env.VITE_FIREBASE_STORAGE_BUCKET,
  messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID,
  appId: import.meta.env.VITE_FIREBASE_APP_ID
}

let firebaseApp = null
let firestore = null

export function isFirebaseConfigured() {
  return import.meta.env.VITE_FIREBASE_ENABLED === 'true'
    && Boolean(firebaseConfig.apiKey)
    && Boolean(firebaseConfig.authDomain)
    && Boolean(firebaseConfig.projectId)
    && Boolean(firebaseConfig.appId)
}

export function getGameFirestore() {
  if (!isFirebaseConfigured()) return null

  if (!firebaseApp) {
    firebaseApp = initializeApp(firebaseConfig)
  }

  if (!firestore) {
    firestore = getFirestore(firebaseApp)
  }

  return firestore
}
