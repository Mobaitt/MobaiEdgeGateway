<template>
  <div class="devices-view page-enter">
    <PageHeader title="设备管理" desc="管理边缘采集设备及其通信配置">
      <el-button type="primary" :icon="Plus" @click="openCreate">新增设备</el-button>
    </PageHeader>

    <div class="toolbar">
      <el-input
        v-model="searchText"
        placeholder="搜索设备名称 / 编码..."
        prefix-icon="Search"
        clearable
        style="width: 280px"
      />
      <el-select v-model="filterProtocol" placeholder="全部协议" clearable style="width: 160px">
        <el-option
          v-for="o in CollectionProtocolOptions"
          :key="o.value"
          :label="o.label"
          :value="o.value"
        />
      </el-select>
      <el-button :icon="Refresh" circle :loading="refreshing" title="刷新数据" @click="manualRefresh" />
      <span class="total-hint mono">共 {{ filteredDevices.length }} 台设备</span>
      <span v-if="autoRefreshEnabled" class="auto-refresh-hint">自动刷新中</span>
    </div>

    <div v-loading="loading" class="devices-grid">
      <div
        v-for="device in filteredDevices"
        :key="device.id"
        class="device-card"
        :class="{ disabled: !device.isEnabled }"
      >
        <div class="card-head">
          <div class="status-indicator">
            <div class="pulse-dot" :class="getStatusClass(device)" />
            <span class="status-text">{{ getStatusText(device) }}</span>
          </div>
          <el-switch
            :model-value="device.isEnabled"
            size="small"
            active-color="#38dcc4"
            @change="toggleDevice(device)"
          />
        </div>

        <div class="card-body">
          <div class="device-name">{{ device.name }}</div>
          <div class="device-code mono">{{ device.code }}</div>
          <div class="device-protocol">
            <el-tag
              :style="{ background: getProtocolBg(device.protocolValue), color: getProtocolColor(device.protocolValue), border: 'none' }"
              size="small"
            >
              {{ device.protocol }}
            </el-tag>
          </div>
          <div class="device-address mono">
            <el-icon size="12"><Location /></el-icon>
            {{ device.address }}{{ device.port ? `:${device.port}` : '' }}
          </div>
          <div class="device-runtime">
            <span class="runtime-label">运行状态</span>
            <span class="runtime-value">{{ device.runtimeStatusMessage || '未运行' }}</span>
          </div>
          <div v-if="device.currentReconnectRound" class="device-runtime">
            <span class="runtime-label">重连进度</span>
            <span class="runtime-value">
              第 {{ device.currentReconnectRound }} 轮
              <template v-if="device.currentReconnectAttempt">
                / 第 {{ device.currentReconnectAttempt }} 次
              </template>
            </span>
          </div>
          <div v-if="device.lastReadAt" class="device-runtime">
            <span class="runtime-label">最后读取</span>
            <span class="runtime-value">{{ formatDateTime(device.lastReadAt) }}</span>
          </div>
          <div v-if="device.lastError" class="device-runtime runtime-error" :title="device.lastError">
            <span class="runtime-label">异常信息</span>
            <span class="runtime-value">{{ device.lastError }}</span>
          </div>
          <div v-if="device.description" class="device-desc">{{ device.description }}</div>
        </div>

        <div class="card-foot">
          <div class="stats-row">
            <div class="stat-item">
              <el-icon size="14"><Timer /></el-icon>
              <span class="mono">{{ formatInterval(device.pollingIntervalMs) }}</span>
            </div>
            <div class="stat-item">
              <el-icon size="14"><DataLine /></el-icon>
              <span class="mono dp-count">{{ device.dataPointCount }}</span>
              <span class="stat-label">数据点</span>
            </div>
            <div class="stat-item" v-if="typeof device.readFailureRatePercent === 'number'">
              <span class="mono">{{ Number(device.readFailureRatePercent || 0).toFixed(0) }}%</span>
              <span class="stat-label">失败率</span>
            </div>
          </div>
          <div class="foot-actions">
            <el-button size="small" text @click="goDataPoints(device)">
              <el-icon><DataLine /></el-icon>
            </el-button>
            <el-button size="small" text @click="openEdit(device)">
              <el-icon><Edit /></el-icon>
            </el-button>
            <el-button size="small" text type="danger" @click="confirmDelete(device)">
              <el-icon><Delete /></el-icon>
            </el-button>
          </div>
        </div>
      </div>

      <AddCard class="device-card" text="新增设备" @click="openCreate" />
    </div>

    <DeviceDialog
      v-model="dialogVisible"
      :editing-device="editingDevice"
      :protocol-options="CollectionProtocolOptions"
      :submitting="submitting"
      @submit="handleSubmit"
      @close="handleDialogClose"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { DataLine, Delete, Edit, Location, Plus, Refresh, Timer } from '@element-plus/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import AddCard from '@/components/AddCard.vue'
