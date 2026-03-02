import request from './request'
import type { Rule, RuleConfig, CreateRuleRequest, UpdateRuleRequest } from '@/types/rule'

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

/**
 * 获取所有规则
 */
export function getRules() {
  return request<Rule[]>({
    url: '/rules',
    method: 'get'
  })
}

/**
 * 获取指定规则
 */
export function getRule(id: number) {
  return request<Rule>({
    url: `/rules/${id}`,
    method: 'get'
  })
}

/**
 * 获取数据点的规则
 */
export function getRulesByDataPoint(dataPointId: number) {
  return request<Rule[]>({
    url: `/rules/datapoint/${dataPointId}`,
    method: 'get'
  })
}

/**
 * 获取设备的规则
 */
export function getRulesByDevice(deviceId: number) {
  return request<Rule[]>({
    url: `/rules/device/${deviceId}`,
    method: 'get'
  })
}

/**
 * 获取全局规则
 */
export function getGlobalRules() {
  return request<Rule[]>({
    url: '/rules/global',
    method: 'get'
  })
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
