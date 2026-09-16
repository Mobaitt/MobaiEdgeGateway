<template>
  <el-dialog
    :model-value="modelValue"
    :title="editingDataPoint ? '编辑数据点' : '新增数据点'"
    width="780px"
    destroy-on-close
    class="app-dialog datapoint-dialog"
    align-center
    @update:model-value="$emit('update:modelValue', $event)"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      class="datapoint-form"
      label-width="86px"
      label-position="left"
    >
      <!-- 基本信息 -->
      <div class="form-section">
        <div class="section-title">
          <el-icon><Document /></el-icon> 基本信息
        </div>
        <el-row :gutter="24">
          <el-col :span="8">
            <el-form-item label="名称" prop="name">
              <el-input
                v-model="form.name"
                placeholder="如：温度"
                @blur="handleNameBlur"
              />
            </el-form-item>
          </el-col>
          <el-col :span="16">
            <el-form-item label="Tag" prop="tag">
              <div class="tag-with-btn">
                <span class="tag-prefix mono">{{ deviceCode }}</span>
                <span class="tag-separator">.</span>
                <el-input v-model="tagSuffix" placeholder="Temperature" class="mono-input tag-input" />
                <el-button size="small" text class="btn-auto-generate" @click="handleGenerateTag">
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
            placeholder="可选描述信息"
          />
        </el-form-item>
      </div>

      <!-- 采集配置 -->
      <div class="form-section">
        <div class="section-title">
          <el-icon><Setting /></el-icon> 采集配置
        </div>
        <el-row :gutter="24" class="collection-primary-row">
          <el-col :span="12">
            <el-form-item label="地址" prop="address">
              <el-input
                v-model="form.address"
                placeholder="40001"
                class="mono-input"
              />
            </el-form-item>
          </el-col>
          <el-col v-if="!isModbusDevice" :span="12">
            <el-form-item label="数据类型" prop="dataType">
              <el-select
                v-model="form.dataType"
                placeholder="选择类型"
                style="width: 100%"
              >
                <el-option
                  v-for="o in dataTypeOptions"
                  :key="o.value"
                  :label="o.label"
                  :value="o.value"
                />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col v-else :span="12">
            <el-form-item label="单位">
              <el-input v-model="form.unit" placeholder="℃、MPa" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row v-if="isModbusDevice" :gutter="24" class="collection-state-row">
          <el-col :span="12">
            <el-form-item label="是否启用">
              <el-switch v-model="form.isEnabled" active-color="#38dcc4" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="允许控制" class="switch-item">
              <el-switch v-model="form.isControllable" active-color="#38dcc4" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row v-else :gutter="24" class="collection-state-row">
          <el-col :span="8">
            <el-form-item label="单位">
              <el-input v-model="form.unit" placeholder="℃、MPa" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="是否启用">
              <el-switch v-model="form.isEnabled" active-color="#38dcc4" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="允许控制" class="switch-item">
              <el-switch v-model="form.isControllable" active-color="#38dcc4" />
            </el-form-item>
          </el-col>
        </el-row>
      </div>

      <!-- Modbus 配置：仅当设备为 Modbus 时显示 -->
      <div v-if="isModbusDevice" class="form-section">
        <div class="section-title">
          <el-icon><Connection /></el-icon> Modbus 配置
        </div>
        <el-row :gutter="24" class="modbus-primary-row">
          <el-col :span="8">
            <el-form-item label="从站地址">
              <el-input-number
                v-model="form.modbusSlaveId"
                :min="1"
                :max="247"
                :step="1"
                style="width: 100%"
                controls-position="right"
              />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="功能码">
              <el-select
                v-model="form.modbusFunctionCode"
                placeholder="功能码"
                style="width: 100%"
              >
                <el-option label="01 · 读线圈" :value="1" />
                <el-option label="02 · 离散输入" :value="2" />
                <el-option label="03 · 保持寄存器" :value="3" />
                <el-option label="04 · 输入寄存器" :value="4" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="寄存器长度">
              <div class="derived-value">
                <span class="derived-value__number">{{ form.registerLength }}</span>
                <span>个寄存器</span>
                <span class="derived-value__bits">{{ form.registerLength * 16 }} 位</span>
              </div>
            </el-form-item>
          </el-col>
        </el-row>
        <el-row v-if="form.modbusBitIndex !== null" :gutter="24" class="modbus-bit-row">
          <el-col :span="8">
            <el-form-item label="位索引">
              <el-input-number
                v-model="form.modbusBitIndex"
                :min="0"
                :max="15"
                :step="1"
                style="width: 100%"
                controls-position="right"
              />
            </el-form-item>
          </el-col>
          <el-col :span="16">
            <div class="bit-index-hint">Bit0 为最低位，Bit15 为最高位；多个点位可以共用同一个寄存器地址。</div>
          </el-col>
        </el-row>
        <el-form-item label="数据类型" prop="dataType" class="modbus-type-item">
          <el-select
            v-model="selectedModbusType"
            class="modbus-type-select"
            placeholder="选择 Modbus 数据类型"
            filterable
          >
            <el-option-group
              v-for="group in modbusTypeGroups"
              :key="group.label"
              :label="group.label"
            >
              <el-option
                v-for="option in group.options"
                :key="option.key"
                :label="option.label"
                :value="option.key"
              >
                <div class="modbus-type-option">
                  <span>{{ option.label }}</span>
                  <span class="modbus-type-option__meta">{{ option.meta }}</span>
                </div>
              </el-option>
            </el-option-group>
          </el-select>
          <div class="field-hint">
            选择项会自动设置数据类型、字节顺序和寄存器长度；Hex/Binary 按 16 位无符号寄存器处理。
          </div>
        </el-form-item>
        <div class="modbus-summary">
          <span class="modbus-summary__label">当前映射</span>
          <span class="modbus-summary__value mono">{{ selectedModbusTypeLabel }}</span>
          <span class="modbus-summary__separator">·</span>
          <span class="modbus-summary__value">{{ form.registerLength * 16 }} 位</span>
          <span class="modbus-summary__separator">·</span>
          <span class="modbus-summary__value mono">{{ form.modbusBitIndex !== null ? `Bit${form.modbusBitIndex}` : selectedByteOrderLabel }}</span>
        </div>
      </div>
    </el-form>

    <template #footer>
      <el-button @click="handleCancel">取消</el-button>
      <el-button
        type="primary"
        :loading="submitting"
        @click="handleSubmit"
      >
        {{ editingDataPoint ? '保存修改' : '创建数据点' }}
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Document, Setting, Connection, MagicStick } from '@element-plus/icons-vue'
import { nameToCode } from '@/utils/codeGenerate'
import { CollectionProtocol } from '@/api/constants'
import type { DataPointItem } from '@/types'

