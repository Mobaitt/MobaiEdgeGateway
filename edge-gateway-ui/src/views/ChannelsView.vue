<template>
  <div class="channels-view page-enter">
    <!-- 页面头部 -->
    <div class="page-header">
      <div>
        <h1 class="page-title">发送通道</h1>
        <p class="page-desc">配置数据发送目标及数据点绑定关系</p>
      </div>
      <el-button type="primary" :icon="Plus" @click="openCreate">新增通道</el-button>
    </div>

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

      <!-- 新增占位卡 -->
      <div class="channel-card add-card" @click="openCreate">
        <el-icon size="32" color="var(--border-muted)"><Plus /></el-icon>
        <span style="color:var(--text-muted);font-size:13px;margin-top:8px">新增通道</span>
      </div>
    </div>

    <!-- 新增/编辑通道弹窗 -->
    <el-dialog v-model="dialogVisible" :title="editingChannel ? '编辑发送通道' : '新增发送通道'" width="580px" destroy-on-close>
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px" label-position="left">
        <el-form-item label="通道名称" prop="name">
          <el-input v-model="form.name" placeholder="如：云端 MQTT" />
        </el-form-item>
        <el-form-item label="通道编码" prop="code">
          <el-input v-model="form.code" placeholder="全局唯一编码，如 CH_MQTT_01" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="2" placeholder="Optional description" />
        </el-form-item>
        <el-form-item label="Send Protocol" prop="protocol">
          <el-select v-model="form.protocol" placeholder="选择协议" style="width:100%">
            <el-option v-for="o in SendProtocolOptions" :key="o.value" :label="o.label" :value="o.value" />
          </el-select>
        </el-form-item>
        
        <!-- WebSocket 模式说明 -->
        <el-form-item v-if="form.protocol === SendProtocol.WebSocket.value" label="说明">
          <div style="font-size:12px;color:var(--text-muted);line-height:1.6">
            <el-icon size="14"><InfoFilled /></el-icon>
            WebSocket 作为<strong>服务端模式</strong>运行，等待客户端连接。
            采集数据会自动推送给订阅的客户端。
          </div>
        </el-form-item>
        <el-form-item label="Endpoint" prop="endpoint">
          <el-input v-model="form.endpoint" :placeholder="getEndpointPlaceholder(form.protocol)" />
        </el-form-item>
        <el-form-item label="配置 JSON">
          <el-input
            v-model="form.configJson" type="textarea" :rows="4"
            :placeholder="getConfigPlaceholder(form.protocol)"
            class="mono-input"
          />
          <div style="font-size:11px;color:var(--text-muted);margin-top:4px">{{ getConfigHint(form.protocol) }}</div>
        </el-form-item>
        <el-form-item label="是否启用">
          <el-switch v-model="form.isEnabled" active-color="#38dcc4" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitForm">{{ editingChannel ? '保存修改' : '创建通道' }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Link, Connection, InfoFilled } from '@element-plus/icons-vue'
import { getChannels, createChannel, updateChannel, deleteChannel, toggleChannel } from '@/api/channel'
import { SendProtocol, SendProtocolOptions, formatDateTime } from '@/api/constants'

type ChannelItem = {
  id: number
  name: string
  code: string
  description?: string
  protocol: string
  protocolValue: number
  endpoint: string
  mappedDataPointCount: number
  isEnabled: boolean
  createdAt?: string
}

type ChannelForm = {
  name: string
  code: string
  description: string
  protocol: number | null
  endpoint: string
  configJson: string
  isEnabled: boolean
}

const router = useRouter()
const loading = ref(false)
const channels = ref<ChannelItem[]>([])

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
  configJson: '',
  isEnabled: true
})

const rules = {
  name: [{ required: true, message: '请输入通道名称' }],
  code: [{ required: true, message: '请输入通道编码' }],
  protocol: [{ required: true, message: '请选择发送协议' }],
  endpoint: [{ required: true, message: '请输入端点地址' }]
}

const getProtoColor = (value: number) => {
  const protocol = Object.values(SendProtocol).find((item) => item.value === value)
  return protocol?.color ?? '#8fa5c5'
}

