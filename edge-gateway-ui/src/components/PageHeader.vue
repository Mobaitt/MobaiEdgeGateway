<template>
  <div class="page-header">
    <div class="header-left">
      <slot name="left" />
      <div v-if="title || $slots.title" class="title-block">
        <h1 v-if="title" class="page-title">{{ title }}</h1>
        <slot v-else name="title" />
        <p v-if="desc" class="page-desc">{{ desc }}</p>
        <slot name="extra-title" />
      </div>
    </div>
    <div v-if="$slots.default" class="header-actions">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  title?: string
  desc?: string
}>()
</script>

<style scoped>
.page-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 24px;
}
.header-left {
  display: flex;
  align-items: center;
  gap: 14px;
}
.title-block {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.page-title {
  font-size: 22px;
  font-weight: 800;
  color: var(--text-primary);
  letter-spacing: -0.01em;
}
.page-desc {
  font-size: 13px;
  color: var(--text-muted);
  margin-top: 0;
}
.header-actions {
  flex-shrink: 0;
}
</style>
