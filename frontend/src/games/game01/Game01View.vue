<template>
  <main
    class="game01-view"
    :style="viewStyle"
  >
    <section class="topbar">
      <RouterLink
        to="/"
        class="back-link"
      >
        返回遊戲廳
      </RouterLink>

      <div>
        <p class="eyebrow">Game 01</p>
        <h1>翻牌遊戲</h1>
      </div>
    </section>

    <p
      v-if="ui.error"
      class="error-banner"
    >
      {{ ui.error }}
    </p>

    <section class="layout">
      <div class="sidebar">
        <Game01SettingsPanel
          :board-size="settings.boardSize"
          :board-sizes="BOARD_SIZE_OPTIONS"
          :back-image="settings.backImage"
          :front-images-text="settings.frontImagesText"
          :disabled="ui.loading || ui.resolving"
          @update:board-size="settings.boardSize = $event"
          @update:back-image="settings.backImage = $event"
          @update:front-images-text="settings.frontImagesText = $event"
          @start="startNewGame"
          @reset="resetCurrentGame"
          @clear-records="clearRecords"
        />

        <Game01ScorePanel
          :status-title="statusTitle"
          :last-action="gameState.lastAction"
          :players="gameState.players"
          :current-player-index="gameState.currentPlayerIndex"
          :moves="gameState.moves"
          :matches="gameState.matches"
          :is-completed="gameState.isCompleted"
          :record-count="records.length"
        />
      </div>

      <div class="board-area">
        <Game01Board
          :cards="gameState.cards"
          :board-size="gameState.boardSize"
          :back-image="resolvedBackImage"
          :disabled="ui.loading || ui.resolving || gameState.isCompleted"
          :is-shuffling="ui.isShuffling"
          @flip="handleCardFlip"
        />

        <section class="records-panel">
          <div class="records-heading">
            <div>
              <p class="eyebrow">遊戲紀錄</p>
              <h2>目前保存在瀏覽器本機資料</h2>
            </div>
            <span>{{ records.length }} 筆</span>
          </div>

          <div
            v-if="records.length"
            class="record-list"
          >
            <article
              v-for="record in records"
              :key="record.sessionId"
              class="record-card"
            >
              <strong>{{ record.winner }}</strong>
              <span>{{ record.boardSize }} x {{ record.boardSize }} ｜ {{ record.moves }} 回合 ｜ {{ record.matches }} 組配對</span>
              <p>
                {{ record.finalScores[0]?.name }} {{ record.finalScores[0]?.score ?? 0 }} 分
                /
                {{ record.finalScores[1]?.name }} {{ record.finalScores[1]?.score ?? 0 }} 分
              </p>
            </article>
          </div>

          <p
            v-else
            class="empty-text"
          >
            目前還沒有完成的遊戲紀錄，先開始第一局吧。
          </p>
        </section>
      </div>
    </section>
  </main>
</template>

<script setup>
import { computed, onMounted, reactive } from 'vue'
import { RouterLink } from 'vue-router'
import Game01Board from './components/Game01Board.vue'
import Game01ScorePanel from './components/Game01ScorePanel.vue'
import Game01SettingsPanel from './components/Game01SettingsPanel.vue'
import { appendGame01Record, clearGame01Records, fetchGame01Store, resetGame01, saveGame01State } from './game01Api'
import { recordGameResult } from '@/data/lobbyScore'
import { BOARD_SIZE_OPTIONS, DEFAULT_BACK_IMAGE, createDefaultFrontImages, normalizeImageList } from './game01Defaults'
import { assetUrl } from '@/utils/assetUrl'

const viewStyle = {
  '--game01-bg': `url("${assetUrl('/assets/G01/bg-starry-table.png')}")`
}

const ui = reactive({
  loading: true,
  resolving: false,
  isShuffling: false,
  error: ''
})

const settings = reactive({
  boardSize: 4,
  backImage: '',
  frontImagesText: ''
})

const gameState = reactive(createBlankState())
const records = reactive([])

