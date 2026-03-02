using EdgeGateway.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace EdgeGateway.WebApi.Controllers;

/// <summary>
/// 枚举选项接口 - 提供前端下拉选择所需的数据
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EnumsController : ControllerBase
{
    /// <summary>
    /// 获取采集协议选项
    /// </summary>
    [HttpGet("collection-protocols")]
    public IActionResult GetCollectionProtocols()
    {
        var options = Enum.GetValues(typeof(CollectionProtocol))
            .Cast<CollectionProtocol>()
            .Select(p => new
            {
                value = (int)p,
                label = p.ToString(),
                desc = GetProtocolDescription(p)
            })
            // 过滤掉没有实现的
            .Where(p => ((List<int>)[1, 0]).Contains(p.value))
            .ToList();

        return Ok(new { data = options, message = "success" });
    }

    /// <summary>
    /// 获取发送协议选项
    /// </summary>
    [HttpGet("send-protocols")]
    public IActionResult GetSendProtocols()
    {
        var options = Enum.GetValues(typeof(SendProtocol))
            .Cast<SendProtocol>()
            .Select(p => new
            {
                value = (int)p,
                label = p.ToString(),
                desc = GetSendProtocolDescription(p)
            })
            .
            // 过滤掉没有实现的
            Where(p => ((List<int>)[2, 5]).Contains(p.value)).ToList();

        return Ok(new { data = options, message = "success" });
    }

    /// <summary>
    /// 获取数据类型选项
    /// </summary>
    [HttpGet("data-value-types")]
    public IActionResult GetDataValueTypes()
    {
        var options = Enum.GetValues(typeof(DataValueType))
            .Cast<DataValueType>()
            .Select(p => new
            {
                value = (int)p,
                label = p.ToString()
            })
            .ToList();

        return Ok(new { data = options, message = "success" });
    }

    /// <summary>
    /// 获取 Modbus 字节序选项
    /// </summary>
    [HttpGet("modbus-byte-orders")]
    public IActionResult GetModbusByteOrders()
    {
        var options = Enum.GetValues(typeof(ModbusByteOrder))
            .Cast<ModbusByteOrder>()
            .Select(p => new
            {
                value = (int)p,
                label = p.ToString(),
                desc = GetByteOrderDescription(p)
            })
            .ToList();

        return Ok(new { data = options, message = "success" });
    }

    private static string GetByteOrderDescription(ModbusByteOrder order)
    {
        return order switch
        {
            ModbusByteOrder.ABCD => "大端模式 (ABCD) - 高字节在前，标准 Modbus 顺序",
            ModbusByteOrder.BADC => "字节交换 (BADC) - 每个寄存器内字节交换",
            ModbusByteOrder.CDAB => "字交换 (CDAB) - 两个寄存器顺序交换",
            ModbusByteOrder.DCBA => "完全交换 (DCBA) - 寄存器内 + 寄存器间交换",
            _ => order.ToString()
        };
    }

    private static string GetProtocolDescription(CollectionProtocol protocol)
    {
        return protocol switch
        {
            CollectionProtocol.Modbus => "Modbus TCP/RTU 工业协议",
            CollectionProtocol.OpcUa => "OPC UA 工业自动化",
            CollectionProtocol.S7 => "西门子 S7 PLC",
            CollectionProtocol.Simulator => "模拟器（测试用）",
            CollectionProtocol.Virtual => "虚拟设备（计算节点）",
            _ => protocol.ToString()
        };
    }

    private static string GetSendProtocolDescription(SendProtocol protocol)
    {
        return protocol switch
        {
            SendProtocol.Mqtt => "MQTT 发布/订阅模式",
            SendProtocol.Http => "HTTP 客户端/服务端模式",
            SendProtocol.Kafka => "Kafka 消息队列",
            SendProtocol.LocalFile => "本地文件写入",
            SendProtocol.WebSocket => "WebSocket 服务端推送",
            _ => protocol.ToString()
        };
    }
}