import DeviceDialog from '@/dialogs/device/DeviceDialog.vue'
import { useConfirmDelete } from '@/composables/useConfirmDelete'
import { createDevice, deleteDevice, getDevices, toggleDevice as apiToggle, updateDevice } from '@/api/device'
import { getCollectionProtocols } from '@/api/enums'
import { formatInterval } from '@/api/constants'
import type { DeviceItem } from '@/types'

const router = useRouter()
const { confirm: confirmDeleteFn } = useConfirmDelete()
const loading = ref(false)
const devices = ref<DeviceItem[]>([])
const CollectionProtocolOptions = ref<any[]>([])

const searchText = ref('')
const filterProtocol = ref<number | null>(null)
const dialogVisible = ref(false)
const editingDevice = ref<DeviceItem | null>(null)
const submitting = ref(false)
const autoRefreshEnabled = ref(true)
const refreshing = ref(false)

let refreshTimer: number | null = null
const REFRESH_INTERVAL = 3000

const loadCollectionProtocols = async () => {
  try {
    const res = await getCollectionProtocols()
    CollectionProtocolOptions.value = (res as any).data || []
  } catch (error) {
    console.error('加载采集协议失败:', error)
  }
}

function resolveProtocolValue(device: DeviceItem): number | null {
  const d = device as any
  if (typeof d.protocolValue === 'number') return d.protocolValue
  if (typeof d.protocol === 'number') return d.protocol
  if (typeof d.protocol === 'string') {
    const opt = CollectionProtocolOptions.value.find((o) => o.label === d.protocol)
    return opt ? opt.value : null
  }
  return null
}

const filteredDevices = computed(() => {
  const filterVal = filterProtocol.value
  return devices.value.filter((device) => {
    const matchText =
      !searchText.value ||
      device.name.includes(searchText.value) ||
      (device.code && device.code.includes(searchText.value))
    const deviceProtocolNum = resolveProtocolValue(device)
    const matchProtocol = !filterVal || deviceProtocolNum === filterVal
    return matchText && matchProtocol
  })
})

const getProtocolColor = (value: number) => {
  const protocol = CollectionProtocolOptions.value.find(p => p.value === value)
  return protocol ? '#38dcc4' : '#8fa5c5'
}

const getProtocolBg = (value: number) => {
  const protocol = CollectionProtocolOptions.value.find(p => p.value === value)
  return protocol ? `${protocol.color}22` : '#8fa5c522'
}

const fetchDevices = async () => {
  loading.value = true
  try {
    const res = await getDevices()
    devices.value = ((res as { data?: DeviceItem[] })?.data ?? []) as DeviceItem[]
  } finally {
    loading.value = false
  }
}

const startAutoRefresh = () => {
  if (!autoRefreshEnabled.value) return
  refreshTimer = window.setInterval(async () => {
    if (refreshing.value) return
    refreshing.value = true
    try {
      await fetchDevices()
    } catch (error) {
      console.error('自动刷新失败:', error)
    } finally {
      refreshing.value = false
    }
  }, REFRESH_INTERVAL)
}

const stopAutoRefresh = () => {
  if (refreshTimer) {
    clearInterval(refreshTimer)
    refreshTimer = null
  }
}

const manualRefresh = async () => {
  refreshing.value = true
  try {
    await fetchDevices()
    ElMessage.success('数据已刷新')
  } catch {
    ElMessage.error('刷新失败')
  } finally {
    refreshing.value = false
  }
}

const openCreate = () => {
  editingDevice.value = null
  dialogVisible.value = true
}

const openEdit = (row: DeviceItem) => {
  editingDevice.value = row
  dialogVisible.value = true
}

const handleSubmit = async (data: any) => {
  submitting.value = true
  try {
    if (editingDevice.value) {
      await updateDevice(editingDevice.value.id, data)
      ElMessage.success('设备更新成功')
    } else {
      await createDevice(data)
      ElMessage.success('设备创建成功')
    }
    dialogVisible.value = false
    await fetchDevices()
  } catch (error: any) {
    ElMessage.error(`操作失败：${error.message || '未知错误'}`)
  } finally {
    submitting.value = false
  }
}

const confirmDelete = (row: DeviceItem) => {
  confirmDeleteFn({
    message: `确定要删除设备“${row.name}”吗？此操作将同时移除相关数据点和映射关系。`,
    onConfirm: async () => {
      await deleteDevice(row.id)
      await fetchDevices()
    }
  })
}

