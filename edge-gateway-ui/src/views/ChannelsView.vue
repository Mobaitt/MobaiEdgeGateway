<template>
  <div class="channels-view page-enter">
    <PageHeader title="发送通道" desc="配置数据发送目标及数据点绑定关系">
      <el-button type="primary" :icon="Plus" @click="openCreate">新增通道</el-button>
    </PageHeader>

    <!-- 通道卡片列表 -->
    <div v-loading="loading" class="channels-grid">
      <div
        v-for="ch in channels" :key="ch.id"
        class="channel-card"
        :class="{ disabled: !ch.isEnabled }"
      >
        <!-- 卡片顶部 -->
        <div class="card-head">
          <div class="protocol-badge" :style="{ color: getProtoColor(ch.protocolValue), borderColor: getProtoColor(ch.protocolValue) + '55', background: getProtoColor(ch.protocolValue) + '15' }">
            {{ ch.protocol }}
          </div>
          <div class="card-actions">
            <el-switch
              :model-value="ch.isEnabled"
              size="small" active-color="#38dcc4"
              @change="handleToggle(ch)"
            />
          </div>
        </div>

        <!-- 通道信息 -->
        <div class="card-body">
          <div class="ch-name">{{ ch.name }}</div>
          <div class="ch-code mono">{{ ch.code }}</div>
          <div class="ch-endpoint mono">
            <el-icon size="12"><Link /></el-icon>
            {{ ch.endpoint }}
          </div>
          <div v-if="ch.description" class="ch-desc">{{ ch.description }}</div>
        </div>

        <!-- 卡片底部 -->
        <div class="card-foot">
          <div class="map-count">
            <el-icon size="14"><Connection /></el-icon>
            <span class="mono">{{ ch.mappedDataPointCount }}</span>
            <span style="color:var(--text-muted)">个数据点</span>
          </div>
          <div class="foot-actions">
            <el-button size="small" text @click="goMappings(ch)">
              绑定数据点
            </el-button>
            <el-button size="small" text @click="openEdit(ch)">
              编辑
            </el-button>
            <el-button size="small" text type="danger" @click="handleDelete(ch)">
              删除
            </el-button>
          </div>
        </div>
      </div>

      <AddCard class="channel-card" text="新增通道" @click="openCreate" />
    </div>

    <!-- 新增/编辑通道弹窗：紧凑排版减少滚动 -->
    <el-dialog v-model="dialogVisible" :title="editingChannel ? '编辑发送通道' : '新增发送通道'" width="840px" destroy-on-close class="channel-dialog app-dialog channel-dialog-compact" align-center>
      <el-form ref="formRef" :model="form" :rules="rules" label-width="96px" label-position="left" class="channel-form">
        <FormSection title="基本信息" icon="Document" class="compact">
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item label="通道名称" prop="name">
                <el-input v-model="form.name" placeholder="如：云端 MQTT" @blur="generateCodeIfEmpty" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="通道编码" prop="code">
                <div class="channel-code-with-btn">
                  <el-input v-model="form.code" placeholder="全局唯一编码，如 CH_MQTT_01" class="channel-code-input" />
                  <el-button size="small" text class="btn-auto-generate" @click="generateCode">
                    <el-icon><MagicStick /></el-icon>
                    <span>自动生成</span>
                  </el-button>
                </div>
              </el-form-item>
            </el-col>
          </el-row>
          <el-form-item label="描述">
            <el-input v-model="form.description" type="textarea" :rows="1" placeholder="可选描述信息" autosize />
          </el-form-item>
        </FormSection>

        <FormSection title="协议配置" icon="Setting" class="compact">
          <el-row :gutter="16">
            <el-col :span="form.protocol === 2 ? 12 : 24">
              <el-form-item label="发送协议" prop="protocol">
                <el-select v-model="form.protocol" placeholder="选择协议" style="width:100%">
                  <el-option v-for="o in SendProtocolOptions" :key="o.value" :label="o.label" :value="o.value">
                    <div class="protocol-option">
                      <span>{{ o.label }}</span>
                      <span class="protocol-desc">{{ o.desc }}</span>
                    </div>
                  </el-option>
                </el-select>
              </el-form-item>
            </el-col>
            <el-col v-if="form.protocol === 2" :span="12">
              <el-form-item label="运行模式">
                <el-radio-group v-model="form.httpMode" class="mode-radio-group">
                  <el-radio label="client"><el-icon><Upload /></el-icon> 客户端</el-radio>
                  <el-radio label="server"><el-icon><Download /></el-icon> 服务端</el-radio>
                </el-radio-group>
              </el-form-item>
            </el-col>
          </el-row>
          <div v-if="form.protocol === 5" class="protocol-hint protocol-hint-inline">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>WebSocket 服务端，数据推送给订阅客户端。</span>
            <code class="hint-code">ws://localhost:5000/ws?topic=device/data</code>
          </div>
          <div v-if="form.protocol === 2 && form.httpMode === 'client'" class="protocol-hint protocol-hint-inline">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>HTTP 客户端，主动 POST 到目标接口。</span>
          </div>
          <div v-if="form.protocol === 2 && form.httpMode === 'server'" class="protocol-hint protocol-hint-inline">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>HTTP 服务端，GET 获取数据。</span>
            <code class="hint-code">http://localhost:5000/api/http-data/xxx</code>
          </div>
          <div v-if="form.protocol === 1" class="protocol-hint protocol-hint-inline">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>发布到 MQTT 主题，支持 QoS。</span>
            <code class="hint-code">edge/device/data</code>
          </div>
          <div v-if="form.protocol === 4" class="protocol-hint protocol-hint-inline">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>追加写入本地 JSON（NDJSON）。</span>
          </div>
        </FormSection>

        <FormSection title="连接配置" icon="Connection" class="compact">
          <el-form-item label="端点地址" prop="endpoint">
            <el-input v-model="form.endpoint" :placeholder="getEndpointPlaceholder(form.protocol)" />
          </el-form-item>

          <template v-if="form.protocol === 1">
            <el-row :gutter="16">
              <el-col :span="12">
                <el-form-item label="主题" prop="mqttTopic">
                  <el-input v-model="form.mqttTopic" placeholder="edge/device/data" />
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="客户端 ID">
                  <el-input v-model="form.mqttClientId" placeholder="device_001" />
                </el-form-item>
              </el-col>
            </el-row>
            <el-row :gutter="16">
              <el-col :span="12">
                <el-form-item label="用户名">
                  <el-input v-model="form.mqttUsername" placeholder="可选" />
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="密码">
                  <el-input v-model="form.mqttPassword" type="password" placeholder="可选" />
                </el-form-item>
              </el-col>
            </el-row>
            <el-form-item label="QoS">
              <el-radio-group v-model="form.mqttQos">
                <el-radio :label="0">0 - 至多一次</el-radio>
                <el-radio :label="1">1 - 至少一次</el-radio>
                <el-radio :label="2">2 - 只有一次</el-radio>
              </el-radio-group>
            </el-form-item>
          </template>

          <template v-if="form.protocol === 2 && form.httpMode === 'client'">
            <el-row :gutter="16">
              <el-col :span="8">
                <el-form-item label="HTTP 方法">
                  <el-select v-model="form.httpMethod" style="width:100%">
                    <el-option label="POST" value="POST" />
                    <el-option label="PUT" value="PUT" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="认证 Token">
                  <el-input v-model="form.httpToken" type="password" placeholder="Bearer xxx" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="超时(ms)">
                  <el-input-number v-model="form.httpTimeout" :min="1000" :max="60000" :step="1000" style="width:100%" controls-position="right" />
                </el-form-item>
              </el-col>
            </el-row>
          </template>
          <div v-if="form.protocol === 2 && form.httpMode === 'server'" class="protocol-hint protocol-hint-inline">
            <el-icon class="hint-icon"><InfoFilled /></el-icon>
            <span>服务端复用端口 5000。</span>
            <code class="hint-code">http://localhost:5000/api/http-data/xxx</code>
          </div>

          <template v-if="form.protocol === 5">
            <el-row :gutter="16">
              <el-col :span="12">
                <el-form-item label="订阅主题">
                  <el-input v-model="form.wsSubscribeTopic" placeholder="device/data" />
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="心跳间隔(ms)">
                  <el-input-number v-model="form.wsHeartbeatInterval" :min="5000" :max="120000" :step="5000" style="width:100%" controls-position="right" />
                </el-form-item>
              </el-col>
            </el-row>
          </template>

          <template v-if="form.protocol === 4">
            <el-row :gutter="16">
              <el-col :span="12">
                <el-form-item label="文件格式">
                  <el-select v-model="form.fileFormat" style="width:100%">
                    <el-option label="JSON" value="json" />
                    <el-option label="CSV" value="csv" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="保存路径">
                  <el-input v-model="form.filePath" placeholder="./output/data.json" />
                </el-form-item>
              </el-col>
            </el-row>
          </template>

          <el-form-item label="是否启用" class="form-item-inline">
            <el-switch v-model="form.isEnabled" active-color="#38dcc4" />
            <span class="form-hint">停用后停止发送</span>
          </el-form-item>
        </FormSection>

      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitForm">
          {{ editingChannel ? '保存修改' : '创建通道' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Link, Connection, InfoFilled, Upload, Download, MagicStick } from '@element-plus/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import FormSection from '@/components/FormSection.vue'
import AddCard from '@/components/AddCard.vue'
import { useConfirmDelete } from '@/composables/useConfirmDelete'
import { getChannels, createChannel, updateChannel, deleteChannel, toggleChannel } from '@/api/channel'
import { generateCodeWithTimestamp } from '@/utils/codeGenerate'
import { getSendProtocols } from '@/api/enums'
import { formatDateTime } from '@/api/constants'
import type { ChannelItem } from '@/types'

type ChannelForm = {
  name: string
  code: string
  description: string
  protocol: number | null
  endpoint: string
  mqttTopic: string
  mqttClientId: string
  mqttUsername: string
  mqttPassword: string
  mqttQos: number
  httpMethod: string
  httpToken: string
  httpTimeout: number
  httpMode: string
  wsSubscribeTopic: string
  wsHeartbeatInterval: number
  fileFormat: string
  filePath: string
  isEnabled: boolean
}

const router = useRouter()
const { confirm: confirmDeleteFn } = useConfirmDelete()
const loading = ref(false)
const channels = ref<ChannelItem[]>([])
const SendProtocolOptions = ref<any[]>([])

// 加载发送协议选项
const loadSendProtocols = async () => {
  try {
    const res = await getSendProtocols()
    SendProtocolOptions.value = (res as any).data || []
  } catch (error) {
    console.error('加载发送协议失败:', error)
  }
}

const dialogVisible = ref(false)
const submitting = ref(false)
const editingChannel = ref<ChannelItem | null>(null)
const formRef = ref<any>()
const form = ref<ChannelForm>({
  name: '',
  code: '',
  description: '',
  protocol: null,
  endpoint: '',
  mqttTopic: '',
  mqttClientId: '',
  mqttUsername: '',
  mqttPassword: '',
  mqttQos: 0,
  httpMethod: 'POST',
  httpToken: '',
  httpTimeout: 5000,
  httpMode: 'client',
  wsSubscribeTopic: '',
  wsHeartbeatInterval: 30000,
  fileFormat: 'json',
  filePath: './output/data.json',
  isEnabled: true
})

const rules = {
  name: [{ required: true, message: '请输入通道名称' }],
  code: [{ required: true, message: '请输入通道编码' }],
  protocol: [{ required: true, message: '请选择发送协议' }],
  endpoint: [{ required: true, message: '请输入端点地址' }]
}

/** 根据通道名称生成通道编码（名称转编码 + 时间戳） */
const generateCode = () => {
  if (!form.value.name?.trim()) {
    ElMessage.warning('请先输入通道名称')
    return
  }
  form.value.code = generateCodeWithTimestamp(form.value.name, 'CH')
  ElMessage.success('通道编码已生成')
}

const generateCodeIfEmpty = () => {
  if (!form.value.code && form.value.name?.trim()) {
    form.value.code = generateCodeWithTimestamp(form.value.name, 'CH')
  }
}

const getProtoColor = (value: number) => {
  const protocol = SendProtocolOptions.value.find((item) => item.value === value)
  return protocol?.color ?? '#8fa5c5'
}

const getEndpointPlaceholder = (protocol: number | null) => {
  if (protocol === 1) return 'mqtt://host:1883'
  if (protocol === 2) return 'https://api.example.com/data/upload'
  if (protocol === 5) return 'ws://localhost:8080/ws 或 wss://api.example.com/ws'
  if (protocol === 4) return './output/data.json'
  return '请输入端点地址'
}

const fetchChannels = async () => {
  loading.value = true
  try {
    const res = await getChannels()
    channels.value = ((res as { data?: ChannelItem[] })?.data ?? []) as ChannelItem[]
  } finally {
    loading.value = false
  }
}

const openCreate = () => {
  editingChannel.value = null
  form.value = {
    name: '',
    code: '',
    description: '',
    protocol: null,
    endpoint: '',
    mqttTopic: '',
    mqttClientId: '',
    mqttUsername: '',
    mqttPassword: '',
    mqttQos: 0,
    httpMethod: 'POST',
    httpToken: '',
    httpTimeout: 5000,
    httpMode: 'client',
    wsSubscribeTopic: '',
    wsHeartbeatInterval: 30000,
    fileFormat: 'json',
    filePath: './output/data.json',
    isEnabled: true
  }
  dialogVisible.value = true
}

const openEdit = (channel: ChannelItem) => {
  editingChannel.value = channel
  form.value = {
    name: channel.name,
    code: channel.code,
    description: channel.description || '',
    protocol: channel.protocolValue,
    endpoint: channel.endpoint,
    mqttTopic: channel.mqttTopic || '',
    mqttClientId: channel.mqttClientId || '',
    mqttUsername: channel.mqttUsername || '',
    mqttPassword: channel.mqttPassword || '',
    mqttQos: channel.mqttQos ?? 0,
    httpMethod: channel.httpMethod || 'POST',
    httpToken: channel.httpToken || '',
    httpTimeout: channel.httpTimeout ?? 5000,
    httpMode: channel.httpMode || 'client',
    wsSubscribeTopic: channel.wsSubscribeTopic || '',
    wsHeartbeatInterval: channel.wsHeartbeatInterval ?? 30000,
    fileFormat: channel.fileFormat || 'json',
    filePath: channel.filePath || './output/data.json',
    isEnabled: channel.isEnabled
  }
  dialogVisible.value = true
}

const submitForm = async () => {
  await formRef.value?.validate()
  submitting.value = true
  try {
    const submitData = { ...form.value }

    if (editingChannel.value) {
      // 编辑模式
      await updateChannel(editingChannel.value.id, submitData)
      ElMessage.success('通道已更新')
    } else {
      // 创建模式
      await createChannel(submitData)
      ElMessage.success('通道创建成功')
    }

    dialogVisible.value = false
    fetchChannels()
  } catch (error: any) {
    ElMessage.error(`操作失败：${error.message || '未知错误'}`)
  } finally {
    submitting.value = false
  }
}

const handleDelete = async (channel: ChannelItem) => {
  try {
    await confirmDeleteFn({
      title: '删除通道',
      message: `确定要删除发送通道 "${channel.name}" 吗？删除后无法恢复。`,
      onConfirm: async () => {
        await deleteChannel(channel.id)
        fetchChannels()
      },
      successMessage: '通道已删除'
    })
  } catch (e: unknown) {
    if (e && typeof (e as Error).message === 'string') {
      ElMessage.error(`删除失败：${(e as Error).message}`)
    }
  }
}

const goMappings = (channel: ChannelItem) => {
  router.push({ name: 'Mappings', params: { id: channel.id }, query: { channelName: channel.name } })
}

const handleToggle = async (channel: ChannelItem) => {
  const action = channel.isEnabled ? '停用' : '启用'
  
  try {
    await ElMessageBox.confirm(
      `确定要${action}发送通道 "${channel.name}" 吗？`,
      `${action}通道`,
      {
        type: 'warning',
        confirmButtonText: '确认',
        cancelButtonText: '取消'
      }
    )
    
    await toggleChannel(channel.id)
    channel.isEnabled = !channel.isEnabled
    ElMessage.success(`通道已${action}`)
  } catch (e: any) {
    if (e !== 'cancel') {
      ElMessage.error(`${action}失败：${e.message || '未知错误'}`)
    }
  }
}

onMounted(() => {
  fetchChannels()
  loadSendProtocols()
})
</script>

<style scoped>
/* 卡片网格 */
.channels-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 20px;
}
.channel-card {
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  transition: all 0.25s ease;
  position: relative;
  overflow: hidden;
}
.channel-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  background: linear-gradient(90deg, transparent, var(--cyan), transparent);
  opacity: 0;
  transition: opacity 0.25s;
}
.channel-card:hover {
  border-color: var(--cyan);
  box-shadow: 0 8px 32px rgba(0, 255, 255, 0.1);
  transform: translateY(-2px);
}
.channel-card:hover::before {
  opacity: 1;
}
.channel-card.disabled {
  opacity: 0.5;
  filter: grayscale(0.3);
}
.channel-card.disabled:hover {
  transform: none;
}