const getEndpointPlaceholder = (protocol: number | null) => {
  if (protocol === SendProtocol.Mqtt.value) return 'mqtt://host:1883'
  if (protocol === SendProtocol.Http.value) return 'https://api.example.com/data'
  if (protocol === SendProtocol.WebSocket.value) return 'ws://localhost:8080/ws 或 wss://api.example.com/ws'
  if (protocol === SendProtocol.LocalFile.value) return './output/data.json'
  return '请输入端点地址'
}

const getConfigPlaceholder = (protocol: number | null) => {
  if (protocol === SendProtocol.Mqtt.value) return '{"topic":"edge/data","clientId":"device01"}'
  if (protocol === SendProtocol.Http.value) return '{"token":"Bearer xxx","timeout":5000}'
  if (protocol === SendProtocol.WebSocket.value) {
    return '{"subscribeTopic":"device/data","heartbeatInterval":30000}'
  }
  if (protocol === SendProtocol.LocalFile.value) return '{"format":"json","path":"./data"}'
  return '配置 JSON（可选）'
}

const getConfigHint = (protocol: number | null) => {
  if (protocol === SendProtocol.Mqtt.value) return 'MQTT：topic(主题), clientId(客户端 ID), username, password'
  if (protocol === SendProtocol.Http.value) return 'HTTP：token(认证令牌), timeout(超时毫秒), method'
  if (protocol === SendProtocol.WebSocket.value) return 'WebSocket：subscribeTopic(订阅主题), heartbeatInterval(心跳间隔 ms)'
  if (protocol === SendProtocol.LocalFile.value) return '本地文件：format(json/csv), path(保存路径)'
  return '根据所选协议填写相应配置'
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
    configJson: '',
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
    configJson: '',
    isEnabled: channel.isEnabled
  }
  dialogVisible.value = true
}

const buildConfigJson = () => {
  let config: Record<string, any> = {}

  try {
    if (form.value.configJson.trim()) {
      config = JSON.parse(form.value.configJson)
    }
  } catch {
    // 如果解析失败，使用空对象
  }

  return JSON.stringify(config, null, 2)
}

const submitForm = async () => {
  await formRef.value?.validate()
  submitting.value = true
  try {
    const submitData = {
      ...form.value,
      configJson: buildConfigJson()
    }
    
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
  } finally {
    submitting.value = false
  }
}

const handleDelete = async (channel: ChannelItem) => {
  try {
    await ElMessageBox.confirm(
      `确定要删除发送通道 "${channel.name}" 吗？删除后无法恢复。`,
      '删除通道',
      {
        type: 'warning',
        confirmButtonText: '删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }
    )

    await deleteChannel(channel.id)
    ElMessage.success('通道已删除')
    fetchChannels()
  } catch (e: any) {
    if (e !== 'cancel') {
      ElMessage.error(`删除失败：${e.message || '未知错误'}`)
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

onMounted(fetchChannels)
</script>

<style scoped>
.page-header { display:flex; align-items:flex-start; justify-content:space-between; margin-bottom:24px; }
.page-title  { font-size:22px; font-weight:800; color:var(--text-primary); }
.page-desc   { font-size:13px; color:var(--text-muted); margin-top:4px; }

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

/* 新增卡片 */
.add-card {
  border: 1px dashed var(--border-muted);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  min-height: 180px;
  transition: all 0.25s;
  background: transparent;
}
.add-card:hover {
  border-color: var(--cyan);
  background: rgba(0, 255, 255, 0.03);
}
.add-card .el-icon {
  transition: transform 0.25s;
}
.add-card:hover .el-icon {
  transform: scale(1.1) rotate(90deg);
}

/* 弹窗样式 */
:deep(.el-dialog) {
  background: var(--bg-card) !important;
  border: 1px solid var(--border-muted) !important;
  border-radius: var(--radius-lg) !important;
}
:deep(.el-dialog__header) { border-bottom: 1px solid var(--border-subtle); }
:deep(.el-dialog__title) { color: var(--text-primary) !important; }
:deep(.el-dialog__body) { color: var(--text-primary); }
:deep(.el-dialog__footer) { border-top: 1px solid var(--border-subtle); }
:deep(.el-form-item__label) { color: var(--text-secondary) !important; }
:deep(.el-input__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-select__wrapper) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; }
:deep(.el-textarea__inner) { background: var(--bg-base) !important; border-color: var(--border-muted) !important; color: var(--text-primary) !important; }
</style>
