<template>
  <canvas
    ref="canvasRef"
    class="tech-bg"
    aria-hidden="true"
  />
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'

const canvasRef = ref(null)

let ctx = null
let raf = 0
let w = 0
let h = 0
let dpr = 1
let particles = []
let signals = []
let panels = []
let reduced = false
const mouse = { x: 0, y: 0, tx: 0, ty: 0 }

function resize() {
  const c = canvasRef.value
  if (!c) return
  dpr = Math.min(2, window.devicePixelRatio || 1)
  w = window.innerWidth
  h = window.innerHeight
  c.width = Math.floor(w * dpr)
  c.height = Math.floor(h * dpr)
  c.style.width = `${w}px`
  c.style.height = `${h}px`
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
  build()
}

function build() {
  // flowing particles
  const count = Math.round(Math.min(100, Math.max(46, (w * h) / 17000)))
  particles = []
  for (let i = 0; i < count; i += 1) {
    const depth = Math.random()
    particles.push({
      x: Math.random() * w,
      y: Math.random() * h,
      vx: (Math.random() - 0.5) * (0.12 + depth * 0.25),
      vy: -(0.1 + depth * 0.3),
      r: 0.5 + depth * 1.8,
      depth,
      c: Math.random() < 0.6 ? '94,234,212' : Math.random() < 0.5 ? '130,185,255' : '167,139,250'
    })
  }

  // full-width oscilloscope signals
  signals = [
    { y: 0.2, amp: 26, f1: 0.012, f2: 0.04, sp1: 0.0022, sp2: -0.0035, c: '94,234,212', a: 0.5 },
    { y: 0.5, amp: 18, f1: 0.018, f2: 0.05, sp1: -0.0018, sp2: 0.004, c: '130,185,255', a: 0.4 },
    { y: 0.74, amp: 30, f1: 0.009, f2: 0.03, sp1: 0.0015, sp2: -0.0028, c: '167,139,250', a: 0.42 },
    { y: 0.88, amp: 14, f1: 0.025, f2: 0.07, sp1: 0.003, sp2: 0.005, c: '52,211,153', a: 0.38 }
  ]

  // HUD instrument panels (decorative, in the margins)
  panels = [
    { x: w - 320, y: 40, w: 264, h: 120, label: 'CH-01 / SIGNAL', seed: 1.3, c: '94,234,212' },
    { x: 36, y: h - 190, w: 250, h: 124, label: 'CH-02 / FLUX', seed: 4.1, c: '167,139,250' }
  ]
}

function drawBase() {
  const g = ctx.createLinearGradient(0, 0, w, h)
  g.addColorStop(0, '#120c2c')
  g.addColorStop(0.55, '#0c0820')
  g.addColorStop(1, '#06040e')
  ctx.fillStyle = g
  ctx.fillRect(0, 0, w, h)
  // glow blobs
  ctx.save()
  ctx.globalCompositeOperation = 'lighter'
  const blobs = [
    ['rgba(34,211,238,0.08)', 0.2, 0.22],
    ['rgba(167,139,250,0.08)', 0.82, 0.7],
    ['rgba(52,211,153,0.05)', 0.6, 0.38]
  ]
  for (const [col, bx, by] of blobs) {
    const cx = w * bx
    const cy = h * by
    const rr = Math.min(w, h) * 0.5
    const rg = ctx.createRadialGradient(cx, cy, 0, cx, cy, rr)
    rg.addColorStop(0, col)
    rg.addColorStop(1, 'rgba(0,0,0,0)')
    ctx.fillStyle = rg
    ctx.fillRect(0, 0, w, h)
  }
  ctx.restore()
}

function drawGrid() {
  ctx.save()
  ctx.strokeStyle = 'rgba(120,180,220,0.05)'
  ctx.lineWidth = 1
  const step = 44
  for (let x = 0; x < w; x += step) {
    ctx.beginPath()
    ctx.moveTo(x, 0)
    ctx.lineTo(x, h)
    ctx.stroke()
  }
  for (let y = 0; y < h; y += step) {
    ctx.beginPath()
    ctx.moveTo(0, y)
    ctx.lineTo(w, y)
    ctx.stroke()
  }
  ctx.restore()
}

function waveAt(sig, x, t, baseY, amp) {
  return (
    baseY +
    Math.sin(x * sig.f1 + t * sig.sp1) * amp +
    Math.sin(x * sig.f2 + t * sig.sp2) * amp * 0.45
  )
}

