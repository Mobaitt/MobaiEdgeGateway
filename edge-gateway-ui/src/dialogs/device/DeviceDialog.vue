<template>
  <el-dialog
    :model-value="modelValue"
    :title="editingDevice ? '编辑设备' : '新增设备'"
    width="800px"
    destroy-on-close
    class="app-dialog device-dialog"
    align-center
    @update:model-value="$emit('update:modelValue', $event)"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="92px"
      label-position="left"
      class="device-form"
    >
      <FormSection title="基本信息" icon="Document" class="compact">
        <el-row :gutter="16">
          <el-col :span="8">
            <el-form-item label="设备名称" prop="name">
              <el-input
                v-model="form.name"
                placeholder="如：车间 A-PLC01"
                @blur="handleNameBlur"
              />
            </el-form-item>
          </el-col>
          <el-col :span="16">
            <el-form-item label="设备编码" prop="code">
              <div class="input-with-btn">
                <el-input
                  v-model="form.code"
                  placeholder="DEV_PLC_001"
                  class="input-field"
                />
                <el-button size="small" text class="btn-auto-generate" @click="handleGenerateCode">
                  <el-icon><MagicStick /></el-icon>
                  <span>自动生成</span>
                </el-button>
              </div>
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="描述">
          <el-input
            v-model="form.description"
            type="textarea"
            :rows="1"
            placeholder="可选描述信息"
            autosize
          />
        </el-form-item>
      </FormSection>

      <FormSection title="通信配置" icon="Setting" class="compact">
        <el-form-item v-if="!editingDevice" label="通信协议" prop="protocol">
          <el-select v-model="form.protocol" placeholder="选择协议" style="width: 100%">
            <el-option
              v-for="o in protocolOptions"
              :key="o.value"
              :label="o.label"
              :value="o.value"
            >
              <div class="protocol-option">
                <span>{{ o.label }}</span>
                <span class="protocol-desc">{{ o.desc }}</span>
              </div>
            </el-option>
          </el-select>
        </el-form-item>
        <el-form-item v-else label="通信协议">
          <el-tag>
            {{ protocolOptions.find(o => o.value === form.protocol)?.label || '-' }}
          </el-tag>
        </el-form-item>

        <div v-if="isModbusDevice" class="protocol-hint protocol-hint-inline">
          <el-icon class="hint-icon"><InfoFilled /></el-icon>
          <span>Modbus TCP/RTU，支持功能码 01/02/03/04/05/06/15/16。</span>
        </div>
        <div v-else-if="isOpcUaDevice" class="protocol-hint protocol-hint-inline">
          <el-icon class="hint-icon"><InfoFilled /></el-icon>
          <span>OPC UA，填写服务器端点地址。</span>
        </div>
        <div v-else-if="isS7Device" class="protocol-hint protocol-hint-inline">
          <el-icon class="hint-icon"><InfoFilled /></el-icon>
          <span>S7 协议，填写 PLC IP 与机架/槽号。</span>
        </div>
        <div v-else-if="isHttpDevice" class="protocol-hint protocol-hint-inline">
          <el-icon class="hint-icon"><InfoFilled /></el-icon>
          <span>HTTP 接口，填写数据源 URL。</span>
        </div>
        <div v-else-if="isSimulatorDevice" class="protocol-hint protocol-hint-inline">
          <el-icon class="hint-icon"><InfoFilled /></el-icon>
          <span>模拟器自动生成随机数据，无需地址配置。</span>
        </div>

        <template v-if="needAddressPort">
          <el-row :gutter="16">
            <el-col :span="showPortField ? 16 : 24">
              <el-form-item label="设备地址" prop="address">
                <el-input
                  v-model="form.address"
                  :placeholder="addressPlaceholder"
                />
              </el-form-item>
            </el-col>
            <el-col v-if="showPortField" :span="8">
              <el-form-item label="端口">
                <el-input-number
                  v-model="form.port"
                  :min="1"
                  :max="65535"
                  placeholder="502"
                  style="width: 100%"
                  controls-position="right"
                />
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
                :min="100"
                :max="60000"
                :step="500"
                style="width: 80%"
                controls-position="right"
              />
              <span class="form-hint" style="margin-left: 8px">ms</span>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="是否启用" class="form-item-inline">
              <el-switch v-model="form.isEnabled" active-color="#38dcc4" />
              <span class="form-hint">停用后停止采集</span>
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item>
              <template #label>
                <span class="label-with-tip">
                  启用重连
                  <el-tooltip content="设备连接不上，或读取异常满足重连条件时，会自动重新建立连接。" placement="top">
                    <el-icon class="field-help"><QuestionFilled /></el-icon>
                  </el-tooltip>
                </span>
              </template>
              <el-switch v-model="form.reconnectEnabled" active-color="#38dcc4" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item>
              <template #label>
                <span class="label-with-tip">
                  失败阈值
                  <el-tooltip content="连续读取失败达到这个次数后立即重连，例如设为 3，则连续失败 3 次就重连。" placement="top">
                    <el-icon class="field-help"><QuestionFilled /></el-icon>
                  </el-tooltip>
                </span>
              </template>
              <el-input-number
                v-model="form.maxConsecutiveReadFailures"
                :min="1"
                :max="100"
                style="width: 100%"
                controls-position="right"
              />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="8">
            <el-form-item>
              <template #label>
                <span class="label-with-tip">
                  单轮次数
                  <el-tooltip content="一次重连轮次里最多尝试连接多少次，例如设为 3，则本轮最多连 3 次。" placement="top">
                    <el-icon class="field-help"><QuestionFilled /></el-icon>
                  </el-tooltip>
                </span>
              </template>
              <el-input-number
                v-model="form.reconnectRetryCount"
                :min="1"
                :max="100"
                style="width: 100%"
                controls-position="right"
                :disabled="!form.reconnectEnabled"
              />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item>
              <template #label>
                <span class="label-with-tip">
                  单次间隔
                  <el-tooltip content="同一轮里每次重试之间等待多久，例如 1000 表示等待 1 秒再试下一次。" placement="top">
                    <el-icon class="field-help"><QuestionFilled /></el-icon>
                  </el-tooltip>
                </span>
              </template>
              <el-input-number
                v-model="form.reconnectRetryDelayMs"
                :min="100"
                :max="60000"
                :step="100"
                style="width: 100%"
                controls-position="right"
                :disabled="!form.reconnectEnabled"
              />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item>
              <template #label>
                <span class="label-with-tip">
                  轮次间隔
                  <el-tooltip content="一整轮重连都失败后，下一轮开始前等待多久。" placement="top">
                    <el-icon class="field-help"><QuestionFilled /></el-icon>
                  </el-tooltip>
                </span>
              </template>
              <el-input-number
                v-model="form.reconnectIntervalMs"
                :min="500"
                :max="600000"
                :step="500"
                style="width: 100%"
                controls-position="right"
                :disabled="!form.reconnectEnabled"
              />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item>
              <template #label>
                <span class="label-with-tip">
                  比例窗口
                  <el-tooltip content="统计最近多少次读取结果用于计算失败率，例如设为 10，则看最近 10 次采集。" placement="top">
                    <el-icon class="field-help"><QuestionFilled /></el-icon>
                  </el-tooltip>
                </span>
              </template>
              <el-input-number
                v-model="form.readFailureWindowSize"
                :min="3"
                :max="1000"
                style="width: 100%"
                controls-position="right"
                :disabled="!form.reconnectEnabled"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item>
              <template #label>
                <span class="label-with-tip">
                  比例阈值
                  <el-tooltip content="在比例窗口内失败占比超过这个值就重连，例如窗口 10、阈值 50%，表示最近 10 次里失败 5 次及以上就重连。" placement="top">
                    <el-icon class="field-help"><QuestionFilled /></el-icon>
                  </el-tooltip>
                </span>
              </template>
              <el-input-number
                v-model="form.readFailureRateThresholdPercent"
                :min="1"
                :max="100"
                :step="5"
                style="width: 80%"
                controls-position="right"
                :disabled="!form.reconnectEnabled"
              />
              <span class="form-hint" style="margin-left: 8px">%</span>
            </el-form-item>
          </el-col>
        </el-row>
      </FormSection>
    </el-form>

    <template #footer>
      <el-button @click="handleCancel">取消</el-button>
      <el-button
        type="primary"
        :loading="submitting"
        @click="handleSubmit"
      >
        {{ editingDevice ? '保存修改' : '创建设备' }}
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { InfoFilled, MagicStick, QuestionFilled } from '@element-plus/icons-vue'
import FormSection from '@/components/FormSection.vue'
import { generateCodeWithTimestamp } from '@/utils/codeGenerate'
import { CollectionProtocol } from '@/api/constants'
import type { DeviceItem } from '@/types'

