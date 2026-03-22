<template>
  <el-dialog
    :model-value="modelValue"
    width="420px"
    destroy-on-close
    align-center
    class="app-dialog"
    title="点位控制"
    @update:model-value="$emit('update:modelValue', $event)"
    @close="handleClose"
  >
    <div v-if="dataPoint" class="control-dialog">
      <div class="point-meta">
        <div class="point-name">{{ dataPoint.name }}</div>
        <div class="point-tag mono">{{ dataPoint.tag }}</div>
      </div>

      <div class="current-value">
        <span class="label">当前值</span>
        <span class="value mono">{{ formattedCurrentValue }}</span>
      </div>

      <el-form label-position="top">
        <el-form-item label="目标值">
          <el-switch
            v-if="isBoolPoint"
            v-model="boolValue"
            inline-prompt
            active-text="ON"
            inactive-text="OFF"
            active-color="#38dcc4"
          />
          <el-input
            v-else
            v-model="inputValue"
            :placeholder="inputPlaceholder"
            clearable
          />
        </el-form-item>
      </el-form>
    </div>

    <template #footer>
      <el-button @click="$emit('update:modelValue', false)">取消</el-button>
      <el-button type="primary" :loading="submitting" @click="handleSubmit">执行控制</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { DataPointItem } from '@/types'

interface Props {
  modelValue: boolean
  dataPoint: DataPointItem | null
  currentValue?: unknown
  submitting?: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'submit', value: unknown): void
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  dataPoint: null,
  currentValue: undefined,
  submitting: false
})

const emit = defineEmits<Emits>()

const boolValue = ref(false)
const inputValue = ref('')

const pointTypeValue = computed(() => {
  if (!props.dataPoint) return null
  return typeof props.dataPoint.dataTypeValue === 'number'
    ? props.dataPoint.dataTypeValue
    : typeof props.dataPoint.dataType === 'number'
      ? props.dataPoint.dataType
      : null
})

const isBoolPoint = computed(() => pointTypeValue.value === 1)

const formattedCurrentValue = computed(() => {
  if (props.currentValue === null || props.currentValue === undefined || props.currentValue === '') {
    return '-'
  }
  return String(props.currentValue)
})

const inputPlaceholder = computed(() => {
  if (!props.dataPoint) return '请输入目标值'
  return `请输入 ${props.dataPoint.dataType} 类型值`
})

watch(
  () => [props.modelValue, props.dataPoint?.id],
  ([visible]) => {
    if (!visible) return

    if (isBoolPoint.value) {
      boolValue.value = props.currentValue === true || props.currentValue === 'true' || props.currentValue === 1 || props.currentValue === '1'
    } else {
      inputValue.value = props.currentValue === null || props.currentValue === undefined
        ? ''
        : String(props.currentValue)
    }
  },
  { immediate: true }
)

const handleSubmit = () => {
  if (isBoolPoint.value) {
    emit('submit', boolValue.value)
    return
  }

  if (!inputValue.value.trim()) {
    ElMessage.warning('请输入目标值')
    return
  }

  emit('submit', inputValue.value.trim())
}

const handleClose = () => {
  inputValue.value = ''
  boolValue.value = false
  emit('close')
}
</script>

<style scoped lang="scss">
.control-dialog {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.point-meta,
.current-value {
  padding: 14px 16px;
  border-radius: 14px;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
}

.point-name {
  font-size: 18px;
  font-weight: 700;
}

.point-tag {
  margin-top: 6px;
  color: var(--cyan);
  font-size: 12px;
}

.current-value {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.label {
  color: var(--text-muted);
  font-size: 13px;
}

.value {
  font-size: 16px;
  font-weight: 700;
}
</style>
