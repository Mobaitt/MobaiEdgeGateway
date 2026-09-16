using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Host;
using EdgeGateway.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EdgeGateway.Tests;

internal sealed class GatewayTestContext : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public ServiceProvider Services { get; }
    public IServiceScope Scope { get; }
    public GatewayDbContext Db => Scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
    public DataSendService Sender => Services.GetRequiredService<DataSendService>();
    public DeviceManagementService Management => Scope.ServiceProvider.GetRequiredService<DeviceManagementService>();
    public DataCollectionService Collection => Services.GetRequiredService<DataCollectionService>();

    private GatewayTestContext(SqliteConnection connection, ServiceProvider services)
    {
        _connection = connection;
        Services = services;
        Scope = services.CreateScope();
    }

    public static async Task<GatewayTestContext> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        var name = $"gateway-test-{Guid.NewGuid():N}";
        var connection = new SqliteConnection($"Data Source={name};Mode=Memory;Cache=Shared");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEdgeGateway($"{name};Mode=Memory;Cache=Shared");
        configure?.Invoke(services);
        var context = new GatewayTestContext(connection, services.BuildServiceProvider());
        await context.Db.Database.EnsureCreatedAsync();
        return context;
    }

    public async Task<DataPoint> AddPointAsync()
    {
        var device = new Device { Code = "test-device", Name = "Test", Protocol = CollectionProtocol.Simulator, IsEnabled = true };
        var point = new DataPoint { Device = device, Name = "Value", Tag = "test.value", DataType = DataValueType.Int16, IsEnabled = true, IsControllable = true };
        Db.DataPoints.Add(point);
        await Db.SaveChangesAsync();
        return point;
    }

    public async ValueTask DisposeAsync()
    {
        await Sender.DisposeAllAsync();
        Scope.Dispose();
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
