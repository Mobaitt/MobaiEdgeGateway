<template>
  <div class="mappings-view page-content page-enter">
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

    <!-- 主内容区域 -->
    <div class="main-content">
      <!-- 已绑定映射列表 -->
      <div class="table-wrap">
        <!-- 批量操作工具栏 -->
        <div v-if="selectedMappings.size > 0" class="batch-toolbar">
          <span class="batch-info">已选择 {{ selectedMappings.size }} 项</span>
          <el-button size="small" type="danger" plain @click="batchUnbind">
            <el-icon><Delete /></el-icon> 批量解绑
          </el-button>
        </div>

        <!-- 搜索工具栏 -->
        <div class="mapping-toolbar">
          <div class="toolbar-left">
            <el-input
              v-model="searchText"
              placeholder="搜索 Tag / 名称..."
              prefix-icon="Search"
              clearable
              style="width: 280px"
              @input="handleSearch"
            />
            <el-select v-model="filterIsEnabled" placeholder="启用状态" clearable style="width: 120px" @change="handleFilterChange">
              <el-option label="启用" :value="true" />
              <el-option label="禁用" :value="false" />
            </el-select>
            <el-select v-model="filterIsVirtual" placeholder="数据类型" clearable style="width: 120px" @change="handleFilterChange">
              <el-option label="普通数据点" :value="false" />
              <el-option label="虚拟数据点" :value="true" />
            </el-select>
          </div>
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

        <!-- 分页 -->
        <div class="pagination-bar">
          <el-pagination
            v-model:current-page="pagination.page"
            v-model:page-size="pagination.pageSize"
            :page-sizes="[20, 50, 100, 200]"
            :total="pagination.total"
            layout="total, sizes, prev, pager, next, jumper"
            @size-change="fetchMappings"
            @current-change="fetchMappings"
          />
        </div>

        <EmptyState v-if="mappings.length === 0 && !loading" message="尚未绑定任何数据点" icon="Connection">
          <el-button type="primary" size="small" @click="openBindDialog">立即绑定</el-button>
        </EmptyState>
      </div>
    </div>

    <!-- 绑定数据点弹窗 -->
    <BindDataPointDialog
      v-model="bindDialogVisible"
      :device-tree="deviceTree"
      :virtual-data-points="allVirtualPoints"
      :mapped-ids="mappedIds"
      :virtual-mapped-ids="virtualMappedIds"
      :loading="binding"
      @submit="handleBindSubmit"
      @close="handleBindDialogClose"
    />
  </div>
</template>

<script setup lang="ts">
import {computed, onMounted, ref} from 'vue'
import {useRoute, useRouter} from 'vue-router'
import {ElMessage, ElMessageBox} from 'element-plus'
import {ArrowLeft, Delete, InfoFilled, Plus, Search} from '@element-plus/icons-vue'
import EmptyState from '@/components/EmptyState.vue'
import BindDataPointDialog from '@/dialogs/mapping/BindDataPointDialog.vue'
import {bindDataPoints, bindVirtualDataPoints, deleteMapping, getMappings, getMappingsPaged} from '@/api/channel'
import {getDataPoints, getDevices} from '@/api/device'
import {getVirtualDataPoints} from '@/api/virtualNode'
import {formatDateTime} from '@/api/constants'
import type {DataPointItem, DeviceNode, MappingItem} from '@/types'
import type {VirtualDataPoint} from '@/types/virtualNode'

interface DeviceTreeItem extends DeviceNode {
  virtualPoints?: VirtualDataPoint[]
}

const route = useRoute()
const router = useRouter()
const channelId = computed(() => Number(route.params.id))

const loading = ref(false)
const mappings = ref<MappingItem[]>([])
const selectedMappings = ref(new Set<number>())

// 分页配置
const pagination = ref<{ page: number; pageSize: number; total: number }>({
  page: 1,
  pageSize: 50,
  total: 0
})

// 搜索和过滤
const searchText = ref('')
const filterIsEnabled = ref<boolean | null>(null)
const filterIsVirtual = ref<boolean | null>(null)

const bindDialogVisible = ref(false)
const binding = ref(false)
const deviceTree = ref<DeviceTreeItem[]>([])
const allVirtualPoints = ref<VirtualDataPoint[]>([])

// 所有已绑定的 ID（用于对话框回显）
const allMappedIds = ref<Set<number>>(new Set())
const allVirtualMappedIds = ref<Set<number>>(new Set())

const mappedIds = computed(() => allMappedIds.value)
const virtualMappedIds = computed(() => allVirtualMappedIds.value)

