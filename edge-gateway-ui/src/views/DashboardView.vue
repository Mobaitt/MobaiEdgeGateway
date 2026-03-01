<template>
  <div class="dashboard page-enter">
    <!-- 页面头部 -->
    <div class="page-header">
      <h1 class="page-title">系统总览</h1>
      <el-button size="small" :loading="loading" @click="refresh" :icon="Refresh">刷新</el-button>
    </div>

    <!-- 统计卡片 -->
    <div class="stat-grid">
      <div class="stat-card" v-for="stat in stats" :key="stat.key">
        <div class="stat-icon" :style="{ color: stat.color, background: stat.bg }">
          <el-icon size="22"><component :is="stat.icon" /></el-icon>
        </div>
        <div class="stat-info">
          <div class="stat-value mono">{{ stat.value }}</div>
          <div class="stat-label">{{ stat.label }}</div>
        </div>
        <div class="stat-sub">
          <span :class="['badge', stat.badge]">{{ stat.sub }}</span>
        </div>
      </div>
    </div>

    <!-- 底部两列 -->
    <div class="bottom-grid">
      <!-- 设备列表 -->
      <div class="panel">
        <div class="panel-header">
          <span class="panel-title">设备状态</span>
          <router-link to="/devices" class="panel-link">查看全部 →</router-link>
        </div>
        <div class="device-list">
          <div
            v-for="d in devices"
            :key="d.id"
            class="device-row"
          >
            <div class="device-left">
              <div class="pulse-dot" :class="d.isEnabled ? 'online' : 'offline'" />
              <div>
                <div class="device-name">{{ d.name }}</div>
                <div class="device-addr mono">{{ d.address }} · {{ d.protocol }}</div>
              </div>
            </div>
            <div class="device-right">
              <span class="badge" :class="d.isEnabled ? 'good' : 'bad'">
                {{ d.isEnabled ? '已启用' : '已禁用' }}
              </span>
              <span class="dp-count mono">{{ d.dataPointCount }} 点</span>
            </div>
          </div>
          <div v-if="devices.length === 0" class="empty-hint">暂无设备数据</div>
        </div>
      </div>

      <!-- 发送通道 -->
      <div class="panel">
        <div class="panel-header">
          <span class="panel-title">发送通道</span>
          <router-link to="/channels" class="panel-link">查看全部 →</router-link>
        </div>
        <div class="channel-list">
          <div v-for="c in channels" :key="c.id" class="channel-row">
            <div class="channel-left">
              <div class="protocol-dot" :style="{ background: getChannelColor(c.protocolValue) }" />
              <div>
                <div class="channel-name">{{ c.name }}</div>
                <div class="channel-ep mono">{{ c.endpoint }}</div>
              </div>
            </div>
            <div>
              <span class="badge" :class="c.isEnabled ? 'good' : 'bad'">
                {{ c.isEnabled ? '开启' : '关闭' }}
              </span>
            </div>
          </div>
          <div v-if="channels.length === 0" class="empty-hint">暂无通道数据</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { getGatewayStatus } from '@/api/gateway'
import { getDevices } from '@/api/device'
import { getChannels } from '@/api/channel'
import { SendProtocol } from '@/api/constants'

type StatusData = {
  isRunning?: boolean
  totalDevices?: number
  enabledDevices?: number
  totalChannels?: number
  enabledChannels?: number
  totalDataPoints?: number
}

type DeviceItem = {
  id: number
  name: string
  address: string
  protocol: string
  isEnabled: boolean
  dataPointCount: number
}

type ChannelItem = {
  id: number
  name: string
  endpoint: string
  protocolValue: number
  isEnabled: boolean
}

const loading = ref(false)
const status = ref<StatusData>({})
const devices = ref<DeviceItem[]>([])
const channels = ref<ChannelItem[]>([])

