<template>
  <div class="datapoints-view page-content page-enter">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-left">
        <el-button text :icon="ArrowLeft" @click="router.back()" class="back-btn">返回设备列表</el-button>
        <div class="title-block">
          <h1 class="page-title">数据点管理</h1>
          <div class="device-tag mono">{{ route.query.deviceName || `设备 #${route.params.id}` }}</div>
          <el-tag v-if="deviceEnabled" size="small" type="success" style="margin-left:8px">采集中</el-tag>
          <el-tag v-else size="small" type="info" style="margin-left:8px">已停止</el-tag>
        </div>
      </div>
    </div>

    <!-- 统计栏 -->
    <div class="stats-bar">
      <div class="stat-item">
        <span class="s-num mono">{{ dataPoints.length }}</span>
        <span class="s-label">总数据点</span>
      </div>
      <div class="stat-item">
        <span class="s-num mono" style="color:var(--text-success)">{{ enabledCount }}</span>
        <span class="s-label">已启用</span>
      </div>
      <div class="stat-item">
        <span class="s-num mono" style="color:var(--text-muted)">{{ dataPoints.length - enabledCount }}</span>
        <span class="s-label">已禁用</span>
      </div>
      <div class="stat-item" style="margin-left:auto">
        <span class="s-num mono" style="color:var(--cyan)">{{ lastUpdateTime ? formatDateTime(lastUpdateTime) : '—' }}</span>
        <span class="s-label">最后更新</span>
      </div>
    </div>

    <!-- 主内容区域 -->
    <div class="main-content">
      <!-- 工具栏 -->
      <div class="toolbar">
        <div class="toolbar-left">
          <el-input
            v-model="searchText" placeholder="搜索 Tag / 名称 / 地址..."
            prefix-icon="Search" clearable style="width: 280px"
            @change="handleFilterChange"
          />
          <el-select v-model="filterDataType" placeholder="数据类型" clearable style="width: 140px" @change="handleFilterChange">
            <el-option
              v-for="o in DataValueTypeOptions"
              :key="o.value" :label="o.label" :value="o.value"
            />
          </el-select>
          <el-select v-model="filterQuality" placeholder="数据质量" clearable style="width: 140px" @change="handleFilterChange">
            <el-option label="Good" value="Good" />
            <el-option label="Bad" value="Bad" />
            <el-option label="Uncertain" value="Uncertain" />
          </el-select>
          <el-select v-model="filterType" placeholder="数据类型" clearable style="width: 120px" @change="handleFilterChange">
            <el-option label="全部" value="" />
            <el-option label="普通数据点" value="normal" />
            <el-option label="虚拟数据点" value="virtual" />
          </el-select>
        </div>
        <div class="toolbar-right">
          <el-button :icon="Refresh" circle @click="refreshData" :loading="refreshing" title="刷新数据" />
          <el-dropdown split-button type="primary" @click="openCreate">
            <el-icon><Plus /></el-icon>
            新增数据点
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item @click="openCreate">普通数据点</el-dropdown-item>
                <el-dropdown-item @click="openVirtualNodeCreate">
                  <el-icon><Cpu /></el-icon> 虚拟节点
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </div>

      <!-- 表格 -->
      <div class="table-wrap">
        <el-table :data="dataPoints" v-loading="loading" row-key="id" :default-sort="{ prop: 'createdAt', order: 'descending' }">
        <el-table-column type="index" label="#" width="50" align="center" />

        <el-table-column prop="tag" label="Tag" min-width="200" sortable>
          <template #default="{ row }">
            <span class="mono tag-text">{{ row.tag }}</span>
            <el-tag v-if="row.isVirtual" size="small" type="warning" style="margin-left:6px">虚拟</el-tag>
          </template>
        </el-table-column>

        <el-table-column prop="name" label="名称" width="120" sortable />

        <el-table-column prop="address" label="地址" width="100" sortable>
          <template #default="{ row }">
            <span v-if="!row.isVirtual" class="mono addr-text">{{ row.address }}</span>
            <span v-else style="color:var(--text-muted);font-size:12px">表达式</span>
          </template>
        </el-table-column>

        <el-table-column prop="dataType" label="数据类型" width="100" align="center" sortable>
          <template #default="{ row }">
            <span class="badge info mono">{{ getDataTypeLabel(row.dataType) }}</span>
          </template>
        </el-table-column>

        <el-table-column prop="unit" label="单位" width="70" align="center" />

        <el-table-column label="实时值" width="140" align="center">
          <template #default="{ row }">
            <span v-if="getRealtimeData(row)" class="mono realtime-value" :class="getQualityClass(getRealtimeData(row)!.quality)">
              {{ formatRowValue(row) }}
              <span v-if="row.unit" class="value-unit">{{ row.unit }}</span>
            </span>
            <span v-else style="color:var(--text-muted);font-size:12px">—</span>
          </template>
        </el-table-column>

        <el-table-column prop="quality" label="质量" width="90" align="center" sortable>
          <template #default="{ row }">
            <span v-if="getRealtimeData(row)" class="badge mono" :class="getQualityClass(getRealtimeData(row)!.quality)">
              {{ getRealtimeData(row)!.quality }}
            </span>
            <span v-else style="color:var(--text-muted);font-size:12px">—</span>
          </template>
        </el-table-column>

        <el-table-column label="启用" width="70" align="center">
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

        <el-table-column prop="createdAt" label="创建时间" width="160" sortable>
          <template #default="{ row }">
            <span class="mono time-text">{{ formatDateTime(row.createdAt) }}</span>
          </template>
        </el-table-column>

        <el-table-column label="操作" width="140" align="right" fixed="right">
          <template #default="{ row }">
            <el-button v-if="row.isVirtual" size="small" text type="success" @click="openVirtualNodeEdit(row)">
              <el-icon><Edit /></el-icon> 编辑
            </el-button>
            <el-button v-else size="small" text type="primary" @click="openEdit(row)">
              <el-icon><Edit /></el-icon> 编辑
            </el-button>
            <el-button size="small" text type="danger" @click="row.isVirtual ? confirmDeleteVirtualNode(row) : confirmDelete(row)">
              <el-icon><Delete /></el-icon> 删除
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <!-- 分页 -->
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

    <!-- 新增/编辑数据点弹窗 -->
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

    <!-- 虚拟节点对话框 -->
    <VirtualNodeDialog
      v-model="virtualNodeDialogVisible"
      :editing-virtual-node="editingVirtualNode"
      :device-code="deviceCode"
      :data-type-options="DataValueTypeOptions"
      :submitting="virtualNodeSubmitting"
      @submit="handleVirtualSubmit"
      @close="handleVirtualDialogClose"
    />
  </div>
