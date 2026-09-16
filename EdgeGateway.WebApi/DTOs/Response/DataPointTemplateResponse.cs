using EdgeGateway.Domain.Entities;

namespace EdgeGateway.WebApi.DTOs.Response;

public class DataPointTemplateResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public int PointCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static DataPointTemplateResponse FromEntity(DataPointTemplate template)
    {
        var pointCount = 0;
        try
        {
            pointCount = System.Text.Json.JsonSerializer.Deserialize<List<DataPointTemplateItem>>(template.PointsJson)?.Count ?? 0;
        }
        catch (System.Text.Json.JsonException)
        {
            // 坏模板内容由套用接口报告，列表接口仍可正常展示模板名称。
        }

        return new DataPointTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Protocol = template.Protocol.ToString(),
            PointCount = pointCount,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }
}

public class DataPointTemplateDetailResponse : DataPointTemplateResponse
{
    public List<DataPointTemplateItem> Points { get; set; } = new();

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    public static DataPointTemplateDetailResponse FromEntityWithPoints(DataPointTemplate template)
    {
        var result = new DataPointTemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Protocol = template.Protocol.ToString(),
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };

        try
        {
            result.Points = System.Text.Json.JsonSerializer.Deserialize<List<DataPointTemplateItem>>(template.PointsJson, JsonOptions) ?? new();
        }
        catch (System.Text.Json.JsonException)
        {
            result.Points = new();
        }

        result.PointCount = result.Points.Count;
        return result;
    }
}
