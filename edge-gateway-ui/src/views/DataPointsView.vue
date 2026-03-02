<template>
  <div class="datapoints-view page-enter">
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
      <el-button type="primary" :icon="Plus" @click="openCreate">新增数据点</el-button>
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

    <!-- 表格 -->
    <div class="table-wrap">
      <el-table :data="dataPoints" v-loading="loading" row-key="id">
        <el-table-column label="Tag" min-width="220">
          <template #default="{ row }">
            <span class="mono tag-text">{{ row.tag }}</span>
          </template>
        </el-table-column>

        <el-table-column prop="name" label="名称" width="130" />

        <el-table-column label="地址" width="120">
          <template #default="{ row }">
            <span class="mono addr-text">{{ row.address }}</span>
          </template>
        </el-table-column>

        <el-table-column label="数据类型" width="120" align="center">
          <template #default="{ row }">
            <span class="badge info mono">{{ row.dataType }}</span>
          </template>
        </el-table-column>

        <el-table-column label="单位" width="80" align="center">
          <template #default="{ row }">
            <span style="color:var(--text-muted);font-size:13px">{{ row.unit || '—' }}</span>
          </template>
        </el-table-column>

        <el-table-column label="实时值" width="150" align="center">
          <template #default="{ row }">
            <span v-if="realtimeData[row.id]" class="mono realtime-value" :class="getQualityClass(realtimeData[row.id].quality)">
              {{ formatValue(realtimeData[row.id].value, row.dataType) }}
              <span v-if="row.unit" class="value-unit">{{ row.unit }}</span>
            </span>
            <span v-else style="color:var(--text-muted);font-size:12px">—</span>
          </template>
        </el-table-column>

        <el-table-column label="数据质量" width="100" align="center">
          <template #default="{ row }">
            <span v-if="realtimeData[row.id]" class="badge mono" :class="getQualityClass(realtimeData[row.id].quality)">
              {{ realtimeData[row.id].quality }}
            </span>
            <span v-else style="color:var(--text-muted);font-size:12px">—</span>
          </template>
        </el-table-column>

        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <span class="badge" :class="row.isEnabled ? 'good' : 'bad'">
              {{ row.isEnabled ? 'ON' : 'OFF' }}
            </span>
          </template>
        </el-table-column>

        <el-table-column label="创建时间" width="160">
          <template #default="{ row }">
            <span class="mono time-text">{{ formatDateTime(row.createdAt) }}</span>
          </template>
        </el-table-column>

        <el-table-column label="操作" width="150" align="right">
          <template #default="{ row }">
            <el-button size="small" text @click="openEdit(row)">
              <el-icon><Edit /></el-icon>
              编辑
            </el-button>
            <el-button size="small" text type="danger" @click="confirmDelete(row)">
              <el-icon><Delete /></el-icon>
              删除
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 新增/编辑数据点弹窗 -->
    <el-dialog v-model="dialogVisible" :title="editingDataPoint ? '编辑数据点' : '新增数据点'" width="720px" destroy-on-close class="datapoint-dialog" top="5vh">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px" label-position="left">
        
        <!-- 基本信息 -->
        <div class="form-section">
          <div class="section-title"><el-icon><Document /></el-icon> 基本信息</div>
          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="名称" prop="name">
                <el-input v-model="form.name" placeholder="如：温度" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="Tag" prop="tag">
                <el-input v-model="form.tag" placeholder="DEV001.Temperature" class="mono-input" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-form-item label="描述">
            <el-input v-model="form.description" placeholder="可选描述信息" />
          </el-form-item>
        </div>

        <!-- 采集配置 -->
        <div class="form-section">
          <div class="section-title"><el-icon><Setting /></el-icon> 采集配置</div>
          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="地址" prop="address">
                <el-input v-model="form.address" placeholder="40001" class="mono-input" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="数据类型" prop="dataType">
                <el-select v-model="form.dataType" placeholder="选择类型" style="width:100%">
                  <el-option v-for="o in DataValueTypeOptions" :key="o.value" :label="o.label" :value="o.value" />
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="单位">
                <el-input v-model="form.unit" placeholder="℃、MPa" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="是否启用">
                <el-switch v-model="form.isEnabled" active-color="#38dcc4" />
              </el-form-item>
            </el-col>
          </el-row>
        </div>

        <!-- Modbus 高级配置 -->
        <div class="form-section">
          <div class="section-title"><el-icon><Connection /></el-icon> Modbus 配置</div>
          <el-row :gutter="20">
            <el-col :span="8">
              <el-form-item label="从站地址">
                <el-input-number v-model="form.modbusSlaveId" :min="1" :max="247" :step="1" style="width:100%" controls-position="right" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="功能码">
                <el-select v-model="form.modbusFunctionCode" placeholder="功能码" style="width:100%">
                  <el-option label="01 - 读线圈" :value="1" />
                  <el-option label="02 - 读离散输入" :value="2" />
                  <el-option label="03 - 读保持寄存器" :value="3" />
                  <el-option label="04 - 读输入寄存器" :value="4" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="字节顺序">
                <el-select v-model="form.modbusByteOrder" placeholder="字节序" style="width:100%">
                  <el-option v-for="o in ModbusByteOrderOptions" :key="o.value" :label="o.label" :value="o.value">
                    <div class="byte-order-option">
                      <span>{{ o.label }}</span>
                      <span class="byte-order-desc">{{ o.desc }}</span>
                    </div>
                  </el-option>
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="20" style="margin-top: 12px">
            <el-col :span="8">
              <el-form-item label="寄存器长度">
                <el-select v-model="form.registerLength" placeholder="长度" style="width:100%">
                  <el-option label="1 个寄存器 (16 位)" :value="1" />
                  <el-option label="2 个寄存器 (32 位)" :value="2" />
                  <el-option label="4 个寄存器 (64 位)" :value="4" />
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>
          <div class="protocol-hint">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>32 位数据类型（Int32、UInt32、Float）需要 2 个寄存器，64 位数据类型（Int64、UInt64、Double）需要 4 个寄存器。</span>
          </div>
        </div>

      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitForm">
          {{ editingDataPoint ? '保存修改' : '创建数据点' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessageBox, ElMessage } from 'element-plus'
import { Plus, Delete, ArrowLeft, Document, Setting, Connection, InfoFilled, Edit } from '@element-plus/icons-vue'
import { getDataPoints, createDataPoint, updateDataPoint, deleteDataPoint, getDeviceRealtimeData, getDevice } from '@/api/device'
import { getDataValueTypes, getModbusByteOrders } from '@/api/enums'
import { formatDateTime } from '@/api/constants'

type DataPointItem = {
  id: number
  name: string
  tag: string
  description?: string
  address: string
  dataType: string | number
  dataTypeValue?: number
  unit?: string
  isEnabled: boolean
  createdAt?: string
  modbusSlaveId?: number
  modbusFunctionCode?: number
  modbusByteOrder?: number
  registerLength?: number
}

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

type RealtimeDataItem = {
  dataPointId: number
  value: any
  quality: string
  timestamp: string
}

const route = useRoute()
const router = useRouter()
const deviceId = computed(() => Number(route.params.id))
const DataValueTypeOptions = ref<any[]>([])
const ModbusByteOrderOptions = ref<any[]>([])

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
const dataPoints = ref<DataPointItem[]>([])
const enabledCount = computed(() => dataPoints.value.filter((item) => item.isEnabled).length)
const deviceEnabled = ref(false)

// 实时数据
const realtimeData = ref<Record<number, RealtimeDataItem>>({})
const lastUpdateTime = ref<Date | null>(null)
let realtimeTimer: number | null = null

const dialogVisible = ref(false)
const submitting = ref(false)
const editingDataPoint = ref<DataPointItem | null>(null)
const formRef = ref<any>()
const form = ref<DataPointForm>({
  name: '',
  tag: '',
  description: '',
  address: '',
  dataType: null,
  unit: '',
  isEnabled: true,
  modbusSlaveId: 1,
  modbusFunctionCode: 3,
  modbusByteOrder: 1,
  registerLength: 1
})

const rules = {
  name: [{ required: true, message: '请输入名称' }],
  tag: [{ required: true, message: '请输入标签' }],
  address: [{ required: true, message: '请输入地址' }],
  dataType: [{ required: true, message: '请选择数据类型' }]
}

const fetchDevice = async () => {
  try {
    const res = await getDevice(deviceId.value)
    const device = (res as { data?: any })?.data
    if (device) {
      deviceEnabled.value = device.isEnabled
    }
  } catch (e) {
    console.error('获取设备信息失败', e)
  }
}

const fetchDataPoints = async () => {
  loading.value = true
  try {
    const res = await getDataPoints(deviceId.value)
    dataPoints.value = ((res as { data?: DataPointItem[] })?.data ?? []) as DataPointItem[]
  } finally {
    loading.value = false
  }
}

const fetchRealtimeData = async () => {
  try {
    const res = await getDeviceRealtimeData(deviceId.value)
    const dataList = ((res as { data?: RealtimeDataItem[] })?.data ?? []) as RealtimeDataItem[]
    
    // 转换为 Map 方便查找
    const dataMap: Record<number, RealtimeDataItem> = {}
    dataList.forEach((item) => {
      dataMap[item.dataPointId] = item
    })
    
    realtimeData.value = dataMap
    
    if (dataList.length > 0) {
      lastUpdateTime.value = new Date()
    }
  } catch (e) {
    console.error('获取实时数据失败', e)
  }
}

const startRealtimePolling = () => {
  fetchRealtimeData()
  realtimeTimer = window.setInterval(() => {
    fetchRealtimeData()
  }, 1000) // 每秒刷新一次
}

const stopRealtimePolling = () => {
  if (realtimeTimer) {
    clearInterval(realtimeTimer)
    realtimeTimer = null
  }
}

const getQualityClass = (quality: string) => {
  if (quality === 'Good') return 'good'
  if (quality === 'Bad') return 'bad'
  if (quality === 'Uncertain') return 'uncertain'
  return ''
}

const formatValue = (value: any, dataType: string | number) => {
  if (value === null || value === undefined) return '—'

  // 数字类型保留小数
  if (typeof value === 'number') {
    const strVal = value.toString()
    if (strVal.includes('.')) {
      return value.toFixed(2)
    }
    return strVal
  }

  return String(value)
}

// 根据数据类型自动设置寄存器长度
watch(() => form.value.dataType, (newType) => {
  if (newType === null) return
  // 32 位数据类型：Int32(4), UInt32(5), Float(6)
  if ([4, 5, 6].includes(newType)) {
    form.value.registerLength = 2
  }
  // 64 位数据类型：Int64(7), UInt64(8), Double(9)
  else if ([7, 8, 9].includes(newType)) {
    form.value.registerLength = 4
  }
  // 16 位数据类型
  else {
    form.value.registerLength = 1
  }
})

const openCreate = () => {
  editingDataPoint.value = null
  form.value = {
    name: '',
    tag: '',
    description: '',
    address: '',
    dataType: null,
    unit: '',
    isEnabled: true,
    modbusSlaveId: 1,
    modbusFunctionCode: 3,
    modbusByteOrder: 1,
    registerLength: 1
  }
  dialogVisible.value = true
}

const openEdit = (row: DataPointItem) => {
  editingDataPoint.value = row
  form.value = {
    name: row.name,
    tag: row.tag,
    description: row.description || '',
    address: row.address,
    dataType: row.dataTypeValue ?? (typeof row.dataType === 'number' ? row.dataType : null),
    unit: row.unit || '',
    isEnabled: row.isEnabled,
    modbusSlaveId: row.modbusSlaveId || 1,
    modbusFunctionCode: row.modbusFunctionCode || 3,
    modbusByteOrder: row.modbusByteOrder || 1,
    registerLength: row.registerLength || 1
  }
  dialogVisible.value = true
}

const submitForm = async () => {
  await formRef.value?.validate()
  submitting.value = true
  try {
    if (editingDataPoint.value) {
      // 编辑模式
      await updateDataPoint(deviceId.value, editingDataPoint.value.id, form.value)
      ElMessage.success('数据点更新成功')
    } else {
      // 创建模式
      await createDataPoint(deviceId.value, form.value)
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
      // 清除该数据点的实时数据
      delete realtimeData.value[row.id]
    })
    .catch(() => {})
}

onMounted(() => {
  fetchDevice()
  fetchDataPoints()
  loadDataValueTypes()
  loadModbusByteOrders()
  startRealtimePolling()
})

onUnmounted(() => {
  stopRealtimePolling()
})
</script>

<style scoped>
.page-header {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: 20px;
}
.header-left { display: flex; align-items: center; gap: 14px; }
.back-btn { color: var(--text-muted) !important; padding: 0 !important; }
.title-block { display: flex; align-items: baseline; gap: 12px; }
.page-title  { font-size: 22px; font-weight: 800; color: var(--text-primary); }
.device-tag  { font-size: 12px; color: var(--cyan); background: var(--cyan-dim); padding: 2px 10px; border-radius: 10px; border: 1px solid rgba(56,220,196,0.25); }

.stats-bar {
  display: flex; gap: 32px; margin-bottom: 16px;
  padding: 14px 20px; background: var(--bg-card);
  border: 1px solid var(--border-subtle); border-radius: var(--radius-lg);
}
.stat-item { display: flex; align-items: baseline; gap: 8px; }
.s-num   { font-size: 24px; font-weight: 700; color: var(--text-primary); }
.s-label { font-size: 12px; color: var(--text-muted); }

.table-wrap { background: var(--bg-card); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); overflow: hidden; }
.tag-text  { font-size: 13px; color: var(--cyan); }
.addr-text { font-size: 13px; color: var(--text-secondary); }
.time-text { font-size: 11px; color: var(--text-muted); }
:deep(.mono-input .el-input__inner) { font-family: var(--font-mono); }