</template>

<script setup lang="ts">
import {computed, onMounted, onUnmounted, ref} from 'vue'
import {useRoute, useRouter} from 'vue-router'
import {ElMessage, ElMessageBox} from 'element-plus'
import {ArrowLeft, Cpu, Delete, Edit, Plus, Refresh} from '@element-plus/icons-vue'
import {
  createDataPoint,
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
import {getDataValueTypes, getModbusByteOrders} from '@/api/enums'
import {formatDateTime} from '@/api/constants'
import type {DataPointItem, RealtimeDataItem} from '@/types'
import type {VirtualDataPoint} from '@/types/virtualNode'
import DataPointDialog from '@/dialogs/dataPoint/DataPointDialog.vue'
import VirtualNodeDialog from '@/dialogs/dataPoint/VirtualNodeDialog.vue'

// 扩展的数据点类型，支持虚拟节点
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

// 筛选器
const filterType = ref<string>('')

// 分页配置
const pagination = ref<{ page: number; pageSize: number; total: number }>({
  page: 1,
  pageSize: 50,
  total: 0
})

// 加载数据类型选项
const loadDataValueTypes = async () => {
  try {
    const res = await getDataValueTypes()
    DataValueTypeOptions.value = (res as any).data || []
  } catch (error) {
    console.error('加载数据类型失败:', error)
  }
}

// 加载 Modbus 字节序选项
const loadModbusByteOrders = async () => {
  try {
    const res = await getModbusByteOrders()
    ModbusByteOrderOptions.value = (res as any).data || []
  } catch (error) {
    console.error('加载 Modbus 字节序失败:', error)
  }
}

const loading = ref(false)
const refreshing = ref(false)
const dataPoints = ref<DataPointWithVirtual[]>([])
const enabledCount = computed(() => dataPoints.value.filter((item) => item.isEnabled).length)
const deviceEnabled = ref(false)
const deviceCode = ref('')
const deviceProtocol = ref<number | null>(null)

// 搜索和筛选
const searchText = ref('')
const filterDataType = ref<number | null>(null)
const filterQuality = ref<string | null>(null)

const refreshData = async () => {
  refreshing.value = true
  try {
    pagination.value.page = 1
    await fetchDataPoints()
    await loadVirtualDataPoints()
    await fetchRealtimeData()
    ElMessage.success('数据已刷新')
  } catch (error) {
    ElMessage.error('刷新失败')
  } finally {
    refreshing.value = false
  }
}

const handleFilterChange = () => {
  pagination.value.page = 1
  fetchDataPoints()
}

// 实时数据
const realtimeData = ref<Record<string, RealtimeDataItem>>({})
const lastUpdateTime = ref<Date | null>(null)
let realtimeTimer: number | null = null

const getDataTypeLabel = (dataType: string | number) => {
  if (typeof dataType === 'number') {
    const option = DataValueTypeOptions.value.find(o => o.value === dataType)
    return option?.label || String(dataType)
  }
  return dataType
}

// 数据点弹窗状态
const dialogVisible = ref(false)
const submitting = ref(false)
const editingDataPoint = ref<DataPointItem | null>(null)

// 虚拟节点弹窗状态
const virtualNodeDialogVisible = ref(false)
const editingVirtualNode = ref<VirtualDataPoint | null>(null)
const virtualNodeSubmitting = ref(false)

const fetchDevice = async () => {
  try {
    const res = await getDevice(deviceId.value)
    const device = (res as { data?: any })?.data
    if (device) {
      deviceEnabled.value = device.isEnabled
      deviceProtocol.value = device.protocolValue ?? device.protocol ?? null
      deviceCode.value = device.code ?? ''
    }
  } catch (e) {
    console.error('获取设备信息失败', e)
  }
}

const fetchDataPoints = async () => {
  loading.value = true
  try {
    const res = await getDataPointsPaged(deviceId.value, {
      page: pagination.value.page,
      pageSize: pagination.value.pageSize,
      search: searchText.value || undefined,
      dataType: filterDataType.value || undefined,
      isEnabled: filterType.value === '' ? undefined : (filterType.value === 'normal' ? true : undefined)
    })
    
    const responseData = (res as { data?: { items: DataPointItem[]; total: number } })?.data
    if (responseData) {
      dataPoints.value = responseData.items.map(item => ({
        ...item,
        isVirtual: false
      })) as DataPointWithVirtual[]
      pagination.value.total = responseData.total
    }
  } finally {
    loading.value = false
  }
}

const loadVirtualDataPoints = async () => {
  try {
    const res = await getVirtualDataPointsByDevice(deviceId.value)
    const virtualPoints = ((res as { data?: VirtualDataPoint[] })?.data ?? []).map(item => ({
      ...item,
      isVirtual: true
    })) as DataPointWithVirtual[]

    // 虚拟点追加到列表末尾
    const normalPoints = dataPoints.value.filter(item => !item.isVirtual)
    dataPoints.value = [...normalPoints, ...virtualPoints]
    // 更新总数（包含虚拟点）
    pagination.value.total = normalPoints.length + virtualPoints.length
  } catch (error) {
    console.error('加载虚拟数据点失败:', error)
  }
}

const fetchRealtimeData = async () => {
  try {
    const res = await getDeviceRealtimeData(deviceId.value)
    const dataList = ((res as { data?: RealtimeDataItem[] })?.data ?? []) as RealtimeDataItem[]

    const newData: Record<string, RealtimeDataItem> = { ...realtimeData.value }
    let hasChanges = false

    dataList.forEach((item) => {
      if (item.tag) {
        const oldItem = newData[item.tag]
        if (!oldItem || oldItem.value !== item.value || oldItem.quality !== item.quality) {
          newData[item.tag] = item
          hasChanges = true
        }
      }
    })

    if (hasChanges) {
      realtimeData.value = newData
      lastUpdateTime.value = new Date()
    }
  } catch (e) {
    console.error('获取实时数据失败:', e)
  }
}

const startRealtimePolling = () => {
  fetchRealtimeData()
  realtimeTimer = window.setInterval(() => {
    fetchRealtimeData()
  }, 500)
}

const stopRealtimePolling = () => {
  if (realtimeTimer) {
    clearInterval(realtimeTimer)
    realtimeTimer = null
  }
}

const getRealtimeData = (row: DataPointWithVirtual): RealtimeDataItem | null => {
  debugger
  return realtimeData.value[row.tag] || null
}

const getQualityClass = (quality: string) => {
  if (quality === 'Good') return 'good'
  if (quality === 'Bad') return 'bad'
  if (quality === 'Uncertain') return 'uncertain'
  return ''
}

const formatRowValue = (row: DataPointWithVirtual): string => {
  const data = getRealtimeData(row)
  if (!data || data.value === null || data.value === undefined) {
    return '—'
  }

  const value = data.value
  if (typeof value === 'number') {
    const strVal = value.toString()
    if (strVal.includes('.')) {
      return value.toFixed(2)
    }
    return strVal
  }

  return String(value)
}

const toggleDataPoint = async (row: DataPointItem) => {
  try {
    await apiToggleDataPoint(deviceId.value, row.id, row.isEnabled)
    ElMessage.success(row.isEnabled ? '数据点已启用' : '数据点已禁用')
  } catch (error: any) {
    row.isEnabled = !row.isEnabled
    ElMessage.error(`操作失败：${error.message || '未知错误'}`)
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
    fetchDataPoints()
  } catch (error: any) {
    ElMessage.error(`操作失败：${error.message || '未知错误'}`)
  } finally {
    submitting.value = false
  }
}

const confirmDelete = (row: DataPointItem) => {
  ElMessageBox.confirm(`确定要删除数据点 "${row.tag}" 吗？`, '删除确认', {
    type: 'warning',
    confirmButtonText: '删除',
    cancelButtonText: '取消'
  })
    .then(async () => {
      await deleteDataPoint(deviceId.value, row.id)
      ElMessage.success('删除成功')
      fetchDataPoints()
      delete realtimeData.value[row.tag]
    })
    .catch(() => {})
}

// 虚拟节点相关
const openVirtualNodeCreate = () => {
  editingVirtualNode.value = null
  virtualNodeDialogVisible.value = true
}

const openVirtualNodeEdit = (row: DataPointWithVirtual) => {
  if (row.isVirtual) {
    editingVirtualNode.value = row as VirtualDataPoint
    virtualNodeDialogVisible.value = true
  }
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
    loadVirtualDataPoints()
  } catch (error: any) {
    ElMessage.error(`操作失败：${error.message || '未知错误'}`)
  } finally {
    virtualNodeSubmitting.value = false
  }
}

