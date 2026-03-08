/**
 * 规则类型枚举
 */
export enum RuleType {
  Limit = 0,
  Transform = 1,
  Validation = 2,
  Calculation = 3
}

/**
 * 转换类型枚举
 */
export enum TransformType {
  None = 0,
  Linear = 1,
  Formula = 2,
  LookupTable = 3,
  UnitConversion = 4,
  Polynomial = 5
}

/**
 * 校验类型枚举
 */
export enum ValidationType {
  None = 0,
  Range = 1,
  RateOfChange = 2,
  DeadBand = 3,
  Rationality = 4,
  FixedValue = 5
}

/**
 * 计算类型枚举
 */
export enum CalculationType {
  Custom = 0,
  Sum = 1,
  Average = 2,
  Max = 3,
  Min = 4,
  Count = 5,
  StandardDeviation = 6,
  WeightedAverage = 7
}

/**
 * 失败处理方式
 */
export enum FailureAction {
  Pass = 0,
  Reject = 1,
  DefaultValue = 2
}

/**
 * 数据质量
 */
export enum DataQuality {
  Good = 0,
  Bad = 1,
  Uncertain = 2
}

/**
 * 规则实体
 */
export interface Rule {
  id: number
  dataPointIds: number[]
  dataPointNames: string[]
  deviceId: number | null
  deviceName: string | null
  name: string
  description: string | null
  ruleType: RuleType
  isEnabled: boolean
  priority: number
  ruleConfig: string
  onFailure: FailureAction
  defaultValue: any | null
  createdAt: string
  updatedAt: string
}

/**
 * 规则配置
 */
export interface RuleConfig {
  validationType?: ValidationType
  minValue?: number
  maxValue?: number
  maxRateOfChange?: number
  deadBand?: number
  transformType?: TransformType
  scale?: number
  offset?: number
  formula?: string
  rationalityExpression?: string
  fixedValue?: number
  fixedValueTolerance?: number
  [key: string]: any
}

/**
 * 创建规则请求
 */
export interface CreateRuleRequest {
  dataPointIds?: number[]
  deviceId?: number
  name: string
  description?: string
  ruleType: RuleType
  isEnabled?: boolean
  priority?: number
  ruleConfig: string
  onFailure?: FailureAction
  defaultValue?: any
}

/**
 * 更新规则请求
 */
export interface UpdateRuleRequest {
  id: number
  dataPointIds?: number[]
  deviceId?: number
  name: string
  description?: string
  ruleType: RuleType
  isEnabled: boolean
  priority: number
  ruleConfig: string
  onFailure: FailureAction
  defaultValue?: any
}
