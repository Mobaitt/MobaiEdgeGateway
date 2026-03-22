<template>
  <div class="main-layout">
    <!-- 左侧边栏 -->
    <aside class="sidebar" :class="{ collapsed }">
      <!-- Logo 区域 -->
      <div class="sidebar-logo">
        <div class="logo-icon">
          <el-icon size="22"><Connection /></el-icon>
        </div>
        <transition name="fade">
          <div v-if="!collapsed" class="logo-text">
            <span class="logo-title">EdgeGateway</span>
            <span class="logo-sub">边缘采集网关</span>
          </div>
        </transition>
      </div>

      <!-- 网关在线状态 -->
      <div v-if="!collapsed" class="status-badge" :class="statusClass">
        <div class="pulse-dot" :class="gatewayOnline ? 'online' : 'offline'" />
        <span>{{ gatewayOnline ? '网关在线' : '连接中...' }}</span>
      </div>

      <!-- 导航菜单 -->
      <nav class="nav-menu">
        <router-link
          v-for="item in navItems"
          :key="item.path"
          :to="item.path"
          class="nav-item"
          :class="{ active: isActive(item.path) }"
        >
          <el-icon class="nav-icon"><component :is="item.icon" /></el-icon>
          <transition name="fade">
            <span v-if="!collapsed" class="nav-label">{{ item.title }}</span>
          </transition>
          <transition name="fade">
            <span v-if="!collapsed && item.count !== undefined" class="nav-count">{{ item.count }}</span>
          </transition>
        </router-link>
      </nav>

      <!-- 底部折叠按钮 -->
      <div class="sidebar-footer">
        <button class="collapse-btn" @click="collapsed = !collapsed">
          <el-icon><component :is="collapsed ? 'DArrowRight' : 'DArrowLeft'" /></el-icon>
        </button>
      </div>
    </aside>

    <!-- 主内容区 -->
    <div class="main-content">
      <!-- 顶部栏 -->
      <header class="topbar">
        <div class="topbar-left">
          <!-- 面包屑 -->
          <el-breadcrumb separator="/">
            <el-breadcrumb-item :to="{ path: '/' }">EdgeGateway</el-breadcrumb-item>
            <el-breadcrumb-item>{{ currentTitle }}</el-breadcrumb-item>
          </el-breadcrumb>
        </div>
        <div class="topbar-right">
          <!-- 演示模式提示 -->
          <div v-if="demoModeEnabled" class="demo-mode-badge" title="演示模式：只读">
            <el-icon><Lock /></el-icon>
            <span>演示模式</span>
          </div>
          <button class="theme-toggle" type="button" :title="isDark ? '切换到明亮模式' : '切换到暗色模式'" @click="toggleTheme">
            <el-icon><component :is="isDark ? 'Sunny' : 'Moon'" /></el-icon>
            <span>{{ isDark ? '明亮模式' : '暗色模式' }}</span>
          </button>
          <div class="sys-time mono">{{ currentTime }}</div>
          <div class="version-tag">v1.0.0</div>
        </div>
      </header>

      <!-- 页面内容 -->
      <main class="page-body">
        <router-view v-slot="{ Component }">
          <transition name="page" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getGatewayStatus } from '@/api/gateway'
import type { GatewayStatus } from '@/types'
import { getDemoModeStatus } from '@/api/system'
import { useTheme } from '@/composables/useTheme'

const route = useRoute()
const router = useRouter()
const collapsed = ref(false)
const gatewayOnline = ref(false)
const demoModeEnabled = ref(false)
const currentTime = ref('')
const deviceCount = ref(0)
const channelCount = ref(0)
const { isDark, toggleTheme } = useTheme()