interface DataPointForm {
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

interface ModbusTypeOption {
  key: string
  label: string
  meta: string
  dataType: number
  byteOrder: number
  registerLength: number
  bitMode?: boolean
}

interface ModbusTypeGroup {
  label: string
  options: ModbusTypeOption[]
}

const byteOrders = [
  { key: 'abcd', label: 'AB CD', doubleLabel: 'AB CD EF GH', value: 1 },
  { key: 'cdab', label: 'CD AB', doubleLabel: 'GH EF CD AB', value: 3 },
  { key: 'badc', label: 'BA DC', doubleLabel: 'BA DC FE HG', value: 2 },
  { key: 'dcba', label: 'DC BA', doubleLabel: 'HG FE BA DC', value: 4 }
]

const modbusTypeGroups: ModbusTypeGroup[] = [
  {
    label: '16 位寄存器',
    options: [
      { key: 'signed', label: 'Signed', meta: 'Int16 · 1 个寄存器', dataType: 2, byteOrder: 1, registerLength: 1 },
      { key: 'unsigned', label: 'Unsigned', meta: 'UInt16 · 1 个寄存器', dataType: 3, byteOrder: 1, registerLength: 1 },
      { key: 'hex', label: 'Hex', meta: 'Hex · 1 个寄存器', dataType: 11, byteOrder: 1, registerLength: 1 },
      { key: 'binary', label: 'Binary', meta: 'Binary · 1 个寄存器', dataType: 12, byteOrder: 1, registerLength: 1 }
    ]
  },
  {
    label: '寄存器位',
    options: [
      { key: 'register-bit', label: 'Bit Boolean', meta: 'Bool · 1 个寄存器 · Bit0~15', dataType: 1, byteOrder: 1, registerLength: 1, bitMode: true }
    ]
  },
  {
    label: '32 位 Long',
    options: byteOrders.flatMap(order => [
      { key: `long-${order.key}`, label: `Long ${order.label}`, meta: `Int32 · ${order.label}`, dataType: 4, byteOrder: order.value, registerLength: 2 },
      { key: `ulong-${order.key}`, label: `Unsigned Long ${order.label}`, meta: `UInt32 · ${order.label}`, dataType: 5, byteOrder: order.value, registerLength: 2 }
    ])
  },
  {
    label: '32 位 Float',
    options: byteOrders.map(order => ({
      key: `float-${order.key}`,
      label: `Float ${order.label}`,
      meta: `Float · ${order.label}`,
      dataType: 6,
      byteOrder: order.value,
      registerLength: 2
    }))
  },
  {
    label: '64 位数值',
    options: byteOrders.flatMap(order => [
      { key: `int64-${order.key}`, label: `Int64 ${order.doubleLabel}`, meta: `Int64 · ${order.doubleLabel}`, dataType: 7, byteOrder: order.value, registerLength: 4 },
      { key: `uint64-${order.key}`, label: `Unsigned Int64 ${order.doubleLabel}`, meta: `UInt64 · ${order.doubleLabel}`, dataType: 8, byteOrder: order.value, registerLength: 4 },
      { key: `double-${order.key}`, label: `Double ${order.doubleLabel}`, meta: `Double · ${order.doubleLabel}`, dataType: 9, byteOrder: order.value, registerLength: 4 }
    ])
  },
  {
    label: '线圈',
    options: [
      { key: 'bool', label: 'Boolean', meta: 'Bool · 1 个线圈', dataType: 1, byteOrder: 1, registerLength: 1 }
    ]
  }
]

const allModbusTypeOptions = modbusTypeGroups.flatMap(group => group.options)

interface Props {
  modelValue: boolean
  editingDataPoint: DataPointItem | null
  deviceCode: string
  deviceProtocol: number | null
  dataTypeOptions: any[]
  byteOrderOptions: any[]
  submitting: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'submit', data: DataPointForm): void
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  editingDataPoint: null,
  deviceCode: '',
  deviceProtocol: null,
  dataTypeOptions: () => [],
  byteOrderOptions: () => [],
  submitting: false
})