interface DeviceForm {
  name: string
  code: string
  description: string
  protocol: number | null
  address: string
  port: number | null
  pollingIntervalMs: number
  isEnabled: boolean
  reconnectEnabled: boolean
  reconnectRetryCount: number
  reconnectRetryDelayMs: number
  reconnectIntervalMs: number
  maxConsecutiveReadFailures: number
  readFailureWindowSize: number
  readFailureRateThresholdPercent: number
}

interface Props {
  modelValue: boolean
  editingDevice: DeviceItem | null
  protocolOptions: any[]
  submitting: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'submit', data: DeviceForm): void
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  editingDevice: null,
  protocolOptions: () => [],
  submitting: false
})

const emit = defineEmits<Emits>()

const formRef = ref<any>()
const form = ref<DeviceForm>({
  name: '',
  code: '',
  description: '',
  protocol: null,
  address: '',
  port: null,
  pollingIntervalMs: 1000,
  isEnabled: true,
  reconnectEnabled: true,
  reconnectRetryCount: 3,
  reconnectRetryDelayMs: 1000,
  reconnectIntervalMs: 5000,
  maxConsecutiveReadFailures: 3,
  readFailureWindowSize: 10,
  readFailureRateThresholdPercent: 50
})

const rules = {
  name: [{ required: true, message: '请输入设备名称' }],
  code: [{ required: true, message: '请输入设备编码' }],
  protocol: [{ required: true, message: '请选择通信协议' }],
  address: [{ required: true, message: '请输入设备地址' }],
  pollingIntervalMs: [{ required: true, message: '请输入采集周期' }]
}

