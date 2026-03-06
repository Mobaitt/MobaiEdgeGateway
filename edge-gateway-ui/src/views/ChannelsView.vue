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

    <!-- 新增/编辑通道弹窗 -->
    <ChannelDialog
      v-model="dialogVisible"
      :editing-channel="editingChannel"
      :protocol-options="SendProtocolOptions"
      :submitting="submitting"
      @submit="handleSubmit"
      @close="handleDialogClose"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Link, Connection } from '@element-plus/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import AddCard from '@/components/AddCard.vue'
import ChannelDialog from '@/dialogs/channel/ChannelDialog.vue'
import { useConfirmDelete } from '@/composables/useConfirmDelete'
import { getChannels, createChannel, updateChannel, deleteChannel, toggleChannel } from '@/api/channel'
import { getSendProtocols } from '@/api/enums'
import type { ChannelItem } from '@/types'

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

const getProtoColor = (value: number) => {
  const protocol = SendProtocolOptions.value.find((item) => item.value === value)
  return protocol?.color ?? '#8fa5c5'
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
  dialogVisible.value = true
}

const openEdit = (channel: ChannelItem) => {
  editingChannel.value = channel
  dialogVisible.value = true
}

const handleSubmit = async (data: any) => {
  submitting.value = true
  try {
    if (editingChannel.value) {
      // 编辑模式
      await updateChannel(editingChannel.value.id, data)
      ElMessage.success('通道已更新')
    } else {
      // 创建模式
      await createChannel(data)
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

const handleDialogClose = () => {
  editingChannel.value = null
}

onMounted(() => {
  fetchChannels()
  loadSendProtocols()
})
</script>

<style scoped lang="scss">
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

  &::before {
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

  &:hover {
    border-color: var(--cyan);
    box-shadow: 0 8px 32px rgba(0, 255, 255, 0.1);
    transform: translateY(-2px);
  }

  &:hover::before {
    opacity: 1;
  }

  &.disabled {
    opacity: 0.5;
    filter: grayscale(0.3);

    &:hover {
      transform: none;
    }
  }
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

  .el-icon {
    flex-shrink: 0;
  }
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

  .mono {
    font-size: 15px;
  }
}

.foot-actions {
  display: flex;
  align-items: center;
  gap: 4px;

  .el-button {
    font-size: 12px;
    padding: 4px 8px;
  }
}

.channel-card.add-card {
  min-height: 180px;
}
</style>
