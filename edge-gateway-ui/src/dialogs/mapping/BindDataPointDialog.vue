<template>
  <el-dialog
    :model-value="modelValue"
    title="绑定数据点到通道"
    width="900px"
    destroy-on-close
    class="app-dialog bind-dialog"
    align-center
    top="5vh"
    @update:model-value="$emit('update:modelValue', $event)"
    @close="handleClose"
  >
    <div class="bind-layout">
      <!-- 左：设备树选择数据点 -->
      <div class="device-selector">
        <div class="selector-header">
          <div class="selector-title">
            <el-icon><Monitor /></el-icon> 选择数据点
          </div>
          <el-input
            v-model="searchText"
            placeholder="搜索数据点或设备..."
            prefix-icon="Search"
            size="small"
            clearable
            class="search-input"
          />
        </div>

        <div class="scroll-content">
          <div class="device-tree">
            <div v-for="dev in filteredDeviceTree" :key="dev.id" class="dev-group">
              <div class="dev-group-title">
                <el-checkbox
                  :model-value="isDeviceAllSelected(dev)"
                  :indeterminate="isDeviceIndeterminate(dev)"
                  @change="toggleDevice(dev)"
                />
                <el-icon size="13"><Monitor /></el-icon>
                <span>{{ dev.name }}</span>
                <span class="mono" style="color: var(--text-muted); font-size: 11px">{{ dev.code }}</span>
              </div>
              <div class="dp-list">
                <!-- 普通数据点 -->
                <div
                  v-for="dp in dev.dataPoints"
                  :key="dp.id"
                  class="dp-item"
                  :class="{ already: isMapped(dp.id) }"
                >
                  <el-checkbox
                    :model-value="selectedIds.has(dp.id)"
                    :disabled="isMapped(dp.id)"
                    @change="(v) => toggleDp(dp.id, v)"
                  />
                  <div class="dp-info">
                    <span class="mono" style="color: var(--cyan); font-size: 12px">{{ dp.tag }}</span>
                    <span class="dp-name" v-if="dp.name && dp.name.trim()">· {{ dp.name }}</span>
                    <span class="dp-unit" v-if="dp.unit && dp.unit.trim()">· {{ dp.unit }}</span>
                    <el-tag
                      v-if="isMapped(dp.id)"
                      size="small"
                      type="info"
                      style="margin-left: 6px; font-size: 10px"
                    >
                      已绑定
                    </el-tag>
                  </div>
                </div>
                <!-- 虚拟数据点 -->
                <div
                  v-for="vp in dev.virtualPoints || []"
                  :key="'v-' + vp.id"
                  class="dp-item"
                  :class="{ already: isVirtualMapped(vp.id) }"
                >
                  <el-checkbox
                    :model-value="virtualSelectedIds.has(vp.id)"
                    :disabled="isVirtualMapped(vp.id)"
                    @change="(v) => toggleVirtualDp(vp.id, v)"
                  />
                  <div class="dp-info">
                    <span class="mono" style="color: var(--purple); font-size: 12px">{{ vp.tag }}</span>
                    <span class="vp-name" v-if="vp.name && vp.name.trim()">· {{ vp.name }}</span>
                    <span class="vp-unit" v-if="vp.unit && vp.unit.trim()">· {{ vp.unit }}</span>
                    <el-tag
                      v-if="isVirtualMapped(vp.id)"
                      size="small"
                      type="info"
                      style="margin-left: 6px; font-size: 10px"
                    >
                      已绑定
                    </el-tag>
                  </div>
                </div>
              </div>
            </div>
            <div v-if="filteredDeviceTree.length === 0" class="empty-hint">无可用数据点</div>
          </div>
        </div>
      </div>

      <!-- 右：已选预览 -->
      <div class="selected-preview">
        <div class="selector-title">
          <el-icon><Connection /></el-icon> 已选
          <span class="mono" style="color: var(--cyan)">{{ selectedIds.size + virtualSelectedIds.size }}</span> 个
        </div>
        <div class="selected-list">
          <!-- 普通数据点 -->
          <div v-for="id in [...selectedIds]" :key="id" class="selected-item">
            <span class="selected-text">{{ getDataPointDisplay(id) }}</span>
            <el-button size="small" text circle @click="selectedIds.delete(id)">
              <el-icon size="12"><Close /></el-icon>
            </el-button>
          </div>
          <!-- 虚拟数据点 -->
          <div v-for="id in [...virtualSelectedIds]" :key="'v-' + id" class="selected-item">
            <span class="selected-text virtual">{{ getVirtualPointDisplay(id) }}</span>
            <el-button size="small" text circle @click="virtualSelectedIds.delete(id)">
              <el-icon size="12"><Close /></el-icon>
            </el-button>
          </div>
          <div v-if="selectedIds.size + virtualSelectedIds.size === 0" class="empty-hint">从左侧选择数据点</div>
        </div>
      </div>
    </div>

    <template #footer>
      <el-button @click="handleCancel">取消</el-button>
      <el-button
        type="primary"
        :loading="loading"
        :disabled="selectedIds.size + virtualSelectedIds.size === 0"
        @click="handleSubmit"
      >
        绑定 {{ selectedIds.size + virtualSelectedIds.size > 0 ? `(${selectedIds.size + virtualSelectedIds.size})` : '' }}
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Monitor, Connection, Close, Search } from '@element-plus/icons-vue'
import type { DeviceNode, DataPointItem } from '@/types'
import type { VirtualDataPoint } from '@/types/virtualNode'