const emit = defineEmits<Emits>()

const formRef = ref<any>()
const form = ref<DataPointForm>({
  name: '',
  tag: '',
  description: '',
  address: '',
  dataType: null,
  unit: '',
  isEnabled: true,
  isControllable: false,
  modbusSlaveId: 1,
  modbusFunctionCode: 3,
  modbusByteOrder: 1,
  registerLength: 1,
  modbusBitIndex: null
})

const tagSuffix = ref('')

const rules = {
  name: [{ required: true, message: '请输入名称' }],
  tag: [
    { required: true, message: '请输入标签' },
    {
      pattern: /^[A-Z0-9_]+\.[A-Z0-9_]+$/i,
      message: 'Tag 格式不正确，应为：设备编码。数据点标识（例：DEV_PLC_001.Temperature）'
    }
  ],
  address: [{ required: true, message: '请输入地址' }],
  dataType: [{ required: true, message: '请选择数据类型' }]
}

const isModbusDevice = computed(() => props.deviceProtocol === CollectionProtocol.Modbus.value)

const selectedModbusOption = computed(() => {
  const current = form.value
  return allModbusTypeOptions.find(option =>
    option.dataType === current.dataType &&
    option.byteOrder === current.modbusByteOrder &&
    option.registerLength === current.registerLength &&
    Boolean(option.bitMode) === (current.modbusBitIndex !== null)
  ) || allModbusTypeOptions.find(option => option.dataType === current.dataType)
})

