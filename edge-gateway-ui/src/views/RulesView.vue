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

    <!-- 创建/编辑规则弹窗 -->
    <RuleDialog
      v-model="dialogVisible"
      :editing-rule="editingRule"
      :devices="devices"
      :data-points="dataPoints"
      :submitting="submitting"
      @submit="handleSubmit"
      @close="handleDialogClose"
      @show-help="showConfigHelp"
    />

    <!-- 测试对话框 -->
    <RuleTestDialog
      v-model="testDialogVisible"
      :rule="currentRule"
      :testing="testing"
      @test="runTest"
      @close="handleTestDialogClose"
    />

    <!-- 配置帮助对话框 -->
    <RuleHelpDialog
      v-model="helpDialogVisible"
      @close="handleHelpDialogClose"
    />
  </div>
</template>

<script setup lang="ts">
import {onMounted, reactive, ref} from 'vue'
import {ElMessage, ElMessageBox} from 'element-plus'
import {Delete, Edit, Plus} from '@element-plus/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import RuleDialog from '@/dialogs/rule/RuleDialog.vue'
import RuleTestDialog from '@/dialogs/rule/RuleTestDialog.vue'
import RuleHelpDialog from '@/dialogs/rule/RuleHelpDialog.vue'
import type {CreateRuleRequest, Rule, RuleType, UpdateRuleRequest} from '@/types/rule'
import type {DataPoint, Device} from '@/types/device'
import {createRule, deleteRule as deleteRuleApi, getRules, testRule, toggleRule, updateRule} from '@/api/rule'
import {getAllDataPoints, getDevices} from '@/api/device'

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
const editingRule = ref<Rule | null>(null)
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
  editingRule.value = null
  dialogVisible.value = true
}

const openEditDialog = (rule: Rule) => {
  editingRule.value = rule
  dialogVisible.value = true
}

const openTestDialog = (rule: Rule) => {
  currentRule.value = rule
  testResult.value = ''
  testDialogVisible.value = true
}

const handleSubmit = async (data: CreateRuleRequest) => {
  submitting.value = true
  try {
    if (editingRule.value) {
      await updateRule(editingRule.value.id, {
        id: editingRule.value.id,
        ...data
      } as UpdateRuleRequest)
      ElMessage.success('更新成功')
    } else {
      await createRule(data)
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false
    loadRules()
  } catch (error) {
    ElMessage.error('操作失败')
  } finally {
    submitting.value = false
  }
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

const runTest = async (value: string) => {
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
        value: parseFloat(value) || value
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

const handleDialogClose = () => {
  editingRule.value = null
}

const handleTestDialogClose = () => {
  currentRule.value = null
  testResult.value = ''
}

const handleHelpDialogClose = () => {}

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
}
</style>
