export const BOARD_SIZE_OPTIONS = [4, 6, 8]

const palettes = [
  ['#fde0d5', '#f8b4b4'],
  ['#fee9b2', '#f8cb88'],
  ['#d8ecff', '#b6d7ff'],
  ['#d8f5e5', '#aee6c4'],
  ['#efe2ff', '#d3c1fb'],
  ['#ffe2f0', '#f8b8d8'],
  ['#fbe8d8', '#efc89b'],
  ['#dff2f7', '#a7d8e8']
]

function createSvgDataUrl(title, subtitle, from, to, textColor = '#4d3b30') {
  const svg = `
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 240 320">
      <defs>
        <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stop-color="${from}" />
          <stop offset="100%" stop-color="${to}" />
        </linearGradient>
      </defs>
      <rect width="240" height="320" rx="28" fill="url(#bg)" />
      <circle cx="190" cy="64" r="28" fill="rgba(255,255,255,0.35)" />
      <circle cx="52" cy="252" r="38" fill="rgba(255,255,255,0.25)" />
      <text x="28" y="154" font-size="44" font-weight="800" fill="${textColor}" font-family="Segoe UI, Arial, sans-serif">${title}</text>
      <text x="30" y="192" font-size="18" fill="${textColor}" opacity="0.82" font-family="Segoe UI, Arial, sans-serif">${subtitle}</text>
    </svg>
  `

  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`
}

export const DEFAULT_BACK_IMAGE = createSvgDataUrl('GAME', 'Factory', '#f6d8bf', '#ef9f89', '#fff9f2')

export function createDefaultFrontImages(requiredCount = 32) {
  return Array.from({ length: Math.max(requiredCount, 32) }, (_, index) => {
    const cardNumber = String(index + 1).padStart(2, '0')
    const palette = palettes[index % palettes.length]
    return createSvgDataUrl(`Card ${cardNumber}`, 'Game 01', palette[0], palette[1])
  })
}

export function normalizeImageList(rawText) {
  return rawText
    .split(/\r?\n/)
    .map((item) => item.trim())
    .filter(Boolean)
}
