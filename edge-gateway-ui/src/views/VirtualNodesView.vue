<template>
  <div class="virtual-nodes-view page-enter">
    <PageHeader title="虚拟节点" desc="管理虚拟数据点，支持表达式计算与依赖解析，虚拟节点依附于普通设备">
      <el-button type="primary" size="small" :icon="Plus" @click="openPointCreateDialog">新建虚拟数据点</el-button>
    </PageHeader>

    <el-row :gutter="20">
      <!-- 设备列表 -->
      <el-col :span="8">
        <div class="panel table-wrap">
          <div class="panel-header">
            <span class="panel-title">设备</span>
          </div>
          <el-table :data="devices" v-loading="devicesLoading" stripe highlight-current-row @current-change="handleDeviceSelect">
            <el-table-column prop="id" label="ID" width="60" />
            <el-table-column prop="name" label="名称" min-width="120" />
            <el-table-column prop="code" label="编码" min-width="120" />
            <el-table-column prop="isEnabled" label="状态" width="70">
              <template #default="{ row }">
                <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">
                  {{ row.isEnabled ? '启用' : '禁用' }}
                </el-tag>
              </template>
            </el-table-column>
          </el-table>
        </div>
      </el-col>

      <!-- 虚拟数据点列表 -->
      <el-col :span="16">
        <div class="panel table-wrap">
          <div class="panel-header">
            <span class="panel-title">虚拟数据点 {{ selectedDevice ? `- ${selectedDevice.name}` : '' }}</span>
            <el-button
              type="primary"
              size="small"
              :disabled="!selectedDevice"
              @click="openPointCreateDialog"
            >
              <el-icon><Plus /></el-icon>
              新建
            </el-button>
          </div>
          <el-alert
            v-if="!selectedDevice"
            title="请先选择左侧的设备"
            type="info"
            :closable="false"
            show-icon
          />
          <el-table
            v-else
            :data="virtualDataPoints"
            v-loading="pointsLoading"
            stripe
          >
            <el-table-column prop="id" label="ID" width="60" />
            <el-table-column prop="name" label="名称" min-width="120" />
            <el-table-column prop="tag" label="标签" min-width="180" />
            <el-table-column prop="expression" label="表达式" min-width="200" show-overflow-tooltip />
            <el-table-column prop="isEnabled" label="状态" width="70">
              <template #default="{ row }">
                <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">
                  {{ row.isEnabled ? '启用' : '禁用' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="200" fixed="right">
              <template #default="{ row }">
                <el-button size="small" text @click="openPointEditDialog(row)">编辑</el-button>
                <el-button size="small" text @click="calculatePoint(row.id)">计算</el-button>
                <el-button size="small" text type="danger" @click="deletePoint(row.id)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </div>
      </el-col>
    </el-row>

    <!-- 计算结果 -->
    <div class="panel table-wrap results-panel">
      <div class="panel-header">
        <span class="panel-title">计算结果</span>
        <el-button type="success" size="small" :disabled="!selectedDevice" @click="calculateDevice">
          <el-icon><Refresh /></el-icon>
          计算设备虚拟节点
        </el-button>
        <el-button type="success" size="small" @click="calculateAll">
          <el-icon><Refresh /></el-icon>
          计算全部
        </el-button>
      </div>
      <el-table :data="calculationResults" stripe>
        <el-table-column prop="tag" label="数据点" min-width="200" />
        <el-table-column prop="value" label="计算结果" min-width="150" />
        <el-table-column prop="quality" label="质量" width="100">
          <template #default="{ row }">
            <el-tag :type="row.quality === 0 ? 'success' : 'danger'" size="small">
              {{ row.quality === 0 ? 'Good' : 'Bad' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="timestamp" label="计算时间" width="180" />
        <el-table-column label="依赖值" min-width="200">
          <template #default="{ row }">
            <el-popover placement="top" trigger="hover">
              <template #default>
                <div v-for="(value, key) in row.dependencyValues" :key="key">
                  <strong>{{ key }}:</strong> {{ value }}
                </div>
              </template>
              <template #reference>
                <el-tag size="small">查看 {{ Object.keys(row.dependencyValues || {}).length }} 个依赖</el-tag>
              </template>
            </el-popover>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 虚拟数据点对话框 -->
    <el-dialog
      v-model="pointDialogVisible"
      :title="isPointEdit ? '编辑虚拟数据点' : '新建虚拟数据点'"
      width="700px"
      class="app-dialog"
      destroy-on-close
      align-center
      @close="resetPointForm"
    >
      <el-form ref="pointFormRef" :model="pointForm" :rules="pointFormRules" label-width="120px">
        <el-form-item label="所属设备" prop="deviceId">
          <el-select v-model="pointForm.deviceId" placeholder="请选择设备" style="width: 100%" :disabled="!!selectedDevice">
            <el-option
              v-for="device in devices"
              :key="device.id"
              :label="device.name"
              :value="device.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="数据点名称" prop="name">
          <el-input v-model="pointForm.name" placeholder="请输入数据点名称" />
        </el-form-item>
        <el-form-item label="标签" prop="tag">
          <el-input v-model="pointForm.tag" placeholder="如：DEV001.Virtual.TempAvg" />
        </el-form-item>
        <el-form-item label="计算表达式" prop="expression">
          <el-input
            v-model="pointForm.expression"
            type="textarea"
            :rows="3"
            placeholder="如：(Point1 + Point2) / 2 或 Avg(Temp1, Temp2, Temp3)"
          />
          <div class="form-tip">
            <el-link type="primary" @click="showExpressionHelp">查看表达式语法</el-link>
            <el-link type="primary" style="margin-left: 10px" @click="parseDependencies">解析依赖</el-link>
          </div>
        </el-form-item>
        <el-form-item label="依赖项" v-if="dependencies.length > 0">
          <el-tag v-for="dep in dependencies" :key="dep" style="margin-right: 5px">
            {{ dep }}
          </el-tag>
        </el-form-item>
        <el-form-item label="计算类型" prop="calculationType">
          <el-select v-model="pointForm.calculationType" placeholder="请选择计算类型" style="width: 100%">
            <el-option label="自定义表达式" :value="0" />
            <el-option label="求和" :value="1" />
            <el-option label="平均值" :value="2" />
            <el-option label="最大值" :value="3" />
            <el-option label="最小值" :value="4" />
            <el-option label="计数" :value="5" />
            <el-option label="加权平均" :value="7" />
          </el-select>
        </el-form-item>
        <el-form-item label="数据类型" prop="dataType">
          <el-select v-model="pointForm.dataType" placeholder="请选择数据类型" style="width: 100%">
            <el-option label="布尔" :value="0" />
            <el-option label="Int16" :value="1" />
            <el-option label="Int32" :value="2" />
            <el-option label="Float" :value="3" />
            <el-option label="Double" :value="4" />
          </el-select>
        </el-form-item>
        <el-form-item label="单位" prop="unit">
          <el-input v-model="pointForm.unit" placeholder="如：℃, MPa" />
        </el-form-item>
        <el-form-item label="启用" prop="isEnabled">
          <el-switch v-model="pointForm.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="pointDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submitPointForm" :loading="pointSubmitting">确定</el-button>
      </template>
    </el-dialog>

    <!-- 表达式帮助对话框 -->
    <el-dialog v-model="expressionHelpVisible" title="表达式语法帮助" width="700px" class="app-dialog" align-center>
      <el-tabs>
        <el-tab-pane label="基础语法">
          <h4>四则运算</h4>
          <pre>Point1 + Point2
Point1 * 2 + Point2 / 3
(Point1 + Point2) / 2</pre>

          <h4>数学函数</h4>
          <pre>Math.Abs(Value)        // 绝对值
Math.Sqrt(Power)       // 平方根
Math.Sin(Angle)        // 正弦
Math.Cos(Angle)        // 余弦
Math.Pow(Base, Exp)    // 幂运算
Math.Round(Value, 2)   // 四舍五入</pre>
        </el-tab-pane>
        <el-tab-pane label="聚合函数">
          <pre>Max(Point1, Point2, Point3)  // 最大值
Min(Point1, Point2)        // 最小值
Avg(Temp1, Temp2, Temp3)   // 平均值
Sum(Value1, Value2)        // 求和</pre>
        </el-tab-pane>
        <el-tab-pane label="条件表达式">
          <pre>Temperature > 100 ? 1 : 0
Pressure > 50 && Temperature < 80 ? "Normal" : "Alert"</pre>
        </el-tab-pane>
      </el-tabs>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh } from '@element-plus/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import type { VirtualDataPoint, CreateVirtualDataPointRequest, UpdateVirtualDataPointRequest } from '@/types/virtualNode'
import type { Device } from '@/types/device'
import {
  getVirtualDataPoints,
  getVirtualDataPointsByDevice,
  createVirtualDataPoint,
  updateVirtualDataPoint,
  deleteVirtualDataPoint,
  calculateVirtualDataPoint,
  calculateDeviceVirtualDataPoints,
  calculateAllVirtualDataPoints,
  parseDependencies as parseDependenciesApi
} from '@/api/virtualNode'
import { getDevices } from '@/api/device'

const devicesLoading = ref(false)
const pointsLoading = ref(false)
const pointSubmitting = ref(false)
const devices = ref<Device[]>([])
const virtualDataPoints = ref<VirtualDataPoint[]>([])
const selectedDevice = ref<Device | null>(null)
const calculationResults = ref<any[]>([])
const dependencies = ref<string[]>([])

const pointDialogVisible = ref(false)
const expressionHelpVisible = ref(false)
const isPointEdit = ref(false)

const pointFormRef = ref()

const pointForm = reactive<CreateVirtualDataPointRequest & { id?: number }>({
  deviceId: 0,
  name: '',
  tag: '',
  expression: '',
  calculationType: 0,
  dataType: 3,
  unit: '',
  isEnabled: true
})

const pointFormRules = {
  deviceId: [{ required: true, message: '请选择设备', trigger: 'change' }],
  name: [{ required: true, message: '请输入数据点名称', trigger: 'blur' }],
  tag: [{ required: true, message: '请输入标签', trigger: 'blur' }],
  expression: [{ required: true, message: '请输入计算表达式', trigger: 'blur' }]
}

const getDataTypeName = (type: number) => {
  const names: Record<number, string> = {
    0: 'Boolean',
    1: 'Int16',
    2: 'Int32',
    3: 'Float',
    4: 'Double'
  }
  return names[type] || 'Unknown'
}

const loadDevices = async () => {
  devicesLoading.value = true
  try {
    const res = await getDevices()
    devices.value = res.data
  } catch (error) {
    ElMessage.error('加载设备失败')
  } finally {
    devicesLoading.value = false
  }
}

const loadVirtualDataPoints = async (deviceId: number) => {
  pointsLoading.value = true
  try {
    const res = await getVirtualDataPointsByDevice(deviceId)
    virtualDataPoints.value = res.data
  } catch (error) {
    ElMessage.error('加载虚拟数据点失败')
  } finally {
    pointsLoading.value = false
  }
}

const handleDeviceSelect = (row: Device | null) => {
  selectedDevice.value = row
  if (row) {
    loadVirtualDataPoints(row.id)
  } else {
    virtualDataPoints.value = []
  }
}

const openPointCreateDialog = () => {
  isPointEdit.value = false
  pointForm.deviceId = selectedDevice.value?.id || 0
  pointForm.name = ''
  pointForm.tag = ''
  pointForm.expression = ''
  pointForm.calculationType = 0
  pointForm.dataType = 3
  pointForm.unit = ''
  pointForm.isEnabled = true
  dependencies.value = []
  pointDialogVisible.value = true
}

const openPointEditDialog = (point: VirtualDataPoint) => {
  isPointEdit.value = true
  pointForm.id = point.id
  pointForm.deviceId = point.deviceId
  pointForm.name = point.name
  pointForm.tag = point.tag
  pointForm.expression = point.expression
  pointForm.calculationType = point.calculationType
  pointForm.dataType = point.dataType
  pointForm.unit = point.unit || ''
  pointForm.isEnabled = point.isEnabled
  dependencies.value = point.dependencyTags || []
  pointDialogVisible.value = true
}

const resetPointForm = () => {
  pointFormRef.value?.resetFields()
  pointForm.deviceId = selectedDevice.value?.id || 0
  pointForm.name = ''
  pointForm.tag = ''
  pointForm.expression = ''
  pointForm.calculationType = 0
  pointForm.dataType = 3
  pointForm.unit = ''
  pointForm.isEnabled = true
  dependencies.value = []
}

const submitPointForm = async () => {
  if (!pointFormRef.value) return

  await pointFormRef.value.validate(async (valid: boolean) => {
    if (!valid) return

    pointSubmitting.value = true
    try {
      if (isPointEdit.value && pointForm.id) {
        await updateVirtualDataPoint(pointForm.id, {
          id: pointForm.id,
          deviceId: pointForm.deviceId,
          name: pointForm.name || '',
          tag: pointForm.tag || '',
          description: '',
          expression: pointForm.expression || '',
          calculationType: pointForm.calculationType,
          dataType: pointForm.dataType,
          unit: pointForm.unit || null,
          isEnabled: pointForm.isEnabled
        })
        ElMessage.success('更新成功')
      } else {
        await createVirtualDataPoint({
          deviceId: pointForm.deviceId,
          name: pointForm.name || '',
          tag: pointForm.tag || '',
          description: '',
          expression: pointForm.expression || '',
          calculationType: pointForm.calculationType,
          dataType: pointForm.dataType,
          unit: pointForm.unit || null,
          isEnabled: pointForm.isEnabled
        })
        ElMessage.success('创建成功')
      }
      pointDialogVisible.value = false
      if (selectedDevice.value) {
        loadVirtualDataPoints(selectedDevice.value.id)
      }
    } catch (error: any) {
      ElMessage.error(error.message || '操作失败')
    } finally {
      pointSubmitting.value = false
    }
  })
}

const deletePoint = async (id: number) => {
  try {
    await ElMessageBox.confirm('确认删除该虚拟数据点？', '提示', {
      type: 'warning'
    })
    await deleteVirtualDataPoint(id)
    ElMessage.success('删除成功')
    if (selectedDevice.value) {
      loadVirtualDataPoints(selectedDevice.value.id)
    }
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.message || '删除失败')
    }
  }
}

const calculatePoint = async (id: number) => {
  try {
    const res = await calculateVirtualDataPoint(id)
    ElMessage.success('计算成功')
    calculationResults.value = [res.data]
  } catch (error: any) {
    ElMessage.error(error.message || '计算失败')
  }
}

const calculateDevice = async () => {
  if (!selectedDevice.value) return
  try {
    const res = await calculateDeviceVirtualDataPoints(selectedDevice.value.id)
    ElMessage.success(`计算完成：${res.data.filter((r: any) => r.success).length}/${res.data.length}`)
    calculationResults.value = res.data
  } catch (error: any) {
    ElMessage.error(error.message || '计算失败')
  }
}

const calculateAll = async () => {
  try {
    const res = await calculateAllVirtualDataPoints()
    ElMessage.success(`计算完成：${res.data.filter((r: any) => r.success).length}/${res.data.length}`)
    calculationResults.value = res.data
  } catch (error: any) {
    ElMessage.error(error.message || '计算失败')
  }
}

const parseDependencies = async () => {
  if (!pointForm.expression) {
    ElMessage.warning('请先输入表达式')
    return
  }
  try {
    const res = await parseDependenciesApi(pointForm.expression)
    dependencies.value = res.data
    ElMessage.success(`解析到 ${dependencies.value.length} 个依赖项`)
  } catch (error: any) {
    ElMessage.error(error.message || '解析失败')
  }
}

const showExpressionHelp = () => {
  expressionHelpVisible.value = true
}

onMounted(() => {
  loadDevices()
})
</script>

<style scoped>
.virtual-nodes-view {
  /* 与设备管理、数据点等页面统一：由 MainLayout 提供内边距 */
}

.panel {
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  overflow: hidden;
}

.table-wrap {
  min-height: 280px;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 20px;
  border-bottom: 1px solid var(--border-subtle);
}

.panel-title {
  font-size: 14px;
  font-weight: 700;
  color: var(--text-primary);
  letter-spacing: 0.02em;
}

.results-panel {
  margin-top: 20px;
}

/* 表格与全局表格样式一致 */
.virtual-nodes-view :deep(.el-table) {
  --el-table-bg-color: transparent;
  --el-table-tr-bg-color: transparent;
}

.form-tip {
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 6px;
}

.form-tip .el-link {
  color: var(--cyan);
}

.form-tip .el-link + .el-link {
  margin-left: 10px;
}

/* 帮助弹窗内代码块 */
.virtual-nodes-view pre {
  background: var(--bg-base);
  color: var(--text-secondary);
  padding: 12px 14px;
  border-radius: var(--radius);
  font-size: 12px;
  overflow-x: auto;
  margin: 6px 0;
  border: 1px solid var(--border-subtle);
  font-family: var(--font-mono);
}

.virtual-nodes-view h4 {
  margin: 12px 0 6px;
  color: var(--text-primary);
  font-size: 13px;
  font-weight: 600;
}

.virtual-nodes-view h4:first-child {
  margin-top: 0;
}
</style>