const resolveProtocolValue = (device: DeviceItem): number | null => {
  const d = device as any
  if (typeof d.protocolValue === 'number') return d.protocolValue
  if (typeof d.protocol === 'number') return d.protocol
  if (typeof d.protocol === 'string') {
    const opt = props.protocolOptions.find((o) => o.label === d.protocol)
    return opt ? opt.value : null
  }
  return null
}

const resetForm = () => {
  form.value = {
    name: '',
    code: '',
    description: '',
    protocol: null,
    address: '',
    port: null,
    pollingIntervalMs: 1000,
    isEnabled: true,
    reconnectEnabled: true,
    reconnectRetryCount: 3,
    reconnectRetryDelayMs: 1000,
    reconnectIntervalMs: 5000,
    maxConsecutiveReadFailures: 3,
    readFailureWindowSize: 10,
    readFailureRateThresholdPercent: 50
  }
  formRef.value?.clearValidate()
}

watch(
  () => props.editingDevice,
  (device) => {
    if (device) {
      const protocolNum = resolveProtocolValue(device)
      form.value = {
        name: device.name,
        code: device.code ?? '',
        description: device.description ?? '',
        protocol: protocolNum,
        address: device.address ?? '',
        port: device.port ?? null,
        pollingIntervalMs: device.pollingIntervalMs ?? 1000,
        isEnabled: device.isEnabled ?? true,
        reconnectEnabled: device.reconnectEnabled ?? true,
        reconnectRetryCount: device.reconnectRetryCount ?? 3,
        reconnectRetryDelayMs: device.reconnectRetryDelayMs ?? 1000,
        reconnectIntervalMs: device.reconnectIntervalMs ?? 5000,
        maxConsecutiveReadFailures: device.maxConsecutiveReadFailures ?? 3,
        readFailureWindowSize: device.readFailureWindowSize ?? 10,
        readFailureRateThresholdPercent: device.readFailureRateThresholdPercent ?? 50
      }
    } else {
      resetForm()
    }
  },
  { immediate: true }
)

const isModbusDevice = computed(() => form.value.protocol === CollectionProtocol.Modbus.value)
const isSimulatorDevice = computed(() => form.value.protocol === CollectionProtocol.Simulator.value)
const isOpcUaDevice = computed(() => form.value.protocol === CollectionProtocol.OpcUa.value)
const isS7Device = computed(() => form.value.protocol === CollectionProtocol.S7.value)
const isHttpDevice = computed(() => form.value.protocol === CollectionProtocol.Http.value)

const needAddressPort = computed(() => {
  const p = form.value.protocol
  return p !== null && p !== CollectionProtocol.Simulator.value
})

const addressPlaceholder = computed(() => {
  switch (form.value.protocol) {
    case CollectionProtocol.Modbus.value: return 'IP 地址 或 COM3'
    case CollectionProtocol.OpcUa.value: return 'opc.tcp://host:4840'
    case CollectionProtocol.S7.value: return 'PLC IP 地址'
    case CollectionProtocol.Http.value: return 'https://api.example.com/data'
    default: return '设备地址或 URL'
  }
})

const showPortField = computed(() => {
  const p = form.value.protocol
  return p === CollectionProtocol.Modbus.value || p === CollectionProtocol.S7.value
})

const handleNameBlur = () => {
  if (!form.value.code && form.value.name?.trim()) {
    form.value.code = generateCodeWithTimestamp(form.value.name, 'DEV')
  }
}

const handleGenerateCode = () => {
  if (!form.value.name?.trim()) {
    ElMessage.warning('请先输入设备名称')
    return
  }
  form.value.code = generateCodeWithTimestamp(form.value.name, 'DEV')
  ElMessage.success('设备编码已生成')
}

const handleSubmit = async () => {
  await formRef.value?.validate()
  emit('submit', { ...form.value })
}

const handleCancel = () => {
  emit('update:modelValue', false)
}

const handleClose = () => {
  formRef.value?.resetFields()
  emit('close')
}
</script>

<style scoped lang="scss">
.device-form {
  :deep(.form-section) {
    border-radius: 18px;
    padding: 18px;
    background:
      linear-gradient(180deg, rgba(255, 255, 255, 0.96), rgba(248, 251, 252, 0.92));
    box-shadow: 0 10px 30px rgba(15, 37, 51, 0.04);
  }

  :deep(.section-title) {
    font-size: 13px;
    letter-spacing: 0.03em;
    margin-bottom: 14px;
    padding-bottom: 8px;
  }

  :deep(.el-form-item__label) {
    font-weight: 600;
    color: var(--text-primary);
  }

  .compact {
    margin-bottom: 16px;
  }
}


.label-with-tip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.field-help {
  color: var(--text-muted);
  font-size: 14px;
  cursor: help;
}
</style>