const navItems = computed(() => {
  const layoutRoute = router.getRoutes().find((r) => r.path === '/' && r.children?.length)
  const children = layoutRoute?.children ?? []
  const fullPath = (r: { path: string }) => (r.path.startsWith('/') ? r.path : `/${r.path}`)
  const countByPath: Record<string, number> = {
    '/devices': deviceCount.value,
    '/channels': channelCount.value
  }
  return children
    .filter((r) => !(r.meta?.hidden === true))
    .map((r) => {
      const path = fullPath(r)
      return {
        path,
        title: (r.meta?.title as string) ?? (r.name as string) ?? '',
        icon: (r.meta?.icon as string) ?? 'Document',
        count: countByPath[path]
      }
    })
})

const currentTitle = computed(() => {
  const metaTitle = typeof route.meta?.title === 'string' ? route.meta.title : ''
  if (metaTitle) return metaTitle
  const nav = navItems.value.find((n) => route.path.startsWith(n.path))
  return nav?.title ?? ''
})

const statusClass = computed(() => (gatewayOnline.value ? 'online' : 'offline'))

const isActive = (path: string) => route.path.startsWith(path)

let timer: ReturnType<typeof setInterval> | null = null
let statusTimer: ReturnType<typeof setInterval> | null = null

const updateTime = () => {
  currentTime.value = new Date().toLocaleTimeString('zh-CN', { hour12: false })
}

const fetchStatus = async () => {
  try {
    const res = await getGatewayStatus()
    const data = (res as { data?: GatewayStatus })?.data
    gatewayOnline.value = data?.isRunning ?? false
    deviceCount.value = data?.totalDevices ?? 0
    channelCount.value = data?.totalChannels ?? 0
  } catch {
    gatewayOnline.value = false
  }
}

const fetchDemoModeStatus = async () => {
  try {
    const res = await getDemoModeStatus()
    demoModeEnabled.value = (res as { data?: { enabled?: boolean } })?.data?.enabled ?? false
  } catch {
    demoModeEnabled.value = false
  }
}

onMounted(() => {
  updateTime()
  timer = setInterval(updateTime, 1000)
  fetchStatus()
  fetchDemoModeStatus()
  statusTimer = setInterval(fetchStatus, 10000)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
  if (statusTimer) clearInterval(statusTimer)
})
</script>

<style scoped>
.main-layout {
  display: flex;
  height: 100vh;
  overflow: hidden;
  background: var(--bg-base);
}

/* ===== 左侧边栏 ===== */
.sidebar {
  width: 220px;
  min-width: 220px;
  height: 100%;
  background: var(--bg-panel);
  border-right: 1px solid var(--border-subtle);
  display: flex;
  flex-direction: column;
  transition: width 0.25s ease, min-width 0.25s ease;
  position: relative;
  z-index: 10;
}
.sidebar.collapsed { width: 64px; min-width: 64px; }

