<template>
  <div class="datapoints-view page-content page-enter">
    <div class="page-header">
      <div class="header-left">
        <el-button text :icon="ArrowLeft" class="back-btn" @click="router.back()">返回设备列表</el-button>
        <div class="title-block">
          <h1 class="page-title">数据点管理</h1>
          <div class="device-tag mono">{{ route.query.deviceName || `设备 #${route.params.id}` }}</div>
          <el-tag v-if="deviceEnabled" size="small" type="success">采集中</el-tag>
          <el-tag v-else size="small" type="info">已停止</el-tag>
        </div>
      </div>
    </div>

    <div class="stats-bar">
      <div class="stat-item">
        <span class="s-num mono">{{ dataPoints.length }}</span>
        <span class="s-label">总点位</span>
      </div>
      <div class="stat-item">
        <span class="s-num mono success">{{ enabledCount }}</span>
        <span class="s-label">已启用</span>
      </div>
      <div class="stat-item">
        <span class="s-num mono muted">{{ dataPoints.length - enabledCount }}</span>
        <span class="s-label">已禁用</span>
      </div>
      <div class="stat-item tail">
        <span class="s-num mono cyan">{{ lastUpdateTime ? formatDateTime(lastUpdateTime) : '-' }}</span>
        <span class="s-label">最后更新</span>
      </div>
    </div>

    <div class="main-content">
      <div class="toolbar">
        <div class="toolbar-left">
          <el-input
            v-model="searchText"
            placeholder="搜索 Tag / 名称 / 地址..."
            clearable
            style="width: 280px"
            @change="handleFilterChange"
          />
          <el-select v-model="filterDataType" clearable placeholder="数据类型" style="width: 140px" @change="handleFilterChange">
            <el-option v-for="item in DataValueTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
          </el-select>
          <el-select v-model="filterQuality" clearable placeholder="数据质量" style="width: 140px">
            <el-option label="Good" value="Good" />
            <el-option label="Bad" value="Bad" />
            <el-option label="Uncertain" value="Uncertain" />
          </el-select>
          <el-select v-model="filterType" clearable placeholder="点位类型" style="width: 140px">
            <el-option label="全部" value="" />
            <el-option label="普通" value="normal" />
            <el-option label="虚拟" value="virtual" />
          </el-select>
        </div>
        <div class="toolbar-right">
          <el-button :icon="Refresh" circle :loading="refreshing" @click="refreshData" />
          <el-dropdown split-button type="primary" @click="openCreate">
            <el-icon><Plus /></el-icon>
            新增数据点
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item @click="openCreate">普通数据点</el-dropdown-item>
                <el-dropdown-item @click="openVirtualNodeCreate">
                  <el-icon><Cpu /></el-icon>
                  虚拟节点
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </div>

      <div class="table-wrap">
        <el-table :data="filteredDataPoints" v-loading="loading" row-key="id">
          <el-table-column type="index" label="#" width="50" align="center" />

          <el-table-column prop="tag" label="Tag" min-width="220">
            <template #default="{ row }">
              <span class="mono tag-text">{{ row.tag }}</span>
              <el-tag v-if="row.isVirtual" size="small" type="warning" style="margin-left: 6px">虚拟</el-tag>
            </template>
          </el-table-column>

          <el-table-column prop="name" label="名称" width="140" />

          <el-table-column prop="address" label="地址" width="120">
            <template #default="{ row }">
              <span v-if="!row.isVirtual" class="mono addr-text">{{ row.address }}</span>
              <span v-else class="addr-text">表达式</span>
            </template>
          </el-table-column>

          <el-table-column prop="dataType" label="类型" width="110" align="center">
            <template #default="{ row }">
              <span class="badge info mono">{{ getDataTypeLabel(row.dataType) }}</span>
            </template>
          </el-table-column>

          <el-table-column prop="unit" label="单位" width="80" align="center" />

          <el-table-column label="实时值" width="150" align="center">
            <template #default="{ row }">
              <span
                v-if="getRealtimeData(row)"
                class="mono realtime-value"
                :class="getQualityClass(getRealtimeData(row)!.quality)"
              >
                {{ formatRowValue(row) }}
                <span v-if="row.unit" class="value-unit">{{ row.unit }}</span>
              </span>
              <span v-else class="empty-text">-</span>
            </template>
          </el-table-column>

          <el-table-column label="质量" width="100" align="center">
            <template #default="{ row }">
              <span v-if="getRealtimeData(row)" class="badge mono" :class="getQualityClass(getRealtimeData(row)!.quality)">
                {{ getRealtimeData(row)!.quality }}
              </span>
              <span v-else class="empty-text">-</span>
            </template>
          </el-table-column>

          <el-table-column label="启用" width="80" align="center">
            <template #default="{ row }">
              <el-switch
                v-model="row.isEnabled"
                size="small"
                active-color="#38dcc4"
                inactive-color="#999"
                @change="row.isVirtual ? toggleVirtualNode(row) : toggleDataPoint(row)"
              />
            </template>
          </el-table-column>

          <el-table-column prop="createdAt" label="创建时间" width="180">
            <template #default="{ row }">
              <span class="mono time-text">{{ formatDateTime(row.createdAt) }}</span>
            </template>
          </el-table-column>

          <el-table-column label="操作" width="240" align="right" fixed="right">
            <template #default="{ row }">
              <el-button
                v-if="!row.isVirtual"
                size="small"
                text
                type="warning"
                :disabled="!row.isEnabled || !deviceEnabled"
                @click="openControl(row)"
              >
                控制
              </el-button>
              <el-button v-if="row.isVirtual" size="small" text type="success" @click="openVirtualNodeEdit(row)">编辑</el-button>
              <el-button v-else size="small" text type="primary" @click="openEdit(row)">编辑</el-button>
              <el-button size="small" text type="danger" @click="row.isVirtual ? confirmDeleteVirtualNode(row) : confirmDelete(row)">删除</el-button>
            </template>
          </el-table-column>
        </el-table>

        <div class="pagination-bar">
          <el-pagination
            v-model:current-page="pagination.page"
            v-model:page-size="pagination.pageSize"
            :page-sizes="[20, 50, 100, 200]"
            :total="pagination.total"
            layout="total, sizes, prev, pager, next, jumper"
            @size-change="fetchDataPoints"
            @current-change="fetchDataPoints"
          />
        </div>
      </div>
    </div>

    <DataPointDialog
      v-model="dialogVisible"
      :editing-data-point="editingDataPoint"
      :device-code="deviceCode"
      :device-protocol="deviceProtocol"
      :data-type-options="DataValueTypeOptions"
      :byte-order-options="ModbusByteOrderOptions"
      :submitting="submitting"
      @submit="handleSubmit"
      @close="handleDialogClose"
    />

    <VirtualNodeDialog
      v-model="virtualNodeDialogVisible"
      :editing-virtual-node="editingVirtualNode"
      :device-code="deviceCode"
      :data-type-options="DataValueTypeOptions"
      :submitting="virtualNodeSubmitting"
      @submit="handleVirtualSubmit"
      @close="handleVirtualDialogClose"
    />

    <DataPointControlDialog
      v-model="controlDialogVisible"
      :data-point="controllingDataPoint"
      :current-value="controllingCurrentValue"
      :submitting="controlSubmitting"
      @submit="handleControlSubmit"
      @close="handleControlClose"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft, Cpu, Plus, Refresh } from '@element-plus/icons-vue'
