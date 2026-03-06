<template>
  <el-dialog
    :model-value="modelValue"
    :title="isEdit ? '编辑规则' : '新建规则'"
    width="600px"
    class="app-dialog rule-dialog"
    destroy-on-close
    align-center
    @update:model-value="$emit('update:modelValue', $event)"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="100px"
    >
      <el-form-item label="规则名称" prop="name">
        <el-input v-model="form.name" placeholder="请输入规则名称" />
      </el-form-item>

      <el-form-item label="规则类型" prop="ruleType">
        <el-select v-model="form.ruleType" placeholder="请选择规则类型" style="width: 100%">
          <el-option label="限制规则" :value="0" />
          <el-option label="转换规则" :value="1" />
          <el-option label="校验规则" :value="2" />
          <el-option label="计算规则" :value="3" />
        </el-select>
      </el-form-item>

      <el-form-item label="设备" prop="deviceId">
        <el-select
          v-model="form.deviceId"
          placeholder="可选，留空表示不绑定设备"
          clearable
          style="width: 100%"
        >
          <el-option
            v-for="device in devices"
            :key="device.id"
            :label="device.name"
            :value="device.id"
          />
        </el-select>
      </el-form-item>

      <el-form-item label="数据点" prop="dataPointId">
        <el-select
          v-model="form.dataPointId"
          placeholder="可选，留空表示全局规则"
          clearable
          style="width: 100%"
        >
          <el-option
            v-for="point in filteredDataPoints"
            :key="point.id"
            :label="point.name"
            :value="point.id"
          />
        </el-select>
      </el-form-item>

      <el-form-item label="优先级" prop="priority">
        <el-input-number v-model="form.priority" :min="0" :max="1000" />
        <div class="form-tip">数值越小优先级越高</div>
      </el-form-item>

      <el-form-item label="失败处理" prop="onFailure">
        <el-select v-model="form.onFailure" placeholder="请选择失败处理方式" style="width: 100%">
          <el-option label="放行" :value="0" />
          <el-option label="拒绝" :value="1" />
          <el-option label="使用默认值" :value="2" />
        </el-select>
      </el-form-item>

      <el-form-item v-if="form.onFailure === 2" label="默认值" prop="defaultValue">
        <el-input v-model="form.defaultValue" placeholder="失败时使用的默认值" />
      </el-form-item>

      <el-form-item label="规则配置" prop="ruleConfig">
        <el-input
          v-model="form.ruleConfig"
          type="textarea"
          :rows="6"
          placeholder="JSON 格式配置"
        />
        <div class="form-tip">
          <el-link type="primary" @click="$emit('showHelp')">查看配置帮助</el-link>
        </div>
      </el-form-item>

      <el-form-item label="描述" prop="description">
        <el-input
          v-model="form.description"
          type="textarea"
          :rows="2"
        />
      </el-form-item>

      <el-form-item label="启用" prop="isEnabled">
        <el-switch v-model="form.isEnabled" />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="handleCancel">取消</el-button>
      <el-button type="primary" :loading="submitting" @click="handleSubmit">确定</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Rule, CreateRuleRequest } from '@/types/rule'
import type { Device, DataPoint } from '@/types/device'

interface RuleForm extends CreateRuleRequest {}

interface Props {
  modelValue: boolean
  editingRule: Rule | null
  devices: Device[]
  dataPoints: DataPoint[]
  submitting: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'submit', data: RuleForm): void
  (e: 'close'): void
  (e: 'showHelp'): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  editingRule: null,
  devices: () => [],
  dataPoints: () => [],
  submitting: false
})

const emit = defineEmits<Emits>()

const formRef = ref<any>()
const form = ref<RuleForm>({
  name: '',
  ruleType: 2,
  deviceId: null,
  dataPointId: null,
  priority: 100,
  ruleConfig: '{}',
  onFailure: 0,
  defaultValue: null,
  isEnabled: true,
  description: ''
})

const rules = {
  name: [{ required: true, message: '请输入规则名称' }],
  ruleType: [{ required: true, message: '请选择规则类型' }],
  priority: [{ required: true, message: '请输入优先级' }],
  onFailure: [{ required: true, message: '请选择失败处理方式' }],
  ruleConfig: [{ required: true, message: '请输入规则配置' }]
}

const isEdit = computed(() => !!props.editingRule)

const filteredDataPoints = computed(() => {
  if (form.value.deviceId) {
    return props.dataPoints.filter(p => p.deviceId === form.value.deviceId)
  }
  return props.dataPoints
})

// 监听编辑规则变化，填充表单
watch(
  () => props.editingRule,
  (rule) => {
    if (rule) {
      form.value = {
        name: rule.name,
        ruleType: rule.ruleType,
        deviceId: rule.deviceId,
        dataPointId: rule.dataPointId,
        priority: rule.priority,
        ruleConfig: rule.ruleConfig,
        onFailure: rule.onFailure,
        defaultValue: rule.defaultValue,
        isEnabled: rule.isEnabled,
        description: rule.description || ''
      }
    } else {
      resetForm()
    }
  },
  { immediate: true }
)

const resetForm = () => {
  form.value = {
    name: '',
    ruleType: 2,
    deviceId: null,
    dataPointId: null,
    priority: 100,
    ruleConfig: '{}',
    onFailure: 0,
    defaultValue: null,
    isEnabled: true,
    description: ''
  }
  formRef.value?.clearValidate()
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
.form-tip {
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 5px;

  .el-link {
    color: var(--cyan);
  }
}
</style>
