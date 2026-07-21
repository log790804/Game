import { createRouter, createWebHashHistory, createWebHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'

const createHistory = import.meta.env.VITE_ROUTER_MODE === 'hash'
  ? createWebHashHistory
  : createWebHistory

const router = createRouter({
  history: createHistory(import.meta.env.BASE_URL),
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
      path: '/game05',
      name: 'game05',
      component: () => import('../games/game05/Game05View.vue')
    },
    {
      path: '/game06',
      name: 'game06',
      component: () => import('../games/game06/Game06View.vue')
    },
    {
      path: '/game07',
      name: 'game07',
      component: () => import('../games/game07/Game07View.vue')
    },
    {
      path: '/game08',
      name: 'game08',
      component: () => import('../games/game08/Game08View.vue')
    },
    {
      path: '/game09',
      name: 'game09',
      component: () => import('../games/game09/Game09View.vue')
    },
    {
      path: '/game10',
      name: 'game10',
      component: () => import('../games/game10/Game10View.vue')
    },
    {
      path: '/game11',
      name: 'game11',
      component: () => import('../games/game11/Game11View.vue')
    },
    {
      path: '/game12',
      name: 'game12',
      component: () => import('../games/game12/Game12View.vue')
    },
    {
      path: '/game13',
      name: 'game13',
      component: () => import('../games/game13/Game13View.vue')
    },
    {
      path: '/game14',
      name: 'game14',
      component: () => import('../games/game14/Game14View.vue')
    },
    {
      path: '/game15',
      name: 'game15',
      component: () => import('../games/game15/Game15View.vue')
    },
    {
      path: '/game16',
      name: 'game16',
      component: () => import('../games/game16/Game16View.vue')
    },
    {
      path: '/game17',
      name: 'game17',
      component: () => import('../games/game17/Game17View.vue')
    },
    {
      path: '/game18',
      name: 'game18',
      component: () => import('../games/game18/Game18View.vue')
    },
    {
      path: '/game19',
      name: 'game19',
      component: () => import('../games/game19/Game19View.vue')
    },
    {
      path: '/game20',
      name: 'game20',
      component: () => import('../games/game20/Game20View.vue')
    },
    {
      path: '/about',
      name: 'about',
      component: () => import('../views/AboutView.vue')
    }
  ]
})

export default router
