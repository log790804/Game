const palettes = [
  ['#cfe7d7', '#f7d7c4'],
  ['#f6dfb3', '#f3c4c9'],
  ['#c8e1f3', '#d9d0f8'],
  ['#c9eadf', '#f8efc9']
]

export const gameLobbyCards = Array.from({ length: 16 }, (_, index) => {
  const palette = palettes[index % palettes.length]
  const cardNumber = String(index + 1).padStart(2, '0')
  const isGame01 = index === 0
  const isGame02 = index === 1

  return {
    id: `game-slot-${cardNumber}`,
    title: isGame01 ? '翻牌遊戲' : isGame02 ? '雙人射擊' : `遊戲入口 ${cardNumber}`,
    subtitle: isGame01 || isGame02 ? '已開放遊玩' : index < 4 ? '優先規劃區' : '預留入口',
    description: isGame01
      ? '雙人輪流翻牌配對計分，支援 4x4 到 8x8、圖片客製與遊戲紀錄保存。'
      : isGame02
        ? '雙人同場縱向射擊，玩家 1 與玩家 2 可同步作戰，結算時比較分數高低。'
        : '之後可切換為對應的遊戲 component，這裡先保留版位與封面位置。',
    actionLabel: isGame01 || isGame02 ? '進入遊戲' : '保留入口',
    route: isGame01 ? '/game01' : isGame02 ? '/game02' : '',
    imageStyle: {
      background: `linear-gradient(135deg, ${palette[0]}, ${palette[1]})`
    }
  }
})
