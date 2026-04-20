using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

namespace PartFinder.Services;

public sealed class BackendApiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions ReadJson = new() { PropertyNameCaseInsensitive = true };

    private readonly AdminSessionStore _session;
    private readonly ILocalSetupContext _setup;

    public BackendApiClient(AdminSessionStore session, ILocalSetupContext setup)
    {
        _session = session;
        _setup = setup;
    }

    public async Task<(bool Ok, string? Error, DashboardStatsDto? Data)> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var result = await GetAsync<Envelope<DashboardStatsDto>>("/dashboard/stats", ct).ConfigureAwait(true);
        return result.Ok ? (true, null, result.Data?.Data) : (false, result.Error, null);
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<DashboardTrendPointDto> Data)> GetDashboardTrendAsync(CancellationToken ct = default)
    {
        var result = await GetAsync<Envelope<List<DashboardTrendPointDto>>>("/dashboard/trend", ct).ConfigureAwait(true);
        return result.Ok
            ? (true, null, (IReadOnlyList<DashboardTrendPointDto>)(result.Data?.Data ?? []))
            : (false, result.Error, Array.Empty<DashboardTrendPointDto>());
    }

    public async Task<(bool Ok, string? Error, string? JobId)> StartTemplateImportAsync(
        string templateId,
        string csvFilePath,
        IReadOnlyDictionary<string, string> headerMap,
        CancellationToken ct = default)
    {
        _session.Load();
        _setup.Refresh();
        var orgId = _setup.OrgCode?.Trim();
        var token = _session.AccessToken?.Trim();
        if (string.IsNullOrWhiteSpace(orgId) || string.IsNullOrWhiteSpace(token))
        {
            return (false, "Missing session or organization context.", null);
        }

        var baseUrl = LicenseApiClient.GetBaseUrl().TrimEnd('/');
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/templates/{templateId}/import");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("X-Org-Id", orgId);

        var multipart = new MultipartFormDataContent();
        var bytes = await File.ReadAllBytesAsync(csvFilePath, ct).ConfigureAwait(true);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        multipart.Add(fileContent, "file", Path.GetFileName(csvFilePath));
        multipart.Add(new StringContent(JsonSerializer.Serialize(headerMap)), "headerMap");
        req.Content = multipart;

        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req, ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }

        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        if (!resp.IsSuccessStatusCode)
        {
            return (false, string.IsNullOrWhiteSpace(text) ? "Import request failed." : text, null);
        }

        var dto = JsonSerializer.Deserialize<Envelope<ImportStartDto>>(text, ReadJson);
        return (true, null, dto?.Data?.JobId);
    }

    public async Task<(bool Ok, string? Error, ImportStatusDto? Status)> GetTemplateImportStatusAsync(
        string templateId,
        CancellationToken ct = default)
    {
        var result = await GetAsync<Envelope<ImportStatusDto>>($"/templates/{templateId}/import/status", ct).ConfigureAwait(true);
        return result.Ok ? (true, null, result.Data?.Data) : (false, result.Error, null);
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<TemplateLiteDto> Data)> GetTemplatesLiteAsync(CancellationToken ct = default)
    {
        var result = await GetAsync<Envelope<List<TemplateLiteDto>>>("/templates", ct).ConfigureAwait(true);
        return result.Ok
            ? (true, null, (IReadOnlyList<TemplateLiteDto>)(result.Data?.Data ?? []))
            : (false, result.Error, Array.Empty<TemplateLiteDto>());
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<EnrichedRowDto> Data)> GetViewDataAsync(
        string primaryTemplateId,
        CancellationToken ct = default)
    {
        var result = await GetAsync<Envelope<List<EnrichedRowDto>>>($"/view-data/{primaryTemplateId}", ct).ConfigureAwait(true);
        return result.Ok
            ? (true, null, (IReadOnlyList<EnrichedRowDto>)(result.Data?.Data ?? []))
            : (false, result.Error, Array.Empty<EnrichedRowDto>());
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<WorksheetRelationDto> Data)> GetRelationsAsync(
        CancellationToken ct = default)
    {
        var result = await GetAsync<Envelope<List<WorksheetRelationDto>>>("/relations", ct).ConfigureAwait(true);
        return result.Ok
            ? (true, null, (IReadOnlyList<WorksheetRelationDto>)(result.Data?.Data ?? []))
            : (false, result.Error, Array.Empty<WorksheetRelationDto>());
    }

    public async Task<(bool Ok, string? Error, WorksheetRelationDto? Data)> CreateRelationAsync(
        SaveWorksheetRelationRequest request,
        CancellationToken ct = default)
    {
        var result = await SendJsonAsync<SaveWorksheetRelationRequest, Envelope<WorksheetRelationDto>>(HttpMethod.Post, "/relations", request, ct)
            .ConfigureAwait(true);
        return result.Ok ? (true, null, result.Data?.Data) : (false, result.Error, null);
    }

    public async Task<(bool Ok, string? Error, WorksheetRelationDto? Data)> UpdateRelationAsync(
        string relationId,
        SaveWorksheetRelationRequest request,
        CancellationToken ct = default)
    {
        var result = await SendJsonAsync<SaveWorksheetRelationRequest, Envelope<WorksheetRelationDto>>(HttpMethod.Patch, $"/relations/{relationId}", request, ct)
            .ConfigureAwait(true);
        return result.Ok ? (true, null, result.Data?.Data) : (false, result.Error, null);
    }

    private async Task<(bool Ok, string? Error, T? Data)> GetAsync<T>(string path, CancellationToken ct)
    {
        _session.Load();
        _setup.Refresh();
        var orgId = _setup.OrgCode?.Trim();
        var token = _session.AccessToken?.Trim();

        if (string.IsNullOrWhiteSpace(orgId) || string.IsNullOrWhiteSpace(token))
        {
            return (false, "Missing session or organization context.", default);
        }

        var baseUrl = LicenseApiClient.GetBaseUrl().TrimEnd('/');
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api{path}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("X-Org-Id", orgId);

        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req, ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, default);
        }

        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        if (!resp.IsSuccessStatusCode)
        {
            return (false, string.IsNullOrWhiteSpace(text) ? "Request failed." : text, default);
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

    private async Task<(bool Ok, string? Error, TResponse? Data)> SendJsonAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest requestBody,
        CancellationToken ct)
    {
        _session.Load();
        _setup.Refresh();
        var orgId = _setup.OrgCode?.Trim();
        var token = _session.AccessToken?.Trim();

        if (string.IsNullOrWhiteSpace(orgId) || string.IsNullOrWhiteSpace(token))
        {
            return (false, "Missing session or organization context.", default);
        }

        var baseUrl = LicenseApiClient.GetBaseUrl().TrimEnd('/');
        using var req = new HttpRequestMessage(method, $"{baseUrl}/api{path}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("X-Org-Id", orgId);
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req, ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, default);
        }

        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        if (!resp.IsSuccessStatusCode)
        {
            return (false, string.IsNullOrWhiteSpace(text) ? "Request failed." : text, default);
        }

        try
        {
            var dto = JsonSerializer.Deserialize<TResponse>(text, ReadJson);
            return (true, null, dto);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, default);
        }
    }

    private sealed class Envelope<T>
    {
        public T? Data { get; set; }
    }
}

