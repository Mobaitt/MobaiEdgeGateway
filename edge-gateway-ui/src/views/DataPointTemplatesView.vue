<template>
  <div class="templates-view page-content page-enter">
    <div class="page-header">
      <div class="title-block">
        <h1 class="page-title">点位模板</h1>
        <div class="page-desc">复用设备点位配置，统一维护采集地址和 Modbus 参数</div>
      </div>
      <el-button class="template-refresh-btn" :icon="Refresh" :loading="loading" @click="fetchTemplates">刷新</el-button>
    </div>

    <div class="toolbar eg-toolbar-surface">
      <div class="template-search-group">
        <el-input
          v-model="search"
          class="template-search-input"
          prefix-icon="Search"
          clearable
          placeholder="搜索模板名称或描述"
          @keyup.enter="searchTemplates"
          @clear="searchTemplates"
        />
        <el-button class="template-search-btn" type="primary" @click="searchTemplates">搜索</el-button>
      </div>
      <span class="template-count mono">共 {{ total }} 个模板</span>
    </div>

    <div class="table-wrap">
      <AppTable :data="templates" :loading="loading" row-key="id">
        <el-table-column prop="name" label="模板名称" min-width="220" />
        <el-table-column prop="description" label="描述" min-width="260" show-overflow-tooltip />
        <el-table-column prop="protocol" label="协议" width="120" />
        <el-table-column prop="pointCount" label="点位数" width="100" align="center" />
        <el-table-column label="更新时间" width="190">
          <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="220" align="right">
          <template #default="{ row }">
            <el-button text type="primary" @click="openTemplate(row.id)">查看点位</el-button>
            <el-button text type="danger" @click="removeTemplate(row)">删除</el-button>
          </template>
        </el-table-column>
      </AppTable>
      <div class="pagination-bar eg-pagination-bar">
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :page-sizes="[10, 20, 50, 100]"
          :total="total"
          layout="total, sizes, prev, pager, next, jumper"
          @current-change="fetchTemplates"
          @size-change="handlePageSizeChange"
        />
      </div>
    </div>

    <el-dialog v-model="detailVisible" width="1280px" destroy-on-close class="template-detail-dialog">
      <template #header>
        <div class="dialog-heading">
          <div>
            <div class="dialog-heading__title">{{ editing ? '编辑模板点位' : '查看模板点位' }}</div>
            <div class="dialog-heading__sub">维护模板中的地址、类型与 Modbus 映射配置</div>
          </div>
          <el-tag v-if="detail" effect="dark" type="success">{{ detail.points.length }} 个点位</el-tag>
        </div>
      </template>
      <el-form v-if="detail" :model="detail" label-width="80px" class="template-detail-form">
        <el-row :gutter="16">
          <el-col :span="12"><el-form-item label="模板名称"><el-input v-model="detail.name" :disabled="!editing" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="描述"><el-input v-model="detail.description" :disabled="!editing" /></el-form-item></el-col>
        </el-row>
        <div class="point-table-wrap">
          <AppTable :data="detail.points" border max-height="480" class="point-table">
          <el-table-column type="index" width="55" />
          <el-table-column label="名称" min-width="150">
            <template #default="{ row }"><el-input v-if="editing" v-model="row.name" size="small" /><span v-else>{{ row.name }}</span></template>
          </el-table-column>
          <el-table-column label="Tag 后缀" min-width="180">
            <template #default="{ row }"><el-input v-if="editing" v-model="row.tagSuffix" size="small" /><span v-else class="mono">{{ row.tagSuffix }}</span></template>
          </el-table-column>
          <el-table-column label="地址" width="140">
            <template #default="{ row }"><el-input v-if="editing" v-model="row.address" size="small" /><span v-else class="mono">{{ row.address }}</span></template>
          </el-table-column>
          <el-table-column label="功能码" width="160">
            <template #default="{ row }">
              <el-select v-if="editing" v-model="row.modbusFunctionCode" size="small">
                <el-option v-for="option in functionCodeOptions" :key="option.value" :label="option.label" :value="option.value" />
              </el-select>
              <span v-else class="code-label">{{ functionCodeLabel(row.modbusFunctionCode) }}</span>
            </template>
          </el-table-column>
          <el-table-column label="寄存器数" width="100">
            <template #default="{ row }">
              <el-select v-if="editing" v-model="row.registerLength" size="small">
                <el-option v-for="option in registerLengthOptions" :key="option.value" :label="option.label" :value="option.value" />
              </el-select>
              <span v-else class="register-label">{{ registerLengthLabel(row) }}</span>
            </template>
          </el-table-column>
          <el-table-column label="位索引" width="90">
            <template #default="{ row }"><el-input-number v-if="editing" v-model="row.modbusBitIndex" :min="0" :max="15" size="small" controls-position="right" /><span v-else>{{ row.modbusBitIndex ?? '-' }}</span></template>
          </el-table-column>
          <el-table-column label="类型" width="120">
            <template #default="{ row }"><el-select v-if="editing" v-model="row.dataType" size="small" @change="syncRegisterLength(row)"><el-option v-for="type in dataTypeOptions" :key="type.value" :label="type.label" :value="type.value" /></el-select><span v-else>{{ dataTypeLabel(row.dataType) }}</span></template>
          </el-table-column>
          <el-table-column label="启用" width="80" align="center">
            <template #default="{ row }"><el-switch v-if="editing" v-model="row.isEnabled" size="small" /><span v-else>{{ row.isEnabled ? '是' : '否' }}</span></template>
          </el-table-column>
          </AppTable>
        </div>
      </el-form>
      <template #footer>
        <el-button @click="detailVisible = false">关闭</el-button>
        <el-button v-if="!editing" type="primary" @click="editing = true">编辑点位</el-button>
        <el-button v-else type="primary" :loading="saving" @click="saveTemplate">保存修改</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh } from '@element-plus/icons-vue'
