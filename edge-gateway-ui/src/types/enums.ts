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
  Bool = 1,
  Int16 = 2,
  UInt16 = 3,
  Int32 = 4,
  UInt32 = 5,
  Float = 6,
  Int64 = 7,
  UInt64 = 8,
  Double = 9,
  String = 10,
  Hex = 11,
  Binary = 12
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
