import { ElMessageBox, ElMessage } from 'element-plus'

export interface ConfirmDeleteOptions {
  title?: string
  message: string
  confirmText?: string
  cancelText?: string
  onConfirm: () => Promise<void> | void
  successMessage?: string
}

/**
 * 统一的删除确认弹窗逻辑，执行成功后可选刷新列表并提示
 */
export function useConfirmDelete() {
  const confirm = async (options: ConfirmDeleteOptions) => {
    const {
      title = '删除确认',
      message,
      confirmText = '删除',
      cancelText = '取消',
      onConfirm,
      successMessage = '删除成功'
    } = options

    try {
      await ElMessageBox.confirm(message, title, {
        type: 'warning',
        confirmButtonText: confirmText,
        cancelButtonText: cancelText,
        confirmButtonClass: 'el-button--danger'
      })
      await onConfirm()
      ElMessage.success(successMessage)
    } catch (e) {
      if (e !== 'cancel') throw e
    }
  }
  return { confirm }
}
