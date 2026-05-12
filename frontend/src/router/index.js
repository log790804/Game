import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView
    },
    {
      path: '/game01',
      name: 'game01',
      component: () => import('../games/game01/Game01View.vue')
    },
    {
      path: '/game02',
      name: 'game02',
      component: () => import('../games/game02/Game02View.vue')
    },
    {
      path: '/game03',
      name: 'game03',
      component: () => import('../games/game03/Game03View.vue')
    },
    {
      path: '/game04',
      name: 'game04',
      component: () => import('../games/game04/Game04View.vue')
    },
    {
      path: '/about',
      name: 'about',
      component: () => import('../views/AboutView.vue')
    }
  ]
})

export default router