import {
  createDataPoint,
  controlDataPoint,
  deleteDataPoint,
  getDataPointsPaged,
  getDevice,
  getDeviceRealtimeData,
  toggleDataPoint as apiToggleDataPoint,
  updateDataPoint
} from '@/api/device'
import {
  createVirtualDataPoint,
  deleteVirtualDataPoint,
  getVirtualDataPointsByDevice,
  updateVirtualDataPoint
} from '@/api/virtualNode'
import { getDataValueTypes, getModbusByteOrders } from '@/api/enums'
import { formatDateTime } from '@/api/constants'
import type { DataPointItem, RealtimeDataItem } from '@/types'
import type { VirtualDataPoint } from '@/types/virtualNode'
import DataPointDialog from '@/dialogs/dataPoint/DataPointDialog.vue'
import DataPointControlDialog from '@/dialogs/dataPoint/DataPointControlDialog.vue'
import VirtualNodeDialog from '@/dialogs/dataPoint/VirtualNodeDialog.vue'

type DataPointWithVirtual = (DataPointItem | VirtualDataPoint) & { isVirtual?: boolean }

type DataPointForm = {
  name: string
  tag: string
  description: string
  address: string
  dataType: number | null
  unit: string
  isEnabled: boolean
  modbusSlaveId: number
  modbusFunctionCode: number
  modbusByteOrder: number
  registerLength: number
}

type VirtualNodeForm = {
  deviceId: number
  name: string
  tag: string
  description: string
  expression: string
  calculationType: number
  dataType: number
  unit: string
  isEnabled: boolean
}

