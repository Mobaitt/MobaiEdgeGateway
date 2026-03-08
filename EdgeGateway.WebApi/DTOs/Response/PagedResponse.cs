namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>
/// 分页响应包装
/// 用于需要分页的列表查询接口，包含数据列表和分页元数据
/// </summary>
public class PagedResponse<T>
{
    /// <summary>当前页数据列表</summary>
    public List<T> Items { get; set; } = new();

    /// <summary>总记录数</summary>
    public int Total { get; set; }

    /// <summary>当前页码（从1开始）</summary>
    public int Page { get; set; }

    /// <summary>每页大小</summary>
    public int PageSize { get; set; }

    /// <summary>总页数（自动计算）</summary>
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);

    /// <summary>是否有上一页</summary>
    public bool HasPrevious => Page > 1;

    /// <summary>是否有下一页</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>创建分页响应</summary>
    public static PagedResponse<T> Create(List<T> items, int total, int page, int pageSize)
    {
        return new PagedResponse<T>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
