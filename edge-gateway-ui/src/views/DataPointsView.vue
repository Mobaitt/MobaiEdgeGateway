<template>
  <div class="datapoints-view page-content page-enter">
    <div class="page-header">
      <div class="header-left">
        <el-button :icon="ArrowLeft" class="back-btn" @click="router.back()">返回设备列表</el-button>
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
      <div class="toolbar eg-toolbar-surface">
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
          <el-button class="eg-circle-action" :icon="Refresh" circle :loading="refreshing" @click="refreshData" />
          <el-dropdown class="eg-split-action" popper-class="datapoints-dropdown-popper" split-button type="primary" @click="openCreate">
            <el-icon><Plus /></el-icon>
            新增数据点
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item @click="openCreate">
                  <el-icon><DataLine /></el-icon>
                  普通数据点
                </el-dropdown-item>
                <el-dropdown-item @click="openVirtualNodeCreate">
                  <el-icon><Cpu /></el-icon>
                  虚拟节点
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
          <el-button class="template-action" @click="saveAsTemplate" :disabled="normalDataPointCount === 0">保存为模板</el-button>
          <el-select
            v-model="selectedTemplateId"
            class="template-select"
            placeholder="套用模板"
            :disabled="templates.length === 0"
            popper-class="datapoints-dropdown-popper"
            @change="handleTemplateSelection"
          >
            <el-option v-for="template in templates" :key="template.id" :label="template.name" :value="template.id">
              <span class="template-option-name">{{ template.name }}</span>
              <span class="template-option-count">{{ template.pointCount }} 点</span>
            </el-option>
          </el-select>
        </div>
      </div>

      <div class="table-wrap">
        <DataPointTable
          :data="filteredDataPoints"
          :loading="loading"
          :device-enabled="deviceEnabled"
          :realtime-data="realtimeData"
          :page="pagination.page"
          :page-size="pagination.pageSize"
          :total="pagination.total"
          :get-data-type-label="getDataTypeLabel"
          :format-row-value="formatRowValue"
          :get-quality-class="getQualityClass"
          @toggle-data-point="handleTableToggleDataPoint"
          @toggle-virtual="handleTableToggleVirtual"
          @control="openControl"
          @edit="handleTableEdit"
          @edit-virtual="openVirtualNodeEdit"
          @delete="handleTableDelete"
          @delete-virtual="confirmDeleteVirtualNode"
          @size-change="handleTableSizeChange"
          @page-change="handleTablePageChange"
        />
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
import { ArrowLeft, Cpu, DataLine, Plus, Refresh } from '@element-plus/icons-vue'
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
import { CollectionProtocol, formatDateTime } from '@/api/constants'
import type { DataPointItem, DataPointTemplateItem, RealtimeDataItem } from '@/types'
import { applyDataPointTemplate, createDataPointTemplateFromDevice, getDataPointTemplates } from '@/api/dataPointTemplate'
import type { VirtualDataPoint } from '@/types/virtualNode'
import DataPointDialog from '@/dialogs/dataPoint/DataPointDialog.vue'
import DataPointControlDialog from '@/dialogs/dataPoint/DataPointControlDialog.vue'
import VirtualNodeDialog from '@/dialogs/dataPoint/VirtualNodeDialog.vue'
import DataPointTable from '@/components/DataPointTable.vue'

type DataPointWithVirtual = (DataPointItem | VirtualDataPoint) & { isVirtual?: boolean }

type DataPointForm = {
  name: string
  tag: string
  description: string
  address: string
  dataType: number | null
  unit: string
  isEnabled: boolean
  isControllable: boolean
  modbusSlaveId: number
  modbusFunctionCode: number
  modbusByteOrder: number
  registerLength: number
  modbusBitIndex: number | null
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
const templates = ref<DataPointTemplateItem[]>([])
const selectedTemplateId = ref<number | null>(null)

const enabledCount = computed(() => dataPoints.value.filter(item => item.isEnabled).length)
const normalDataPointCount = computed(() => dataPoints.value.filter(item => !item.isVirtual).length)

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
  await fetchTemplates()
}

