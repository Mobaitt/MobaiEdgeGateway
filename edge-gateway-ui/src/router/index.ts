import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: () => import('@/layouts/MainLayout.vue'),
    redirect: '/dashboard',
    children: [
      {
        path: 'dashboard',
        name: 'Dashboard',
        component: () => import('@/views/DashboardView.vue'),
        meta: { title: '系统总览', icon: 'DataAnalysis' }
      },
      {
        path: 'devices',
        name: 'Devices',
        component: () => import('@/views/DevicesView.vue'),
        meta: { title: '设备管理', icon: 'Monitor' }
      },
      {
        path: 'devices/:id/datapoints',
        name: 'DataPoints',
        component: () => import('@/views/DataPointsView.vue'),
        meta: { title: '数据点管理', hidden: true }
      },
      {
        path: 'channels',
        name: 'Channels',
        component: () => import('@/views/ChannelsView.vue'),
        meta: { title: '发送通道', icon: 'Share' }
      },
      {
        path: 'channels/:id/mappings',
        name: 'Mappings',
        component: () => import('@/views/MappingsView.vue'),
        meta: { title: '数据点映射', hidden: true }
      },
      {
        path: 'rules',
        name: 'Rules',
        component: () => import('@/views/RulesView.vue'),
        meta: { title: '规则管理', icon: 'Setting' }
      },
      {
        path: 'virtual-nodes',
        name: 'VirtualNodes',
        component: () => import('@/views/VirtualNodesView.vue'),
        meta: { title: '虚拟节点', icon: 'Cpu' }
      }
    ]
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.afterEach((to) => {
  const title = typeof to.meta.title === 'string' ? to.meta.title : ''
  document.title = title ? `${title} - EdgeGateway` : 'EdgeGateway'
})

export default router