const route = useRoute()
const router = useRouter()
const deviceId = computed(() => Number(route.params.id))

const DataValueTypeOptions = ref<any[]>([])
const ModbusByteOrderOptions = ref<any[]>([])
const loading = ref(false)
const refreshing = ref(false)
const dataPoints = ref<DataPointWithVirtual[]>([])
const deviceEnabled = ref(false)
const deviceCode = ref('')
const deviceProtocol = ref<number | null>(null)
const searchText = ref('')
const filterDataType = ref<number | null>(null)
const filterQuality = ref<string | null>(null)
const filterType = ref<string>('')
const lastUpdateTime = ref<Date | null>(null)
const realtimeData = ref<Record<string, RealtimeDataItem>>({})
let realtimeTimer: number | null = null

const pagination = ref({
  page: 1,
  pageSize: 50,
  total: 0
})

const dialogVisible = ref(false)
const submitting = ref(false)
const editingDataPoint = ref<DataPointItem | null>(null)

const virtualNodeDialogVisible = ref(false)
const editingVirtualNode = ref<VirtualDataPoint | null>(null)
const virtualNodeSubmitting = ref(false)

const controlDialogVisible = ref(false)
const controllingDataPoint = ref<DataPointItem | null>(null)
const controlSubmitting = ref(false)

const enabledCount = computed(() => dataPoints.value.filter(item => item.isEnabled).length)

const filteredDataPoints = computed(() => {
  return dataPoints.value.filter(item => {
    if (filterType.value === 'normal' && item.isVirtual) return false
    if (filterType.value === 'virtual' && !item.isVirtual) return false

    if (filterDataType.value !== null && getRowDataTypeValue(item) !== filterDataType.value) {
      return false
    }

    if (filterQuality.value) {
      const quality = getRealtimeData(item)?.quality
      if (quality !== filterQuality.value) return false
    }

    return true
  })
})

const controllingCurrentValue = computed(() => {
  if (!controllingDataPoint.value) return undefined
  return getRealtimeData(controllingDataPoint.value)?.value
})

const loadDataValueTypes = async () => {
  const res = await getDataValueTypes()
  DataValueTypeOptions.value = (res as any).data || []
}

const loadModbusByteOrders = async () => {
  const res = await getModbusByteOrders()
  ModbusByteOrderOptions.value = (res as any).data || []
}

const fetchDevice = async () => {
  const res = await getDevice(deviceId.value)
  const device = (res as { data?: any }).data
  if (!device) return

  deviceEnabled.value = device.isEnabled
  deviceProtocol.value = device.protocolValue ?? device.protocol ?? null
  deviceCode.value = device.code ?? ''
}

const fetchDataPoints = async () => {
  loading.value = true
  try {
    const res = await getDataPointsPaged(deviceId.value, {
      page: pagination.value.page,
      pageSize: pagination.value.pageSize,
      search: searchText.value || undefined,
      dataType: filterDataType.value ?? undefined
    })

    const responseData = (res as { data?: { items: DataPointItem[]; total: number } }).data
    const normalPoints = (responseData?.items || []).map(item => ({ ...item, isVirtual: false })) as DataPointWithVirtual[]
    const virtualPoints = await loadVirtualDataPoints()

    dataPoints.value = [...normalPoints, ...virtualPoints]
    pagination.value.total = (responseData?.total || 0) + virtualPoints.length
  } finally {
    loading.value = false
  }
}

const loadVirtualDataPoints = async (): Promise<DataPointWithVirtual[]> => {
  const res = await getVirtualDataPointsByDevice(deviceId.value)
  return (((res as { data?: VirtualDataPoint[] }).data) || []).map(item => ({
    ...item,
    isVirtual: true
  })) as DataPointWithVirtual[]
}

const fetchRealtimeData = async () => {
  const res = await getDeviceRealtimeData(deviceId.value)
  const dataList = ((res as { data?: RealtimeDataItem[] }).data || []) as RealtimeDataItem[]

  const snapshot = { ...realtimeData.value }
  let changed = false

  dataList.forEach(item => {
    if (!item.tag) return

    const old = snapshot[item.tag]
    if (!old || old.value !== item.value || old.quality !== item.quality || old.timestamp !== item.timestamp) {
      snapshot[item.tag] = item
      changed = true
    }
  })

  if (changed) {
    realtimeData.value = snapshot
    lastUpdateTime.value = new Date()
  }
}

const startRealtimePolling = () => {
  void fetchRealtimeData()
  realtimeTimer = window.setInterval(() => {
    void fetchRealtimeData()
  }, 500)
}