interface DeviceTreeItem extends DeviceNode {
  virtualPoints?: VirtualDataPoint[]
}

interface Props {
  modelValue: boolean
  deviceTree: DeviceTreeItem[]
  virtualDataPoints: VirtualDataPoint[]
  mappedIds: Set<number>
  virtualMappedIds: Set<number>
  loading: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'submit', selectedIds: Set<number>, virtualSelectedIds: Set<number>): void
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  deviceTree: () => [],
  virtualDataPoints: () => [],
  mappedIds: () => new Set(),
  virtualMappedIds: () => new Set(),
  loading: false
})

const emit = defineEmits<Emits>()

const searchText = ref('')
const selectedIds = ref(new Set<number>())
const virtualSelectedIds = ref(new Set<number>())

// 重置选择状态
const resetSelection = () => {
  selectedIds.value = new Set()
  virtualSelectedIds.value = new Set()
  searchText.value = ''
}

// 监听弹窗打开，重置选择
watch(
  () => props.modelValue,
  (newVal) => {
    if (newVal) {
      resetSelection()
    }
  }
)

const isMapped = (id: number) => props.mappedIds.has(id)
const isVirtualMapped = (id: number) => props.virtualMappedIds.has(id)

// 获取设备的虚拟数据点
const getVirtualPointsByDevice = (deviceId: number) => {
  const device = props.deviceTree.find(d => d.id === deviceId)
  if (!device) return []
  return device.virtualPoints || []
}

// 获取过滤后的虚拟数据点
const getFilteredVirtualPointsByDevice = (deviceId: number) => {
  const virtualPoints = getVirtualPointsByDevice(deviceId)
  if (!searchText.value) return virtualPoints

  return virtualPoints.filter(
    vp =>
      vp.tag.toLowerCase().includes(searchText.value.toLowerCase()) ||
      vp.name.toLowerCase().includes(searchText.value.toLowerCase())
  )
}

const filteredDeviceTree = computed<DeviceTreeItem[]>(() => {
  if (!searchText.value) {
    return props.deviceTree.map(device => ({
      ...device,
      virtualPoints: getVirtualPointsByDevice(device.id)
    }))
  }

  return props.deviceTree
    .map((device) => {
      const filteredNormal = device.dataPoints.filter(
        (dp) =>
          dp.tag.toLowerCase().includes(searchText.value.toLowerCase()) ||
          dp.name.toLowerCase().includes(searchText.value.toLowerCase())
      )
      const filteredVirtual = getFilteredVirtualPointsByDevice(device.id)

      return {
        ...device,
        dataPoints: filteredNormal,
        virtualPoints: filteredVirtual
      } as DeviceTreeItem
    })
    .filter((device) => device.dataPoints.length > 0 || (device.virtualPoints?.length ?? 0) > 0)
})

const isDeviceAllSelected = (device: DeviceTreeItem) => {
  const virtualPoints = getFilteredVirtualPointsByDevice(device.id)
  const normalAllSelected = device.dataPoints.every(
    (dp) => isMapped(dp.id) || selectedIds.value.has(dp.id)
  )
  const virtualAllSelected = virtualPoints.every((vp) => virtualSelectedIds.value.has(vp.id))
  return normalAllSelected && virtualAllSelected
}

