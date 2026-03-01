import axios from 'axios'
import { ElMessage } from 'element-plus'

const request = axios.create({
  baseURL: '/api',
  timeout: 10000,
  headers: { 'Content-Type': 'application/json' }
})

request.interceptors.request.use(
  (config) => config,
  (error) => Promise.reject(error)
)

request.interceptors.response.use(
  (response) => {
    const data = response.data

    if (data && typeof data === 'object' && 'success' in data && data.success === false) {
      const message = (data as { message?: string }).message || 'Request failed'
      ElMessage.error(message)
      return Promise.reject(new Error(message))
    }

    return data
  },
  (error) => {
    const msg =
      error?.response?.data?.message ||
      error?.message ||
      'Network error, please check your connection'

    ElMessage.error(msg)
    return Promise.reject(error)
  }
)

export default request