const fetchTemplates = async () => {
  const res = await getDataPointTemplates(deviceProtocol.value ?? undefined)
  templates.value = ((res as { data?: DataPointTemplateItem[] }).data || [])
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

  for (const item of dataList) {
    if (!item.tag) continue

    const old = snapshot[item.tag]
    if (!old || old.value !== item.value || old.quality !== item.quality || old.timestamp !== item.timestamp) {
      snapshot[item.tag] = item
      changed = true
    }
  }

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

const saveAsTemplate = async () => {
  try {
    const { value: name } = await ElMessageBox.prompt('模板会保存当前设备的全部普通数据点配置。', '保存点位模板', {
      confirmButtonText: '保存',
      cancelButtonText: '取消',
      inputPlaceholder: '例如：标准水泵点位',
      inputValidator: value => value.trim().length > 0 || '请输入模板名称'
    })
    await createDataPointTemplateFromDevice(deviceId.value, { name: name.trim() })
    await fetchTemplates()
    ElMessage.success('模板已保存')
  } catch {
    // 用户取消时不提示错误。
  }
}

const applyTemplate = async (templateId: number) => {
  const template = templates.value.find(item => item.id === templateId)
  if (!template) return

  try {
    await ElMessageBox.confirm(
      `将套用模板“${template.name}”，同名 Tag 会跳过，是否继续？`,
      '套用点位模板',
      { confirmButtonText: '套用', cancelButtonText: '取消', type: 'warning' }
    )
    const res = await applyDataPointTemplate(deviceId.value, templateId)
    const result = (res as { data?: { created: number; overwritten: number; skipped: number } }).data
    await fetchDataPoints()
    await fetchRealtimeData()
    ElMessage.success(`模板已套用：新增 ${result?.created ?? 0}，跳过 ${result?.skipped ?? 0}`)
  } catch {
    // 用户取消时不提示错误。
  }
}

const handleTemplateSelection = async (templateId: number | null) => {
  if (templateId === null || templateId === undefined) return
  selectedTemplateId.value = null
  await applyTemplate(templateId)
}

const getRealtimeData = (row: DataPointWithVirtual): RealtimeDataItem | null => realtimeData.value[row.tag] || null

const getQualityClass = (quality: string) => {
  if (quality === 'Good') return 'good'
  if (quality === 'Bad') return 'bad'
  if (quality === 'Uncertain') return 'uncertain'
  return ''
}

const getRowDataTypeValue = (item: DataPointWithVirtual) => {
  return 'dataTypeValue' in item && typeof item.dataTypeValue === 'number'
    ? item.dataTypeValue
    : Number(item.dataType)
}

const modbusByteOrderLabels: Record<number, string> = {
  1: 'AB CD',
  2: 'BA DC',
  3: 'CD AB',
  4: 'DC BA'
}

const modbusDoubleByteOrderLabels: Record<number, string> = {
  1: 'AB CD EF GH',
  2: 'BA DC FE HG',
  3: 'GH EF CD AB',
  4: 'HG FE BA DC'
}

/**
 * Modbus 的 dataType 只是基础枚举，列表需要同时展示字节序，
 * 否则 Int32/UInt32/Float/Double 的具体解析方式会被隐藏。
 */
const getDataTypeLabel = (row: DataPointWithVirtual) => {
  const dataType = getRowDataTypeValue(row)

  if (row.isVirtual || deviceProtocol.value !== CollectionProtocol.Modbus.value) {
    if (typeof row.dataType === 'number') {
      const option = DataValueTypeOptions.value.find(item => item.value === dataType)
      return option?.label || String(dataType)
    }
    return String(row.dataType)
  }

  const byteOrder = 'modbusByteOrder' in row ? Number(row.modbusByteOrder) || 1 : 1
  const orderLabel = modbusByteOrderLabels[byteOrder] || modbusByteOrderLabels[1]
  const doubleOrderLabel = modbusDoubleByteOrderLabels[byteOrder] || modbusDoubleByteOrderLabels[1]

  switch (dataType) {
    case 1: return 'modbusBitIndex' in row && row.modbusBitIndex !== null && row.modbusBitIndex !== undefined
      ? `Bit${row.modbusBitIndex} Boolean`
      : 'Boolean'
    case 2: return 'Signed'
    case 3: return 'Unsigned'
    case 4: return `Long ${orderLabel}`
    case 5: return `Unsigned Long ${orderLabel}`
    case 6: return `Float ${orderLabel}`
    case 7: return `Int64 ${doubleOrderLabel}`
    case 8: return `Unsigned Int64 ${doubleOrderLabel}`
    case 9: return `Double ${doubleOrderLabel}`
    case 11: return 'Hex'
    case 12: return 'Binary'
    default: return String(row.dataType ?? dataType)
  }
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
    await fetchRealtimeData()
  } catch (error: any) {
    ElMessage.error(error.message || '操作失败')
  } finally {
    submitting.value = false
  }
}

