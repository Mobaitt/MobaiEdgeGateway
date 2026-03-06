<template>
  <div class="devices-view page-enter">
    <PageHeader title="设备管理" desc="管理边缘采集设备及其通信配置">
      <el-button type="primary" :icon="Plus" @click="openCreate">新增设备</el-button>
    </PageHeader>

    <!-- 工具栏 -->
    <div class="toolbar">
      <el-input
        v-model="searchText" placeholder="搜索设备名称 / 编码..."
        prefix-icon="Search" clearable style="width: 280px"
      />
      <el-select v-model="filterProtocol" placeholder="全部协议" clearable style="width: 160px">
        <el-option
          v-for="o in CollectionProtocolOptions"
          :key="o.value" :label="o.label" :value="o.value"
        />
      </el-select>
      <el-button :icon="Refresh" circle @click="manualRefresh" :loading="refreshing" title="刷新数据" />
      <span class="total-hint mono">共{{ filteredDevices.length }} 台设备</span>
      <span v-if="autoRefreshEnabled" class="auto-refresh-hint">· 自动刷新中</span>
    </div>

    <!-- 设备卡片列表 -->
    <div v-loading="loading" class="devices-grid">
      <div
        v-for="device in filteredDevices" :key="device.id"
        class="device-card"
        :class="{ disabled: !device.isEnabled }"
      >
        <!-- 卡片顶部：状态开关 -->
        <div class="card-head">
          <div class="status-indicator">
            <div class="pulse-dot" :class="device.isEnabled ? 'online' : 'offline'" />
            <span class="status-text">{{ device.isEnabled ? '运行中' : '已禁用' }}</span>
          </div>
          <el-switch
            :model-value="device.isEnabled"
            size="small" active-color="#38dcc4"
            @change="toggleDevice(device)"
          />
        </div>

        <!-- 卡片主体：设备信息 -->
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
          <div v-if="device.description" class="device-desc">{{ device.description }}</div>
        </div>

        <!-- 卡片底部：统计和操作 -->
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

    <!-- 新增/编辑设备弹窗 -->
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
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Plus, Edit, Delete, DataLine, Location, Timer, InfoFilled, Refresh } from '@element-plus/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import AddCard from '@/components/AddCard.vue'
import DeviceDialog from '@/dialogs/device/DeviceDialog.vue'
import { useConfirmDelete } from '@/composables/useConfirmDelete'
import {
  getDevices,
  createDevice,
  updateDevice,
  deleteDevice,
  toggleDevice as apiToggle
} from '@/api/device'
import { getCollectionProtocols } from '@/api/enums'
import { formatInterval } from '@/api/constants'
import { CollectionProtocol } from '@/api/constants'
import type { DeviceItem } from '@/types'

const router = useRouter()
const { confirm: confirmDeleteFn } = useConfirmDelete()
const loading = ref(false)
const devices = ref<DeviceItem[]>([])
const CollectionProtocolOptions = ref<any[]>([])

// 加载采集协议选项
const loadCollectionProtocols = async () => {
  try {
    const res = await getCollectionProtocols()
    CollectionProtocolOptions.value = (res as any).data || []
  } catch (error) {
    console.error('加载采集协议失败:', error)
  }
}

const searchText = ref('')
const filterProtocol = ref<number | null>(null)

/** 从设备数据解析出协议数值 */
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

const dialogVisible = ref(false)
const editingDevice = ref<DeviceItem | null>(null)
const submitting = ref(false)

const fetchDevices = async () => {
  loading.value = true
  try {
    const res = await getDevices()
    devices.value = ((res as { data?: DeviceItem[] })?.data ?? []) as DeviceItem[]
  } finally {
    loading.value = false
  }
}

// 自动轮询机制
const autoRefreshEnabled = ref(true)
const refreshing = ref(false)
let refreshTimer: number | null = null
const REFRESH_INTERVAL = 3000

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
  } catch (error) {
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
    fetchDevices()
  } catch (error: any) {
    ElMessage.error(`操作失败：${error.message || '未知错误'}`)
  } finally {
    submitting.value = false
  }
}

const confirmDelete = (row: DeviceItem) => {
  confirmDeleteFn({
    message: `确定要删除设备 "${row.name}" 吗？此操作将同时移除所有相关的数据点和映射关系。`,
    onConfirm: async () => {
      await deleteDevice(row.id)
      fetchDevices()
    }
  })
}

const toggleDevice = async (row: DeviceItem) => {
  try {
    await apiToggle(row.id)
    row.isEnabled = !row.isEnabled
    ElMessage.success(row.isEnabled ? '设备已启用' : '设备已禁用')
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
/* 工具栏 */
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

/* 卡片网格 */
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

/* 卡片头部 */
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

/* 卡片主体 */
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

.device-desc {
  font-size: 12px;
  color: var(--text-muted);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

/* 卡片底部 */
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
