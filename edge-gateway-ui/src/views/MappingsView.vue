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
import {ArrowLeft, Delete, InfoFilled, Plus} from '@element-plus/icons-vue'
import EmptyState from '@/components/EmptyState.vue'
import BindDataPointDialog from '@/dialogs/mapping/BindDataPointDialog.vue'
import {bindDataPoints, bindVirtualDataPoints, deleteMapping, getMappings} from '@/api/channel'
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

const bindDialogVisible = ref(false)
const binding = ref(false)
const deviceTree = ref<DeviceTreeItem[]>([])
const allVirtualPoints = ref<VirtualDataPoint[]>([])

const mappedIds = computed(() => new Set(mappings.value.filter(m => !m.isVirtual).map((item) => item.dataPointId)))
const virtualMappedIds = computed(() => new Set(mappings.value.filter(m => m.isVirtual).map((item) => item.virtualDataPointId)))

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
    ElMessage.success(`成功绑定 ${selectedIds.size + virtualSelectedIds.size} 个数据点`)
    bindDialogVisible.value = false
    fetchMappings()
  } catch (error: any) {
    ElMessage.error(`绑定失败：${error.message || '未知错误'}`)
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

const handleBindDialogClose = () => {}

onMounted(fetchMappings)
</script>

<style scoped lang="scss">
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
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
}

.table-wrap {
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  overflow: hidden;
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
}

.batch-info {
  font-size: 13px;
  color: var(--text-primary);
  font-weight: 500;
}
</style>