/* 卡片头部 */
.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
.protocol-badge {
  padding: 4px 12px;
  border-radius: 6px;
  border: 1px solid;
  font-size: 11px;
  font-family: var(--font-mono);
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  white-space: nowrap;
}
.card-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* 卡片主体 */
.card-body {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 12px 0;
}
.ch-name {
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
  letter-spacing: 0.02em;
}
.ch-code {
  font-size: 11px;
  color: var(--text-muted);
  font-family: var(--font-mono);
  background: var(--bg-base);
  padding: 3px 8px;
  border-radius: 4px;
  align-self: flex-start;
}
.ch-endpoint {
  font-size: 12px;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  gap: 6px;
  word-break: break-all;
  line-height: 1.5;
}
.ch-endpoint .el-icon {
  flex-shrink: 0;
}
.ch-desc {
  font-size: 12px;
  color: var(--text-muted);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

/* 卡片底部 */
.card-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-top: 1px solid var(--border-subtle);
  padding-top: 12px;
  margin-top: 4px;
}
.map-count {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--cyan);
  font-weight: 600;
}
.map-count .mono {
  font-size: 15px;
}
.foot-actions {
  display: flex;
  align-items: center;
  gap: 4px;
}
.foot-actions .el-button {
  font-size: 12px;
  padding: 4px 8px;
}

