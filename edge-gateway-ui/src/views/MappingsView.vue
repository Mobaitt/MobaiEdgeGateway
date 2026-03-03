<template>
  <div class="mappings-view page-enter">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-left">
        <el-button text :icon="ArrowLeft" @click="router.back()" class="back-btn">返回通道列表</el-button>
        <div class="title-block">
          <h1 class="page-title">数据点映射</h1>
          <div class="channel-tag mono">{{ route.query.channelName || `通道 #${route.params.id}` }}</div>
        </div>
      </div>
      <el-button type="primary" :icon="Plus" @click="openBindDialog">绑定数据点</el-button>
    </div>

    <!-- 说明横幅 -->
    <div class="info-banner">
      <el-icon><InfoFilled /></el-icon>
      <span>选择需要通过此通道发出的数据点。采集到的数据将按下方映射关系发送，可为每个数据点设置<strong>别名</strong>以适配目标系统的字段名。</span>
    </div>

    <!-- 已绑定映射列表 -->
    <div class="table-wrap">
      <!-- 批量操作工具栏 -->
      <div v-if="selectedMappings.size > 0" class="batch-toolbar">
        <span class="batch-info">已选择 {{ selectedMappings.size }} 项</span>
        <el-button size="small" type="danger" plain @click="batchUnbind">
          <el-icon><Delete /></el-icon> 批量解绑
        </el-button>
      </div>

      <el-table :data="mappings" v-loading="loading" row-key="id" @selection-change="handleSelectionChange">
        <el-table-column type="selection" width="55" :reserve-selection="true" />

        <el-table-column label="数据点 Tag" min-width="220">
          <template #default="{ row }">
            <span class="mono tag-text">{{ row.dataPointTag }}</span>
            <el-tag v-if="row.isVirtual" size="small" type="warning" style="margin-left:6px;">虚拟</el-tag>
          </template>
        </el-table-column>

        <el-table-column prop="dataPointName" label="名称" width="130" />

        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small" effect="plain">
              {{ row.isEnabled ? 'ON' : 'OFF' }}
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column label="绑定时间" width="160">
          <template #default="{ row }">
            <span class="mono time-text">{{ formatDateTime(row.createdAt) }}</span>
          </template>
        </el-table-column>

        <el-table-column label="操作" width="100" align="right">
          <template #default="{ row }">
            <el-button size="small" text type="danger" @click="confirmUnbind(row)">
              <el-icon><Delete /></el-icon> 解绑
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <EmptyState v-if="mappings.length === 0 && !loading" message="尚未绑定任何数据点" icon="Connection">
        <el-button type="primary" size="small" @click="openBindDialog">立即绑定</el-button>
      </EmptyState>
    </div>

    <!-- 绑定数据点弹窗 -->
    <el-dialog v-model="bindDialogVisible" title="绑定数据点到通道" width="900px" destroy-on-close class="bind-dialog app-dialog" align-center>
      <div class="bind-layout">
        <!-- 左：设备树选择数据点 -->
        <div class="device-selector">
          <div class="selector-title">
            <el-icon><Monitor /></el-icon> 选择数据点
          </div>
          <el-input v-model="dpSearch" placeholder="搜索数据点或设备..." prefix-icon="Search" size="small" clearable />

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
                <span class="mono" style="color:var(--text-muted); font-size:11px">{{ dev.code }}</span>
              </div>
              <div class="dp-list">
                <div
                  v-for="dp in dev.dataPoints" :key="dp.id"
                  class="dp-item"
                  :class="{ already: isMapped(dp.id) }"
                >
                  <el-checkbox
                    :model-value="selectedIds.has(dp.id)"
                    :disabled="isMapped(dp.id)"
                    @change="(v) => toggleDp(dp.id, v)"
                  />
                  <div class="dp-info">
                    <span class="mono" style="color:var(--cyan); font-size:12px">{{ dp.tag }}</span>
                    <span v-if="dp.unit" style="color:var(--text-muted); font-size:11px">· {{ dp.unit }}</span>
                    <el-tag v-if="isMapped(dp.id)" size="small" type="info" style="margin-left:6px; font-size:10px">已绑定</el-tag>
                  </div>
                </div>
              </div>
            </div>
            <div v-if="filteredDeviceTree.length === 0" class="empty-hint">无可用数据点</div>
          </div>

          <!-- 虚拟数据点区域 -->
          <div class="virtual-section">
            <div class="selector-title" style="margin-top:12px;">
              <el-icon><Cpu /></el-icon> 选择虚拟数据点
            </div>
            <el-input v-model="virtualSearch" placeholder="搜索虚拟数据点..." prefix-icon="Search" size="small" clearable />
            <div class="virtual-list">
              <div
                v-for="vp in filteredVirtualDataPoints"
                :key="vp.id"
                class="dp-item"
                :class="{ already: isVirtualMapped(vp.id) }"
              >
                <el-checkbox
                  :model-value="virtualSelectedIds.has(vp.id)"
                  :disabled="isVirtualMapped(vp.id)"
                  @change="(v) => toggleVirtualDp(vp.id, v)"
                />
                <div class="dp-info">
                  <span class="mono" style="color:var(--purple); font-size:12px">{{ getVirtualPointFullTag(vp) }}</span>
                  <span v-if="vp.unit" style="color:var(--text-muted); font-size:11px">· {{ vp.unit }}</span>
                  <el-tag v-if="isVirtualMapped(vp.id)" size="small" type="info" style="margin-left:6px; font-size:10px">已绑定</el-tag>
                </div>
              </div>
              <div v-if="virtualDataPoints.length === 0" class="empty-hint">暂无虚拟数据点</div>
            </div>
          </div>
        </div>

        <!-- 右：已选预览 -->
        <div class="selected-preview">
          <div class="selector-title">
            <el-icon><Connection /></el-icon> 已选<span class="mono" style="color:var(--cyan);">{{ selectedIds.size + virtualSelectedIds.size }}</span> 个
          </div>
          <div class="selected-list">
            <!-- 普通数据点 -->
            <div v-for="id in [...selectedIds]" :key="id" class="selected-item">
              <span class="mono" style="font-size:12px; color:var(--text-secondary)">{{ getTagById(id) }}</span>
              <el-button size="small" text circle @click="selectedIds.delete(id)">
                <el-icon size="12"><Close /></el-icon>
              </el-button>
            </div>
            <!-- 虚拟数据点 -->
            <div v-for="id in [...virtualSelectedIds]" :key="'v-' + id" class="selected-item">
              <span class="mono" style="font-size:12px; color:var(--purple)">{{ getVirtualPointFullTagById(id) }}</span>
              <el-button size="small" text circle @click="virtualSelectedIds.delete(id)">
                <el-icon size="12"><Close /></el-icon>
              </el-button>
            </div>
            <div v-if="selectedIds.size + virtualSelectedIds.size === 0" class="empty-hint">从左侧选择数据点</div>
          </div>
        </div>
      </div>

      <template #footer>
        <el-button @click="bindDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="binding" :disabled="selectedIds.size + virtualSelectedIds.size === 0" @click="submitBind">
          绑定 {{ selectedIds.size + virtualSelectedIds.size > 0 ? `(${selectedIds.size + virtualSelectedIds.size})` : '' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessageBox, ElMessage } from 'element-plus'
import { Plus, Delete, ArrowLeft, Connection, InfoFilled, Monitor, Close, Cpu } from '@element-plus/icons-vue'
import EmptyState from '@/components/EmptyState.vue'
import { getMappings, bindDataPoints, bindVirtualDataPoints, deleteMapping } from '@/api/channel'
import { getDevices, getDataPoints } from '@/api/device'
import { getVirtualDataPoints } from '@/api/virtualNode'
import { formatDateTime } from '@/api/constants'
import type { MappingItem, DataPointItem, DeviceNode } from '@/types'
import type { VirtualDataPoint } from '@/types/virtualNode'

const route = useRoute()
const router = useRouter()
const channelId = computed(() => Number(route.params.id))

const loading = ref(false)
const mappings = ref<MappingItem[]>([])
const selectedMappings = ref(new Set<number>())

const bindDialogVisible = ref(false)
const binding = ref(false)
const dpSearch = ref('')
const virtualSearch = ref('')
const deviceTree = ref<DeviceNode[]>([])
const virtualDataPoints = ref<VirtualDataPoint[]>([])
const selectedIds = ref(new Set<number>())
const virtualSelectedIds = ref(new Set<number>())

const mappedIds = computed(() => new Set(mappings.value.filter(m => !m.isVirtual).map((item) => item.dataPointId)))
const virtualMappedIds = computed(() => new Set(mappings.value.filter(m => m.isVirtual).map((item) => item.virtualDataPointId)))

const isMapped = (id: number) => mappedIds.value.has(id)
const isVirtualMapped = (id: number) => virtualMappedIds.value.has(id)

const filteredDeviceTree = computed(() => {
  if (!dpSearch.value) return deviceTree.value

  return deviceTree.value
    .map((device) => ({
      ...device,
      dataPoints: device.dataPoints.filter(
        (dp) =>
          dp.tag.toLowerCase().includes(dpSearch.value.toLowerCase()) ||
          dp.name.toLowerCase().includes(dpSearch.value.toLowerCase())
      )
    }))
    .filter((device) => device.dataPoints.length > 0)
})

const filteredVirtualDataPoints = computed(() => {
  if (!virtualSearch.value) return virtualDataPoints.value.filter(vp => !isVirtualMapped(vp.id))
  
  return virtualDataPoints.value.filter(
    (vp) =>
      !isVirtualMapped(vp.id) &&
      (vp.tag.toLowerCase().includes(virtualSearch.value.toLowerCase()) ||
       vp.name.toLowerCase().includes(virtualSearch.value.toLowerCase()))
  )
})

const isDeviceAllSelected = (device: DeviceNode) =>
  device.dataPoints.every((dp) => isMapped(dp.id) || selectedIds.value.has(dp.id))

const isDeviceIndeterminate = (device: DeviceNode) => {
  const selectable = device.dataPoints.filter((dp) => !isMapped(dp.id))
  const selectedCount = selectable.filter((dp) => selectedIds.value.has(dp.id)).length
  return selectedCount > 0 && selectedCount < selectable.length
}

const toggleDevice = (device: DeviceNode) => {
  const selectable = device.dataPoints.filter((dp) => !isMapped(dp.id))
  if (isDeviceAllSelected(device)) {
    selectable.forEach((dp) => selectedIds.value.delete(dp.id))
  } else {
    selectable.forEach((dp) => selectedIds.value.add(dp.id))
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

const getTagById = (id: number) => {
  for (const device of deviceTree.value) {
    const dp = device.dataPoints.find((item) => item.id === id)
    if (dp) return dp.tag
  }
  return String(id)
}

const getVirtualTagById = (id: number) => {
  const vp = virtualDataPoints.value.find((item) => item.id === id)
  return vp ? vp.tag : String(id)
}

// 获取虚拟点位的完整 Tag（{deviceCode}.{tag}）
const getVirtualPointFullTag = (vp: VirtualDataPoint) => {
  const device = deviceTree.value.find(d => d.id === vp.deviceId)
  return device ? `${device.code}.${vp.tag}` : vp.tag
}

const getVirtualPointFullTagById = (id: number) => {
  const vp = virtualDataPoints.value.find((item) => item.id === id)
  if (!vp) return String(id)
  const device = deviceTree.value.find(d => d.id === vp.deviceId)
  return device ? `${device.code}.${vp.tag}` : vp.tag
}

const fetchMappings = async () => {
  loading.value = true
  try {
    const res = await getMappings(channelId.value)
    mappings.value = ((res as { data?: MappingItem[] })?.data ?? []) as MappingItem[]
  } finally {
    loading.value = false
  }
}

const openBindDialog = async () => {
  selectedIds.value = new Set<number>()
  virtualSelectedIds.value = new Set<number>()
  dpSearch.value = ''
  virtualSearch.value = ''

  // 加载设备和普通数据点
  const devRes = await getDevices()
  const devList = ((devRes as { data?: Array<{ id: number; name: string; code: string }> })?.data ?? []) as Array<{
    id: number
    name: string
    code: string
  }>

  deviceTree.value = await Promise.all(
    devList.map(async (device) => {
      const dpRes = await getDataPoints(device.id)
      const dataPoints = ((dpRes as { data?: DataPointItem[] })?.data ?? []) as DataPointItem[]
      return {
        ...device,
        dataPoints: dataPoints.filter((dp) => dp.isEnabled)
      }
    })
  )

  // 加载虚拟数据点
  const virtualRes = await getVirtualDataPoints()
  virtualDataPoints.value = ((virtualRes as any).data ?? [])
    .filter((vp: VirtualDataPoint) => vp.isEnabled)

  bindDialogVisible.value = true
}

const submitBind = async () => {
  if (selectedIds.value.size === 0 && virtualSelectedIds.value.size === 0) return

  binding.value = true
  try {
    // 绑定普通数据点
    if (selectedIds.value.size > 0) {
      await bindDataPoints(channelId.value, [...selectedIds.value])
    }
    // 绑定虚拟数据点
    if (virtualSelectedIds.value.size > 0) {
      await bindVirtualDataPoints(channelId.value, [...virtualSelectedIds.value])
    }
    ElMessage.success(`成功绑定 ${selectedIds.value.size + virtualSelectedIds.value.size} 个数据点`)
    bindDialogVisible.value = false
    fetchMappings()
  } finally {
    binding.value = false
  }
}

const confirmUnbind = async (row: MappingItem) => {
  try {
    await ElMessageBox.confirm(`确定要解绑数据点 "${row.dataPointTag}" 吗？`, '解绑确认', {
      type: 'warning',
      confirmButtonText: '解绑',
      cancelButtonText: '取消'
    })
    await deleteMapping(row.id)
    ElMessage.success('解绑成功')
    fetchMappings()
  } catch {
    // 用户取消
  }
}

const batchUnbind = async () => {
  try {
    await ElMessageBox.confirm(`确定要解绑选中的 ${selectedMappings.value.size} 个数据点吗？`, '批量解绑确认', {
      type: 'warning',
      confirmButtonText: '批量解绑',
      cancelButtonText: '取消'
    })
    
    const promises = [...selectedMappings.value].map(id => deleteMapping(id))
    await Promise.all(promises)
    
    ElMessage.success(`成功解绑 ${selectedMappings.value.size} 个数据点`)
    selectedMappings.value.clear()
    fetchMappings()
  } catch {
    // 用户取消
  }
}

const handleSelectionChange = (selection: any[]) => {
  selectedMappings.value = new Set(selection.map(item => item.id))
}

onMounted(fetchMappings)
</script>

<style scoped>
.page-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:16px; }
.header-left { display:flex; align-items:center; gap:14px; }
.back-btn    { color:var(--text-muted) !important; padding:0 !important; }
.title-block { display:flex; align-items:baseline; gap:12px; }
.page-title  { font-size:22px; font-weight:800; color:var(--text-primary); }
.channel-tag { font-size:12px; color:var(--cyan); background:var(--cyan-dim); padding:2px 10px; border-radius:10px; border:1px solid rgba(56,220,196,0.25); }

.info-banner {
  display:flex; align-items:center; gap:10px;
  background:rgba(66,153,225,0.08); border:1px solid rgba(66,153,225,0.2);
  border-radius:var(--radius); padding:10px 16px; margin-bottom:16px;
  font-size:13px; color:var(--text-secondary);
}

.table-wrap { background:var(--bg-card); border:1px solid var(--border-subtle); border-radius:var(--radius-lg); overflow:hidden; }
.tag-text  { font-size:13px; color:var(--cyan); }
.time-text  { font-size:11px; color:var(--text-muted); }

/* 批量操作工具栏 */
.batch-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  background: rgba(240, 89, 89, 0.08);
  border-bottom: 1px solid rgba(240, 89, 89, 0.2);
}
.batch-info {
  font-size: 13px;
  color: var(--text-primary);
  font-weight: 500;
}


/* 绑定弹窗布局 */
:deep(.bind-dialog .el-dialog__body) { max-height: 60vh; }
.bind-layout { 
  display:grid; 
  grid-template-columns:1fr 220px; 
  gap:16px; 
  max-height:60vh;
  overflow: hidden;
}
.device-selector, .selected-preview {
  display:flex; flex-direction:column; gap:10px;
  border:1px solid var(--border-subtle); border-radius:var(--radius); padding:12px;
  overflow:hidden;
  background: var(--bg-base);
}
.selector-title { 
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px; 
  font-weight: 700; 
  color: var(--text-primary);
  letter-spacing:0.06em; 
  text-transform:uppercase;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--border-subtle);
}
.selector-title .el-icon {
  color: var(--cyan);
}
.device-tree, .selected-list {
  flex:1;
  overflow-y:auto;
  display:flex;
  flex-direction:column;
  gap:6px;
}

