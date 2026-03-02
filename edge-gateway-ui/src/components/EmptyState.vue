<template>
  <div class="empty-state">
    <el-icon v-if="icon" size="48" color="var(--border-muted)">
      <component :is="icon" />
    </el-icon>
    <div v-if="message" class="empty-message">{{ message }}</div>
    <slot />
  </div>
</template>

<script setup lang="ts">
defineProps<{
  message?: string
  icon?: string
}>()
</script>

<style scoped>
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 48px 20px;
}
.empty-message {
  margin-top: 12px;
  color: var(--text-muted);
  font-size: 14px;
}
.empty-state > :deep(.el-button) {
  margin-top: 12px;
}
</style>