import AppTable from '@/components/AppTable.vue'
import { formatDateTime } from '@/api/constants'
import { getDataValueTypes } from '@/api/enums'
import {
  deleteDataPointTemplate,
  getDataPointTemplate,
  getDataPointTemplatesPaged,
  updateDataPointTemplate,
  type DataPointTemplateDetail,
  type DataPointTemplateItem
} from '@/api/dataPointTemplate'

const templates = ref<DataPointTemplateItem[]>([])
const search = ref('')
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const loading = ref(false)
const saving = ref(false)
const detailVisible = ref(false)
const editing = ref(false)
const detail = ref<DataPointTemplateDetail | null>(null)
const dataTypeOptions = ref<any[]>([])
const functionCodeOptions = [
  { value: 1, label: '01 · 线圈' },
  { value: 2, label: '02 · 离散输入' },
  { value: 3, label: '03 · 保持寄存器' },
  { value: 4, label: '04 · 输入寄存器' }
]
const registerLengthOptions = [
  { value: 1, label: '1 个 · 16 位' },
  { value: 2, label: '2 个 · 32 位' },
  { value: 4, label: '4 个 · 64 位' }
]

const fetchTemplates = async () => {
  loading.value = true
  try {
    const res = await getDataPointTemplatesPaged({ page: page.value, pageSize: pageSize.value, search: search.value || undefined })
    const data = (res as { data?: { items: DataPointTemplateItem[]; total: number } }).data
    templates.value = data?.items || []
    total.value = data?.total || 0
  } finally {
    loading.value = false
  }
}

const searchTemplates = () => {
  page.value = 1
  void fetchTemplates()
}

const handlePageSizeChange = () => {
  page.value = 1
  void fetchTemplates()
}

const openTemplate = async (id: number) => {
  const res = await getDataPointTemplate(id)
  detail.value = (res as { data?: DataPointTemplateDetail }).data || null
  editing.value = false
  detailVisible.value = true
}

const saveTemplate = async () => {
  if (!detail.value) return
  saving.value = true
  try {
    await updateDataPointTemplate(detail.value.id, {
      name: detail.value.name,
      description: detail.value.description || undefined,
      points: detail.value.points
    })
    editing.value = false
    await fetchTemplates()
    ElMessage.success('模板已更新')
  } finally {
    saving.value = false
  }
}

const removeTemplate = async (template: DataPointTemplateItem) => {
  try {
    await ElMessageBox.confirm(`确定删除模板“${template.name}”吗？`, '删除确认', { type: 'warning' })
    await deleteDataPointTemplate(template.id)
    if (templates.value.length === 1 && page.value > 1) page.value--
    await fetchTemplates()
    ElMessage.success('模板已删除')
  } catch {
    // 用户取消时不提示错误。
  }
}

const dataTypeLabel = (value: number) => dataTypeOptions.value.find(item => item.value === value)?.label || String(value)

const functionCodeLabel = (value?: number | null) =>
  functionCodeOptions.find(option => option.value === Number(value))?.label || '-'

const registerLengthLabel = (point: DataPointTemplateDetail['points'][number]) => {
  const option = registerLengthOptions.find(item => item.value === Number(point.registerLength))
  return option?.label || `${point.registerLength || '-'} 个`
}

const syncRegisterLength = (point: DataPointTemplateDetail['points'][number]) => {
  if ([4, 5, 6].includes(Number(point.dataType))) point.registerLength = 2
  else if ([7, 8, 9].includes(Number(point.dataType))) point.registerLength = 4
  else point.registerLength = 1
}

onMounted(async () => {
  const typeRes = await getDataValueTypes()
  dataTypeOptions.value = (typeRes as any).data || []
  await fetchTemplates()
})
</script>

