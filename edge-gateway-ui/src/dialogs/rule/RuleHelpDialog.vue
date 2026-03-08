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
      <el-tab-pane label="计算规则 (Calculation)">
        <pre>{{ calculationConfigExample }}</pre>
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

const calculationConfigExample = `{
  "CalculationType": 0,     // 0=Custom 自定义计算
  "Expression": "a + b * c" // 自定义表达式
}

// 求和计算
{
  "CalculationType": 1,     // 1=Sum
  "SourceDataPointTags": ["tag1", "tag2", "tag3"]
}

// 平均值计算
{
  "CalculationType": 2,     // 2=Average
  "SourceDataPointTags": ["temp1", "temp2", "temp3"]
}

// 最大值计算
{
  "CalculationType": 3,     // 3=Max
  "SourceDataPointTags": ["sensor1", "sensor2", "sensor3"]
}

// 最小值计算
{
  "CalculationType": 4,     // 4=Min
  "SourceDataPointTags": ["pressure1", "pressure2"]
}

// 加权平均计算
{
  "CalculationType": 7,     // 7=WeightedAverage
  "SourceDataPointTags": ["value1", "value2", "value3"],
  "Weights": [0.3, 0.5, 0.2]  // 权重列表，总和应为 1
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
