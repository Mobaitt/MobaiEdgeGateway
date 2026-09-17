using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Enums;
using EdgeGateway.WebApi.DTOs.Request;
using EdgeGateway.WebApi.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace EdgeGateway.WebApi.Controllers;

[ApiController]
[Route("api/datapoint-templates")]
public class DataPointTemplatesController : ControllerBase
{
    private readonly DataPointTemplateService _templateService;

    public DataPointTemplatesController(DataPointTemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] CollectionProtocol? protocol = null)
    {
        var templates = await _templateService.GetAllAsync(protocol);
        return Ok(ApiResponse<List<DataPointTemplateResponse>>.Ok(
            templates.Select(DataPointTemplateResponse.FromEntity).ToList()));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var template = await _templateService.GetByIdAsync(id);
        return template == null
            ? NotFound(ApiResponse.Fail($"Template ID={id} was not found"))
            : Ok(ApiResponse<DataPointTemplateDetailResponse>.Ok(DataPointTemplateDetailResponse.FromEntityWithPoints(template)));
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] CollectionProtocol? protocol = null)
    {
        var (items, total) = await _templateService.GetPagedAsync(page, pageSize, search, protocol);
        var result = PagedResponse<DataPointTemplateResponse>.Create(
            items.Select(DataPointTemplateResponse.FromEntity).ToList(), total, page, pageSize);
        return Ok(ApiResponse<PagedResponse<DataPointTemplateResponse>>.Ok(result));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDataPointTemplateRequest request)
    {
        var items = request.Points.Select(point => new EdgeGateway.Domain.Entities.DataPointTemplateItem
        {
            Name = point.Name,
            TagSuffix = point.TagSuffix,
            Description = point.Description,
            Address = point.Address,
            DataType = point.DataType,
            Unit = point.Unit,
            ModbusSlaveId = point.ModbusSlaveId,
            ModbusFunctionCode = point.ModbusFunctionCode,
            ModbusByteOrder = point.ModbusByteOrder,
            RegisterLength = point.RegisterLength,
            ModbusBitIndex = point.ModbusBitIndex,
            IsEnabled = point.IsEnabled,
            IsControllable = point.IsControllable
        }).ToList();
        var template = await _templateService.UpdateAsync(id, request.Name, request.Description, items, HttpContext.RequestAborted);
        return Ok(ApiResponse<DataPointTemplateResponse>.Ok(DataPointTemplateResponse.FromEntity(template), "Template updated"));
    }

    [HttpPost("from-device/{deviceId:int}")]
    public async Task<IActionResult> CreateFromDevice(int deviceId, [FromBody] CreateDataPointTemplateRequest request)
    {
        var template = await _templateService.CreateFromDeviceAsync(
            deviceId, request.Name, request.Description, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetById), new { id = template.Id },
            ApiResponse<DataPointTemplateResponse>.Ok(DataPointTemplateResponse.FromEntity(template), "Template created"));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _templateService.DeleteAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse.Ok("Template deleted"));
    }
}
