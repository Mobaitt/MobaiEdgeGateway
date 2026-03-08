<template>
  <div class="dashboard page-enter">
    <PageHeader title="系统总览">
      <el-button size="small" :loading="loading" @click="refresh" :icon="Refresh">刷新</el-button>
    </PageHeader>

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

    <!-- 中间统计行 -->
    <div class="mid-grid">
      <!-- 设备协议分布 -->
      <div class="panel">
        <div class="panel-header">
          <span class="panel-title"><el-icon><Monitor /></el-icon> 设备协议分布</span>
        </div>
        <div class="protocol-dist">
          <div v-for="item in protocolDistribution" :key="item.name" class="dist-item">
            <div class="dist-left">
              <div class="dist-dot" :style="{ background: item.color }" />
              <span class="dist-name">{{ item.name }}</span>
            </div>
            <div class="dist-right">
              <span class="dist-value mono">{{ item.count }} 台</span>
              <div class="dist-bar-bg">
                <div class="dist-bar" :style="{ width: item.percentage + '%', background: item.color }" />
              </div>
            </div>
          </div>
          <div v-if="protocolDistribution.length === 0" class="empty-hint">暂无设备数据</div>
        </div>
      </div>

      <!-- 通道协议分布 -->
      <div class="panel">
        <div class="panel-header">
          <span class="panel-title"><el-icon><Share /></el-icon> 发送通道分布</span>
        </div>
        <div class="protocol-dist">
          <div v-for="item in channelDistribution" :key="item.name" class="dist-item">
            <div class="dist-left">
              <div class="dist-dot" :style="{ background: item.color }" />
              <span class="dist-name">{{ item.name }}</span>
            </div>
            <div class="dist-right">
              <span class="dist-value mono">{{ item.count }} 个</span>
              <div class="dist-bar-bg">
                <div class="dist-bar" :style="{ width: item.percentage + '%', background: item.color }" />
              </div>
            </div>
          </div>
          <div v-if="channelDistribution.length === 0" class="empty-hint">暂无通道数据</div>
        </div>
      </div>

      <!-- 数据点类型分布 -->
      <div class="panel">
        <div class="panel-header">
          <span class="panel-title"><el-icon><DataLine /></el-icon> 数据点类型分布</span>
        </div>
        <div class="protocol-dist">
          <div v-for="item in dataTypeDistribution" :key="item.name" class="dist-item">
            <div class="dist-left">
              <div class="dist-dot" :style="{ background: item.color }" />
              <span class="dist-name">{{ item.name }}</span>
            </div>
            <div class="dist-right">
              <span class="dist-value mono">{{ item.count }} 个</span>
              <div class="dist-bar-bg">
                <div class="dist-bar" :style="{ width: item.percentage + '%', background: item.color }" />
              </div>
            </div>
          </div>
          <div v-if="dataTypeDistribution.length === 0" class="empty-hint">暂无数据点</div>
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
import {computed, onMounted, ref} from 'vue'
import {DataLine, Monitor, Refresh, Share} from '@element-plus/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {getGatewayStatus} from '@/api/gateway'
import {getDevices} from '@/api/device'
import {getChannels} from '@/api/channel'
import {CollectionProtocol, DataValueType, SendProtocol} from '@/api/constants'
import type {GatewayStatus} from '@/types'

const loading = ref(false)
const status = ref<GatewayStatus>({})
const allDevices = ref<Array<{ id: number; name: string; address: string; protocol: string; protocolValue: number; isEnabled: boolean; dataPointCount: number }>>([])
const allChannels = ref<Array<{ id: number; name: string; endpoint: string; protocolValue: number; isEnabled: boolean }>>([])
const allDataPoints = ref<Array<{ id: number; dataType: string | number; dataTypeValue: number }>>([])

// 显示前几条数据
const devices = computed(() => allDevices.value.slice(0, 6))
const channels = computed(() => allChannels.value.slice(0, 5))

// 统计卡片数据
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
    value: allDevices.value.length,
    color: '#4299e1',
    bg: 'rgba(66,153,225,0.1)',
    badge: 'info',
    sub: `${allDevices.value.filter(d => d.isEnabled).length} 台启用`
  },
  {
    key: 'channels',
    label: '通道总数',
    icon: 'Share',
    value: allChannels.value.length,
    color: '#38dcc4',
    bg: 'rgba(56,220,196,0.1)',
    badge: 'info',
    sub: `${allChannels.value.filter(c => c.isEnabled).length} 个启用`
  },
  {
    key: 'datapoints',
    label: '数据点总数',
    icon: 'DataLine',
    value: allDataPoints.value.length,
    color: '#9f7aea',
    bg: 'rgba(159,122,234,0.1)',
    badge: 'info',
    sub: `${allDataPoints.value.length} 采集点`
  },
  {
    key: 'enabledDevices',
    label: '运行中设备',
    icon: 'Cpu',
    value: allDevices.value.filter(d => d.isEnabled).length,
    color: '#4ade80',
    bg: 'rgba(74,222,128,0.1)',
    badge: 'good',
    sub: `${Math.round((allDevices.value.filter(d => d.isEnabled).length / (allDevices.value.length || 1)) * 100)}% 运行率`
  },
  {
    key: 'enabledChannels',
    label: '活跃通道',
    icon: 'TrendCharts',
    value: allChannels.value.filter(c => c.isEnabled).length,
    color: '#f6ad55',
    bg: 'rgba(246,173,85,0.1)',
    badge: 'good',
    sub: `${Math.round((allChannels.value.filter(c => c.isEnabled).length / (allChannels.value.length || 1)) * 100)}% 活跃率`
  }
])