const fetchMappings = async () => {
  loading.value = true
  try {
    const res = await getMappingsPaged(channelId.value, {
      page: pagination.value.page,
      pageSize: pagination.value.pageSize,
      search: searchText.value || undefined,
      isEnabled: filterIsEnabled.value ?? undefined,
      isVirtual: filterIsVirtual.value ?? undefined
    })
    
    const responseData = (res as { data?: { items: MappingItem[]; total: number } })?.data
    if (responseData) {
      mappings.value = responseData.items
      pagination.value.total = responseData.total
    }
  } finally {
    loading.value = false
  }
}

const handleSearch = () => {
  pagination.value.page = 1
  fetchMappings()
}

const handleFilterChange = () => {
  pagination.value.page = 1
  fetchMappings()
}

const openBindDialog = async () => {
  bindDialogVisible.value = true

  // 加载设备和普通数据点
  const devRes = await getDevices()
  const devList = ((devRes as { data?: Array<{ id: number; name: string; code: string }> })?.data ?? []) as Array<{
    id: number
    name: string
    code: string
  }>

  // 加载虚拟数据点
  const virtualRes = await getVirtualDataPoints()
  allVirtualPoints.value = ((virtualRes as any).data ?? [])
    .filter((vp: VirtualDataPoint) => vp.isEnabled)

  // 获取所有已绑定的映射（不分页，用于回显）
  const allMappingsRes = await getMappings(channelId.value)
  const allMappings = ((allMappingsRes as { data?: MappingItem[] })?.data ?? []) as MappingItem[]
  allMappedIds.value = new Set(allMappings.filter(m => !m.isVirtual).map(m => m.dataPointId))
  allVirtualMappedIds.value = new Set(allMappings.filter(m => m.isVirtual).map(m => m.virtualDataPointId))

  deviceTree.value = await Promise.all(
    devList.map(async (device) => {
      const dpRes = await getDataPoints(device.id)
      const dataPoints = ((dpRes as { data?: DataPointItem[] })?.data ?? []) as DataPointItem[]
      const deviceVirtualPoints = allVirtualPoints.value.filter((vp: VirtualDataPoint) => vp.deviceId === device.id)
      return {
        ...device,
        dataPoints: dataPoints.filter((dp) => dp.isEnabled),
        virtualPoints: deviceVirtualPoints
      } as DeviceTreeItem
    })
  )
}

const handleBindSubmit = async (selectedIds: Set<number>, virtualSelectedIds: Set<number>) => {
  if (selectedIds.size === 0 && virtualSelectedIds.size === 0) return

  binding.value = true
  try {
    if (selectedIds.size > 0) {
      await bindDataPoints(channelId.value, [...selectedIds])
    }
    if (virtualSelectedIds.size > 0) {
      await bindVirtualDataPoints(channelId.value, [...virtualSelectedIds])
    }
    
    // 刷新已绑定 ID 列表
    const allMappingsRes = await getMappings(channelId.value)
    const allMappings = ((allMappingsRes as { data?: MappingItem[] })?.data ?? []) as MappingItem[]
    allMappedIds.value = new Set(allMappings.filter(m => !m.isVirtual).map(m => m.dataPointId))
    allVirtualMappedIds.value = new Set(allMappings.filter(m => m.isVirtual).map(m => m.virtualDataPointId))
    
    ElMessage.success(`成功绑定 ${selectedIds.size + virtualSelectedIds.size} 个数据点`)
    bindDialogVisible.value = false
    fetchMappings()
  } catch (error: any) {
    ElMessage.error(`绑定失败：${error.message || '未知错误'}`)
  } finally {
    binding.value = false
  }
}

// 刷新已绑定 ID 列表
const refreshAllMappedIds = async () => {
  const allMappingsRes = await getMappings(channelId.value)
  const allMappings = ((allMappingsRes as { data?: MappingItem[] })?.data ?? []) as MappingItem[]
  allMappedIds.value = new Set(allMappings.filter(m => !m.isVirtual).map(m => m.dataPointId))
  allVirtualMappedIds.value = new Set(allMappings.filter(m => m.isVirtual).map(m => m.virtualDataPointId))
}

