/**
 * 采集协议类型
 */
export enum CollectionProtocol {
  Simulator = 0,
  Modbus = 1,
  OpcUa = 2,
  Virtual = 3,
  S7 = 4
}

/**
 * 发送协议类型
 */
export enum SendProtocol {
  LocalFile = 0,
  Mqtt = 1,
  Http = 2,
  WebSocket = 3
}

/**
 * 数据类型
 */
export enum DataValueType {
  Bool = 0,
  Int16 = 1,
  Int32 = 2,
  Float = 3,
  Double = 4,
  String = 5
}

/**
 * 计算类型
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