const toggleDevice = async (row: DeviceItem) => {
  try {
    await apiToggle(row.id)
    row.isEnabled = !row.isEnabled
    ElMessage.success(row.isEnabled ? '设备已启用' : '设备已禁用')
    await fetchDevices()
  } catch (e: any) {
    ElMessage.error(`操作失败：${e.message || '未知错误'}`)
  }
}

const goDataPoints = (row: DeviceItem) => {
  router.push({ name: 'DataPoints', params: { id: row.id }, query: { deviceName: row.name } })
}

const handleDialogClose = () => {
  editingDevice.value = null
}

const getStatusClass = (device: DeviceItem) => {
  if (!device.isEnabled) return 'offline'

  switch (device.runtimeStatus) {
    case 'running':
      return 'online'
    case 'reconnecting':
      return 'warning'
    case 'warning':
    case 'error':
      return 'error'
    default:
      return device.isConnected ? 'online' : 'offline'
  }
}

const getStatusText = (device: DeviceItem) => {
  if (!device.isEnabled) return '已禁用'

  switch (device.runtimeStatus) {
    case 'running':
      return '运行中'
    case 'reconnecting':
      return '重连中'
    case 'warning':
      return '异常告警'
    case 'error':
      return '连接异常'
    default:
      return device.runtimeStatusMessage || '未运行'
  }
}

const formatDateTime = (value?: string | null) => {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`
}

onMounted(() => {
  fetchDevices()
  loadCollectionProtocols()
  startAutoRefresh()
})

onUnmounted(() => {
  stopAutoRefresh()
})
</script>

<style scoped lang="scss">
.toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 20px;
}

.total-hint {
  font-size: 12px;
  color: var(--text-muted);
  margin-left: auto;
}

.auto-refresh-hint {
  font-size: 12px;
  color: var(--text-success);
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.devices-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 20px;
}

.device-card {
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 14px;
  transition: all 0.25s ease;
  position: relative;
  overflow: hidden;

  &::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    height: 3px;
    background: linear-gradient(90deg, transparent, var(--cyan), transparent);
    opacity: 0;
    transition: opacity 0.25s;
  }

  &:hover {
    border-color: var(--cyan);
    box-shadow: 0 8px 32px rgba(0, 255, 255, 0.1);
    transform: translateY(-2px);
  }

  &:hover::before {
    opacity: 1;
  }

  &.disabled {
    opacity: 0.5;
    filter: grayscale(0.3);

    &:hover {
      transform: none;
    }
  }
}

.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.status-indicator {
  display: flex;
  align-items: center;
  gap: 8px;
}

.status-text {
  font-size: 12px;
  color: var(--text-secondary);
  font-weight: 500;
}

.pulse-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #8fa5c5;

  &.online {
    background: #38dcc4;
    box-shadow: 0 0 0 4px rgba(56, 220, 196, 0.18);
  }

  &.offline {
    background: #8fa5c5;
  }

  &.warning {
    background: #f2b24d;
    box-shadow: 0 0 0 4px rgba(242, 178, 77, 0.18);
  }

  &.error {
    background: #ef6b73;
    box-shadow: 0 0 0 4px rgba(239, 107, 115, 0.18);
  }
}

.card-body {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 8px 0;
}

.device-name {
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
  letter-spacing: 0.02em;
}

.device-code {
  font-size: 11px;
  color: var(--text-muted);
  font-family: var(--font-mono);
  background: var(--bg-base);
  padding: 3px 8px;
  border-radius: 4px;
  align-self: flex-start;
}

.device-protocol {
  align-self: flex-start;
}

.device-address {
  font-size: 12px;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  gap: 6px;
  word-break: break-all;

  .el-icon {
    flex-shrink: 0;
  }
}

.device-runtime {
  display: flex;
  gap: 8px;
  font-size: 12px;
  line-height: 1.5;
}

.runtime-label {
  color: var(--text-muted);
  flex: 0 0 64px;
}

.runtime-value {
  color: var(--text-secondary);
  word-break: break-all;
}

.runtime-error .runtime-value {
  color: #ef6b73;
}

.device-desc {
  font-size: 12px;
  color: var(--text-muted);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.card-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-top: 1px solid var(--border-subtle);
  padding-top: 12px;
  margin-top: 4px;
}

.stats-row {
  display: flex;
  align-items: center;
  gap: 16px;
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: 12px;
  color: var(--text-secondary);

  .mono {
    font-size: 14px;
    font-weight: 600;
    color: var(--cyan);
  }
}

.stat-label {
  font-size: 11px;
  color: var(--text-muted);
}

.dp-count {
  font-size: 15px;
  font-weight: 700;
  color: var(--text-accent);
}

.foot-actions {
  display: flex;
  align-items: center;
  gap: 4px;

  .el-button {
    font-size: 12px;
    padding: 4px 8px;
  }
}

.device-card.add-card {
  min-height: 200px;
}
</style>
