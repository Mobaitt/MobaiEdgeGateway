<template>
  <el-dialog
    :model-value="modelValue"
    :title="editingDataPoint ? '编辑数据点' : '新增数据点'"
    width="720px"
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
      label-width="90px"
      label-position="left"
    >
      <!-- 基本信息 -->
      <div class="form-section">
        <div class="section-title">
          <el-icon><Document /></el-icon> 基本信息
        </div>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="名称" prop="name">
              <el-input
                v-model="form.name"
                placeholder="如：温度"
                @blur="handleNameBlur"
              />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-form-item label="Tag" prop="tag">
              <div class="tag-with-btn">
                <el-input
                  :value="deviceCode"
                  disabled
                  class="mono-input tag-input"
                  style="width: 120px"
                />
                <span class="tag-separator">.</span>
                <el-input
                  v-model="tagSuffix"
                  placeholder="Temperature"
                  class="mono-input tag-input"
                />
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
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="地址" prop="address">
              <el-input
                v-model="form.address"
                placeholder="40001"
                class="mono-input"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
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
        </el-row>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="单位">
              <el-input
                v-model="form.unit"
                placeholder="℃、MPa"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="是否启用">
              <el-switch v-model="form.isEnabled" active-color="#38dcc4" />
            </el-form-item>
          </el-col>
        </el-row>
      </div>

      <!-- Modbus 配置：仅当设备为 Modbus 时显示 -->
      <div v-if="isModbusDevice" class="form-section">
        <div class="section-title">
          <el-icon><Connection /></el-icon> Modbus 配置
        </div>
        <el-row :gutter="20">
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
                <el-option label="01 - 读线圈" :value="1" />
                <el-option label="02 - 读离散输入" :value="2" />
                <el-option label="03 - 读保持寄存器" :value="3" />
                <el-option label="04 - 读输入寄存器" :value="4" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="字节顺序">
              <el-select
                v-model="form.modbusByteOrder"
                placeholder="字节序"
                style="width: 100%"
              >
                <el-option
                  v-for="o in byteOrderOptions"
                  :key="o.value"
                  :label="o.label"
                  :value="o.value"
                >
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
              <el-select
                v-model="form.registerLength"
                placeholder="长度"
                style="width: 100%"
              >
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
import { Document, Setting, Connection, InfoFilled, MagicStick } from '@element-plus/icons-vue'
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
  modbusSlaveId: number
  modbusFunctionCode: number
  modbusByteOrder: number
  registerLength: number
}

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
  modbusSlaveId: 1,
  modbusFunctionCode: 3,
  modbusByteOrder: 1,
  registerLength: 1
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

const resetForm = () => {
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
        modbusSlaveId: dataPoint.modbusSlaveId || 1,
        modbusFunctionCode: dataPoint.modbusFunctionCode || 3,
        modbusByteOrder: dataPoint.modbusByteOrder || 1,
        registerLength: dataPoint.registerLength || 1
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
.tag-with-btn {
  display: flex;
  align-items: center;
  gap: 8px;

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

.byte-order-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;

  .byte-order-desc {
    font-size: 11px;
    color: var(--text-muted);
  }
}

:deep(.mono-input .el-input__inner) {
  font-family: var(--font-mono);
}
</style>
