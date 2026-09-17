using System.Text.Json;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EdgeGateway.Application.Services;

/// <summary>数据点模板的保存、查询和套用服务。</summary>
public class DataPointTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<GatewayDbContext> _dbFactory;
    private readonly DataCollectionService _collectionService;

    public DataPointTemplateService(
        IDbContextFactory<GatewayDbContext> dbFactory,
        DataCollectionService collectionService)
    {
        _dbFactory = dbFactory;
        _collectionService = collectionService;
    }

    public async Task<List<DataPointTemplate>> GetAllAsync(CollectionProtocol? protocol = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.DataPointTemplates.AsNoTracking();
        if (protocol.HasValue)
            query = query.Where(template => template.Protocol == protocol.Value);

        return await query.OrderBy(template => template.Name).ToListAsync();
    }

    public async Task<DataPointTemplate?> GetByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.DataPointTemplates.AsNoTracking().FirstOrDefaultAsync(template => template.Id == id);
    }

    public async Task<(List<DataPointTemplate> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? search = null, CollectionProtocol? protocol = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.DataPointTemplates.AsNoTracking();
        if (protocol.HasValue)
            query = query.Where(template => template.Protocol == protocol.Value);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(template => template.Name.Contains(search) || (template.Description != null && template.Description.Contains(search)));

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(template => template.UpdatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<DataPointTemplate> UpdateAsync(
        int id, string name, string? description, IReadOnlyList<DataPointTemplateItem> items,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Template name is required");
        name = name.Trim();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.DataPointTemplates.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException($"Template ID={id} was not found");
        if (await db.DataPointTemplates.AnyAsync(item => item.Name == name && item.Id != id, cancellationToken))
            throw new InvalidOperationException($"Template '{name}' already exists");

        var normalized = items.Where(item => !string.IsNullOrWhiteSpace(item.TagSuffix)).ToList();
        if (normalized.GroupBy(item => item.TagSuffix.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Template contains duplicate Tag suffixes");

        foreach (var item in normalized)
            ValidateTemplateItem(item);

        template.Name = name.Trim();
        template.Description = description;
        template.PointsJson = JsonSerializer.Serialize(normalized, JsonOptions);
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task<DataPointTemplate> CreateFromDeviceAsync(
        int deviceId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Template name is required");
        name = name.Trim();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var device = await db.Devices
            .Include(item => item.DataPoints)
            .FirstOrDefaultAsync(item => item.Id == deviceId, cancellationToken)
            ?? throw new InvalidOperationException($"Device ID={deviceId} was not found");

        if (await db.DataPointTemplates.AnyAsync(template => template.Name == name, cancellationToken))
            throw new InvalidOperationException($"Template '{name}' already exists");

        var items = device.DataPoints.Select(ToTemplateItem).ToList();
        var template = new DataPointTemplate
        {
            Name = name.Trim(),
            Description = description,
            Protocol = device.Protocol,
            PointsJson = JsonSerializer.Serialize(items, JsonOptions)
        };

        db.DataPointTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.DataPointTemplates.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException($"Template ID={id} was not found");
        db.DataPointTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ApplyTemplateResult> ApplyAsync(
        int deviceId,
        int templateId,
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var device = await db.Devices
            .Include(item => item.DataPoints)
            .FirstOrDefaultAsync(item => item.Id == deviceId, cancellationToken)
            ?? throw new InvalidOperationException($"Device ID={deviceId} was not found");
        var template = await db.DataPointTemplates.FindAsync([templateId], cancellationToken)
            ?? throw new InvalidOperationException($"Template ID={templateId} was not found");

        if (template.Protocol != device.Protocol)
            throw new InvalidOperationException("Template protocol does not match the target device protocol");

        var items = JsonSerializer.Deserialize<List<DataPointTemplateItem>>(template.PointsJson, JsonOptions)
            ?? [];
        var existingByTag = device.DataPoints.ToDictionary(point => point.Tag, StringComparer.OrdinalIgnoreCase);
        var created = 0;
        var overwritten = 0;
        var skipped = 0;

        foreach (var item in items)
        {
            ValidateTemplateItem(item);
            var suffix = item.TagSuffix.Trim();
            if (suffix.Length == 0)
            {
                skipped++;
                continue;
            }

            var tag = $"{device.Code}.{suffix}";
            if (existingByTag.TryGetValue(tag, out var existing))
            {
                if (!overwriteExisting)
                {
                    skipped++;
                    continue;
                }

                ApplyItem(existing, item, tag);
                overwritten++;
                continue;
            }

            var point = new DataPoint { DeviceId = deviceId };
            ApplyItem(point, item, tag);
            db.DataPoints.Add(point);
            existingByTag[tag] = point;
            created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (device.IsEnabled && (created > 0 || overwritten > 0))
            await _collectionService.RefreshDeviceDataPointsAsync(deviceId, cancellationToken);

        return new ApplyTemplateResult(created, overwritten, skipped);
    }

    private static DataPointTemplateItem ToTemplateItem(DataPoint point)
    {
        var separator = point.Tag.IndexOf('.', StringComparison.Ordinal);
        var suffix = separator >= 0 && separator + 1 < point.Tag.Length
            ? point.Tag[(separator + 1)..]
            : point.Tag;

        return new DataPointTemplateItem
        {
            Name = point.Name,
            TagSuffix = suffix,
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
        };
    }

    private static void ApplyItem(DataPoint point, DataPointTemplateItem item, string tag)
    {
        point.Name = item.Name;
        point.Tag = tag;
        point.Description = item.Description;
        point.Address = item.Address;
        point.DataType = item.DataType;
        point.Unit = item.Unit;
        point.ModbusSlaveId = item.ModbusSlaveId;
        point.ModbusFunctionCode = item.ModbusFunctionCode;
        point.ModbusByteOrder = item.ModbusByteOrder;
        point.RegisterLength = item.RegisterLength;
        point.ModbusBitIndex = item.ModbusBitIndex;
        point.IsEnabled = item.IsEnabled;
        point.IsControllable = item.IsControllable;
    }

    private static void ValidateTemplateItem(DataPointTemplateItem item)
    {
        if (item.RegisterLength is not (1 or 2 or 4))
            throw new InvalidOperationException($"Template point '{item.TagSuffix}' has an invalid register length");
        if (item.ModbusBitIndex is > 15)
            throw new InvalidOperationException($"Template point '{item.TagSuffix}' has an invalid bit index");
        if (item.ModbusBitIndex.HasValue &&
            (item.DataType != DataValueType.Bool || item.ModbusFunctionCode is not (3 or 4) || item.RegisterLength != 1))
            throw new InvalidOperationException($"Template point '{item.TagSuffix}' has an invalid register bit configuration");
        if (item.IsControllable && item.ModbusFunctionCode is 2 or 4)
            throw new InvalidOperationException($"Template point '{item.TagSuffix}' is read-only but marked controllable");
    }
}

public sealed record ApplyTemplateResult(int Created, int Overwritten, int Skipped);