// 设备协议分布
const protocolDistribution = computed(() => {
  const dist: Record<string, { name: string; count: number; color: string }> = {}
  allDevices.value.forEach(device => {
    // device.protocol 是字符串如 "Simulator"
    const protoName = device.protocol || '未知'
    // 使用 CollectionProtocol 查找
    const proto = Object.values(CollectionProtocol).find(p => p.label === protoName)
    const color = proto?.color || '#8fa5c5'
    if (!dist[protoName]) {
      dist[protoName] = { name: protoName, count: 0, color }
    }
    dist[protoName].count++
  })
  const total = allDevices.value.length || 1
  return Object.values(dist)
    .map(item => ({ ...item, percentage: Math.round((item.count / total) * 100) }))
    .sort((a, b) => b.count - a.count)
})

// 通道协议分布
const channelDistribution = computed(() => {
  const dist: Record<string, { name: string; count: number; color: string }> = {}
  allChannels.value.forEach(channel => {
    const proto = Object.values(SendProtocol).find(p => p.value === channel.protocolValue)
    const name = proto?.label || '未知'
    const color = proto?.color || '#8fa5c5'
    if (!dist[name]) {
      dist[name] = { name, count: 0, color }
    }
    dist[name].count++
  })
  const total = allChannels.value.length || 1
  return Object.values(dist)
    .map(item => ({ ...item, percentage: Math.round((item.count / total) * 100) }))
    .sort((a, b) => b.count - a.count)
})

// 数据点类型分布
const dataTypeDistribution = computed(() => {
  const dist: Record<string, { name: string; count: number; color: string }> = {}
  const colors = ['#4299e1', '#38dcc4', '#4ade80', '#f6ad55', '#f05f6e', '#9f7aea', '#ed64a6']
  allDataPoints.value.forEach((dp, index) => {
    const dataType = Object.values(DataValueType).find(t => (t as any).value === dp.dataTypeValue)
    const name = (dataType as any)?.label || '未知'
    const color = colors[index % colors.length]
    if (!dist[name]) {
      dist[name] = { name, count: 0, color }
    }
    dist[name].count++
  })
  const total = allDataPoints.value.length || 1
  return Object.values(dist)
    .map(item => ({ ...item, percentage: Math.round((item.count / total) * 100) }))
    .sort((a, b) => b.count - a.count)
})

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

    status.value = ((statusRes as { data?: GatewayStatus })?.data ?? {}) as GatewayStatus
    allDevices.value = ((deviceRes as { data?: unknown[] })?.data ?? []) as typeof allDevices.value
    
    // 获取所有数据点
    const dataPointPromises = allDevices.value.map(device => 
      fetch(`/api/devices/${device.id}/datapoints`).then(res => res.json())
    )
    const dataPointResults = await Promise.all(dataPointPromises)
    allDataPoints.value = dataPointResults.flatMap((res: any) => res.data || [])
    
    allChannels.value = ((channelRes as { data?: unknown[] })?.data ?? []) as typeof allChannels.value
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<style scoped>
/* 统计卡片 */
.stat-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
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

/* 中间统计行 */
.mid-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

/* 分布面板 */
.protocol-dist { padding: 8px 0; }
.dist-item {
  display: flex; align-items: center; justify-content: space-between;
  padding: 10px 20px;
  transition: background 0.15s;
}
.dist-item:hover { background: var(--bg-hover); }
.dist-left { display: flex; align-items: center; gap: 10px; }
.dist-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
.dist-name { font-size: 13px; font-weight: 500; color: var(--text-primary); }
.dist-right { display: flex; align-items: center; gap: 12px; }
.dist-value { font-size: 13px; font-weight: 600; color: var(--text-primary); min-width: 50px; text-align: right; }
.dist-bar-bg {
  width: 100px; height: 6px; border-radius: 3px;
  background: var(--bg-base); overflow: hidden;
}
.dist-bar { height: 100%; border-radius: 3px; transition: width 0.3s; }

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

@media (max-width: 1400px) {
  .stat-grid { grid-template-columns: repeat(3, 1fr); }
  .mid-grid { grid-template-columns: 1fr; }
}
@media (max-width: 1200px) {
  .stat-grid { grid-template-columns: repeat(2, 1fr); }
  .bottom-grid { grid-template-columns: 1fr; }
}
</style>
