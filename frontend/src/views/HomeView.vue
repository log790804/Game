<template>
  <main class="home-view">
    <HomePixelBackground />
    <HomePageHeader />
    <HomePageContent />
    <HomePageFooter />
  </main>
</template>

<script setup>
import HomePixelBackground from '@/components/home/HomePixelBackground.vue'
import HomePageHeader from '@/components/home/HomePageHeader.vue'
import HomePageContent from '@/components/home/HomePageContent.vue'
import HomePageFooter from '@/components/home/HomePageFooter.vue'
</script>

<style scoped>
.home-view {
  position: relative;
  min-height: 100vh;
  width: min(1240px, calc(100% - 2rem));
  margin: 0 auto;
  display: grid;
  gap: 1.5rem;
  padding: 1.75rem 0 2.5rem;
  color: #fdf3e7;
  font-family: 'Segoe UI', 'Noto Sans TC', system-ui, sans-serif;
}

/* 像素遊樂場背景在 HomePixelBackground.vue（fixed，z-index -1） */
</style>
