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
  flex-wrap: wrap;
  gap: 16px 24px;
  margin-bottom: 24px;
}
.header-left {
  display: flex;
  align-items: center;
  gap: 14px;
  min-width: 0;
}
.title-block {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}
.page-title {
  font-size: 22px;
  font-weight: 800;
  color: var(--text-primary);
  letter-spacing: -0.01em;
  line-height: 1.25;
  overflow-wrap: anywhere;
}
.page-desc {
  font-size: 13px;
  color: var(--text-muted);
  margin-top: 0;
}
.header-actions {
  flex-shrink: 0;
  margin-left: auto;
}

@media (max-width: 640px) {
  .page-header { margin-bottom: 18px; }
  .header-actions { width: 100%; margin-left: 0; }
  .header-actions > * { max-width: 100%; }
}
</style>