const resolvedBackImage = computed(() => gameState.backImage || DEFAULT_BACK_IMAGE)
const statusTitle = computed(() => {
  if (ui.loading) {
    return '讀取遊戲中'
  }

  if (gameState.isCompleted) {
    const [first, second] = gameState.players
    if (first.score === second.score) {
      return '本局平手'
    }

    return `${first.score > second.score ? first.name : second.name} 獲勝`
  }

  return `輪到 ${gameState.players[gameState.currentPlayerIndex]?.name ?? '玩家 1'}`
})

onMounted(async () => {
  await loadStore()
})

async function loadStore() {
  ui.loading = true
  ui.error = ''

  try {
    const store = await fetchGame01Store()
    replaceRecords(store.records ?? [])

    if (store.currentGame?.cards?.length) {
      hydrateGame(store.currentGame)
      syncSettingsFromGame()
    } else {
      hydrateGame(createNextGameState())
      await persistCurrentState()
      await triggerShuffleAnimation()
    }
  } catch {
    ui.error = '讀取本機遊戲資料失敗，已先載入預設盤面。'
    hydrateGame(createNextGameState())
  } finally {
    ui.loading = false
  }
}

async function startNewGame() {
  ui.error = ''
  ui.loading = true

  try {
    await resetGame01({
      boardSize: settings.boardSize,
      backImage: settings.backImage.trim(),
      frontImages: buildFrontImages(settings.boardSize)
    })

    hydrateGame(createNextGameState())
    await persistCurrentState()
    await triggerShuffleAnimation()
  } catch {
    ui.error = '開始新局失敗，請稍後再試。'
  } finally {
    ui.loading = false
  }
}

async function resetCurrentGame() {
  await startNewGame()
}

async function clearRecords() {
  try {
    const store = await clearGame01Records()
    replaceRecords(store.records ?? [])
  } catch {
    ui.error = '清空本機紀錄失敗，請稍後再試。'
  }
}

async function handleCardFlip(cardId) {
  if (ui.loading || ui.resolving || ui.isShuffling || gameState.isCompleted) {
    return
  }

  const selectedCard = gameState.cards.find((card) => card.id === cardId)
  if (!selectedCard || selectedCard.isMatched || selectedCard.isRevealed) {
    return
  }

  selectedCard.isRevealed = true

  const revealedCards = gameState.cards.filter((card) => card.isRevealed && !card.isMatched)
  if (revealedCards.length === 1) {
    updateAction(`${currentPlayerName()} 翻開了第一張牌`)
    await persistCurrentState()
    return
  }

  if (revealedCards.length !== 2) {
    return
  }

  ui.resolving = true
  gameState.moves += 1
  const [firstCard, secondCard] = revealedCards

  window.setTimeout(async () => {
    if (firstCard.pairId === secondCard.pairId) {
      firstCard.isMatched = true
      secondCard.isMatched = true
      gameState.players[gameState.currentPlayerIndex].score += 1
      gameState.matches += 1
      updateAction(`${currentPlayerName()} 配對成功並獲得 1 分`)

      if (gameState.matches === (gameState.boardSize * gameState.boardSize) / 2) {
        gameState.isCompleted = true
        gameState.completedAt = new Date().toISOString()
        updateAction(`遊戲結束，${resolveWinner()} 完成本局`)
        await commitFinishedRecord()
      }
    } else {
      firstCard.isRevealed = false
      secondCard.isRevealed = false
      gameState.currentPlayerIndex = gameState.currentPlayerIndex === 0 ? 1 : 0
      updateAction(`未配對成功，換 ${currentPlayerName()} 繼續`)
    }

    await persistCurrentState()
    ui.resolving = false
  }, 850)
}

function createNextGameState() {
  const boardSize = Number(settings.boardSize)
  const frontImages = buildFrontImages(boardSize)
  const selectedBackImage = settings.backImage.trim()

  return {
    sessionId: createSessionId(),
    boardSize,
    backImage: selectedBackImage,
    frontImages,
    cards: createDeck(boardSize, frontImages),
    players: [
      { name: '玩家 1', score: 0 },
      { name: '玩家 2', score: 0 }
    ],
    currentPlayerIndex: 0,
    moves: 0,
    matches: 0,
    isCompleted: false,
    recordCommitted: false,
    lastAction: '新局開始，輪到 玩家 1',
    startedAt: new Date().toISOString(),
    completedAt: null,
    updatedAt: new Date().toISOString()
  }
}

