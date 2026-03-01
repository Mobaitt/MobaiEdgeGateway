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

      <!-- 新增占位卡 -->
      <div class="device-card add-card" @click="openCreate">
        <el-icon size="36" color="var(--border-muted)"><Plus /></el-icon>
        <span style="color:var(--text-muted);font-size:13px;margin-top:8px">新增设备</span>
      </div>
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
import { Plus, Edit, Delete, DataLine, Location, Timer } from '@element-plus/icons-vue'
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
    devices.value = ((res as { data?: DeviceItem[] })?.data ?? []) as DeviceItem[]
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

onMounted(fetchDevices)
</script>

<style scoped>
.page-header {
  display: flex; align-items: flex-start; justify-content: space-between;
  margin-bottom: 24px;
}
.page-title { font-size: 22px; font-weight: 800; color: var(--text-primary); }
.page-desc  { font-size: 13px; color: var(--text-muted); margin-top: 4px; }

/* 工具栏 */
.toolbar {
  display: flex; align-items: center; gap: 12px; margin-bottom: 20px;
}
.total-hint { font-size: 12px; color: var(--text-muted); margin-left: auto; }

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
}
.device-card::before {
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
.device-card:hover {
  border-color: var(--cyan);
  box-shadow: 0 8px 32px rgba(0, 255, 255, 0.1);
  transform: translateY(-2px);
}
.device-card:hover::before {
  opacity: 1;
}
.device-card.disabled {
  opacity: 0.5;
  filter: grayscale(0.3);
}
.device-card.disabled:hover {
  transform: none;
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
}
.device-address .el-icon {
  flex-shrink: 0;
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
}
.stat-item .mono {
  font-size: 14px;
  font-weight: 600;
  color: var(--cyan);
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
}
.foot-actions .el-button {
  font-size: 12px;
  padding: 4px 8px;
}

/* 新增卡片 */
.add-card {
  border: 1px dashed var(--border-muted);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  min-height: 200px;
  transition: all 0.25s;
  background: transparent;
}
.add-card:hover {
  border-color: var(--cyan);
  background: rgba(0, 255, 255, 0.03);
}
.add-card .el-icon {
  transition: transform 0.25s;
}
.add-card:hover .el-icon {
  transform: scale(1.1) rotate(90deg);
}

/* 弹窗样式 */
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