const confirmUnbind = async (row: MappingItem) => {
  try {
    await ElMessageBox.confirm(`确定要解绑数据点 "${row.dataPointTag}" 吗？`, '解绑确认', {
      type: 'warning',
      confirmButtonText: '解绑',
      cancelButtonText: '取消'
    })
    await deleteMapping(row.id)
    await refreshAllMappedIds()
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

    await refreshAllMappedIds()
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

const handleBindDialogClose = () => {}

onMounted(fetchMappings)
</script>

<style scoped lang="scss">
.page-enter {
  animation: fadeIn 0.2s ease-in-out;
}

.page-content {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 14px;
}

.back-btn {
  color: var(--text-muted) !important;
  padding: 0 !important;
}

.title-block {
  display: flex;
  align-items: baseline;
  gap: 12px;
}

.page-title {
  font-size: 22px;
  font-weight: 800;
  color: var(--text-primary);
}

.channel-tag {
  font-size: 12px;
  color: var(--cyan);
  background: var(--cyan-dim);
  padding: 2px 10px;
  border-radius: 10px;
  border: 1px solid rgba(56, 220, 196, 0.25);
}

.info-banner {
  display: flex;
  align-items: center;
  gap: 10px;
  background: rgba(66, 153, 225, 0.08);
  border: 1px solid rgba(66, 153, 225, 0.2);
  border-radius: var(--radius);
  padding: 10px 16px;
  margin-bottom: 16px;
  font-size: 13px;
  color: var(--text-secondary);
  flex-shrink: 0;
}

/* 主内容区域 */
.main-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-height: 0;
}

/* 表格容器 */
.table-wrap {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  overflow: hidden;
  min-height: 0;
}

/* Element Plus 表格样式优化 */
:deep(.el-table) {
  flex: 1;
  overflow: auto;
}

:deep(.el-table__body-wrapper) {
  overflow: auto;
}

:deep(.el-table__header-wrapper) {
  position: sticky;
  top: 0;
  z-index: 10;
  background: var(--bg-card);
}

/* 搜索工具栏 */
.mapping-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-subtle);
  background: var(--bg-base);
  flex-shrink: 0;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

/* 分页栏 */
.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  padding: 12px 16px;
  border-top: 1px solid var(--border-subtle);
  background: var(--bg-base);
  flex-shrink: 0;
  gap: 8px;
}

/* Element Plus 分页样式优化 */
:deep(.el-pagination) {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0;
}

:deep(.el-pagination__total) {
  color: var(--text-muted);
  font-size: 13px;
  font-weight: 500;
}

:deep(.el-pagination__sizes) {
  margin-right: 8px;
}

:deep(.el-select .el-input) {
  width: 100px;
}

:deep(.el-pager) {
  display: flex;
  align-items: center;
  gap: 4px;
}

:deep(.el-pager li) {
  min-width: 32px;
  height: 32px;
  line-height: 32px;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: 6px;
  transition: all 0.2s ease;
  margin: 0;
  
  &:hover {
    background: var(--bg-hover);
    border-color: var(--cyan);
    color: var(--cyan);
    transform: translateY(-1px);
  }
  
  &.is-active {
    background: linear-gradient(135deg, var(--cyan) 0%, rgba(56, 220, 196, 0.8) 100%);
    border-color: var(--cyan);
    color: #fff;
    font-weight: 600;
    box-shadow: 0 2px 8px rgba(56, 220, 196, 0.3);
  }
}

:deep(.el-pagination__jump) {
  margin-left: 12px;
  color: var(--text-muted);
  font-size: 13px;
  
  .el-input {
    width: 50px;
    margin: 0 4px;
    
    .el-input__wrapper {
      height: 32px;
      padding: 0 8px;
      border-radius: 6px;
      background: var(--bg-card);
      border: 1px solid var(--border-subtle);
      transition: all 0.2s ease;
      
      &:hover {
        border-color: var(--cyan);
      }
      
      &.is-focus {
        border-color: var(--cyan);
        box-shadow: 0 0 0 2px rgba(56, 220, 196, 0.2);
      }
    }
    
    .el-input__inner {
      font-size: 13px;
      text-align: center;
      font-weight: 500;
    }
  }
}

:deep(.btn-prev),
:deep(.btn-next) {
  min-width: 32px;
  height: 32px;
  border-radius: 6px;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  color: var(--text-secondary);
  transition: all 0.2s ease;
  
  &:hover {
    background: var(--bg-hover);
    border-color: var(--cyan);
    color: var(--cyan);
    transform: translateY(-1px);
  }
  
  &:disabled {
    opacity: 0.5;
    cursor: not-allowed;
    
    &:hover {
      background: var(--bg-card);
      border-color: var(--border-subtle);
      color: var(--text-secondary);
      transform: none;
    }
  }
}

:deep(.el-select-dropdown__item) {
  font-size: 13px;
}

:deep(.el-select-dropdown__item.is-selected) {
  color: var(--cyan);
  font-weight: 600;
}

.tag-text {
  font-size: 13px;
  color: var(--cyan);
}

.time-text {
  font-size: 11px;
  color: var(--text-muted);
}

/* 批量操作工具栏 */
.batch-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  background: rgba(240, 89, 89, 0.08);
  border-bottom: 1px solid rgba(240, 89, 89, 0.2);
  flex-shrink: 0;
}

.batch-info {
  font-size: 13px;
  color: var(--text-primary);
  font-weight: 500;
}
</style>