function createDeck(boardSize, frontImages) {
  const pairCount = (boardSize * boardSize) / 2
  const pairs = frontImages.slice(0, pairCount)
  const doubledCards = pairs.flatMap((image, index) => ([
    {
      id: `${index + 1}-a-${createSessionId()}`,
      pairId: `pair-${index + 1}`,
      faceImage: image,
      isRevealed: false,
      isMatched: false
    },
    {
      id: `${index + 1}-b-${createSessionId()}`,
      pairId: `pair-${index + 1}`,
      faceImage: image,
      isRevealed: false,
      isMatched: false
    }
  ]))

  return shuffleCards(doubledCards)
}

function buildFrontImages(boardSize) {
  const requiredPairs = (boardSize * boardSize) / 2
  const customImages = normalizeImageList(settings.frontImagesText)
  const defaultImages = createDefaultFrontImages(requiredPairs)
  const selected = []

  for (let index = 0; index < requiredPairs; index += 1) {
    selected.push(customImages[index] || defaultImages[index])
  }

  return selected
}

function shuffleCards(cards) {
  const deck = [...cards]

  for (let index = deck.length - 1; index > 0; index -= 1) {
    const swapIndex = Math.floor(Math.random() * (index + 1))
    const temp = deck[index]
    deck[index] = deck[swapIndex]
    deck[swapIndex] = temp
  }

  return deck
}

async function persistCurrentState() {
  gameState.updatedAt = new Date().toISOString()

  try {
    const store = await saveGame01State(toPlainGameState())
    replaceRecords(store.records ?? [])
  } catch {
    ui.error = '儲存本機遊戲資料失敗，請確認瀏覽器允許 localStorage。'
  }
}

async function commitFinishedRecord() {
  if (gameState.recordCommitted) {
    return
  }

  const [lobbyFirst, lobbySecond] = gameState.players
  recordGameResult(
    '/game01',
    lobbyFirst.score === lobbySecond.score
      ? 'draw'
      : lobbyFirst.score > lobbySecond.score
        ? 'p1'
        : 'p2'
  )

  try {
    const store = await appendGame01Record({
      sessionId: gameState.sessionId,
      boardSize: gameState.boardSize,
      moves: gameState.moves,
      matches: gameState.matches,
      winner: resolveWinner(),
      finalScores: gameState.players.map((player) => ({ ...player })),
      startedAt: gameState.startedAt,
      completedAt: gameState.completedAt ?? new Date().toISOString()
    })

    gameState.recordCommitted = true
    replaceRecords(store.records ?? [])
  } catch {
    ui.error = '寫入本機遊戲紀錄失敗，請稍後再試。'
  }
}

async function triggerShuffleAnimation() {
  ui.isShuffling = true
  await wait(950)
  ui.isShuffling = false
}

function hydrateGame(state) {
  Object.assign(gameState, createBlankState(), state)
}

function syncSettingsFromGame() {
  settings.boardSize = gameState.boardSize || 4
  settings.backImage = gameState.backImage || ''
  settings.frontImagesText = (gameState.frontImages ?? []).join('\n')
}

function replaceRecords(nextRecords) {
  records.splice(0, records.length, ...nextRecords)
}

function currentPlayerName() {
  return gameState.players[gameState.currentPlayerIndex]?.name ?? '玩家 1'
}

function resolveWinner() {
  const [first, second] = gameState.players
  if (first.score === second.score) {
    return '平手'
  }

  return first.score > second.score ? first.name : second.name
}

function updateAction(message) {
  gameState.lastAction = message
  gameState.updatedAt = new Date().toISOString()
}