const stopRealtimePolling = () => {
  if (realtimeTimer) {
    clearInterval(realtimeTimer)
    realtimeTimer = null
  }
}

const refreshData = async () => {
  refreshing.value = true
  try {
    pagination.value.page = 1
    await fetchDataPoints()
    await fetchRealtimeData()
    ElMessage.success('数据已刷新')
  } finally {
    refreshing.value = false
  }
}

const handleFilterChange = () => {
  pagination.value.page = 1
  void fetchDataPoints()
}

const getRealtimeData = (row: DataPointWithVirtual): RealtimeDataItem | null => realtimeData.value[row.tag] || null

const getQualityClass = (quality: string) => {
  if (quality === 'Good') return 'good'
  if (quality === 'Bad') return 'bad'
  if (quality === 'Uncertain') return 'uncertain'
  return ''
}

const getDataTypeLabel = (dataType: string | number) => {
  if (typeof dataType === 'number') {
    const option = DataValueTypeOptions.value.find(item => item.value === dataType)
    return option?.label || String(dataType)
  }
  return dataType
}

const getRowDataTypeValue = (item: DataPointWithVirtual) => {
  return 'dataTypeValue' in item && typeof item.dataTypeValue === 'number'
    ? item.dataTypeValue
    : Number(item.dataType)
}

const formatRowValue = (row: DataPointWithVirtual) => {
  const data = getRealtimeData(row)
  if (!data || data.value === null || data.value === undefined) return '-'
  return String(data.value)
}

const toggleDataPoint = async (row: DataPointItem) => {
  try {
    await apiToggleDataPoint(deviceId.value, row.id, row.isEnabled)
    ElMessage.success(row.isEnabled ? '数据点已启用' : '数据点已禁用')
  } catch (error: any) {
    row.isEnabled = !row.isEnabled
    ElMessage.error(error.message || '操作失败')
  }
}

const openCreate = () => {
  editingDataPoint.value = null
  dialogVisible.value = true
}

const openEdit = (row: DataPointItem) => {
  editingDataPoint.value = row
  dialogVisible.value = true
}

const openControl = (row: DataPointWithVirtual) => {
  if (row.isVirtual) return
  controllingDataPoint.value = row as DataPointItem
  controlDialogVisible.value = true
}

const handleSubmit = async (data: DataPointForm) => {
  submitting.value = true
  try {
    if (editingDataPoint.value) {
      await updateDataPoint(deviceId.value, editingDataPoint.value.id, data)
      ElMessage.success('数据点更新成功')
    } else {
      await createDataPoint(deviceId.value, data)
      ElMessage.success('数据点创建成功')
    }

    dialogVisible.value = false
    await fetchDataPoints()
  } catch (error: any) {
    ElMessage.error(error.message || '操作失败')
  } finally {
    submitting.value = false
  }
}

const handleControlSubmit = async (value: unknown) => {
  if (!controllingDataPoint.value) return

  controlSubmitting.value = true
  try {
    const res = await controlDataPoint(deviceId.value, controllingDataPoint.value.id, value)
    const latest = (res as { data?: RealtimeDataItem }).data

    if (latest?.tag) {
      realtimeData.value = {
        ...realtimeData.value,
        [latest.tag]: latest
      }
      lastUpdateTime.value = new Date()
    } else {
      await fetchRealtimeData()
    }

    controlDialogVisible.value = false
    ElMessage.success('点位控制成功')
  } catch (error: any) {
    ElMessage.error(error.message || '控制失败')
  } finally {
    controlSubmitting.value = false
  }
}

const confirmDelete = (row: DataPointItem) => {
  ElMessageBox.confirm(`确定删除数据点 "${row.tag}" 吗？`, '删除确认', {
    type: 'warning',
    confirmButtonText: '删除',
    cancelButtonText: '取消'
  }).then(async () => {
    await deleteDataPoint(deviceId.value, row.id)
    delete realtimeData.value[row.tag]
    await fetchDataPoints()
    ElMessage.success('删除成功')
  }).catch(() => {})
}

const openVirtualNodeCreate = () => {
  editingVirtualNode.value = null
  virtualNodeDialogVisible.value = true
}

const openVirtualNodeEdit = (row: DataPointWithVirtual) => {
  if (!row.isVirtual) return
  editingVirtualNode.value = row as VirtualDataPoint
  virtualNodeDialogVisible.value = true
}

