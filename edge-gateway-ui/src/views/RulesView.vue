<template>
  <div class="rules-view page-enter">
    <PageHeader title="规则管理" desc="配置数据校验、转换、限制与计算规则">
      <el-button type="primary" :icon="Plus" @click="openCreateDialog">新建规则</el-button>
    </PageHeader>

    <!-- 工具栏 -->
    <div class="toolbar">
      <el-select
        v-model="filterType"
        placeholder="规则类型"
        clearable
        style="width: 160px"
        @change="loadRules"
      >
        <el-option label="限制规则" :value="0" />
        <el-option label="转换规则" :value="1" />
        <el-option label="校验规则" :value="2" />
        <el-option label="计算规则" :value="3" />
      </el-select>
      <span class="total-hint mono">共 {{ rules.length }} 条规则</span>
    </div>

    <div class="table-wrap">
      <el-table :data="rules" v-loading="loading" stripe>
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column prop="name" label="规则名称" min-width="150" />
        <el-table-column prop="ruleType" label="类型" width="100">
          <template #default="{ row }">
            <el-tag :type="getRuleTypeTag(row.ruleType)">
              {{ getRuleTypeText(row.ruleType) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="dataPointName" label="数据点" min-width="120" />
        <el-table-column prop="deviceName" label="设备" min-width="120" />
        <el-table-column prop="priority" label="优先级" width="80" />
        <el-table-column prop="isEnabled" label="状态" width="80">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'info'">
              {{ row.isEnabled ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="description" label="描述" min-width="150" show-overflow-tooltip />
        <el-table-column label="操作" width="280" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text @click="openEditDialog(row)">
              <el-icon><Edit /></el-icon> 编辑
            </el-button>
            <el-button size="small" text @click="openTestDialog(row)">测试</el-button>
            <el-switch
              v-model="row.isEnabled"
              size="small"
              active-color="#38dcc4"
              @change="toggleRuleStatus(row)"
            />
            <el-button size="small" text type="danger" @click="deleteRule(row.id)">
              <el-icon><Delete /></el-icon> 删除
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 创建/编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '编辑规则' : '新建规则'"
      width="600px"
      class="app-dialog rule-dialog-compact"
      destroy-on-close
      align-center
      @close="resetForm"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
        <el-form-item label="规则名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入规则名称" />
        </el-form-item>
        <el-form-item label="规则类型" prop="ruleType">
          <el-select v-model="form.ruleType" placeholder="请选择规则类型" style="width: 100%">
            <el-option label="限制规则" :value="0" />
            <el-option label="转换规则" :value="1" />
            <el-option label="校验规则" :value="2" />
            <el-option label="计算规则" :value="3" />
          </el-select>
        </el-form-item>
        <el-form-item label="设备" prop="deviceId">
          <el-select v-model="form.deviceId" placeholder="可选，留空表示不绑定设备" clearable style="width: 100%">
            <el-option
              v-for="device in devices"
              :key="device.id"
              :label="device.name"
              :value="device.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="数据点" prop="dataPointId">
          <el-select v-model="form.dataPointId" placeholder="可选，留空表示全局规则" clearable style="width: 100%">
            <el-option
              v-for="point in filteredDataPoints"
              :key="point.id"
              :label="point.name"
              :value="point.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="优先级" prop="priority">
          <el-input-number v-model="form.priority" :min="0" :max="1000" />
          <div class="form-tip">数值越小优先级越高</div>
        </el-form-item>
        <el-form-item label="失败处理" prop="onFailure">
          <el-select v-model="form.onFailure" placeholder="请选择失败处理方式" style="width: 100%">
            <el-option label="放行" :value="0" />
            <el-option label="拒绝" :value="1" />
            <el-option label="使用默认值" :value="2" />
          </el-select>
        </el-form-item>
        <el-form-item label="默认值" prop="defaultValue" v-if="form.onFailure === 2">
          <el-input v-model="form.defaultValue" placeholder="失败时使用的默认值" />
        </el-form-item>
        <el-form-item label="规则配置" prop="ruleConfig">
          <el-input
            v-model="form.ruleConfig"
            type="textarea"
            :rows="6"
            placeholder="JSON 格式配置"
          />
          <div class="form-tip">
            <el-link type="primary" @click="showConfigHelp">查看配置帮助</el-link>
          </div>
        </el-form-item>
        <el-form-item label="描述" prop="description">
          <el-input v-model="form.description" type="textarea" :rows="2" />
        </el-form-item>
        <el-form-item label="启用" prop="isEnabled">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submitForm" :loading="submitting">确定</el-button>
      </template>
    </el-dialog>

    <!-- 测试对话框 -->
    <el-dialog v-model="testDialogVisible" title="测试规则" width="600px" class="app-dialog" align-center>
      <el-form :model="testForm" label-width="100px">
        <el-form-item label="测试值">
          <el-input v-model="testForm.value" placeholder="输入测试值" />
        </el-form-item>
        <el-form-item label="测试结果">
          <el-input
            v-model="testResult"
            type="textarea"
            :rows="8"
            readonly
            placeholder="点击测试按钮查看结果"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="testDialogVisible = false">关闭</el-button>
        <el-button type="primary" @click="runTest" :loading="testing">测试</el-button>
      </template>
    </el-dialog>

    <!-- 配置帮助对话框 -->
    <el-dialog v-model="helpDialogVisible" title="规则配置帮助" width="700px" class="app-dialog" align-center>
      <el-tabs>
        <el-tab-pane label="校验规则 (Validation)">
          <pre>{{ validationConfigExample }}</pre>
        </el-tab-pane>
        <el-tab-pane label="转换规则 (Transform)">
          <pre>{{ transformConfigExample }}</pre>
        </el-tab-pane>
        <el-tab-pane label="限制规则 (Limit)">
          <pre>{{ limitConfigExample }}</pre>
        </el-tab-pane>
      </el-tabs>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Edit, Delete } from '@element-plus/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import type { Rule, CreateRuleRequest, RuleType, FailureAction, UpdateRuleRequest } from '@/types/rule'
import type { Device, DataPoint } from '@/types/device'
import { getRules, createRule, updateRule, deleteRule as deleteRuleApi, toggleRule, testRule } from '@/api/rule'
import { getDevices, getAllDataPoints } from '@/api/device'

const loading = ref(false)
const submitting = ref(false)
const testing = ref(false)
const rules = ref<Rule[]>([])
const devices = ref<Device[]>([])
const dataPoints = ref<DataPoint[]>([])
const filterType = ref<RuleType | null>(null)
const dialogVisible = ref(false)
const testDialogVisible = ref(false)
const helpDialogVisible = ref(false)
const isEdit = ref(false)
const formRef = ref()
const currentRule = ref<Rule | null>(null)
const testResult = ref('')

const form = reactive<CreateRuleRequest>({
  name: '',
  ruleType: 2,
  deviceId: null,
  dataPointId: null,
  priority: 100,
  ruleConfig: '{}',
  onFailure: 0,
  defaultValue: null,
  isEnabled: true,
  description: ''
})

const testForm = reactive({
  value: ''
})

const validationConfigExample = `{
  "ValidationType": 1,      // 1=Range 阈值校验
  "MinValue": 0,            // 最小值
  "MaxValue": 100           // 最大值
}

// 变化率校验
{
  "ValidationType": 2,      // 2=RateOfChange
  "MaxRateOfChange": 10.5   // 每秒最大变化量
}

// 死区校验
{
  "ValidationType": 3,      // 3=DeadBand
  "DeadBand": 0.5           // 死区值
}`

const transformConfigExample = `{
  "TransformType": 1,       // 1=Linear 线性变换
  "Scale": 1.8,             // y = kx + b 中的 k
  "Offset": 32              // y = kx + b 中的 b
}

// 公式计算
{
  "TransformType": 2,       // 2=Formula
  "Formula": "x * 2 + 10"   // NCalc 表达式
}`

const limitConfigExample = `{
  "MinValue": 0,            // 最小值限制
  "MaxValue": 100           // 最大值限制
}`

const filteredDataPoints = computed(() => {
  if (form.deviceId) {
    return dataPoints.value.filter(p => p.deviceId === form.deviceId)
  }
  return dataPoints.value
})

const getRuleTypeTag = (type: RuleType) => {
  const types = ['info', 'warning', 'success', 'primary']
  return types[type] || 'info'
}

const getRuleTypeText = (type: RuleType) => {
  const texts = ['限制规则', '转换规则', '校验规则', '计算规则']
  return texts[type] || '未知'
}

const loadRules = async () => {
  loading.value = true
  try {
    let allRules: Rule[]
    if (filterType.value !== null) {
      const response = await getRules()
      allRules = response.data.filter(r => r.ruleType === filterType.value)
    } else {
      const response = await getRules()
      allRules = response.data
    }
    rules.value = allRules
  } catch (error) {
    ElMessage.error('加载规则失败')
  } finally {
    loading.value = false
  }
}

const loadDevicesAndPoints = async () => {
  try {
    const devicesRes = await getDevices()
    devices.value = devicesRes.data
    const pointsRes = await getAllDataPoints()
    dataPoints.value = pointsRes.data
  } catch (error) {
    console.error('加载设备和数据点失败', error)
  }
}

const openCreateDialog = () => {
  isEdit.value = false
  dialogVisible.value = true
}

const openEditDialog = (rule: Rule) => {
  isEdit.value = true
  currentRule.value = rule
  form.name = rule.name
  form.ruleType = rule.ruleType
  form.deviceId = rule.deviceId
  form.dataPointId = rule.dataPointId
  form.priority = rule.priority
  form.ruleConfig = rule.ruleConfig
  form.onFailure = rule.onFailure
  form.defaultValue = rule.defaultValue
  form.isEnabled = rule.isEnabled
  form.description = rule.description || ''
  dialogVisible.value = true
}

const openTestDialog = (rule: Rule) => {
  currentRule.value = rule
  testForm.value = ''
  testResult.value = ''
  testDialogVisible.value = true
}

const resetForm = () => {
  formRef.value?.resetFields()
  currentRule.value = null
  form.name = ''
  form.ruleType = 2
  form.deviceId = null
  form.dataPointId = null
  form.priority = 100
  form.ruleConfig = '{}'
  form.onFailure = 0
  form.defaultValue = null
  form.isEnabled = true
  form.description = ''
}

const submitForm = async () => {
  if (!formRef.value) return
  
  await formRef.value.validate(async (valid: boolean) => {
    if (!valid) return
    
    submitting.value = true
    try {
      if (isEdit.value && currentRule.value) {
        await updateRule(currentRule.value.id, {
          id: currentRule.value.id,
          name: form.name || '',
          ruleType: form.ruleType,
          deviceId: form.deviceId,
          dataPointId: form.dataPointId,
          priority: form.priority || 100,
          ruleConfig: form.ruleConfig || '{}',
          onFailure: form.onFailure || 0,
          defaultValue: form.defaultValue,
          isEnabled: form.isEnabled ?? true,
          description: form.description
        } as UpdateRuleRequest)
        ElMessage.success('更新成功')
      } else {
        await createRule(form)
        ElMessage.success('创建成功')
      }
      dialogVisible.value = false
      loadRules()
    } catch (error) {
      ElMessage.error('操作失败')
    } finally {
      submitting.value = false
    }
  })
}

const toggleRuleStatus = async (rule: Rule) => {
  try {
    await toggleRule(rule.id, rule.isEnabled)
    ElMessage.success(rule.isEnabled ? '已启用' : '已禁用')
  } catch (error) {
    rule.isEnabled = !rule.isEnabled
    ElMessage.error('操作失败')
  }
}

const deleteRule = async (id: number) => {
  try {
    await ElMessageBox.confirm('确定要删除此规则吗？', '提示', {
      type: 'warning'
    })
    await deleteRuleApi(id)
    ElMessage.success('删除成功')
    loadRules()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error('删除失败')
    }
  }
}

const runTest = async () => {
  if (!currentRule.value) return
  
  testing.value = true
  try {
    const result = await testRule(
      {
        ...form,
        ruleType: currentRule.value.ruleType,
        ruleConfig: currentRule.value.ruleConfig
      },
      {
        dataPointId: currentRule.value.dataPointId || 0,
        deviceId: currentRule.value.deviceId || 0,
        tag: 'test',
        value: parseFloat(testForm.value) || testForm.value
      }
    )
    testResult.value = JSON.stringify(result, null, 2)
  } catch (error) {
    testResult.value = '测试失败：' + error
  } finally {
    testing.value = false
  }
}

const showConfigHelp = () => {
  helpDialogVisible.value = true
}

onMounted(() => {
  loadRules()
  loadDevicesAndPoints()
})
</script>

<style scoped lang="scss">
.rules-view {
  .toolbar {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 16px;
    padding: 12px 16px;
    background: var(--bg-card);
    border: 1px solid var(--border-subtle);
    border-radius: var(--radius-lg);
  }
  .total-hint {
    font-size: 13px;
    color: var(--text-muted);
  }
  .table-wrap {
    background: var(--bg-card);
    border: 1px solid var(--border-subtle);
    border-radius: var(--radius-lg);
    overflow: hidden;
  }
  .form-tip {
    font-size: 12px;
    color: var(--text-muted);
    margin-top: 5px;
  }
  .form-tip .el-link {
    color: var(--cyan);
  }
  pre {
    background: var(--bg-base);
    color: var(--text-secondary);
    padding: 15px;
    border-radius: var(--radius);
    font-size: 12px;
    overflow-x: auto;
    border: 1px solid var(--border-subtle);
    font-family: var(--font-mono);
  }
}
</style>
