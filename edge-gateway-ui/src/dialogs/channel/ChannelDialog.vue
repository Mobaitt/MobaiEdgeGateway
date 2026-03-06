<template>
  <el-dialog
    :model-value="modelValue"
    :title="editingChannel ? '编辑发送通道' : '新增发送通道'"
    width="840px"
    destroy-on-close
    class="app-dialog channel-dialog"
    align-center
    @update:model-value="$emit('update:modelValue', $event)"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="96px"
      label-position="left"
      class="channel-form"
    >
      <FormSection title="基本信息" icon="Document" class="compact">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="通道名称" prop="name">
              <el-input
                v-model="form.name"
                placeholder="如：云端 MQTT"
                @blur="handleNameBlur"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="通道编码" prop="code">
              <div class="input-with-btn">
                <el-input
                  v-model="form.code"
                  placeholder="全局唯一编码，如 CH_MQTT_01"
                  class="input-field"
                />
                <el-button size="small" text class="btn-auto-generate" @click="handleGenerateCode">
                  <el-icon><MagicStick /></el-icon>
                  <span>自动生成</span>
                </el-button>
              </div>
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="描述">
          <el-input
            v-model="form.description"
            type="textarea"
            :rows="1"
            placeholder="可选描述信息"
            autosize
          />
        </el-form-item>
      </FormSection>

      <FormSection title="协议配置" icon="Setting" class="compact">
        <el-row :gutter="16">
          <el-col :span="form.protocol === 2 ? 12 : 24">
            <el-form-item label="发送协议" prop="protocol">
              <el-select v-model="form.protocol" placeholder="选择协议" style="width: 100%">
                <el-option
                  v-for="o in protocolOptions"
                  :key="o.value"
                  :label="o.label"
                  :value="o.value"
                >
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
                <el-radio label="client">
                  <el-icon><Upload /></el-icon> 客户端
                </el-radio>
                <el-radio label="server">
                  <el-icon><Download /></el-icon> 服务端
                </el-radio>
              </el-radio-group>
            </el-form-item>
          </el-col>
        </el-row>

        <!-- 协议提示 -->
        <div v-if="form.protocol === 5" class="protocol-hint protocol-hint-inline">
          <el-icon class="hint-icon"><InfoFilled /></el-icon>
          <span>WebSocket 服务端，数据推送给订阅客户端。</span>
          <code class="hint-code">ws://localhost:5000/ws?topic=device/data</code>
        </div>
        <div v-else-if="form.protocol === 2 && form.httpMode === 'client'" class="protocol-hint protocol-hint-inline">
          <el-icon class="hint-icon"><InfoFilled /></el-icon>
          <span>HTTP 客户端，主动 POST 到目标接口。</span>
        </div>
        <div v-else-if="form.protocol === 2 && form.httpMode === 'server'" class="protocol-hint protocol-hint-inline">
          <el-icon class="hint-icon"><InfoFilled /></el-icon>
          <span>HTTP 服务端，GET 获取数据。</span>
          <code class="hint-code">http://localhost:5000/api/http-data/xxx</code>
        </div>
        <div v-else-if="form.protocol === 1" class="protocol-hint protocol-hint-inline">
          <el-icon class="hint-icon"><InfoFilled /></el-icon>
          <span>发布到 MQTT 主题，支持 QoS。</span>
          <code class="hint-code">edge/device/data</code>
        </div>
        <div v-else-if="form.protocol === 4" class="protocol-hint protocol-hint-inline">
          <el-icon class="hint-icon"><InfoFilled /></el-icon>
          <span>追加写入本地 JSON（NDJSON）。</span>
        </div>
      </FormSection>

      <FormSection title="连接配置" icon="Connection" class="compact">
        <el-form-item label="端点地址" prop="endpoint">
          <el-input
            v-model="form.endpoint"
            :placeholder="getEndpointPlaceholder(form.protocol)"
          />
        </el-form-item>

        <!-- MQTT 协议配置 -->
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

        <!-- HTTP 客户端配置 -->
        <template v-if="form.protocol === 2 && form.httpMode === 'client'">
          <el-row :gutter="16">
            <el-col :span="8">
              <el-form-item label="HTTP 方法">
                <el-select v-model="form.httpMethod" style="width: 100%">
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
              <el-form-item label="超时 (ms)">
                <el-input-number
                  v-model="form.httpTimeout"
                  :min="1000"
                  :max="60000"
                  :step="1000"
                  style="width: 100%"
                  controls-position="right"
                />
              </el-form-item>
            </el-col>
          </el-row>
        </template>

        <!-- HTTP 服务端提示 -->
        <div v-if="form.protocol === 2 && form.httpMode === 'server'" class="protocol-hint protocol-hint-inline">
          <el-icon class="hint-icon"><InfoFilled /></el-icon>
          <span>服务端复用端口 5000。</span>
          <code class="hint-code">http://localhost:5000/api/http-data/xxx</code>
        </div>

        <!-- WebSocket 配置 -->
        <template v-if="form.protocol === 5">
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item label="订阅主题">
                <el-input v-model="form.wsSubscribeTopic" placeholder="device/data" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="心跳间隔 (ms)">
                <el-input-number
                  v-model="form.wsHeartbeatInterval"
                  :min="5000"
                  :max="120000"
                  :step="5000"
                  style="width: 100%"
                  controls-position="right"
                />
              </el-form-item>
            </el-col>
          </el-row>
        </template>

        <!-- 文件配置 -->
        <template v-if="form.protocol === 4">
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item label="文件格式">
                <el-select v-model="form.fileFormat" style="width: 100%">
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
      <el-button @click="handleCancel">取消</el-button>
      <el-button
        type="primary"
        :loading="submitting"
        @click="handleSubmit"
      >
        {{ editingChannel ? '保存修改' : '创建通道' }}
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { InfoFilled, Upload, Download, MagicStick } from '@element-plus/icons-vue'
import FormSection from '@/components/FormSection.vue'
import { generateCodeWithTimestamp } from '@/utils/codeGenerate'
import type { ChannelItem } from '@/types'

