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
          </div>
        </div>
      </div>

      <!-- 新增占位卡 -->
      <div class="channel-card add-card" @click="openCreate">
        <el-icon size="32" color="var(--border-muted)"><Plus /></el-icon>
        <span style="color:var(--text-muted);font-size:13px;margin-top:8px">新增通道</span>
      </div>
    </div>

    <!-- 通道表格（详细视图） -->
    <div class="table-wrap" style="margin-top:24px">
      <div class="table-header-row">
        <span class="table-section-title">通道详情</span>
      </div>
      <el-table :data="channels" row-key="id">
        <el-table-column label="Status" width="70" align="center">
          <template #default="{ row }">
            <div class="pulse-dot" :class="row.isEnabled ? 'online' : 'offline'" style="margin:0 auto" />
          </template>
        </el-table-column>
        <el-table-column prop="name" label="通道名称" min-width="150" />
        <el-table-column label="协议" width="120">
          <template #default="{ row }">
            <span class="badge info" :style="{ color: getProtoColor(row.protocolValue) }">{{ row.protocol }}</span>
          </template>
        </el-table-column>
        <el-table-column label="Endpoint" min-width="200">
          <template #default="{ row }">
            <span class="mono ep-text">{{ row.endpoint }}</span>
          </template>
        </el-table-column>
        <el-table-column label="绑定点数" width="100" align="center">
          <template #default="{ row }">
            <span class="mono" style="color:var(--cyan);font-weight:700">{{ row.mappedDataPointCount }}</span>
          </template>
        </el-table-column>
        <el-table-column label="创建时间" width="160">
          <template #default="{ row }">
            <span class="mono time-text">{{ formatDateTime(row.createdAt) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="160" align="right">
          <template #default="{ row }">
            <el-button size="small" text @click="goMappings(row)">数据点映射</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 新增通道弹窗 -->
    <el-dialog v-model="dialogVisible" title="新增发送通道" width="580px" destroy-on-close>
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
        <el-button type="primary" :loading="submitting" @click="submitForm">创建通道</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Link, Connection, InfoFilled } from '@element-plus/icons-vue'
import { getChannels, createChannel, toggleChannel } from '@/api/channel'
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
    await createChannel(submitData)
    ElMessage.success('通道创建成功')
    dialogVisible.value = false
    fetchChannels()
  } finally {
    submitting.value = false
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
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}
.channel-card {
  background: var(--bg-card); border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg); padding: 18px 20px;
  display: flex; flex-direction: column; gap: 10px;
  transition: border-color 0.2s, box-shadow 0.2s;
}
.channel-card:hover { border-color: var(--border-muted); box-shadow: 0 4px 20px rgba(0,0,0,0.2); }
.channel-card.disabled { opacity: 0.55; }
.add-card {
  border: 1px dashed var(--border-muted);
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  cursor: pointer; min-height: 170px;
  transition: all 0.2s;
}
.add-card:hover { border-color: var(--cyan); }

.card-head { display:flex; align-items:center; justify-content:space-between; }
.protocol-badge {
  padding: 3px 10px; border-radius: 6px; border: 1px solid;
  font-size: 11px; font-family: var(--font-mono); font-weight: 700; letter-spacing: 0.06em;
}
.card-body { display: flex; flex-direction: column; gap: 4px; }
.ch-name     { font-size:15px; font-weight:700; color:var(--text-primary); }
.ch-code     { font-size:11px; color:var(--text-muted); }
.ch-endpoint { font-size:11px; color:var(--text-secondary); display:flex; align-items:center; gap:4px; word-break:break-all; }
.ch-desc     { font-size:12px; color:var(--text-muted); }
.card-foot   { display:flex; align-items:center; justify-content:space-between; border-top:1px solid var(--border-subtle); padding-top:10px; margin-top:4px; }
.map-count   { display:flex; align-items:center; gap:5px; font-size:13px; color:var(--cyan); }

/* 表格 */
.table-wrap { background:var(--bg-card); border:1px solid var(--border-subtle); border-radius:var(--radius-lg); overflow:hidden; }
.table-header-row { padding:14px 20px 0; }
.table-section-title { font-size:11px; color:var(--text-muted); letter-spacing:0.08em; text-transform:uppercase; font-weight:700; }
.ep-text  { font-size:12px; color:var(--text-secondary); }
.time-text { font-size:11px; color:var(--text-muted); }
:deep(.mono-input .el-input__inner) { font-family:var(--font-mono); }

/* 弹窗样式修复 */
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