public sealed class DashboardStatsDto
{
    public int TotalParts { get; set; }
    public int LowStock { get; set; }
    public int ActiveTemplates { get; set; }
    public double ImportSuccessRate { get; set; }
    public List<string> RecentActivity { get; set; } = [];
}

public sealed class DashboardTrendPointDto
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
}

public sealed class ImportStartDto
{
    public string? JobId { get; set; }
}

public sealed class ImportStatusDto
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int FailedRows { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class TemplateLiteDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class EnrichedRowDto
{
    public string RowId { get; set; } = string.Empty;
    public Dictionary<string, string> Cells { get; set; } = [];
    public Dictionary<string, EnrichedRelationDto> LinkedData { get; set; } = [];
}

public sealed class EnrichedRelationDto
{
    public bool Matched { get; set; }
    public string MenuLabel { get; set; } = string.Empty;
    public Dictionary<string, string> DisplayValues { get; set; } = [];
}

public sealed class SaveWorksheetRelationRequest
{
    public string PrimaryTemplateId { get; set; } = string.Empty;
    public string LookupTemplateId { get; set; } = string.Empty;
    public string MenuLabel { get; set; } = string.Empty;
    public List<string> MatchKeys { get; set; } = [];
    public List<string> DisplayColumns { get; set; } = [];
}

public sealed class WorksheetRelationDto
{
    public string Id { get; set; } = string.Empty;
    public string PrimaryTemplateId { get; set; } = string.Empty;
    public string LookupTemplateId { get; set; } = string.Empty;
    public string MenuLabel { get; set; } = string.Empty;
    public List<string> MatchKeys { get; set; } = [];
    public List<string> DisplayColumns { get; set; } = [];
}
