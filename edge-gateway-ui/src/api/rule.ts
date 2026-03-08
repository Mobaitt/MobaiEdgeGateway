import request from './request'
import type { Rule, RuleConfig, CreateRuleRequest, UpdateRuleRequest, RuleType, FailureAction } from '@/types/rule'

export interface TestRuleData {
  dataPointId: number
  deviceId: number
  tag: string
  value: any
}

export interface RuleExecutionResult {
  success: boolean
  value: any
  quality: number
  errorMessage: string | null
  triggeredRules: string[]
  shouldReject: boolean
}

// 规则类型字符串到数字的映射
const ruleTypeMap: Record<string, RuleType> = {
  'Limit': 0,
  'Transform': 1,
  'Validation': 2,
  'Calculation': 3
}

// 失败处理方式字符串到数字的映射
const failureActionMap: Record<string, FailureAction> = {
  'Pass': 0,
  'Reject': 1,
  'DefaultValue': 2
}

/**
 * 将 API 返回的规则数据中的字符串枚举转换为数字
 */
function normalizeRule(rule: any): Rule {
  return {
    ...rule,
    ruleType: typeof rule.ruleType === 'string' ? ruleTypeMap[rule.ruleType] ?? 2 : rule.ruleType,
    onFailure: typeof rule.onFailure === 'string' ? failureActionMap[rule.onFailure] ?? 0 : rule.onFailure
  }
}

/**
 * 获取所有规则
 */
export function getRules() {
  return request<any[]>({
    url: '/rules',
    method: 'get'
  }).then(res => ({
    ...res,
    data: res.data.map(normalizeRule)
  }))
}

/**
 * 获取指定规则
 */
export function getRule(id: number) {
  return request<any>({
    url: `/rules/${id}`,
    method: 'get'
  }).then(res => ({
    ...res,
    data: normalizeRule(res.data)
  }))
}

/**
 * 获取数据点的规则
 */
export function getRulesByDataPoint(dataPointId: number) {
  return request<any[]>({
    url: `/rules/datapoint/${dataPointId}`,
    method: 'get'
  }).then(res => ({
    ...res,
    data: res.data.map(normalizeRule)
  }))
}

/**
 * 获取设备的规则
 */
export function getRulesByDevice(deviceId: number) {
  return request<any[]>({
    url: `/rules/device/${deviceId}`,
    method: 'get'
  }).then(res => ({
    ...res,
    data: res.data.map(normalizeRule)
  }))
}

/**
 * 获取全局规则
 */
export function getGlobalRules() {
  return request<any[]>({
    url: '/rules/global',
    method: 'get'
  }).then(res => ({
    ...res,
    data: res.data.map(normalizeRule)
  }))
}

/**
 * 创建规则
 */
export function createRule(data: CreateRuleRequest) {
  return request<Rule>({
    url: '/rules',
    method: 'post',
    data
  })
}

/**
 * 更新规则
 */
export function updateRule(id: number, data: UpdateRuleRequest) {
  return request<Rule>({
    url: `/rules/${id}`,
    method: 'put',
    data
  })
}

/**
 * 删除规则
 */
export function deleteRule(id: number) {
  return request({
    url: `/rules/${id}`,
    method: 'delete'
  })
}

/**
 * 切换规则状态
 */
export function toggleRule(id: number, isEnabled: boolean) {
  return request({
    url: `/rules/${id}/toggle`,
    method: 'patch',
    data: isEnabled
  })
}

/**
 * 测试规则
 */
export function testRule(rule: CreateRuleRequest, testData: TestRuleData) {
  return request<RuleExecutionResult>({
    url: '/rules/test',
    method: 'post',
    data: {
      rule,
      testData
    }
  })
}
