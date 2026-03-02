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

      <AddCard class="device-card" text="新增设备" @click="openCreate" />
    </div>

    <!-- 新增/编辑设备弹窗：紧凑排版 -->
    <el-dialog
      v-model="dialogVisible"
      :title="editingDevice ? '编辑设备' : '新增设备'"
      width="800px" destroy-on-close class="device-dialog app-dialog device-dialog-compact" align-center
    >
      <el-form
        ref="formRef" :model="form" :rules="rules"
        label-width="92px" label-position="left" class="device-form"
      >
        <FormSection title="基本信息" icon="Document" class="compact">
          <el-row :gutter="16">
            <el-col :span="8">
              <el-form-item label="设备名称" prop="name">
                <el-input v-model="form.name" placeholder="如：车间 A-PLC01" @blur="generateCodeIfEmpty" />
              </el-form-item>
            </el-col>
            <el-col :span="16">
              <el-form-item label="设备编码" prop="code">
                <div class="device-code-with-btn">
                  <el-input v-model="form.code" placeholder="DEV_PLC_001" class="device-code-input" />
                  <el-button size="small" text class="btn-auto-generate" @click="generateCode">
                    <el-icon><MagicStick /></el-icon>
                    <span>自动生成</span>
                  </el-button>
                </div>
              </el-form-item>
            </el-col>
          </el-row>
          <el-form-item label="描述">
            <el-input v-model="form.description" type="textarea" :rows="1" placeholder="可选描述信息" autosize />
          </el-form-item>
        </FormSection>

        <FormSection title="通信配置" icon="Setting" class="compact">
          <el-form-item label="通信协议" prop="protocol" v-if="!editingDevice">
            <el-select v-model="form.protocol" placeholder="选择协议" style="width:100%">
              <el-option v-for="o in CollectionProtocolOptions" :key="o.value" :label="o.label" :value="o.value">
                <div class="protocol-option">
                  <span>{{ o.label }}</span>
                  <span class="protocol-desc">{{ o.desc }}</span>
                </div>
              </el-option>
            </el-select>
          </el-form-item>
          <el-form-item v-else label="通信协议">
            <el-tag>{{ CollectionProtocolOptions.find(o => o.value === form.protocol)?.label || '-' }}</el-tag>
          </el-form-item>

          <!-- 协议说明：单行紧凑 -->
          <div v-if="isModbusDevice" class="protocol-hint protocol-hint-inline">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>Modbus TCP/RTU，支持功能码 01/02/03/04/05/06/15/16。</span>
          </div>
          <div v-else-if="form.protocol === CollectionProtocol.OpcUa.value" class="protocol-hint protocol-hint-inline">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>OPC UA，填写服务器端点地址。</span>
          </div>
          <div v-else-if="form.protocol === CollectionProtocol.S7.value" class="protocol-hint protocol-hint-inline">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>S7 协议，填写 PLC IP 与机架/槽号。</span>
          </div>
          <div v-else-if="form.protocol === CollectionProtocol.Http.value" class="protocol-hint protocol-hint-inline">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>HTTP 接口，填写数据源 URL。</span>
          </div>
          <div v-else-if="isSimulatorDevice" class="protocol-hint protocol-hint-inline">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>模拟器自动生成随机数据，无需地址配置。</span>
          </div>

          <!-- 设备地址/端口 -->
          <template v-if="needAddressPort">
            <el-row :gutter="16">
              <el-col :span="showPortField ? 16 : 24">
                <el-form-item label="设备地址" prop="address">
                  <el-input v-model="form.address" :placeholder="addressPlaceholder" />
                </el-form-item>
              </el-col>
              <el-col v-if="showPortField" :span="8">
                <el-form-item label="端口">
                  <el-input-number v-model="form.port" :min="1" :max="65535" placeholder="502" style="width:100%" controls-position="right" />
                </el-form-item>
              </el-col>
            </el-row>
          </template>
        </FormSection>

        <FormSection title="采集配置" icon="Timer" class="compact">
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item label="采集周期" prop="pollingIntervalMs">
                <el-input-number
                  v-model="form.pollingIntervalMs"
                  :min="100" :max="60000" :step="500"
                  style="width:80%" controls-position="right"
                />
                <span class="form-hint" style="margin-left:8px">ms</span>
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="是否启用" class="form-item-inline">
                <el-switch v-model="form.isEnabled" active-color="#38dcc4" />
                <span class="form-hint">停用后停止采集</span>
              </el-form-item>
            </el-col>
          </el-row>
        </FormSection>

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
import { ElMessage } from 'element-plus'
import { Plus, Edit, Delete, DataLine, Location, Timer, MagicStick, InfoFilled } from '@element-plus/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import FormSection from '@/components/FormSection.vue'
import AddCard from '@/components/AddCard.vue'
import { useConfirmDelete } from '@/composables/useConfirmDelete'
import {
  getDevices,
  createDevice,
  updateDevice,
  deleteDevice,
  toggleDevice as apiToggle
} from '@/api/device'
import { generateCodeWithTimestamp } from '@/utils/codeGenerate'
import { getCollectionProtocols } from '@/api/enums'
import { formatInterval } from '@/api/constants'
import { CollectionProtocol } from '@/api/constants'
import type { DeviceItem } from '@/types'

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

/** 从设备数据解析出协议数值（用于表单回显与筛选），兼容 protocolValue / protocol 数字或字符串 */
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

