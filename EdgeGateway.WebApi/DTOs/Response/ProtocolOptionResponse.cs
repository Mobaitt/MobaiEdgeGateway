namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>
/// 协议能力选项响应
/// 用于统一返回“已定义 / 已实现 / 当前可配置”的协议元数据
/// </summary>
public class ProtocolOptionResponse
{
    public int Value { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Desc { get; set; } = string.Empty;

    public string Color { get; set; } = "#8fa5c5";

    public bool Implemented { get; set; }

    public bool Enabled { get; set; }

    public bool Configurable { get; set; }
}
