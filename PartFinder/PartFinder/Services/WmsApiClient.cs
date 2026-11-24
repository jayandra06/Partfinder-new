using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PartFinder.Helpers;

namespace PartFinder.Services;

public sealed class WmsApiClient
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(10),
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
        MaxResponseContentBufferSize = 10 * 1024 * 1024,
    };
    private static readonly JsonSerializerOptions ReadJson = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 32,
    };
    private static readonly JsonSerializerOptions WriteJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AdminSessionStore _session;
    private readonly ILocalSetupContext _setup;

    public WmsApiClient(AdminSessionStore session, ILocalSetupContext setup)
    {
        _session = session;
        _setup = setup;
    }

    public async Task<(bool Ok, string? Error, WmsHealthDto? Data)> GetHealthAsync(
        CancellationToken ct = default)
    {
        var (ok, err, dto) = await SendCoreAsync<WmsHealthDto>(
                HttpMethod.Get,
                "/wms/v1/health",
                null,
                ct)
            .ConfigureAwait(false);
        return ok ? (true, null, dto) : (false, err, null);
    }

    public Task<(bool Ok, string? Error, WmsTransactionProbeDto? Data)> ProbeTransactionsAsync(
        CancellationToken ct = default) =>
        GetAsync<WmsTransactionProbeDto>("/wms/v1/diagnostics/transactions", ct);

    public Task<(bool Ok, string? Error, IReadOnlyList<WmsWarehouseDto> Items)> ListWarehousesAsync(
        CancellationToken ct = default) =>
        ListAsync<WmsWarehouseDto>("/wms/v1/warehouses", ct);

    public Task<(bool Ok, string? Error, WmsWarehouseDto? Data)> CreateWarehouseAsync(
        CreateWmsWarehouseRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsWarehouseDto>("/wms/v1/warehouses", request, ct);

    public Task<(bool Ok, string? Error, IReadOnlyList<WmsStockOwnerDto> Items)> ListStockOwnersAsync(
        CancellationToken ct = default) =>
        ListAsync<WmsStockOwnerDto>("/wms/v1/stock-owners", ct);

    public Task<(bool Ok, string? Error, WmsStockOwnerDto? Data)> CreateStockOwnerAsync(
        CreateWmsStockOwnerRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsStockOwnerDto>("/wms/v1/stock-owners", request, ct);

    public Task<(bool Ok, string? Error, IReadOnlyList<WmsZoneDto> Items)> ListZonesAsync(
        string warehouseId,
        CancellationToken ct = default) =>
        ListAsync<WmsZoneDto>($"/wms/v1/zones?warehouseId={Uri.EscapeDataString(warehouseId)}", ct);

    public Task<(bool Ok, string? Error, WmsZoneDto? Data)> CreateZoneAsync(
        CreateWmsZoneRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsZoneDto>("/wms/v1/zones", request, ct);

    public Task<(bool Ok, string? Error, IReadOnlyList<WmsLocationDto> Items)> ListLocationsAsync(
        string warehouseId,
        CancellationToken ct = default) =>
        ListAsync<WmsLocationDto>(
            $"/wms/v1/locations?warehouseId={Uri.EscapeDataString(warehouseId)}",
            ct);

    public Task<(bool Ok, string? Error, WmsLocationDto? Data)> CreateLocationAsync(
        CreateWmsLocationRequest request,
        CancellationToken ct = default) =>
        PostAsync<WmsLocationDto>("/wms/v1/locations", request, ct);

    private async Task<(bool Ok, string? Error, IReadOnlyList<T> Items)> ListAsync<T>(
        string path,
        CancellationToken ct)
    {
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<WmsListEnvelope<T>>>(
                HttpMethod.Get,
                path,
                null,
                ct)
            .ConfigureAwait(false);
        if (!ok || env?.Data?.Items is null)
        {
            return (false, err, Array.Empty<T>());
        }

        return (true, null, env.Data.Items);
    }

    private async Task<(bool Ok, string? Error, T? Data)> GetAsync<T>(string path, CancellationToken ct)
        where T : class
    {
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<T>>(HttpMethod.Get, path, null, ct)
            .ConfigureAwait(false);
        return ok ? (true, null, env?.Data) : (false, err, default);
    }

    private async Task<(bool Ok, string? Error, T? Data)> PostAsync<T>(
        string path,
        object body,
        CancellationToken ct)
        where T : class
    {
        var (ok, err, env) = await SendCoreAsync<WmsEnvelope<T>>(HttpMethod.Post, path, body, ct)
            .ConfigureAwait(false);
        return ok ? (true, null, env?.Data) : (false, err, default);
    }

    private async Task<(bool Ok, string? Error, T? Data)> SendCoreAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct)
    {
        _session.Load();
        _setup.Refresh();
        var orgId = _setup.OrgCode?.Trim();
        var token = _session.AccessToken?.Trim();

        if (string.IsNullOrWhiteSpace(orgId))
        {
            return (false, "Missing organisation context.", default);
        }

        var baseUrl = LicenseApiClient.GetBaseUrl().TrimEnd('/');
        using var req = new HttpRequestMessage(method, $"{baseUrl}/api{path}");
        if (!string.IsNullOrWhiteSpace(token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        req.Headers.Add("X-Org-Id", orgId);
        if (body is not null)
        {
            req.Content = new StringContent(
                JsonSerializer.Serialize(body, WriteJson),
                Encoding.UTF8,
                "application/json");
        }

        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, default);
        }

        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            return (false, ApiErrorFormatter.ToUserMessage(text), default);
        }

        try
        {
            var dto = JsonSerializer.Deserialize<T>(text, ReadJson);
            return (true, null, dto);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, default);
        }
    }

    private sealed class WmsEnvelope<T>
    {
        public T? Data { get; set; }
        public bool Success { get; set; }
    }

    private sealed class WmsListEnvelope<T>
    {
        public T[]? Items { get; set; }
        public int Total { get; set; }
    }
}

public sealed class WmsHealthDto
{
    public bool Ok { get; set; }
    public string OrgCode { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public bool StockMutations { get; set; }
}

public sealed class WmsTransactionProbeDto
{
    public bool Supported { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public sealed class WmsWarehouseDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class CreateWmsWarehouseRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "UTC";
    public string? Address { get; set; }
}

public sealed class WmsStockOwnerDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class CreateWmsStockOwnerRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class WmsZoneDto
{
    public string Id { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class CreateWmsZoneRequest
{
    public string WarehouseId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class WmsLocationDto
{
    public string Id { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string? ZoneId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsPickable { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateWmsLocationRequest
{
    public string WarehouseId { get; set; } = string.Empty;
    public string? ZoneId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = "storage";
    public bool IsPickable { get; set; }
}