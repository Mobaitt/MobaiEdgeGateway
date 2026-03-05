<template>
  <div class="form-section">
    <div v-if="title || $slots.title" class="section-title">
      <slot name="icon">
        <el-icon v-if="icon"><component :is="icon" /></el-icon>
      </slot>
      <span>{{ title }}</span>
    </div>
    <slot />
  </div>
</template>

<script setup lang="ts">
defineProps<{
  title?: string
  icon?: string
}>()
</script>

<style scoped>
.form-section {
  margin-bottom: 16px;
  padding: 14px;
  background: var(--bg-base);
  border-radius: var(--radius);
  border: 1px solid var(--border-subtle);
}
.form-section:last-child {
  margin-bottom: 0;
}
.section-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 12px;
  padding-bottom: 6px;
  border-bottom: 1px solid var(--border-subtle);
}
.section-title :deep(.el-icon) {
  color: var(--cyan);
  font-size: 14px;
}
</style>