/* 实时数据值样式 */
.realtime-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
}
.realtime-value.good { color: var(--text-success); }
.realtime-value.bad { color: var(--text-danger); }
.realtime-value.uncertain { color: var(--text-warning); }
.value-unit {
  font-size: 11px;
  color: var(--text-muted);
  margin-left: 2px;
  font-weight: 400;
}

/* 数据质量徽章 */
.badge.good { background: rgba(82,196,26,0.15); color: var(--text-success); border-color: rgba(82,196,26,0.3); }
.badge.bad { background: rgba(240,89,89,0.15); color: var(--text-danger); border-color: rgba(240,89,89,0.3); }
.badge.uncertain { background: rgba(250,173,40,0.15); color: var(--text-warning); border-color: rgba(250,173,40,0.3); }

/* 弹窗样式 */
.datapoint-dialog {
  max-height: 84vh;
  display: flex;
  flex-direction: column;
}
:deep(.el-dialog) {
  background: var(--bg-card) !important;
  border: 1px solid var(--border-muted) !important;
  border-radius: var(--radius-lg) !important;
  max-height: 84vh;
}
:deep(.el-dialog__header) { 
  border-bottom: 1px solid var(--border-subtle);
  padding: 14px 20px;
  flex-shrink: 0;
}
:deep(.el-dialog__title) { 
  color: var(--text-primary) !important;
  font-weight: 600;
  font-size: 15px;
}
:deep(.el-dialog__body) { 
  color: var(--text-primary);
  padding: 16px 20px;
  overflow-y: auto;
  flex: 1;
}
:deep(.el-dialog__footer) { 
  border-top: 1px solid var(--border-subtle);
  padding: 12px 20px;
  flex-shrink: 0;
}

/* 表单分组 */
.form-section {
  margin-bottom: 16px;
  padding: 14px;
  background: var(--bg-base);
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
}
.form-section:last-child {
  margin-bottom: 0;
}
.section-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 12px;
  padding-bottom: 6px;
  border-bottom: 1px solid var(--border-subtle);
}
.section-title .el-icon {
  color: var(--cyan);
  font-size: 14px;
}

/* 弹窗样式修复 */
:deep(.el-form-item__label) { color: var(--text-secondary) !important; font-size: 13px; }
:deep(.el-input__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-select__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-textarea__inner) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; color: var(--text-primary) !important; }
:deep(.mono-input .el-input__inner) { font-family: var(--font-mono); }
:deep(.el-dialog__body) { color: var(--text-primary); }
:deep(.el-dialog__footer) { border-top: 1px solid var(--border-subtle); }
:deep(.el-form-item__label) { color: var(--text-secondary) !important; }
:deep(.el-input__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-select__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-textarea__inner) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; color: var(--text-primary) !important; }

/* 字节序选项样式 */
.byte-order-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
.byte-order-desc {
  font-size: 11px;
  color: var(--text-muted);
}
</style>