const isDeviceIndeterminate = (device: DeviceTreeItem) => {
  const virtualPoints = getFilteredVirtualPointsByDevice(device.id)
  const normalSelectable = device.dataPoints.filter((dp) => !isMapped(dp.id))
  const normalSelectedCount = normalSelectable.filter((dp) => selectedIds.value.has(dp.id)).length
  const virtualSelectedCount = virtualPoints.filter((vp) => virtualSelectedIds.value.has(vp.id)).length
  const totalSelectable = normalSelectable.length + virtualPoints.length
  const totalSelected = normalSelectedCount + virtualSelectedCount
  return totalSelected > 0 && totalSelected < totalSelectable
}

const toggleDevice = (device: DeviceTreeItem) => {
  const virtualPoints = getFilteredVirtualPointsByDevice(device.id)
  const normalSelectable = device.dataPoints.filter((dp) => !isMapped(dp.id))

  if (isDeviceAllSelected(device)) {
    normalSelectable.forEach((dp) => selectedIds.value.delete(dp.id))
    virtualPoints.forEach((vp) => virtualSelectedIds.value.delete(vp.id))
  } else {
    normalSelectable.forEach((dp) => selectedIds.value.add(dp.id))
    virtualPoints.forEach((vp) => virtualSelectedIds.value.add(vp.id))
  }
}

const toggleDp = (id: number, checked: boolean) => {
  if (checked) selectedIds.value.add(id)
  else selectedIds.value.delete(id)
}

const toggleVirtualDp = (id: number, checked: boolean) => {
  if (checked) virtualSelectedIds.value.add(id)
  else virtualSelectedIds.value.delete(id)
}

const getDataPointDisplay = (id: number) => {
  for (const device of props.deviceTree) {
    const dp = device.dataPoints.find((item) => item.id === id)
    if (dp) {
      const parts = [dp.tag]
      if (dp.name && dp.name.trim()) parts.push(dp.name)
      if (dp.unit && dp.unit.trim()) parts.push(dp.unit)
      return parts.join(' · ')
    }
  }
  return String(id)
}

const getVirtualPointDisplay = (id: number) => {
  const vp = props.virtualDataPoints.find((item) => item.id === id)
  if (vp) {
    const parts = [vp.tag]
    if (vp.name && vp.name.trim()) parts.push(vp.name)
    if (vp.unit && vp.unit.trim()) parts.push(vp.unit)
    return parts.join(' · ')
  }
  return String(id)
}

const handleSubmit = () => {
  emit('submit', selectedIds.value, virtualSelectedIds.value)
}

const handleCancel = () => {
  emit('update:modelValue', false)
}

const handleClose = () => {
  emit('close')
}
</script>

<style scoped lang="scss">
.bind-layout {
  display: grid;
  grid-template-columns: 1fr 220px;
  gap: 16px;
  height: 75vh;
  min-height: 500px;
  overflow: hidden;
  padding: 16px;
}

.device-selector,
.selected-preview {
  display: flex;
  flex-direction: column;
  gap: 8px;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius);
  padding: 12px;
  overflow: hidden;
  background: var(--bg-base);
  height: 100%;
}

.selector-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  flex-shrink: 0;
}

.selector-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 700;
  color: var(--text-primary);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  white-space: nowrap;

  .el-icon {
    color: var(--cyan);
  }
}

.search-input {
  flex-shrink: 0;
  width: 200px;
}

:deep(.search-input .el-input__wrapper) {
  height: 28px;
}

.scroll-content {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-height: 0;
}

.device-tree {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.selected-list {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-height: 0;
}

.dev-group-title {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 4px;
  font-size: 13px;
  font-weight: 700;
  color: var(--text-primary);
  border-bottom: 1px solid var(--border-subtle);
  margin-bottom: 4px;
}

.dp-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding-left: 12px;
}

.dp-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 5px 8px;
  border-radius: 4px;
  transition: background 0.15s;

  &:hover {
    background: var(--bg-hover);
  }

  &.already {
    opacity: 0.5;
  }
}

.dp-info {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  flex-wrap: wrap;
}

.dp-name,
.dp-unit,
.vp-name,
.vp-unit {
  color: var(--text-secondary);
  font-size: 12px;
}

.selected-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 5px 8px;
  border-radius: 4px;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
}

.selected-text {
  font-size: 12px;
  color: var(--text-primary);
  word-break: break-all;

  &.virtual {
    color: var(--purple);
  }
}

.empty-hint {
  font-size: 12px;
  color: var(--text-muted);
  text-align: center;
  padding: 20px;
}
</style>
