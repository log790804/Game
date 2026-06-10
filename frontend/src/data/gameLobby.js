const accents = [
  '#22d3ee',
  '#a78bfa',
  '#34d399',
  '#f472b6',
  '#fbbf24',
  '#60a5fa',
  '#f87171',
  '#2dd4bf'
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
    title: '毛毛蟲卡丁車',
    subtitle: '已開放遊玩',
    description: '俯視繞圈卡丁車，輾過道具箱隨機獲得加速蘑菇、香蕉皮、追蹤龜殼、無敵星與地雷互相陷害，先跑完 3 圈者獲勝。',
    route: '/game04'
  },
  4: {
    title: '雙人貪食蛇對決',
    subtitle: '已開放遊玩',
    description: '霓虹格狀競技場，兩條蛇搶食成長並閃避彼此，場地隨時間收縮，三局兩勝且穿插加速、穿牆等道具。',
    route: '/game05'
  },
  5: {
    title: '節奏大師對戰',
    subtitle: '已開放遊玩',
    description: '雙軌落下音符配合 Web Audio 節拍，Perfect／Good／Miss 判定累積 Combo 倍率，限時比拚準度總分。',
    route: '/game06'
  },
  6: {
    title: '空氣曲棍球',
    subtitle: '已開放遊玩',
    description: '俯視球桌物理對抗，球桿移動越快擊出的圓盤越強，圓盤每回合加速，先進 7 球者獲勝。',
    route: '/game07'
  },
  7: {
    title: '接水果大作戰',
    subtitle: '已開放遊玩',
    description: '左右分割各自接住掉落的水果累積連擊倍率，閃避炸彈並善用磁鐵與冰凍道具，60 秒比總分。',
    route: '/game08'
  },
  8: {
    title: '打地鼠對戰',
    subtitle: '已開放遊玩',
    description: '各自半場 3×3 洞口，地鼠探頭立刻按鍵敲擊，金鼠加倍、炸彈鼠扣分，限時內比擊中分數。',
    route: '/game09'
  },
  9: {
    title: '俄羅斯方塊對戰',
    subtitle: '已開放遊玩',
    description: '雙人同場堆疊，一次消除多行會送垃圾行壓制對手，清行可抵銷垃圾，撐到對手堆爆者獲勝。',
    route: '/game10'
  },
  10: {
    title: '坦克大戰',
    subtitle: '已開放遊玩',
    description: '俯視雙坦克互射，磚牆可打穿、鋼牆無法破壞，撿加速／散彈／護盾道具，先擊毀對手 5 次者勝。',
    route: '/game11'
  },
  11: {
    title: '砲彈對決',
    subtitle: '已開放遊玩',
    description: '輪流調整角度與力道隔地形互轟，注意風速與可破壞地形，打中對方扣血，三局兩勝。',
    route: '/game12'
  },
  12: {
    title: '五子棋',
    subtitle: '已開放遊玩',
    description: '經典棋盤策略，輪流落子在橫直斜任一方向連成五子者勝，附最後一手高亮與一次悔棋。',
    route: '/game13'
  },
  13: {
    title: '泡泡龍對戰',
    subtitle: '已開放遊玩',
    description: '瞄準發射泡泡三消，連帶消除懸空泡泡；大量消除壓一排給對手，泡泡頂到底線者落敗。',
    route: '/game14'
  },
  14: {
    title: '大魚吃小魚',
    subtitle: '已開放遊玩',
    description: '同場游動吞食較小的魚成長並躲避大魚，體型夠大可吞對手，衝刺與無敵星助攻，60 秒比分數。',
    route: '/game15'
  },
  15: {
    title: '投籃對決',
    subtitle: '已開放遊玩',
    description: '調整方向蓄力投籃，籃框會左右移動；投進有機率獲得干擾卡，按鍵讓對手籃框瘋狂亂飄，60 秒比進球。',
    route: '/game16'
  },
  16: {
    title: '迷宮競速',
    subtitle: '已開放遊玩',
    description: '兩人挑戰同一座隨機迷宮，從左上角衝向右下角終點，沿途金幣可加成，先抵達者獲勝。',
    route: '/game17'
  },
  17: {
    title: '拔槍反應對決',
    subtitle: '已開放遊玩',
    description: '看到綠色「開槍！」瞬間搶先按鍵者贏得回合，提前出手算偷跑，還有紅色假信號干擾，先贏 3 回合者勝。',
    route: '/game18'
  },
  18: {
    title: '平台爭霸',
    subtitle: '已開放遊玩',
    description: '在平台間跳躍與攻擊撞飛對手，傷害越高被擊飛越遠，被擊出邊界即被擊落，限時內擊落數多者勝。',
    route: '/game19'
  },
  19: {
    title: '雙人格鬥',
    subtitle: '已開放遊玩',
    description: '拳擊與踢擊交鋒，適時防禦化解攻勢，把對手血量打到 0 拿下一局，三局兩勝的擂台對決。',
    route: '/game20'
  }
}

export const gameLobbyCards = Array.from({ length: 20 }, (_, index) => {
  const accent = accents[index % accents.length]
  const cardNumber = String(index + 1).padStart(2, '0')
  const game = games[index]

  return {
    id: `game-slot-${cardNumber}`,
    no: cardNumber,
    title: game?.title ?? `遊戲入口 ${cardNumber}`,
    description: game?.description ?? '之後可切換為對應的遊戲 component。',
    actionLabel: game ? '進入遊戲' : '保留入口',
    route: game?.route ?? '',
    accent
  }
})