function drawSignals(t) {
  ctx.save()
  ctx.globalCompositeOperation = 'lighter'
  for (const sig of signals) {
    const baseY = h * sig.y
    ctx.strokeStyle = `rgba(${sig.c},${sig.a})`
    ctx.lineWidth = 1.6
    ctx.shadowColor = `rgba(${sig.c},0.8)`
    ctx.shadowBlur = 8
    ctx.beginPath()
    for (let x = 0; x <= w; x += 5) {
      const y = waveAt(sig, x, t, baseY, sig.amp)
      if (x === 0) ctx.moveTo(x, y)
      else ctx.lineTo(x, y)
    }
    ctx.stroke()
    // travelling pulse dot
    const px = (t * 0.12) % w
    const py = waveAt(sig, px, t, baseY, sig.amp)
    ctx.shadowBlur = 14
    ctx.fillStyle = `rgba(${sig.c},0.95)`
    ctx.beginPath()
    ctx.arc(px, py, 2.6, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.restore()
}

function drawSpectrum(t) {
  const bars = 56
  const bw = w / bars
  ctx.save()
  ctx.globalCompositeOperation = 'lighter'
  for (let i = 0; i < bars; i += 1) {
    const v = Math.abs(Math.sin(t * 0.004 + i * 0.5) + Math.sin(t * 0.0026 + i * 1.3)) / 2
    const bh = 6 + v * 70
    const x = i * bw
    const grd = ctx.createLinearGradient(0, h - bh, 0, h)
    grd.addColorStop(0, 'rgba(94,234,212,0.45)')
    grd.addColorStop(1, 'rgba(94,234,212,0)')
    ctx.fillStyle = grd
    ctx.fillRect(x + 1, h - bh, bw - 2, bh)
  }
  ctx.restore()
}

function roundRectPath(x, y, ww, hh, r) {
  ctx.beginPath()
  ctx.moveTo(x + r, y)
  ctx.arcTo(x + ww, y, x + ww, y + hh, r)
  ctx.arcTo(x + ww, y + hh, x, y + hh, r)
  ctx.arcTo(x, y + hh, x, y, r)
  ctx.arcTo(x, y, x + ww, y, r)
  ctx.closePath()
}

function drawPanel(p, t) {
  if (p.x < -10 || p.x + p.w > w + 10 || p.y + p.h > h + 10) return
  ctx.save()
  ctx.globalAlpha = 0.6
  roundRectPath(p.x, p.y, p.w, p.h, 10)
  ctx.fillStyle = 'rgba(10,14,30,0.5)'
  ctx.fill()
  ctx.strokeStyle = `rgba(${p.c},0.35)`
  ctx.lineWidth = 1
  ctx.stroke()
  // corner ticks
  ctx.strokeStyle = `rgba(${p.c},0.6)`
  ctx.lineWidth = 2
  const cl = 12
  ctx.beginPath()
  ctx.moveTo(p.x, p.y + cl); ctx.lineTo(p.x, p.y); ctx.lineTo(p.x + cl, p.y)
  ctx.moveTo(p.x + p.w - cl, p.y + p.h); ctx.lineTo(p.x + p.w, p.y + p.h); ctx.lineTo(p.x + p.w, p.y + p.h - cl)
  ctx.stroke()

  // clip + waveform
  ctx.save()
  roundRectPath(p.x + 6, p.y + 24, p.w - 12, p.h - 34, 6)
  ctx.clip()
  // mini grid
  ctx.strokeStyle = 'rgba(120,180,220,0.06)'
  ctx.lineWidth = 1
  for (let gx = p.x + 6; gx < p.x + p.w; gx += 22) {
    ctx.beginPath(); ctx.moveTo(gx, p.y + 24); ctx.lineTo(gx, p.y + p.h - 10); ctx.stroke()
  }
  const midY = p.y + 24 + (p.h - 34) / 2
  ctx.strokeStyle = `rgba(${p.c},0.85)`
  ctx.lineWidth = 1.5
  ctx.shadowColor = `rgba(${p.c},0.8)`
  ctx.shadowBlur = 8
  ctx.beginPath()
  for (let x = p.x + 6; x <= p.x + p.w - 6; x += 3) {
    const k = x - p.x
    const y = midY + Math.sin(k * 0.06 + t * 0.006 + p.seed) * (p.h * 0.16) + Math.sin(k * 0.15 - t * 0.004) * (p.h * 0.06)
    if (x === p.x + 6) ctx.moveTo(x, y)
    else ctx.lineTo(x, y)
  }
  ctx.stroke()
  ctx.restore()

  // label + value
  ctx.shadowBlur = 0
  ctx.fillStyle = `rgba(${p.c},0.85)`
  ctx.font = '10px ui-monospace, "SF Mono", Consolas, monospace'
  ctx.textAlign = 'left'
  ctx.fillText(p.label, p.x + 10, p.y + 16)
  const val = (Math.abs(Math.sin(t * 0.0012 + p.seed)) * 100).toFixed(1)
  ctx.textAlign = 'right'
  ctx.fillText(`${val}%`, p.x + p.w - 10, p.y + 16)
  ctx.restore()
}

function drawParticles() {
  for (const p of particles) {
    p.x += p.vx
    p.y += p.vy
    if (p.y < -10) { p.y = h + 10; p.x = Math.random() * w }
    if (p.x < -10) p.x = w + 10
    else if (p.x > w + 10) p.x = -10
  }
  const mx = mouse.x - w / 2
  const my = mouse.y - h / 2
  const D = 116
  ctx.save()
  ctx.globalCompositeOperation = 'lighter'
  ctx.lineWidth = 1
  for (let i = 0; i < particles.length; i += 1) {
    const a = particles[i]
    const ax = a.x + mx * a.depth * 0.02
    const ay = a.y + my * a.depth * 0.02
    for (let j = i + 1; j < particles.length; j += 1) {
      const b = particles[j]
      const bx = b.x + mx * b.depth * 0.02
      const by = b.y + my * b.depth * 0.02
      const dx = ax - bx
      const dy = ay - by
      const d2 = dx * dx + dy * dy
      if (d2 < D * D) {
        ctx.strokeStyle = `rgba(125,205,235,${(1 - Math.sqrt(d2) / D) * 0.1})`
        ctx.beginPath()
        ctx.moveTo(ax, ay)
        ctx.lineTo(bx, by)
        ctx.stroke()
      }
    }
  }
  for (const p of particles) {
    const px = p.x + mx * p.depth * 0.02
    const py = p.y + my * p.depth * 0.02
    ctx.fillStyle = `rgba(${p.c},${0.3 + p.depth * 0.5})`
    ctx.beginPath()
    ctx.arc(px, py, p.r, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.restore()
}

function drawScan(t) {
  const sy = ((t * 0.05) % (h + 240)) - 120
  const g = ctx.createLinearGradient(0, sy - 70, 0, sy + 70)
  g.addColorStop(0, 'rgba(94,234,212,0)')
  g.addColorStop(0.5, 'rgba(94,234,212,0.05)')
  g.addColorStop(1, 'rgba(94,234,212,0)')
  ctx.fillStyle = g
  ctx.fillRect(0, sy - 70, w, 140)
}

function drawVignette() {
  const g = ctx.createRadialGradient(w / 2, h * 0.45, Math.min(w, h) * 0.3, w / 2, h * 0.5, Math.max(w, h) * 0.8)
  g.addColorStop(0, 'rgba(0,0,0,0)')
  g.addColorStop(1, 'rgba(0,0,0,0.6)')
  ctx.fillStyle = g
  ctx.fillRect(0, 0, w, h)
}

function drawScene(t) {
  drawBase()
  drawGrid()
  drawSignals(t)
  drawSpectrum(t)
  drawParticles()
  for (const p of panels) drawPanel(p, t)
  drawScan(t)
  drawVignette()
}

function frame(t) {
  mouse.x += (mouse.tx - mouse.x) * 0.05
  mouse.y += (mouse.ty - mouse.y) * 0.05
  drawScene(t)
  raf = requestAnimationFrame(frame)
}

function onMove(e) {
  mouse.tx = e.clientX
  mouse.ty = e.clientY
}

function onVisibility() {
  if (document.hidden) {
    if (raf) cancelAnimationFrame(raf)
    raf = 0
  } else if (!reduced && !raf) {
    raf = requestAnimationFrame(frame)
  }
}

onMounted(() => {
  ctx = canvasRef.value.getContext('2d')
  reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches
  mouse.x = window.innerWidth / 2
  mouse.y = window.innerHeight / 2
  mouse.tx = mouse.x
  mouse.ty = mouse.y
  resize()
  window.addEventListener('resize', resize)
  window.addEventListener('pointermove', onMove)
  document.addEventListener('visibilitychange', onVisibility)
  if (reduced) drawScene(0)
  else raf = requestAnimationFrame(frame)
})

onBeforeUnmount(() => {
  if (raf) cancelAnimationFrame(raf)
  window.removeEventListener('resize', resize)
  window.removeEventListener('pointermove', onMove)
  document.removeEventListener('visibilitychange', onVisibility)
})
</script>

<style scoped>
.tech-bg {
  position: fixed;
  inset: 0;
  z-index: -1;
  display: block;
  width: 100%;
  height: 100%;
  pointer-events: none;
}
</style>
