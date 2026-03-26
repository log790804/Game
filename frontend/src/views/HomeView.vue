<script setup>
import WelcomeCard from '@/components/WelcomeCard.vue'
import CounterWidget from '@/components/CounterWidget.vue'
</script>

<template>
  <main>
    <WelcomeCard />
    <CounterWidget />
  </main>
</template>

<style scoped>
main {
  min-height: calc(100vh - 60px);
  background: linear-gradient(135deg, #0f0c29, #302b63, #24243e);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2rem;
  padding: 2rem;
}
</style>
