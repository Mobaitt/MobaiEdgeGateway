using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;

namespace EdgeGateway.Infrastructure.Strategies.Collection;

public static class DataPointWriteValueConverter
{
    public static object Normalize(DataPoint dataPoint, object? value)
    {
        if (value == null)
            throw new InvalidOperationException("Write value cannot be null.");

        // 统一把请求体里的值转换成点位配置的数据类型
        return dataPoint.DataType switch
        {
            DataValueType.Bool => value switch
            {
                bool boolValue => boolValue,
                string stringValue when bool.TryParse(stringValue, out var parsedBool) => parsedBool,
                string stringValue when stringValue == "1" => true,
                string stringValue when stringValue == "0" => false,
                _ => throw new InvalidOperationException("Boolean writes only support true/false or 1/0.")
            },
            DataValueType.Int16 => Convert.ToInt16(value),
            DataValueType.UInt16 => Convert.ToUInt16(value),
            DataValueType.Int32 => Convert.ToInt32(value),
            DataValueType.UInt32 => Convert.ToUInt32(value),
            DataValueType.Float => Convert.ToSingle(value),
            DataValueType.Int64 => Convert.ToInt64(value),
            DataValueType.UInt64 => Convert.ToUInt64(value),
            DataValueType.Double => Convert.ToDouble(value),
            DataValueType.String => value.ToString() ?? string.Empty,
            _ => throw new InvalidOperationException($"Unsupported write type: {dataPoint.DataType}")
        };
    }

}