/** 当前表单所选协议是否为 Modbus */
const isModbusDevice = computed(() => form.value.protocol === CollectionProtocol.Modbus.value)
/** 当前表单所选协议是否为模拟器 */
const isSimulatorDevice = computed(() => form.value.protocol === CollectionProtocol.Simulator.value)
/** 是否需要显示设备地址/端口（Modbus、OPC UA、S7、HTTP 需要，模拟器不需要） */
const needAddressPort = computed(() => {
  const p = form.value.protocol
  return p !== null && p !== CollectionProtocol.Simulator.value
})
/** 设备地址输入框占位文案，按协议区分 */
const addressPlaceholder = computed(() => {
  switch (form.value.protocol) {
    case CollectionProtocol.Modbus.value: return 'IP 地址 或 COM3'
    case CollectionProtocol.OpcUa.value: return 'opc.tcp://host:4840'
    case CollectionProtocol.S7.value: return 'PLC IP 地址'
    case CollectionProtocol.Http.value: return 'https://api.example.com/data'
    default: return '设备地址或 URL'
  }
})
/** 是否显示端口字段（Modbus、S7 需要端口，OPC UA/HTTP 多为 URL 内含端口） */
const showPortField = computed(() => {
  const p = form.value.protocol
  return p === CollectionProtocol.Modbus.value || p === CollectionProtocol.S7.value
})

const getProtocolColor = (value: number) => {
  const protocol = CollectionProtocolOptions.value.find(p => p.value === value)
  return protocol ? '#38dcc4' : '#8fa5c5'
}
const getProtocolBg = (value: number) => {
  const protocol = CollectionProtocolOptions.value.find(p => p.value === value)
  return protocol ? `${protocol.color}22` : '#8fa5c522'
}

// 根据设备名称生成设备编码（名称转编码工具）
const generateCode = () => {
  if (!form.value.name?.trim()) {
    ElMessage.warning('请先输入设备名称')
    return
  }
  form.value.code = generateCodeWithTimestamp(form.value.name, 'DEV')
  ElMessage.success('设备编码已生成')
}

// 设备名称失去焦点时，如果编码为空则自动生成
const generateCodeIfEmpty = () => {
  if (!form.value.code && form.value.name) {
    generateCode()
  }
}

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
  code: [
    { required: true, message: '请输入设备编码' },
    { 
      validator: async (rule, value, callback) => {
        if (!value) return callback()
        if (editingDevice.value && value === editingDevice.value.code) return callback()
        
        try {
          const res = await getDevices()
          const devices = (res as { data?: any[] })?.data || []
          if (devices.some(d => d.code === value)) {
            callback(new Error('设备编码已存在'))
          } else {
            callback()
          }
        } catch (error) {
          callback()
        }
      },
      trigger: 'blur'
    }
  ],
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
    code: row.code ?? '',
    description: row.description ?? '',
    protocol: resolveProtocolValue(row),
    address: row.address ?? '',
    port: row.port ?? null,
    pollingIntervalMs: row.pollingIntervalMs ?? 1000,
    isEnabled: row.isEnabled ?? true
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

onMounted(() => {
  fetchDevices()
  loadCollectionProtocols()
})
</script>

<style scoped>
/* 工具栏 */
.toolbar {
  display: flex; align-items: center; gap: 12px; margin-bottom: 20px;
}
.total-hint { font-size: 12px; color: var(--text-muted); margin-left: auto; }

/* 设备编码：输入框与「自动生成」并排，避免遮挡 */
.device-code-with-btn {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
}
.device-code-with-btn .device-code-input {
  flex: 1;
  min-width: 0;
}
.device-code-with-btn .btn-auto-generate {
  flex-shrink: 0;
  padding: 0 10px;
  font-size: 12px;
  background: transparent !important;
  border: 1px solid var(--border-subtle) !important;
  border-radius: var(--radius) !important;
  color: var(--text-secondary) !important;
  transition: background 0.2s, color 0.2s, border-color 0.2s;
}
.device-code-with-btn .btn-auto-generate:hover {
  background: var(--bg-hover) !important;
  border-color: var(--border-muted) !important;
  color: var(--cyan) !important;
}
.device-code-with-btn .btn-auto-generate .el-icon {
  margin-right: 4px;
  font-size: 14px;
  color: inherit;
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

.device-card.add-card { min-height: 200px; }

/* 弹窗内协议选项与提示 */

/* 协议选项样式 */
.protocol-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
.protocol-desc {
  font-size: 11px;
  color: var(--text-muted);
}

/* 协议提示 */
.protocol-hint {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 10px;
  padding: 8px 12px;
  background: var(--bg-card);
  border-radius: var(--radius-sm);
  border: 1px solid var(--border-subtle);
  font-size: 12px;
  color: var(--text-secondary);
  line-height: 1.5;
}
.protocol-hint .hint-icon {
  color: var(--cyan);
  font-size: 14px;
}
/* 协议说明单行紧凑（设备弹窗内） */
.protocol-hint-inline {
  flex-direction: row !important;
  align-items: center;
  gap: 6px;
  margin-top: 6px !important;
  padding: 5px 10px !important;
  font-size: 11px !important;
  line-height: 1.4;
}
.protocol-hint-inline .hint-icon {
  font-size: 12px;
  flex-shrink: 0;
}

/* 表单提示 */
.form-hint {
  font-size: 11px;
  color: var(--text-muted);
  margin-top: 4px;
  line-height: 1.4;
}

/* 弹窗样式修复 */
:deep(.el-form-item__label) { color: var(--text-secondary) !important; font-size: 13px; }
:deep(.el-input__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-select__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-textarea__inner) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; color: var(--text-primary) !important; }
:deep(.el-input-number) { background: var(--bg-base) !important; border: 1px solid var(--border-muted) !important; }
:deep(.el-tag) { margin-top: 6px; }
</style>
