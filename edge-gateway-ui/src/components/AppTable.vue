<template>
  <el-table
    class="app-table"
    v-bind="$attrs"
    :data="data"
    v-loading="loading"
    :row-key="rowKey"
    :stripe="stripe"
    :border="border"
    :max-height="maxHeight"
  >
    <slot />
  </el-table>
</template>

<script setup lang="ts">
defineOptions({ inheritAttrs: false })

withDefaults(defineProps<{
  data: unknown[]
  loading?: boolean
  rowKey?: string
  stripe?: boolean
  border?: boolean
  maxHeight?: string | number
}>(), {
  loading: false,
  rowKey: 'id',
  stripe: false,
  border: false,
  maxHeight: undefined
})
</script>

<style scoped lang="scss">
.app-table {
  width: 100%;
  display: flex;
  flex-direction: column;
  min-height: 0;
  --el-table-border-color: rgba(120, 155, 190, .12);
  --el-table-header-bg-color: rgba(56, 220, 196, .06);
  --el-table-row-hover-bg-color: rgba(56, 220, 196, .06);
  --el-table-tr-bg-color: transparent;
  --el-table-bg-color: transparent;
}

.app-table :deep(.el-table__header-wrapper th) {
  height: 42px;
  background: rgba(56, 220, 196, .06) !important;
  color: var(--cyan) !important;
  font-size: 12px;
  font-weight: 700;
}

.app-table :deep(.el-table__body-wrapper td) {
  height: 46px;
  color: var(--text-secondary);
  font-size: 12px;
}

.app-table :deep(.el-table__body tr:hover > td) {
  background: rgba(56, 220, 196, .06) !important;
}

.app-table :deep(.el-table__inner-wrapper::before) {
  background-color: rgba(120, 155, 190, .16);
}

.app-table :deep(.el-table__inner-wrapper) {
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  min-height: 0;
}

.app-table :deep(.el-table__header-wrapper) { flex-shrink: 0; }

.app-table :deep(.el-table__body-wrapper) {
  flex: 1 1 auto;
  min-height: 0;
  overflow: auto;
}
</style>
