<template>
  <div class="data-point-table">
    <AppTable :data="data" :loading="loading" row-key="id">
      <el-table-column type="index" label="#" width="50" align="center" />

      <el-table-column prop="tag" label="Tag" min-width="220">
        <template #default="{ row }">
          <span class="mono tag-text">{{ row.tag }}</span>
          <el-tag v-if="row.isVirtual" size="small" type="warning" class="virtual-tag">虚拟</el-tag>
        </template>
      </el-table-column>

      <el-table-column prop="name" label="名称" width="140" />

      <el-table-column prop="address" label="地址" width="120">
        <template #default="{ row }">
          <span v-if="!row.isVirtual" class="mono addr-text">
            {{ row.address }}<span v-if="row.modbusBitIndex !== null && row.modbusBitIndex !== undefined"> · Bit{{ row.modbusBitIndex }}</span>
          </span>
          <span v-else class="addr-text">表达式</span>
        </template>
      </el-table-column>

      <el-table-column prop="dataType" label="类型" width="180" align="center">
        <template #default="{ row }">
          <span class="badge info mono">{{ getDataTypeLabel(row) }}</span>
        </template>
      </el-table-column>

      <el-table-column prop="unit" label="单位" width="80" align="center" />

      <el-table-column label="实时值" width="150" align="center">
        <template #default="{ row }">
          <span v-if="getRealtimeData(row)" class="mono realtime-value" :class="getQualityClass(getRealtimeData(row)!.quality)">
            {{ formatRowValue(row) }}
            <span v-if="row.unit" class="value-unit">{{ row.unit }}</span>
          </span>
          <span v-else class="empty-text">-</span>
        </template>
      </el-table-column>

      <el-table-column label="质量" width="100" align="center">
        <template #default="{ row }">
          <span v-if="getRealtimeData(row)" class="badge mono" :class="getQualityClass(getRealtimeData(row)!.quality)">
            {{ getRealtimeData(row)!.quality }}
          </span>
          <span v-else class="empty-text">-</span>
        </template>
      </el-table-column>

      <el-table-column label="启用" width="80" align="center">
        <template #default="{ row }">
          <el-switch
            v-model="row.isEnabled"
            size="small"
            active-color="#38dcc4"
            inactive-color="#999"
            @change="row.isVirtual ? emit('toggle-virtual', row) : emit('toggle-data-point', row)"
          />
        </template>
      </el-table-column>

      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="{ row }">
          <span class="mono time-text">{{ formatDateTime(row.createdAt) }}</span>
        </template>
      </el-table-column>

      <el-table-column label="操作" width="240" align="right" fixed="right">
        <template #default="{ row }">
          <el-button
            v-if="!row.isVirtual && row.isControllable"
            size="small"
            text
            type="warning"
            :disabled="!row.isEnabled || !deviceEnabled"
            @click="emit('control', row)"
          >
            发送指令
          </el-button>
          <el-button v-if="row.isVirtual" size="small" text type="success" @click="emit('edit-virtual', row)">编辑</el-button>
          <el-button v-else size="small" text type="primary" @click="emit('edit', row)">编辑</el-button>
          <el-button size="small" text type="danger" @click="row.isVirtual ? emit('delete-virtual', row) : emit('delete', row)">删除</el-button>
        </template>
      </el-table-column>
    </AppTable>

    <div class="pagination-bar eg-pagination-bar">
      <el-pagination
        :current-page="page"
        :page-size="pageSize"
        :page-sizes="[20, 50, 100, 200]"
        :total="total"
        layout="total, sizes, prev, pager, next, jumper"
        @size-change="value => emit('size-change', value)"
        @current-change="value => emit('page-change', value)"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { toRefs } from 'vue'
import { formatDateTime } from '@/api/constants'
import type { DataPointItem, RealtimeDataItem } from '@/types'
import type { VirtualDataPoint } from '@/types/virtualNode'
import AppTable from '@/components/AppTable.vue'

type DataPointRow = (DataPointItem | VirtualDataPoint) & { isVirtual?: boolean }

const props = defineProps<{
  data: DataPointRow[]
  loading: boolean
  deviceEnabled: boolean
  realtimeData: Record<string, RealtimeDataItem>
  page: number
  pageSize: number
  total: number
  getDataTypeLabel: (row: DataPointRow) => string
  formatRowValue: (row: DataPointRow) => string
  getQualityClass: (quality: string) => string
}>()

const { data, loading, deviceEnabled, page, pageSize, total, getDataTypeLabel, formatRowValue, getQualityClass } = toRefs(props)

const emit = defineEmits<{
  (event: 'toggle-data-point', row: DataPointRow): void
  (event: 'toggle-virtual', row: DataPointRow): void
  (event: 'control', row: DataPointRow): void
  (event: 'edit', row: DataPointRow): void
  (event: 'edit-virtual', row: DataPointRow): void
  (event: 'delete', row: DataPointRow): void
  (event: 'delete-virtual', row: DataPointRow): void
  (event: 'size-change', value: number): void
  (event: 'page-change', value: number): void
}>()

const getRealtimeData = (row: DataPointRow) => props.realtimeData[row.tag] || null
</script>

<style scoped lang="scss">
.data-point-table {
  display: flex;
  flex-direction: column;
  flex: 1 1 auto;
  min-height: 0;
}

.virtual-tag { margin-left: 6px; }
.tag-text { color: var(--cyan); }
.mono { font-family: var(--font-mono); }
.addr-text, .time-text, .empty-text, .value-unit { color: var(--text-muted); }
.realtime-value { font-weight: 600; }
.realtime-value.good, .badge.good { color: var(--text-success); }
.realtime-value.bad, .badge.bad { color: var(--text-danger); }
.realtime-value.uncertain, .badge.uncertain { color: var(--text-warn); }
.badge.info { color: var(--text-secondary); }

.pagination-bar { min-height: 58px; padding: 11px 16px; }

:deep(.app-table) {
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  min-height: 0;
}

:deep(.app-table .el-table__inner-wrapper) {
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  min-height: 0;
}

:deep(.app-table .el-table__header-wrapper) { flex-shrink: 0; }

:deep(.el-table__body-wrapper) {
  flex: 1 1 auto;
  max-height: none;
  overflow-y: auto;
}

:deep(.el-table__inner-wrapper::before) {
  background-color: rgba(120, 155, 190, .16);
}

:deep(.pagination-bar .el-pagination .btn-prev),
:deep(.pagination-bar .el-pagination .btn-next),
:deep(.pagination-bar .el-pagination__sizes .el-input__wrapper),
:deep(.pagination-bar .el-pagination__jump .el-input__wrapper) {
  border-color: var(--border-subtle);
  background: var(--bg-card);
  color: var(--text-secondary);
}

:deep(.pagination-bar .el-pager li) {
  border: 1px solid var(--border-subtle);
  border-radius: 9px;
  background: var(--bg-card);
  color: var(--text-secondary);
}

:deep(.pagination-bar .el-pager li.is-active) {
  border-color: rgba(56, 220, 196, .58) !important;
  background: var(--theme-button-highlight) !important;
  color: #f3fffd !important;
  box-shadow: 0 6px 16px rgba(56, 220, 196, .18);
}

:deep(.pagination-bar .el-pager li:hover),
:deep(.pagination-bar .el-pagination .btn-prev:hover),
:deep(.pagination-bar .el-pagination .btn-next:hover) {
  border-color: rgba(56, 220, 196, .34);
  background: rgba(56, 220, 196, .08);
  color: var(--cyan);
}
</style>
