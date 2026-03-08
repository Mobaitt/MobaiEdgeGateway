import request from './request'

/**
 * 获取演示模式状态
 */
export function getDemoModeStatus() {
  return request<{ enabled: boolean; message: string }>({
    url: '/system/demo-mode',
    method: 'get'
  })
}
