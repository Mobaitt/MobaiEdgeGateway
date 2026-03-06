<template>
  <el-dialog
    :model-value="modelValue"
    title="规则配置帮助"
    width="700px"
    class="app-dialog"
    align-center
    @update:model-value="$emit('update:modelValue', $event)"
    @close="handleClose"
  >
    <el-tabs>
      <el-tab-pane label="校验规则 (Validation)">
        <pre>{{ validationConfigExample }}</pre>
      </el-tab-pane>
      <el-tab-pane label="转换规则 (Transform)">
        <pre>{{ transformConfigExample }}</pre>
      </el-tab-pane>
      <el-tab-pane label="限制规则 (Limit)">
        <pre>{{ limitConfigExample }}</pre>
      </el-tab-pane>
    </el-tabs>
  </el-dialog>
</template>

<script setup lang="ts">
interface Props {
  modelValue: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false
})

const emit = defineEmits<Emits>()

const validationConfigExample = `{
  "ValidationType": 1,      // 1=Range 阈值校验
  "MinValue": 0,            // 最小值
  "MaxValue": 100           // 最大值
}

// 变化率校验
{
  "ValidationType": 2,      // 2=RateOfChange
  "MaxRateOfChange": 10.5   // 每秒最大变化量
}

// 死区校验
{
  "ValidationType": 3,      // 3=DeadBand
  "DeadBand": 0.5           // 死区值
}`

const transformConfigExample = `{
  "TransformType": 1,       // 1=Linear 线性变换
  "Scale": 1.8,             // y = kx + b 中的 k
  "Offset": 32              // y = kx + b 中的 b
}

// 公式计算
{
  "TransformType": 2,       // 2=Formula
  "Formula": "x * 2 + 10"   // NCalc 表达式
}`

const limitConfigExample = `{
  "MinValue": 0,            // 最小值限制
  "MaxValue": 100           // 最大值限制
}`

const handleCancel = () => {
  emit('update:modelValue', false)
}

const handleClose = () => {
  emit('close')
}
</script>

<style scoped lang="scss">
pre {
  background: var(--bg-base);
  color: var(--text-secondary);
  padding: 15px;
  border-radius: var(--radius);
  font-size: 12px;
  overflow-x: auto;
  border: 1px solid var(--border-subtle);
  font-family: var(--font-mono);
}
</style>