function toPlainGameState() {
  return {
    sessionId: gameState.sessionId,
    boardSize: gameState.boardSize,
    backImage: gameState.backImage,
    frontImages: [...gameState.frontImages],
    cards: gameState.cards.map((card) => ({ ...card })),
    players: gameState.players.map((player) => ({ ...player })),
    currentPlayerIndex: gameState.currentPlayerIndex,
    moves: gameState.moves,
    matches: gameState.matches,
    isCompleted: gameState.isCompleted,
    recordCommitted: gameState.recordCommitted,
    lastAction: gameState.lastAction,
    startedAt: gameState.startedAt,
    completedAt: gameState.completedAt,
    updatedAt: gameState.updatedAt
  }
}

function createBlankState() {
  return {
    sessionId: '',
    boardSize: 4,
    backImage: '',
    frontImages: [],
    cards: [],
    players: [
      { name: '玩家 1', score: 0 },
      { name: '玩家 2', score: 0 }
    ],
    currentPlayerIndex: 0,
    moves: 0,
    matches: 0,
    isCompleted: false,
    recordCommitted: false,
    lastAction: '準備開始新局',
    startedAt: '',
    completedAt: null,
    updatedAt: ''
  }
}

function createSessionId() {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID().replaceAll('-', '')
  }

  return `${Date.now()}${Math.random().toString(16).slice(2)}`
}

function wait(duration) {
  return new Promise((resolve) => {
    window.setTimeout(resolve, duration)
  })
}
</script>

<style scoped>
.game01-view {
  position: relative;
  min-height: 100vh;
  width: min(1360px, calc(100% - 2rem));
  margin: 0 auto;
  display: grid;
  gap: 1.5rem;
  padding: 1.5rem 0 2.5rem;
  color: #e9e6ff;
  font-family: 'Segoe UI', system-ui, sans-serif;
}

.game01-view::before {
  content: '';
  position: fixed;
  inset: 0;
  z-index: -1;
  background: #1b1630 var(--game01-bg) center top / cover no-repeat;
  image-rendering: pixelated;
}

.topbar {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 1rem;
  align-items: center;
}

.back-link {
  display: inline-flex;
  align-items: center;
  padding: 0.8rem 1rem;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(179, 155, 255, 0.3);
  color: #c7bcf0;
  font-weight: 700;
  text-decoration: none;
  transition: 0.2s;
}

.back-link:hover {
  background: rgba(179, 155, 255, 0.14);
  color: #fff;
}

.eyebrow {
  color: #e7c66b;
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.16em;
  text-transform: uppercase;
}

h1 {
  margin-top: 0.3rem;
  font-size: clamp(2.2rem, 4vw, 3.2rem);
  background: linear-gradient(90deg, #e7c66b, #b39bff);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}

.layout {
  display: grid;
  grid-template-columns: minmax(320px, 420px) minmax(0, 1fr);
  gap: 1.4rem;
  align-items: start;
}

.sidebar,
.board-area {
  display: grid;
  gap: 1rem;
}

.records-panel {
  padding: 1.4rem;
  border-radius: 24px;
  background: rgba(18, 16, 34, 0.62);
  border: 1px solid rgba(150, 130, 220, 0.18);
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.45);
}

.records-heading {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 1rem;
  align-items: end;
}

.records-heading h2 {
  margin-top: 0.35rem;
  color: #e9e6ff;
}

.records-heading span {
  color: #e7c66b;
  font-weight: 700;
}

.record-list {
  margin-top: 1rem;
  display: grid;
  gap: 0.8rem;
}

.record-card {
  display: grid;
  gap: 0.2rem;
  padding: 1rem 1.1rem;
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(150, 130, 220, 0.12);
}

.record-card strong {
  color: #e7c66b;
}

.record-card span,
.record-card p {
  color: #b3acd6;
}

.empty-text {
  color: #7d76a8;
}

.error-banner {
  padding: 1rem 1.2rem;
  border-radius: 18px;
  background: rgba(220, 90, 90, 0.14);
  border: 1px solid rgba(255, 120, 120, 0.3);
  color: #ffc9c9;
}

.empty-text {
  margin-top: 1rem;
}

@media (max-width: 1120px) {
  .layout {
    grid-template-columns: 1fr;
  }
}
</style>