.channel-card.add-card { min-height: 180px; }

/* 协议选项样式 */
.protocol-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
.protocol-desc {
  font-size: 11px;
  color: var(--text-muted);
}

/* 模式单选组 */
.mode-radio-group {
  display: flex;
  gap: 12px;
}
.mode-radio-group .el-radio {
  margin-right: 0;
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 13px;
}

/* 协议提示 */
.protocol-hint {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 10px;
  padding: 8px 12px;
  background: var(--bg-card);
  border-radius: var(--radius-sm);
  border: 1px solid var(--border-subtle);
  font-size: 12px;
  color: var(--text-secondary);
  line-height: 1.5;
}
.protocol-hint .hint-icon {
  color: var(--cyan);
  font-size: 14px;
}
.protocol-hint .hint-code {
  background: var(--bg-base);
  padding: 3px 8px;
  border-radius: 4px;
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--cyan);
  align-self: flex-start;
}

/* 协议提示单行紧凑（发送通道弹窗内） */
.protocol-hint-inline {
  flex-direction: row !important;
  align-items: center;
  gap: 6px;
  margin-top: 6px !important;
  padding: 5px 10px !important;
  font-size: 11px !important;
  line-height: 1.4;
}
.protocol-hint-inline .hint-icon {
  font-size: 12px;
  flex-shrink: 0;
}
.protocol-hint-inline .hint-code {
  margin-left: 4px;
}

/* 表单提示 */
.form-hint {
  font-size: 11px;
  color: var(--text-muted);
  margin-top: 4px;
  line-height: 1.4;
}

/* 通道编码与自动生成按钮同行 */
.channel-code-with-btn {
  display: flex;
  align-items: center;
  gap: 8px;
}
.channel-code-with-btn .channel-code-input { flex: 1; min-width: 0; }
.channel-code-with-btn .btn-auto-generate {
  flex-shrink: 0;
  background: var(--bg-base) !important;
  border: 1px solid var(--border-subtle) !important;
  color: var(--text-secondary) !important;
}
.channel-code-with-btn .btn-auto-generate:hover {
  background: var(--bg-hover) !important;
  border-color: var(--border-muted) !important;
  color: var(--cyan) !important;
}

/* 弹窗样式修复 */
:deep(.el-form-item__label) { color: var(--text-secondary) !important; font-size: 13px; }
:deep(.el-input__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-select__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-textarea__inner) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; color: var(--text-primary) !important; }
:deep(.mono-input .el-input__inner) { font-family: var(--font-mono); }
</style>