const confirmDeleteVirtualNode = (row: DataPointWithVirtual) => {
  if (!row.isVirtual) return

  ElMessageBox.confirm(`确定要删除虚拟节点 "${row.tag}" 吗？`, '删除确认', {
    type: 'warning',
    confirmButtonText: '删除',
    cancelButtonText: '取消'
  })
    .then(async () => {
      await deleteVirtualDataPoint(row.id)
      ElMessage.success('删除成功')
      loadVirtualDataPoints()
    })
    .catch(() => {})
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
  } catch (error: any) {
    vp.isEnabled = !vp.isEnabled
    ElMessage.error(`操作失败：${error.message || '未知错误'}`)
  }
}

const handleDialogClose = () => {
  editingDataPoint.value = null
}

const handleVirtualDialogClose = () => {
  editingVirtualNode.value = null
}

onMounted(() => {
  const init = async () => {
    await fetchDevice()
    await fetchDataPoints()
    await loadVirtualDataPoints()
    await loadDataValueTypes()
    await loadModbusByteOrders()
    startRealtimePolling()
  }
  init()
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

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 14px;
}

.back-btn {
  color: var(--text-muted) !important;
  padding: 0 !important;
}

.title-block {
  display: flex;
  align-items: baseline;
  gap: 12px;
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
  border: 1px solid rgba(56, 220, 196, 0.25);
}

.stats-bar {
  display: flex;
  gap: 32px;
  margin-bottom: 16px;
  padding: 14px 20px;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  flex-shrink: 0;
}

.stat-item {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.s-num {
  font-size: 24px;
  font-weight: 700;
  color: var(--text-primary);
}

.s-label {
  font-size: 12px;
  color: var(--text-muted);
}

/* 主内容区域 - 占据剩余空间 */
.main-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-height: 0;
}

/* 工具栏样式 */
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
  padding: 12px 16px;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  flex-shrink: 0;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* 表格容器 - 占据剩余空间 */
.table-wrap {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  overflow: hidden;
  min-height: 0;
}

/* Element Plus 表格样式优化 */
:deep(.el-table) {
  flex: 1;
  overflow: auto;
}

:deep(.el-table__body-wrapper) {
  overflow: auto;
}

:deep(.el-table__header-wrapper) {
  position: sticky;
  top: 0;
  z-index: 10;
  background: var(--bg-card);
}

/* 分页栏 */
.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  padding: 12px 16px;
  border-top: 1px solid var(--border-subtle);
  background: var(--bg-base);
  flex-shrink: 0;
  gap: 8px;
}

