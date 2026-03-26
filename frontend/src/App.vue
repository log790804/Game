<script setup>
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <header>
    <div class="wrapper">
      <nav>
        <RouterLink to="/">首頁</RouterLink>
        <RouterLink to="/about">關於</RouterLink>
      </nav>
    </div>
  </header>

  <RouterView />
</template>

<style scoped>
header {
  line-height: 1.5;
  background-color: #1a1a2e;
  padding: 0 2rem;
}

.wrapper {
  display: flex;
  justify-content: flex-end;
  max-width: 1200px;
  margin: 0 auto;
}

nav {
  display: flex;
  gap: 1.5rem;
  padding: 1rem 0;
}

nav a {
  color: #a0aec0;
  text-decoration: none;
  font-weight: 500;
  transition: color 0.2s;
}

nav a:hover,
nav a.router-link-active {
  color: #63b3ed;
}
</style>
