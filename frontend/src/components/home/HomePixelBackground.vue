<template>
  <div
    class="pixel-bg"
    aria-hidden="true"
  >
    <div class="bg-photo" />
    <div class="bg-stars" />
    <div class="bg-scrim" />
    <!-- 漂浮裝飾 -->
    <img
      class="float float-ufo"
      src="/assets/HOME/deco/ufo-1.png"
      alt=""
    >
    <img
      class="float float-coin"
      src="/assets/HOME/deco/coin-1.png"
      alt=""
    >
    <img
      class="float float-gem"
      src="/assets/HOME/deco/gem-pink.png"
      alt=""
    >
    <img
      class="float float-star"
      src="/assets/HOME/deco/star.png"
      alt=""
    >
    <img
      class="float float-pad"
      src="/assets/HOME/deco/gamepad.png"
      alt=""
    >
  </div>
</template>

<script setup>
// 純 CSS 像素背景：日系夜晚遊樂場 + 星點平鋪 + 漂浮裝飾
</script>

<style scoped>
.pixel-bg {
  position: fixed;
  inset: 0;
  z-index: -1;
  overflow: hidden;
  pointer-events: none;
}

.bg-photo {
  position: absolute;
  inset: 0;
  background-image: url('/assets/HOME/home-bg-pixel-arcade.png');
  background-size: cover;
  background-position: center top;
  image-rendering: pixelated;
}

/* 星點 tile 緩慢飄移閃爍 */
.bg-stars {
  position: absolute;
  inset: -256px;
  background-image: url('/assets/HOME/deco-stars-particles.png');
  background-size: 256px 256px;
  background-repeat: repeat;
  image-rendering: pixelated;
  opacity: 0.55;
  animation: starsDrift 42s linear infinite, starsTwinkle 4.5s ease-in-out infinite;
}

/* 底部加一層暗角，讓上方內容文字更清楚 */
.bg-scrim {
  position: absolute;
  inset: 0;
  background:
    radial-gradient(120% 80% at 50% 0%, rgba(64, 58, 82, 0) 40%, rgba(28, 24, 44, 0.55) 100%),
    linear-gradient(180deg, rgba(40, 36, 60, 0.15) 0%, rgba(28, 24, 44, 0.35) 100%);
}

.float {
  position: absolute;
  image-rendering: pixelated;
  opacity: 0.9;
  filter: drop-shadow(0 6px 0 rgba(40, 36, 60, 0.25));
}
.float-ufo {
  width: 72px;
  top: 12%;
  left: 8%;
  animation: floatX 19s ease-in-out infinite;
}
.float-coin {
  width: 40px;
  top: 26%;
  right: 12%;
  animation: bob 5.5s ease-in-out infinite;
}
.float-gem {
  width: 38px;
  bottom: 22%;
  left: 6%;
  animation: bob 6.5s ease-in-out infinite 0.6s;
}
.float-star {
  width: 34px;
  top: 18%;
  right: 26%;
  animation: spinBob 9s ease-in-out infinite;
}
.float-pad {
  width: 64px;
  bottom: 14%;
  right: 8%;
  animation: bob 7.5s ease-in-out infinite 1.1s;
}

@keyframes starsDrift {
  from { transform: translate(0, 0); }
  to { transform: translate(-256px, -256px); }
}
@keyframes starsTwinkle {
  0%, 100% { opacity: 0.42; }
  50% { opacity: 0.62; }
}
@keyframes bob {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-12px); }
}
@keyframes spinBob {
  0%, 100% { transform: translateY(0) rotate(-8deg); }
  50% { transform: translateY(-10px) rotate(8deg); }
}
@keyframes floatX {
  0%, 100% { transform: translate(0, 0); }
  50% { transform: translate(40px, -16px); }
}

@media (prefers-reduced-motion: reduce) {
  .bg-stars,
  .float {
    animation: none;
  }
}
</style>