/* Logo */
.sidebar-logo {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 22px 16px 18px;
  border-bottom: 1px solid var(--border-subtle);
}
.logo-icon {
  width: 36px; height: 36px;
  background: linear-gradient(135deg, var(--cyan), #4299e1);
  border-radius: 8px;
  display: flex; align-items: center; justify-content: center;
  flex-shrink: 0;
  color: #0a0e1a;
}
.logo-title { display: block; font-size: 15px; font-weight: 800; color: var(--text-primary); line-height: 1.2; }
.logo-sub   { display: block; font-size: 10px; color: var(--text-muted); letter-spacing: 0.06em; margin-top: 2px; }

/* 状态条 */
.status-badge {
  margin: 12px 14px 4px;
  padding: 6px 10px;
  border-radius: 6px;
  display: flex; align-items: center; gap: 8px;
  font-size: 10px; font-family: var(--font-mono); letter-spacing: 0.08em; font-weight: 600;
}
.status-badge.online  { background: rgba(74,222,128,0.07); border: 1px solid rgba(74,222,128,0.2); color: #4ade80; }
.status-badge.offline { background: rgba(240,95,110,0.07); border: 1px solid rgba(240,95,110,0.2); color: #f05f6e; }

/* 导航 */
.nav-menu {
  flex: 1;
  padding: 12px 10px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  overflow-y: auto;
}
.nav-item {
  display: flex; align-items: center; gap: 10px;
  padding: 10px 12px;
  border-radius: var(--radius);
  color: var(--text-secondary);
  text-decoration: none;
  font-size: 13.5px; font-weight: 600;
  transition: all 0.2s;
  position: relative;
  white-space: nowrap;
}
.nav-item:hover { background: var(--bg-hover); color: var(--text-primary); }
.nav-item.active {
  background: var(--cyan-dim);
  color: var(--cyan);
  border-left: 2px solid var(--cyan);
}
.nav-icon { font-size: 18px; flex-shrink: 0; }
.nav-label { flex: 1; }
.nav-count {
  background: rgba(56,220,196,0.15);
  color: var(--cyan);
  border-radius: 10px;
  padding: 1px 8px;
  font-size: 11px;
  font-family: var(--font-mono);
}

/* 底部 */
.sidebar-footer {
  padding: 12px 10px;
  border-top: 1px solid var(--border-subtle);
  display: flex; justify-content: flex-end;
}
.collapse-btn {
  background: none; border: 1px solid var(--border-muted);
  color: var(--text-muted); border-radius: 6px;
  width: 32px; height: 32px; cursor: pointer;
  display: flex; align-items: center; justify-content: center;
  transition: all 0.2s;
}
.collapse-btn:hover { color: var(--cyan); border-color: var(--border-accent); }

/* ===== 主内容区 ===== */
.main-content {
  flex: 1;
  display: flex; flex-direction: column;
  overflow: hidden;
}

/* 顶部栏 */
.topbar {
  height: 56px; min-height: 56px;
  display: flex; align-items: center; justify-content: space-between;
  padding: 0 28px;
  border-bottom: 1px solid var(--border-subtle);
  background: var(--bg-panel);
}
.topbar-right { display: flex; align-items: center; gap: 16px; }

.theme-toggle {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  height: 34px;
  padding: 0 12px;
  border-radius: 12px;
  border: 1px solid var(--border-subtle);
  background: var(--bg-card);
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: var(--theme-surface-glow);
}

.theme-toggle:hover {
  color: var(--cyan);
  border-color: var(--border-accent);
  background: var(--bg-hover);
  transform: translateY(-1px);
}

.theme-toggle span {
  font-size: 12px;
  font-weight: 600;
}

/* 演示模式徽章 */
.demo-mode-badge {
  display: flex; align-items: center; gap: 6px;
  padding: 4px 10px;
  background: rgba(255, 193, 7, 0.15);
  border: 1px solid rgba(255, 193, 7, 0.3);
  border-radius: 12px;
  color: #ffc107;
  font-size: 11px;
  font-weight: 600;
  font-family: var(--font-mono);
  cursor: default;
}

.sys-time { font-size: 13px; color: var(--text-muted); letter-spacing: 0.04em; }
.version-tag {
  font-size: 11px; color: var(--cyan); font-family: var(--font-mono);
  background: var(--cyan-dim); padding: 2px 8px; border-radius: 10px;
  border: 1px solid rgba(56,220,196,0.25);
}

/* 页面区 */
.page-body {
  flex: 1;
  overflow-y: auto;
  padding: 24px 28px;
}

/* 过渡动画 */
.page-enter-active, .page-leave-active { transition: opacity 0.2s, transform 0.2s; }
.page-enter-from { opacity: 0; transform: translateY(8px); }
.page-leave-to   { opacity: 0; transform: translateY(-4px); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

/* Breadcrumb 暗色适配 */
:deep(.el-breadcrumb__item .el-breadcrumb__inner) { color: var(--text-muted); font-size: 13px; }
:deep(.el-breadcrumb__item:last-child .el-breadcrumb__inner) { color: var(--text-primary); font-weight: 600; }
:deep(.el-breadcrumb__separator) { color: var(--text-muted); }
</style>