const stats = computed(() => [
  {
    key: 'status',
    label: '网关状态',
    icon: 'Connection',
    value: status.value.isRunning ? '运行中' : '已停止',
    color: status.value.isRunning ? '#4ade80' : '#f05f6e',
    bg: status.value.isRunning ? 'rgba(74,222,128,0.1)' : 'rgba(240,95,110,0.1)',
    badge: status.value.isRunning ? 'good' : 'bad',
    sub: status.value.isRunning ? '运行中' : '已停止'
  },
  {
    key: 'devices',
    label: '设备总数',
    icon: 'Monitor',
    value: status.value.totalDevices ?? 0,
    color: '#4299e1',
    bg: 'rgba(66,153,225,0.1)',
    badge: 'info',
    sub: `${status.value.enabledDevices ?? 0} 台启用`
  },
  {
    key: 'channels',
    label: '通道总数',
    icon: 'Share',
    value: status.value.totalChannels ?? 0,
    color: '#38dcc4',
    bg: 'rgba(56,220,196,0.1)',
    badge: 'info',
    sub: `${status.value.enabledChannels ?? 0} 台启用`
  },
  {
    key: 'datapoints',
    label: '数据点总数',
    icon: 'DataLine',
    value: status.value.totalDataPoints ?? 0,
    color: '#9f7aea',
    bg: 'rgba(159,122,234,0.1)',
    badge: 'info',
    sub: '采集点'
  }
])

const getChannelColor = (value: number) => {
  const protocol = Object.values(SendProtocol).find((item) => item.value === value)
  return protocol?.color ?? '#8fa5c5'
}

const refresh = async () => {
  loading.value = true
  try {
    const [statusRes, deviceRes, channelRes] = await Promise.all([
      getGatewayStatus(),
      getDevices(),
      getChannels()
    ])

    status.value = ((statusRes as { data?: StatusData })?.data ?? {}) as StatusData
    devices.value = (((deviceRes as { data?: DeviceItem[] })?.data ?? []) as DeviceItem[]).slice(0, 6)
    channels.value = (((channelRes as { data?: ChannelItem[] })?.data ?? []) as ChannelItem[]).slice(0, 5)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<style scoped>
.page-header {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: 24px;
}
.page-title { font-size: 22px; font-weight: 800; color: var(--text-primary); letter-spacing: -0.01em; }

/* 统计卡片 */
.stat-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}
.stat-card {
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  padding: 20px;
  display: flex; align-items: center; gap: 14px;
  transition: border-color 0.2s;
}
.stat-card:hover { border-color: var(--border-muted); }
.stat-icon {
  width: 46px; height: 46px; border-radius: 10px;
  display: flex; align-items: center; justify-content: center;
  flex-shrink: 0;
}
.stat-value { font-size: 26px; font-weight: 700; color: var(--text-primary); line-height: 1; }
.stat-label { font-size: 12px; color: var(--text-muted); margin-top: 4px; letter-spacing: 0.02em; }
.stat-sub { margin-left: auto; }

/* 底部网格 */
.bottom-grid {
  display: grid; grid-template-columns: 1fr 1fr; gap: 16px;
}
.panel {
  background: var(--bg-card); border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg); overflow: hidden;
}
.panel-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 16px 20px 12px;
  border-bottom: 1px solid var(--border-subtle);
}
.panel-title { font-size: 13px; font-weight: 700; color: var(--text-primary); letter-spacing: 0.04em; text-transform: uppercase; }
.panel-link { font-size: 12px; color: var(--cyan); text-decoration: none; }
.panel-link:hover { opacity: 0.8; }

/* 设备列表 */
.device-list, .channel-list { padding: 8px 0; }
.device-row, .channel-row {
  display: flex; align-items: center; justify-content: space-between;
  padding: 10px 20px;
  transition: background 0.15s;
}
.device-row:hover, .channel-row:hover { background: var(--bg-hover); }
.device-left, .channel-left { display: flex; align-items: center; gap: 10px; }
.device-right { display: flex; align-items: center; gap: 10px; }
.device-name, .channel-name { font-size: 13.5px; font-weight: 600; color: var(--text-primary); }
.device-addr, .channel-ep { font-size: 11px; color: var(--text-muted); margin-top: 2px; }
.dp-count { font-size: 11px; color: var(--text-muted); }
.protocol-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
.empty-hint { text-align: center; padding: 24px; font-size: 13px; color: var(--text-muted); }

@media (max-width: 1200px) {
  .stat-grid { grid-template-columns: repeat(2, 1fr); }
  .bottom-grid { grid-template-columns: 1fr; }
}
</style>