const handleVirtualSubmit = async (data: VirtualNodeForm) => {
  virtualNodeSubmitting.value = true
  try {
    if (editingVirtualNode.value) {
      await updateVirtualDataPoint(editingVirtualNode.value.id, {
        id: editingVirtualNode.value.id,
        ...data
      })
      ElMessage.success('虚拟节点更新成功')
    } else {
      await createVirtualDataPoint(data)
      ElMessage.success('虚拟节点创建成功')
    }

    virtualNodeDialogVisible.value = false
    await fetchDataPoints()
  } catch (error: any) {
    ElMessage.error(error.message || '操作失败')
  } finally {
    virtualNodeSubmitting.value = false
  }
}

const confirmDeleteVirtualNode = (row: DataPointWithVirtual) => {
  if (!row.isVirtual) return

  ElMessageBox.confirm(`确定删除虚拟节点 "${row.tag}" 吗？`, '删除确认', {
    type: 'warning',
    confirmButtonText: '删除',
    cancelButtonText: '取消'
  }).then(async () => {
    await deleteVirtualDataPoint(row.id)
    await fetchDataPoints()
    ElMessage.success('删除成功')
  }).catch(() => {})
}

const toggleVirtualNode = async (row: DataPointWithVirtual) => {
  if (!row.isVirtual) return

  const vp = row as VirtualDataPoint
  try {
    await updateVirtualDataPoint(vp.id, {
      id: vp.id,
      deviceId: vp.deviceId,
      name: vp.name,
      tag: vp.tag,
      description: vp.description || '',
      expression: vp.expression,
      calculationType: vp.calculationType,
      dataType: vp.dataType,
      unit: vp.unit || '',
      isEnabled: vp.isEnabled
    })
    ElMessage.success(vp.isEnabled ? '虚拟节点已启用' : '虚拟节点已禁用')
    await fetchDataPoints()
  } catch (error: any) {
    vp.isEnabled = !vp.isEnabled
    ElMessage.error(error.message || '操作失败')
  }
}

const handleDialogClose = () => {
  editingDataPoint.value = null
}

const handleVirtualDialogClose = () => {
  editingVirtualNode.value = null
}

const handleControlClose = () => {
  controllingDataPoint.value = null
}

onMounted(() => {
  void (async () => {
    await fetchDevice()
    await fetchDataPoints()
    await Promise.all([loadDataValueTypes(), loadModbusByteOrders()])
    startRealtimePolling()
  })()
})

onUnmounted(() => {
  stopRealtimePolling()
})
</script>

<style scoped lang="scss">
.page-enter {
  animation: fadeIn 0.2s ease-in-out;
}

.page-content {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.page-header,
.header-left,
.title-block,
.toolbar,
.toolbar-left,
.toolbar-right,
.stats-bar,
.stat-item {
  display: flex;
  align-items: center;
}

.page-header,
.toolbar {
  justify-content: space-between;
}

.page-header {
  margin-bottom: 20px;
}

.header-left,
.toolbar-left,
.title-block,
.stats-bar {
  gap: 12px;
}

.back-btn {
  padding: 0 !important;
}

.page-title {
  font-size: 22px;
  font-weight: 800;
  color: var(--text-primary);
}

.device-tag {
  font-size: 12px;
  color: var(--cyan);
  background: var(--cyan-dim);
  padding: 2px 10px;
  border-radius: 10px;
}

.stats-bar,
.toolbar,
.table-wrap {
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
}

.stats-bar {
  margin-bottom: 16px;
  padding: 14px 20px;
}

.stat-item.tail {
  margin-left: auto;
}

.s-num {
  font-size: 22px;
  font-weight: 700;
  color: var(--text-primary);
}

.s-num.success {
  color: var(--text-success);
}

.s-num.muted {
  color: var(--text-muted);
}

.s-num.cyan {
  color: var(--cyan);
  font-size: 14px;
}

.s-label,
.addr-text,
.time-text,
.empty-text,
.value-unit {
  color: var(--text-muted);
}

.main-content {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.toolbar {
  padding: 12px 16px;
  margin-bottom: 16px;
}

.table-wrap {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.pagination-bar {
  padding: 12px 16px;
  border-top: 1px solid var(--border-subtle);
  display: flex;
  justify-content: flex-end;
}

.tag-text {
  color: var(--cyan);
}

.realtime-value {
  font-weight: 600;
}

.realtime-value.good,
.badge.good {
  color: var(--text-success);
}

.realtime-value.bad,
.badge.bad {
  color: var(--text-danger);
}

.realtime-value.uncertain,
.badge.uncertain {
  color: var(--text-warn);
}

.badge.info {
  color: var(--text-secondary);
}
</style>
