<template>
  <div class="add-card" @click="$emit('click')">
    <el-icon size="36" color="var(--border-muted)"><Plus /></el-icon>
    <span class="add-card-text">{{ text }}</span>
  </div>
</template>

<script setup lang="ts">
import { Plus } from '@element-plus/icons-vue'

defineProps<{
  text?: string
}>()

defineEmits<{
  click: []
}>()
</script>

<style scoped>
.add-card {
  border: 1px dashed var(--border-muted);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  min-height: 180px;
  transition: all 0.25s;
  background: transparent;
  border-radius: var(--radius-lg);
}
.add-card:hover {
  border-color: var(--cyan);
  background: rgba(0, 255, 255, 0.03);
}
.add-card :deep(.el-icon) {
  transition: transform 0.25s;
}
.add-card:hover :deep(.el-icon) {
  transform: scale(1.1) rotate(90deg);
}
.add-card-text {
  color: var(--text-muted);
  font-size: 13px;
  margin-top: 8px;
}
</style>