const selectedModbusType = computed<string>({
  get: () => selectedModbusOption.value?.key || '',
  set: key => {
    const option = allModbusTypeOptions.find(item => item.key === key)
    if (!option) return

    form.value.dataType = option.dataType
    form.value.modbusByteOrder = option.byteOrder
    form.value.registerLength = option.registerLength
    form.value.modbusBitIndex = option.bitMode ? (form.value.modbusBitIndex ?? 0) : null

    if (option.bitMode) {
      form.value.modbusFunctionCode = 3
    } else if (option.dataType === 1) {
      form.value.modbusFunctionCode = 1
    } else if ([1, 2].includes(form.value.modbusFunctionCode)) {
      form.value.modbusFunctionCode = 3
    }
  }
})

const selectedModbusTypeLabel = computed(() => selectedModbusOption.value?.label || '未选择')

const selectedByteOrderLabel = computed(() => {
  return byteOrders.find(order => order.value === form.value.modbusByteOrder)?.label || '—'
})

const resetForm = () => {
  form.value = {
    name: '',
    tag: '',
    description: '',
    address: '',
    dataType: null,
    unit: '',
    isEnabled: true,
    isControllable: false,
    modbusSlaveId: 1,
    modbusFunctionCode: 3,
    modbusByteOrder: 1,
    registerLength: 1,
    modbusBitIndex: null
  }
  tagSuffix.value = ''
  formRef.value?.clearValidate()
}

// 监听编辑数据点变化，填充表单
watch(
  () => props.editingDataPoint,
  (dataPoint) => {
    if (dataPoint) {
      form.value = {
        name: dataPoint.name,
        tag: dataPoint.tag,
        description: dataPoint.description || '',
        address: dataPoint.address,
        dataType: dataPoint.dataTypeValue ?? (typeof dataPoint.dataType === 'number' ? dataPoint.dataType : null),
        unit: dataPoint.unit || '',
        isEnabled: dataPoint.isEnabled,
        isControllable: dataPoint.isControllable ?? false,
        modbusSlaveId: dataPoint.modbusSlaveId || 1,
        modbusFunctionCode: dataPoint.modbusFunctionCode || 3,
        modbusByteOrder: dataPoint.modbusByteOrder || 1,
        registerLength: dataPoint.registerLength || 1,
        modbusBitIndex: dataPoint.modbusBitIndex ?? null
      }
      // 解析 Tag 为设备 Code 和后缀
      const tagParts = dataPoint.tag.split('.')
      if (tagParts.length >= 2) {
        tagSuffix.value = tagParts.slice(1).join('.')
      } else {
        tagSuffix.value = dataPoint.tag
      }
    } else {
      resetForm()
    }
  },
  { immediate: true }
)

// 根据数据类型自动设置寄存器长度
watch(
  () => form.value.dataType,
  (newType) => {
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
  }
)

// 监听 tagSuffix 变化，同步更新 form.tag
watch(tagSuffix, (newSuffix) => {
  if (props.deviceCode && newSuffix) {
    form.value.tag = `${props.deviceCode}.${newSuffix}`
  }
})

const handleNameBlur = () => {
  if (!form.value.tag && form.value.name && props.deviceCode) {
    const suffix = nameToCode(form.value.name, 'TAG')
    tagSuffix.value = suffix
    form.value.tag = `${props.deviceCode}.${suffix}`
  }
}

