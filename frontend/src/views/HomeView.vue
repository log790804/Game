<template>
  <main class="home-view">
    <HomePageHeader />
    <HomePageContent />
    <HomePageFooter />
  </main>
</template>

<script setup>
import HomePageHeader from '@/components/home/HomePageHeader.vue'
import HomePageContent from '@/components/home/HomePageContent.vue'
import HomePageFooter from '@/components/home/HomePageFooter.vue'
</script>

<style scoped>
.home-view {
  width: min(1240px, calc(100% - 2rem));
  margin: 0 auto;
  display: grid;
  gap: 1.5rem;
  padding: 1.5rem 0 2.5rem;
}
</style>
