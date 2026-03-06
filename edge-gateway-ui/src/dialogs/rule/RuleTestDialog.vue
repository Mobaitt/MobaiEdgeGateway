<template>
  <el-dialog
    :model-value="modelValue"
    title="测试规则"
    width="600px"
    class="app-dialog"
    align-center
    @update:model-value="$emit('update:modelValue', $event)"
    @close="handleClose"
  >
    <el-form :model="form" label-width="100px">
      <el-form-item label="测试值">
        <el-input
          v-model="form.value"
          placeholder="输入测试值"
        />
      </el-form-item>

      <el-form-item label="测试结果">
        <el-input
          v-model="result"
          type="textarea"
          :rows="8"
          readonly
          placeholder="点击测试按钮查看结果"
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="handleCancel">关闭</el-button>
      <el-button type="primary" :loading="testing" @click="handleTest">测试</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'

interface TestForm {
  value: string
}

interface Props {
  modelValue: boolean
  rule: any | null
  testing: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'test', value: string): void
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  rule: null,
  testing: false
})

const emit = defineEmits<Emits>()

const form = ref<TestForm>({
  value: ''
})

const result = ref('')

// 监听弹窗打开，重置表单
watch(
  () => props.modelValue,
  (newVal) => {
    if (newVal) {
      form.value.value = ''
      result.value = ''
    }
  }
)

const handleTest = () => {
  emit('test', form.value.value)
}

const handleCancel = () => {
  emit('update:modelValue', false)
}

const handleClose = () => {
  emit('close')
}

// 暴露方法供外部更新结果
defineExpose({
  setResult: (value: string) => {
    result.value = value
  }
})
</script>