const handleGenerateTag = () => {
  if (!form.value.name?.trim()) {
    ElMessage.warning('请先输入数据点名称')
    return
  }
  if (!props.deviceCode) {
    ElMessage.warning('无法获取设备编码，请稍后重试')
    return
  }
  const suffix = nameToCode(form.value.name, 'TAG')
  tagSuffix.value = suffix
  form.value.tag = `${props.deviceCode}.${suffix}`
  ElMessage.success('Tag 已生成')
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
.datapoint-dialog {
  :deep(.el-dialog__body) {
    padding: 20px 24px 10px;
  }

  :deep(.el-dialog__footer) {
    display: flex;
    justify-content: flex-end;
    gap: 10px;
  }

  :deep(.el-form-item) {
    margin-bottom: 16px;
  }

  :deep(.el-form-item__label) {
    line-height: 32px;
  }

  .bit-index-hint {
    min-height: 32px;
    display: flex;
    align-items: center;
    color: var(--text-muted);
    font-size: 12px;
    line-height: 18px;
  }
}

.datapoint-form {
  :deep(.form-section) {
    margin-bottom: 14px;
    padding: 16px 18px 4px;
    border: 1px solid var(--border-subtle);
    border-radius: 14px;
    background: var(--bg-base);
  }

  :deep(.section-title) {
    margin-bottom: 14px;
    padding-bottom: 11px;
    border-bottom: 1px solid rgba(56, 220, 196, 0.12);
    font-size: 13px;
  }

  :deep(.collection-state-row .el-form-item__content) {
    display: flex;
    align-items: center;
    min-height: 32px;
  }

  :deep(.modbus-primary-row .el-form-item__label) {
    white-space: nowrap;
  }

  :deep(.modbus-primary-row > .el-col:last-child .el-form-item__label) {
    width: 80px !important;
    padding-right: 8px;
  }

  :deep(.modbus-type-item .el-form-item__content) {
    display: block;
  }
}

.tag-with-btn {
  display: flex;
  align-items: center;
  gap: 8px;

  .tag-prefix {
    display: inline-flex;
    align-items: center;
    min-width: 112px;
    height: 32px;
    padding: 0 10px;
    overflow: hidden;
    border: 1px solid var(--border-muted);
    border-radius: var(--radius);
    background: var(--bg-base);
    color: var(--text-secondary);
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .tag-input {
    flex: 1;
    min-width: 0;
  }

  .btn-auto-generate {
    flex-shrink: 0;
    background: var(--bg-base) !important;
    border: 1px solid var(--border-subtle) !important;
    color: var(--text-secondary) !important;
    transition: all 0.2s;

    &:hover {
      background: var(--bg-hover) !important;
      border-color: var(--border-muted) !important;
      color: var(--cyan) !important;
    }
  }

  .tag-separator {
    color: var(--text-muted);
    font-size: 14px;
    padding: 0 4px;
  }
}

.modbus-type-item {
  margin-top: 4px;
}

.modbus-type-select {
  width: 100%;
}

.modbus-type-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;

  &__meta {
    color: var(--text-muted);
    font-family: var(--font-mono);
    font-size: 11px;
  }
}

.field-hint {
  margin-top: 6px;
  color: var(--text-muted);
  font-size: 11px;
  line-height: 1.5;
}

.derived-value {
  display: flex;
  align-items: center;
  gap: 5px;
  min-height: 32px;
  padding: 0 7px;
  overflow: hidden;
  border: 1px solid var(--border-muted);
  border-radius: var(--radius);
  background: var(--bg-base);
  color: var(--text-secondary);
  font-size: 11px;
  white-space: nowrap;

  &__number,
  &__bits {
    color: var(--cyan);
    font-family: var(--font-mono);
    font-weight: 700;
  }

  &__number {
    font-size: 15px;
  }

  &__bits {
    margin-left: 2px;
    color: var(--text-muted);
    font-size: 10px;
  }
}

.modbus-summary {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 2px 0 12px 86px;
  padding: 9px 12px;
  border: 1px solid rgba(56, 220, 196, 0.16);
  border-radius: 9px;
  background: rgba(56, 220, 196, 0.06);
  color: var(--text-secondary);
  font-size: 12px;

  &__label {
    color: var(--text-muted);
  }

  &__value {
    color: var(--text-primary);
  }

  &__separator {
    color: var(--text-muted);
  }
}

:global(.el-select-group__title) {
  color: var(--cyan) !important;
  font-size: 11px !important;
  font-weight: 700;
  letter-spacing: 0.05em;
}

:global(.modbus-type-option) {
  min-width: 300px;
}

:global(.modbus-type-option__meta) {
  color: var(--text-muted) !important;
}

@media (max-width: 680px) {
  .datapoint-dialog {
    :deep(.el-dialog__body) {
      padding: 16px 14px 6px;
    }
  }

  .datapoint-form :deep(.el-col) {
    width: 100%;
    max-width: 100%;
    flex: 0 0 100%;
  }

  .modbus-summary {
    margin-left: 0;
    flex-wrap: wrap;
  }

  .tag-prefix {
    min-width: 0;
    max-width: 38%;
  }

}

:deep(.mono-input .el-input__inner) {
  font-family: var(--font-mono);
}
</style>