/* Element Plus 分页样式优化 */
:deep(.el-pagination) {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0;
}

:deep(.el-pagination__total) {
  color: var(--text-muted);
  font-size: 13px;
  font-weight: 500;
}

:deep(.el-pagination__sizes) {
  margin-right: 8px;
}

:deep(.el-select .el-input) {
  width: 100px;
}

:deep(.el-pager) {
  display: flex;
  align-items: center;
  gap: 4px;
}

:deep(.el-pager li) {
  min-width: 32px;
  height: 32px;
  line-height: 32px;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: 6px;
  transition: all 0.2s ease;
  margin: 0;
  
  &:hover {
    background: var(--bg-hover);
    border-color: var(--cyan);
    color: var(--cyan);
    transform: translateY(-1px);
  }
  
  &.is-active {
    background: linear-gradient(135deg, var(--cyan) 0%, rgba(56, 220, 196, 0.8) 100%);
    border-color: var(--cyan);
    color: #fff;
    font-weight: 600;
    box-shadow: 0 2px 8px rgba(56, 220, 196, 0.3);
  }
}

:deep(.el-pagination__jump) {
  margin-left: 12px;
  color: var(--text-muted);
  font-size: 13px;
  
  .el-input {
    width: 50px;
    margin: 0 4px;
    
    .el-input__wrapper {
      height: 32px;
      padding: 0 8px;
      border-radius: 6px;
      background: var(--bg-card);
      border: 1px solid var(--border-subtle);
      transition: all 0.2s ease;
      
      &:hover {
        border-color: var(--cyan);
      }
      
      &.is-focus {
        border-color: var(--cyan);
        box-shadow: 0 0 0 2px rgba(56, 220, 196, 0.2);
      }
    }
    
    .el-input__inner {
      font-size: 13px;
      text-align: center;
      font-weight: 500;
    }
  }
}