const handleControlSubmit = async (value: unknown) => {
  if (!controllingDataPoint.value) return

  try {
    await ElMessageBox.confirm(
      '确认向点位 "' + controllingDataPoint.value.tag + '" 发送指令值 "' + String(value) + '" 吗？',
      '确认发送指令',
      {
        type: 'warning',
        confirmButtonText: '发送',
        cancelButtonText: '取消'
      }
    )
  } catch {
    return
  }

  controlSubmitting.value = true
  try {
    const res = await controlDataPoint(controllingDataPoint.value.tag, value)
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
    ElMessage.success('指令发送成功，设备读回已确认')
  } catch (error: any) {
    ElMessage.error(error.message || '指令发送失败')
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

const handleTableToggleDataPoint = (row: DataPointWithVirtual) => {
  if (!row.isVirtual) void toggleDataPoint(row as DataPointItem)
}

const handleTableToggleVirtual = (row: DataPointWithVirtual) => {
  if (row.isVirtual) void toggleVirtualNode(row)
}

const handleTableEdit = (row: DataPointWithVirtual) => {
  if (!row.isVirtual) openEdit(row as DataPointItem)
}

const handleTableDelete = (row: DataPointWithVirtual) => {
  if (!row.isVirtual) confirmDelete(row as DataPointItem)
}

const handleTableSizeChange = (value: number) => {
  pagination.value.pageSize = value
  pagination.value.page = 1
  void fetchDataPoints()
}

const handleTablePageChange = (value: number) => {
  pagination.value.page = value
  void fetchDataPoints()
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
  height: 34px;
  padding: 0 11px !important;
  border: 1px solid rgba(120, 155, 190, .2) !important;
  border-radius: 9px;
  background: var(--action-bg) !important;
  color: var(--action-text) !important;
  transition: all .2s ease;
}

.back-btn:hover {
  border-color: var(--action-border-hover) !important;
  background: var(--action-bg-hover) !important;
  color: var(--action-text-hover) !important;
  transform: translateX(-1px);
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

.s-label {
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

.toolbar-right {
  gap: 10px;
}

.template-action.el-button {
  height: 38px;
  padding: 0 14px;
  border: 1px solid rgba(120, 155, 190, .22) !important;
  border-radius: 10px;
  background: var(--action-bg) !important;
  color: var(--action-text) !important;
  box-shadow: none !important;
  transition: all .2s ease;
}

.template-action.el-button:hover:not(:disabled) {
  border-color: var(--action-border-hover) !important;
  background: var(--action-bg-hover) !important;
  color: var(--action-text-hover) !important;
  transform: translateY(-1px);
}

.template-action.el-button:disabled {
  opacity: .45;
}

.template-select {
  width: 126px;
}

.template-select :deep(.el-select__wrapper) {
  min-height: 38px;
  height: 38px;
  padding: 0 12px;
  border: 1px solid rgba(120, 155, 190, .22) !important;
  border-radius: 10px;
  background: var(--action-bg) !important;
  box-shadow: none !important;
  transition: all .2s ease;
}

.template-select :deep(.el-select__wrapper:hover),
.template-select :deep(.el-select__wrapper.is-focused) {
  border-color: var(--action-border-hover) !important;
  background: var(--action-bg-hover) !important;
  box-shadow: 0 0 0 2px rgba(56, 220, 196, .04) !important;
}

.template-select :deep(.el-select__placeholder) {
  color: var(--text-secondary);
}

.template-select :deep(.el-select__caret) {
  color: var(--text-muted);
}

.template-option-name {
  color: var(--text-primary);
}

.template-option-count {
  float: right;
  margin-left: 24px;
  color: var(--text-muted);
  font-family: var(--font-mono);
  font-size: 11px;
}

.table-wrap {
  flex: 1 1 auto;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-shadow: 0 12px 32px rgba(0, 0, 0, .12);
}

</style>
