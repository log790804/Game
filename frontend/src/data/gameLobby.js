const palettes = [
  ['#cfe7d7', '#f7d7c4'],
  ['#f6dfb3', '#f3c4c9'],
  ['#c8e1f3', '#d9d0f8'],
  ['#c9eadf', '#f8efc9']
]

const games = {
  0: {
    title: '翻牌遊戲',
    subtitle: '已開放遊玩',
    description: '雙人輪流翻牌配對計分，支援 4x4 到 8x8、圖片客製與遊戲紀錄保存。',
    route: '/game01'
  },
  1: {
    title: '雙人射擊',
    subtitle: '已開放遊玩',
    description: '雙人同場縱向射擊，玩家 1 與玩家 2 可同步作戰，結算時比較分數高低。',
    route: '/game02'
  },
  2: {
    title: '高樓疊疊樂',
    subtitle: '已開放遊玩',
    description: '雙人輪流疊高樓，最多失誤五次；樓層越高，風速與傾斜造成的搖晃越明顯。',
    route: '/game03'
  },
  3: {
    title: '雙人分割賽車',
    subtitle: '已開放遊玩',
    description: '左右分割的偽 3D 第三視角賽車，賽道包含彎道、障礙物與車輛碰撞，先完成一圈者獲勝。',
    route: '/game04'
  }
}

export const gameLobbyCards = Array.from({ length: 16 }, (_, index) => {
  const palette = palettes[index % palettes.length]
  const cardNumber = String(index + 1).padStart(2, '0')
  const game = games[index]

  return {
    id: `game-slot-${cardNumber}`,
    title: game?.title ?? `遊戲入口 ${cardNumber}`,
    subtitle: game?.subtitle ?? (index < 4 ? '優先規劃區' : '預留入口'),
    description: game?.description ?? '之後可切換為對應的遊戲 component，這裡先保留版位與封面位置。',
    actionLabel: game ? '進入遊戲' : '保留入口',
    route: game?.route ?? '',
    imageStyle: {
      background: `linear-gradient(135deg, ${palette[0]}, ${palette[1]})`
    }
  }
})