:deep(.btn-prev),
:deep(.btn-next) {
  min-width: 32px;
  height: 32px;
  border-radius: 6px;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  color: var(--text-secondary);
  transition: all 0.2s ease;
  
  &:hover {
    background: var(--bg-hover);
    border-color: var(--cyan);
    color: var(--cyan);
    transform: translateY(-1px);
  }
  
  &:disabled {
    opacity: 0.5;
    cursor: not-allowed;
    
    &:hover {
      background: var(--bg-card);
      border-color: var(--border-subtle);
      color: var(--text-secondary);
      transform: none;
    }
  }
}

:deep(.el-select-dropdown__item) {
  font-size: 13px;
}

:deep(.el-select-dropdown__item.is-selected) {
  color: var(--cyan);
  font-weight: 600;
}

.tag-text {
  font-size: 13px;
  color: var(--cyan);
}

.addr-text {
  font-size: 13px;
  color: var(--text-secondary);
}

.time-text {
  font-size: 11px;
  color: var(--text-muted);
}

:deep(.mono-input .el-input__inner) {
  font-family: var(--font-mono);
}

/* 工具栏样式 */
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
  padding: 12px 16px;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* 实时数据值样式 */
.realtime-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
}

.realtime-value.good {
  color: var(--text-success);
}

.realtime-value.bad {
  color: var(--text-danger);
}

.realtime-value.uncertain {
  color: var(--text-warn);
}

.value-unit {
  font-size: 11px;
  color: var(--text-muted);
  margin-left: 2px;
  font-weight: 400;
}

/* 数据质量徽章 */
.badge.good {
  background: rgba(82, 196, 26, 0.15);
  color: var(--text-success);
  border-color: rgba(82, 196, 26, 0.3);
}

.badge.bad {
  background: rgba(240, 89, 89, 0.15);
  color: var(--text-danger);
  border-color: rgba(240, 89, 89, 0.3);
}

.badge.uncertain {
  background: rgba(250, 173, 40, 0.15);
  color: var(--text-warn);
  border-color: rgba(250, 173, 40, 0.3);
}

.badge.info {
  background: rgba(66, 153, 225, 0.15);
  color: var(--text-secondary);
  border-color: rgba(66, 153, 225, 0.3);
}
</style>