<style scoped lang="scss">
.templates-view {
  min-height: 100%;
  display: flex;
  flex-direction: column;
}
.page-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 18px;
  padding: 2px 2px 0;
}
.title-block { min-width: 0; }
.page-title {
  color: var(--text-primary);
  font-size: 24px;
  font-weight: 800;
  letter-spacing: .02em;
  line-height: 1.25;
}
.page-desc { color: var(--text-muted); font-size: 12px; margin-top: 7px; }
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  min-height: 58px;
  margin-bottom: 16px;
  padding: 10px 14px;
}
.template-search-group {
  display: flex;
  align-items: center;
  gap: 10px;
}
.template-search-input { width: min(320px, 42vw); }
.template-search-input :deep(.el-input__wrapper) {
  height: 36px;
  border: 1px solid var(--border-muted) !important;
  border-radius: 8px;
  background: var(--bg-base) !important;
  box-shadow: none !important;
}
.template-search-input :deep(.el-input__wrapper:hover),
.template-search-input :deep(.el-input__wrapper.is-focus) {
  border-color: var(--border-accent) !important;
  background: var(--bg-base) !important;
}
.template-search-input :deep(.el-input__prefix),
.template-search-input :deep(.el-input__clear) { color: var(--text-muted); }
.template-count {
  margin-left: auto;
  color: var(--text-muted);
  font-size: 12px;
}
.template-search-btn {
  height: 36px;
  min-width: 64px;
  padding: 0 16px;
  border-radius: 9px;
  border-color: rgba(66, 153, 225, .34) !important;
  background: var(--bg-active) !important;
  color: var(--text-primary) !important;
  box-shadow: none !important;
  font-weight: 700;
}
.template-search-btn:hover {
  border-color: rgba(90, 165, 230, .52) !important;
  background: var(--bg-hover) !important;
  color: var(--text-primary) !important;
  box-shadow: none !important;
}
.template-refresh-btn {
  height: 36px;
  padding: 0 13px;
  border: 1px solid rgba(120, 155, 190, .22) !important;
  border-radius: 10px;
  background: var(--action-bg) !important;
  color: var(--action-text) !important;
  box-shadow: none !important;
}
.template-refresh-btn:hover {
  border-color: var(--action-border-hover) !important;
  background: var(--action-bg-hover) !important;
  color: var(--action-text-hover) !important;
}
.templates-view > .table-wrap {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  background: var(--bg-card);
}
.templates-view > .table-wrap > :deep(.app-table) {
  flex: 1;
  min-height: 0;
}
.template-detail-form { padding-top: 4px; }
.mono { font-family: var(--font-mono); }

.dialog-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding-right: 18px;
  &__title { color: var(--text-primary); font-size: 18px; font-weight: 700; letter-spacing: .02em; }
  &__sub { margin-top: 5px; color: var(--text-muted); font-size: 12px; }
}

.point-table-wrap {
  overflow: hidden;
  border: 1px solid rgba(120, 155, 190, .12);
  border-radius: 12px;
  background: var(--bg-base);
}

.point-table {
  --el-table-border-color: rgba(120, 155, 190, .12);
  --el-table-header-bg-color: rgba(56, 220, 196, .06);
  --el-table-row-hover-bg-color: rgba(56, 220, 196, .06);
  --el-table-tr-bg-color: transparent;
  --el-table-bg-color: transparent;
  :deep(.el-table__header-wrapper th) {
    height: 42px;
    background: rgba(56, 220, 196, .06);
    color: var(--cyan);
    font-size: 12px;
    font-weight: 700;
  }
  :deep(.el-table__body-wrapper td) {
    height: 46px;
    color: var(--text-secondary);
    font-size: 12px;
  }
  :deep(.el-table__body tr:hover > td) { background: rgba(56, 220, 196, .06) !important; }
  :deep(.el-input__wrapper), :deep(.el-select__wrapper) { box-shadow: 0 0 0 1px rgba(103, 145, 190, .32) inset; }
}

.template-detail-dialog {
  width: min(1280px, calc(100vw - 48px));
  :deep(.el-dialog) { overflow: hidden; border: 1px solid rgba(56, 220, 196, .18); border-radius: 16px; background: var(--bg-panel); box-shadow: 0 24px 70px rgba(0, 0, 0, .42); }
  :deep(.el-dialog__header) { margin-right: 0; padding: 22px 24px 16px; border-bottom: 1px solid rgba(120, 155, 190, .12); }
  :deep(.el-dialog__body) { padding: 18px 24px 20px; }
  :deep(.el-dialog__footer) { padding: 14px 24px 20px; border-top: 1px solid rgba(120, 155, 190, .12); }
}

.code-label { color: var(--cyan); font-family: var(--font-mono); font-size: 11px; white-space: nowrap; }
.register-label { color: var(--text-secondary); font-family: var(--font-mono); font-size: 11px; white-space: nowrap; }

@media (max-width: 900px) {
  .template-detail-dialog { width: calc(100vw - 28px) !important; }
  .point-table-wrap { overflow-x: auto; }
}
</style>
