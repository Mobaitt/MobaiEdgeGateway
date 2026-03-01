<template>
  <div class="devices-view page-enter">
    <!-- 页面头部 -->
    <div class="page-header">
      <div>
        <h1 class="page-title">设备管理</h1>
        <p class="page-desc">管理边缘采集设备及其通信配置</p>
      </div>
      <el-button type="primary" :icon="Plus" @click="openCreate">新增设备</el-button>
    </div>

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
      <span class="total-hint mono">共{{ filteredDevices.length }} 台设备</span>
    </div>

    <!-- 设备表格 -->
    <div class="table-wrap">
      <el-table :data="filteredDevices" v-loading="loading" row-key="id">
        <el-table-column label="状态" width="70" align="center">
          <template #default="{ row }">
            <div style="display:flex;justify-content:center">
              <div class="pulse-dot" :class="row.isEnabled ? 'online' : 'offline'" />
            </div>
          </template>
        </el-table-column>

        <el-table-column prop="name" label="设备名称" min-width="160">
          <template #default="{ row }">
            <div class="device-name">{{ row.name }}</div>
            <div class="device-code mono">{{ row.code }}</div>
          </template>
        </el-table-column>

        <el-table-column label="协议" width="130">
          <template #default="{ row }">
            <el-tag
              :style="{ background: getProtocolBg(row.protocolValue), color: getProtocolColor(row.protocolValue), border: 'none' }"
              size="small"
            >
              {{ row.protocol }}
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column label="地址" min-width="180">
          <template #default="{ row }">
            <span class="mono addr-text">{{ row.address }}{{ row.port ? `:${row.port}` : '' }}</span>
          </template>
        </el-table-column>

        <el-table-column label="采集周期" width="120" align="center">
          <template #default="{ row }">
            <span class="mono badge info">{{ formatInterval(row.pollingIntervalMs) }}</span>
          </template>
        </el-table-column>

        <el-table-column label="数据点" width="100" align="center">
          <template #default="{ row }">
            <span class="mono dp-count">{{ row.dataPointCount }}</span>
          </template>
        </el-table-column>

        <el-table-column label="操作" width="220" align="right">
          <template #default="{ row }">
            <el-button size="small" text @click="goDataPoints(row)">
              <el-icon><DataLine /></el-icon> 数据点
            </el-button>
            <el-button size="small" text @click="openEdit(row)">
              <el-icon><Edit /></el-icon> 编辑
            </el-button>
            <el-button size="small" text @click="toggleDevice(row)" :loading="row._toggling">
              {{ row.isEnabled ? '禁用' : '启用' }}
            </el-button>
            <el-button size="small" text type="danger" @click="confirmDelete(row)">
              <el-icon><Delete /></el-icon>
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 新增/编辑设备弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="editingDevice ? '编辑设备' : '新增设备'"
      width="560px" destroy-on-close
    >
      <el-form
        ref="formRef" :model="form" :rules="rules"
        label-width="100px" label-position="left"
      >
        <el-form-item label="设备名称" prop="name">
          <el-input v-model="form.name" placeholder="如：车间 A-PLC01" />
        </el-form-item>
        <el-form-item label="设备编码" prop="code" v-if="!editingDevice">
          <el-input v-model="form.code" placeholder="全局唯一编码，如 DEV_PLC_001" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="2" placeholder="可选" />
        </el-form-item>
        <el-form-item label="通信协议" prop="protocol" v-if="!editingDevice">
          <el-select v-model="form.protocol" placeholder="选择协议" style="width:100%">
            <el-option v-for="o in CollectionProtocolOptions" :key="o.value" :label="o.label" :value="o.value" />
          </el-select>
        </el-form-item>
        <el-form-item label="设备地址" prop="address">
          <el-input v-model="form.address" placeholder="IP 地址 或 COM3" />
        </el-form-item>
        <el-form-item label="端口">
          <el-input-number v-model="form.port" :min="1" :max="65535" placeholder="502" style="width:100%" controls-position="right" />
        </el-form-item>
        <el-form-item label="采集周期" prop="pollingIntervalMs">
          <el-input-number
            v-model="form.pollingIntervalMs" :min="100" :max="60000" :step="500"
            style="width:100%" controls-position="right"
          />
          <span style="margin-left:8px;color:var(--text-muted);font-size:12px">毫秒，100 - 60000</span>
        </el-form-item>
        <el-form-item label="是否启用">
          <el-switch v-model="form.isEnabled" active-color="#38dcc4" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitForm">
          {{ editingDevice ? '保存修改' : '创建设备' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessageBox, ElMessage } from 'element-plus'
import { Plus, Edit, Delete, DataLine } from '@element-plus/icons-vue'
import {
  getDevices,
  createDevice,
  updateDevice,
  deleteDevice,
  toggleDevice as apiToggle
} from '@/api/device'
import { CollectionProtocolOptions, getProtocolConfig, formatInterval } from '@/api/constants'

type DeviceItem = {
  id: number
  name: string
  code: string
  description?: string
  protocol?: number
  protocolValue: number
  address: string
  port?: number | null
  pollingIntervalMs: number
  isEnabled: boolean
  dataPointCount: number
  _toggling?: boolean
}

type DeviceForm = {
  name: string
  code?: string
  description: string
  protocol: number | null
  address: string
  port: number | null
  pollingIntervalMs: number
  isEnabled: boolean
}

const router = useRouter()
const loading = ref(false)
const devices = ref<DeviceItem[]>([])
const searchText = ref('')
const filterProtocol = ref<number | null>(null)

const filteredDevices = computed(() => {
  return devices.value.filter((device) => {
    const matchText =
      !searchText.value ||
      device.name.includes(searchText.value) ||
      device.code.includes(searchText.value)

    const matchProtocol = !filterProtocol.value || device.protocolValue === filterProtocol.value
    return matchText && matchProtocol
  })
})

const getProtocolColor = (value: number) => getProtocolConfig(value).color
const getProtocolBg = (value: number) => `${getProtocolConfig(value).color}22`

const dialogVisible = ref(false)
const editingDevice = ref<DeviceItem | null>(null)
const submitting = ref(false)
const formRef = ref<any>()
const form = ref<DeviceForm>({
  name: '',
  code: '',
  description: '',
  protocol: null,
  address: '',
  port: null,
  pollingIntervalMs: 1000,
  isEnabled: true
})

const rules = {
  name: [{ required: true, message: '请输入设备名称' }],
  code: [{ required: true, message: '请输入设备编码' }],
  protocol: [{ required: true, message: '请选择通信协议' }],
  address: [{ required: true, message: '请输入设备地址' }],
  pollingIntervalMs: [{ required: true, message: '请输入采集周期' }]
}

const fetchDevices = async () => {
  loading.value = true
  try {
    const res = await getDevices()
    devices.value = (((res as { data?: DeviceItem[] })?.data ?? []) as DeviceItem[]).map((device) => ({
      ...device,
      _toggling: false
    }))
  } finally {
    loading.value = false
  }
}

const openCreate = () => {
  editingDevice.value = null
  form.value = {
    name: '',
    code: '',
    description: '',
    protocol: null,
    address: '',
    port: null,
    pollingIntervalMs: 1000,
    isEnabled: true
  }
  dialogVisible.value = true
}

const openEdit = (row: DeviceItem) => {
  editingDevice.value = row
  form.value = {
    name: row.name,
    description: row.description ?? '',
    protocol: row.protocol ?? null,
    address: row.address,
    port: row.port ?? null,
    pollingIntervalMs: row.pollingIntervalMs,
    isEnabled: row.isEnabled
  }
  dialogVisible.value = true
}

const submitForm = async () => {
  await formRef.value?.validate()
  submitting.value = true
  try {
    if (editingDevice.value) {
      await updateDevice(editingDevice.value.id, form.value)
      ElMessage.success('设备更新成功')
    } else {
      await createDevice(form.value)
      ElMessage.success('设备创建成功')
    }
    dialogVisible.value = false
    fetchDevices()
  } finally {
    submitting.value = false
  }
}

const confirmDelete = (row: DeviceItem) => {
  ElMessageBox.confirm(
    `确定要删除设备 "${row.name}" 吗？此操作将同时移除所有相关的数据点和映射关系。`,
    '删除确认',
    {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger'
    }
  )
    .then(async () => {
      await deleteDevice(row.id)
      ElMessage.success('删除成功')
      fetchDevices()
    })
    .catch(() => {})
}

const toggleDevice = async (row: DeviceItem) => {
  row._toggling = true
  try {
    await apiToggle(row.id)
    row.isEnabled = !row.isEnabled
    ElMessage.success(row.isEnabled ? '设备已启用' : '设备已禁用')
  } finally {
    row._toggling = false
  }
}

const goDataPoints = (row: DeviceItem) => {
  router.push({ name: 'DataPoints', params: { id: row.id }, query: { deviceName: row.name } })
}

onMounted(fetchDevices)
</script>

<style scoped>
.page-header {
  display: flex; align-items: flex-start; justify-content: space-between;
  margin-bottom: 20px;
}
.page-title { font-size: 22px; font-weight: 800; color: var(--text-primary); }
.page-desc  { font-size: 13px; color: var(--text-muted); margin-top: 4px; }
.toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 16px; }
.total-hint { font-size: 12px; color: var(--text-muted); margin-left: auto; }
.table-wrap { background: var(--bg-card); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); overflow: hidden; }
.device-name { font-size: 14px; font-weight: 600; color: var(--text-primary); }
.device-code { font-size: 11px; color: var(--text-muted); margin-top: 2px; }
.addr-text   { font-size: 13px; color: var(--text-secondary); }
.dp-count    { font-size: 15px; font-weight: 700; color: var(--text-accent); }

/* 弹窗样式修复 */
:deep(.el-dialog) { 
  background: var(--bg-card) !important; 
  border: 1px solid var(--border-muted) !important;
  border-radius: var(--radius-lg) !important;
}
:deep(.el-dialog__header) { border-bottom: 1px solid var(--border-subtle); }
:deep(.el-dialog__title) { color: var(--text-primary) !important; }
:deep(.el-dialog__body) { color: var(--text-primary); }
:deep(.el-dialog__footer) { border-top: 1px solid var(--border-subtle); }
:deep(.el-form-item__label) { color: var(--text-secondary) !important; }
:deep(.el-input__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-select__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-textarea__inner) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; color: var(--text-primary) !important; }
:deep(.el-input-number) { background: var(--bg-base) !important; border: 1px solid var(--border-muted) !important; }
</style>