.virtual-section {
  margin-top: 12px;
  border-top: 1px solid var(--border-subtle);
  padding-top: 12px;
}
.virtual-list {
  flex:1;
  overflow-y:auto;
  display:flex;
  flex-direction:column;
  gap:6px;
  max-height: 200px;
}

.dev-group { }
.dev-group-title {
  display:flex; align-items:center; gap:8px;
  padding:6px 4px; font-size:13px; font-weight:700; color:var(--text-primary);
  border-bottom:1px solid var(--border-subtle); margin-bottom:4px;
}
.dp-list { display:flex; flex-direction:column; gap:2px; padding-left:12px; }
.dp-item {
  display:flex; align-items:center; gap:8px;
  padding:5px 8px; border-radius:4px; transition:background 0.15s;
}
.dp-item:hover { background:var(--bg-hover); }
.dp-item.already { opacity:0.5; }
.dp-info { display:flex; align-items:center; gap:6px; flex:1; }

/* 虚拟数据点特殊样式 */
.virtual-section .selector-title .el-icon {
  color: var(--purple);
}
.virtual-section .dp-item .mono {
  color: var(--purple) !important;
}

.selected-item {
  display:flex; align-items:center; justify-content:space-between;
  padding:5px 8px; border-radius:4px; background:var(--bg-card);
  border: 1px solid var(--border-subtle);
}
.empty-hint { font-size:12px; color:var(--text-muted); text-align:center; padding:20px; }

/* 弹窗内表单输入样式 */
:deep(.el-input__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-checkbox__label) { color: var(--text-primary) !important; }
:deep(.el-checkbox__input.is-checked .el-checkbox__inner) { background-color: var(--cyan) !important; border-color: var(--cyan) !important; }
:deep(.el-checkbox__inner) { background-color: var(--bg-base) !important; border-color: var(--border-muted) !important; }
</style>