interface ChannelForm {
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

interface Props {
  modelValue: boolean
  editingChannel: ChannelItem | null
  protocolOptions: any[]
  submitting: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'submit', data: ChannelForm): void
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  editingChannel: null,
  protocolOptions: () => [],
  submitting: false
})

const emit = defineEmits<Emits>()

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

const resetForm = () => {
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
  formRef.value?.clearValidate()
}

// 监听编辑通道变化，填充表单
watch(
  () => props.editingChannel,
  (channel) => {
    if (channel) {
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
    } else {
      // 新增模式，重置表单
      resetForm()
    }
  },
  { immediate: true }
)

const getEndpointPlaceholder = (protocol: number | null) => {
  if (protocol === 1) return 'mqtt://host:1883'
  if (protocol === 2) return 'https://api.example.com/data/upload'
  if (protocol === 5) return 'ws://localhost:8080/ws 或 wss://api.example.com/ws'
  if (protocol === 4) return './output/data.json'
  return '请输入端点地址'
}

const handleNameBlur = () => {
  if (!form.value.code && form.value.name?.trim()) {
    form.value.code = generateCodeWithTimestamp(form.value.name, 'CH')
  }
}

const handleGenerateCode = () => {
  if (!form.value.name?.trim()) {
    ElMessage.warning('请先输入通道名称')
    return
  }
  form.value.code = generateCodeWithTimestamp(form.value.name, 'CH')
  ElMessage.success('通道编码已生成')
}

const handleSubmit = async () => {
  await formRef.value?.validate()
  emit('submit', { ...form.value })
}

const handleCancel = () => {
  emit('update:modelValue', false)
}

const handleClose = () => {
  formRef.value?.resetFields()
  emit('close')
}
</script>

<style scoped lang="scss">
.channel-form {
  .compact {
    margin-bottom: 16px;
  }
}
</style>
