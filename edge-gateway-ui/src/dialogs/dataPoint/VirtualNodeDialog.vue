<template>
  <el-dialog
    :model-value="modelValue"
    :title="editingVirtualNode ? '编辑虚拟节点' : '新增虚拟节点'"
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
      label-width="120px"
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
                placeholder="如：平均温度"
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
                  placeholder="TempAvg"
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
            placeholder="可选描述"
            type="textarea"
            :rows="2"
          />
        </el-form-item>
      </div>

      <!-- 计算配置 -->
      <div class="form-section">
        <div class="section-title">
          <el-icon><Cpu /></el-icon> 计算配置
        </div>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="计算类型" prop="calculationType">
              <el-select
                v-model="form.calculationType"
                placeholder="选择计算类型"
                style="width: 100%"
              >
                <el-option label="自定义表达式" :value="1" />
                <el-option label="平均值" :value="2" />
                <el-option label="最大值" :value="3" />
                <el-option label="最小值" :value="4" />
                <el-option label="求和" :value="5" />
              </el-select>
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
        <el-form-item label="计算表达式" prop="expression">
          <el-input
            v-model="form.expression"
            type="textarea"
            :rows="3"
            placeholder="如：(Point1 + Point2) / 2 或 Avg(Temp1, Temp2, Temp3)"
          />
          <div class="form-hint">
            <el-icon><InfoFilled /></el-icon>
            <span>支持四则运算和函数：Avg, Max, Min, Sum, Abs, Sqrt, Pow 等。引用其他数据点使用 Tag 名称。</span>
          </div>
        </el-form-item>
      </div>
    </el-form>

    <template #footer>
      <el-button @click="handleCancel">取消</el-button>
      <el-button
        type="primary"
        :loading="submitting"
        @click="handleSubmit"
      >
        {{ editingVirtualNode ? '保存修改' : '创建虚拟节点' }}
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Document, Cpu, InfoFilled, MagicStick } from '@element-plus/icons-vue'
import { nameToCode } from '@/utils/codeGenerate'
import type { VirtualDataPoint } from '@/types/virtualNode'

interface VirtualNodeForm {
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

interface Props {
  modelValue: boolean
  editingVirtualNode: VirtualDataPoint | null
  deviceCode: string
  dataTypeOptions: any[]
  submitting: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'submit', data: VirtualNodeForm): void
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  editingVirtualNode: null,
  deviceCode: '',
  dataTypeOptions: () => [],
  submitting: false
})

const emit = defineEmits<Emits>()

const formRef = ref<any>()
const form = ref<VirtualNodeForm>({
  deviceId: 0,
  name: '',
  tag: '',
  description: '',
  expression: '',
  calculationType: 1,
  dataType: 1,
  unit: '',
  isEnabled: true
})

const tagSuffix = ref('')

const rules = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  tag: [
    { required: true, message: '请输入 Tag', trigger: 'blur' }
  ],
  expression: [{ required: true, message: '请输入计算表达式', trigger: 'blur' }],
  calculationType: [{ required: true, message: '请选择计算类型', trigger: 'change' }],
  dataType: [{ required: true, message: '请选择数据类型', trigger: 'change' }]
}

const resetForm = () => {
  form.value = {
    deviceId: 0,
    name: '',
    tag: '',
    description: '',
    expression: '',
    calculationType: 1,
    dataType: 1,
    unit: '',
    isEnabled: true
  }
  tagSuffix.value = ''
  formRef.value?.clearValidate()
}

// 监听编辑虚拟节点变化，填充表单
watch(
  () => props.editingVirtualNode,
  (virtualNode) => {
    if (virtualNode) {
      form.value = {
        deviceId: virtualNode.deviceId,
        name: virtualNode.name,
        tag: virtualNode.tag,
        description: virtualNode.description || '',
        expression: virtualNode.expression,
        calculationType: virtualNode.calculationType,
        dataType: virtualNode.dataType,
        unit: virtualNode.unit || '',
        isEnabled: virtualNode.isEnabled
      }
      // 解析 Tag 为设备 Code 和后缀
      const tagParts = virtualNode.tag.split('.')
      if (tagParts.length >= 2) {
        tagSuffix.value = tagParts.slice(1).join('.')
      } else {
        tagSuffix.value = virtualNode.tag
      }
    } else {
      resetForm()
    }
  },
  { immediate: true }
)

const handleNameBlur = () => {
  if (!form.value.tag && form.value.name && props.deviceCode) {
    const suffix = nameToCode(form.value.name, 'TAG')
    tagSuffix.value = suffix
    form.value.tag = `${props.deviceCode}.${suffix}`
  }
}

const handleGenerateTag = () => {
  if (!form.value.name?.trim()) {
    ElMessage.warning('请先输入虚拟节点名称')
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

:deep(.mono-input .el-input__inner) {
  font-family: var(--font-mono);
}
</style>
