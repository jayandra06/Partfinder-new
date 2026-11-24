using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using PartFinder.Models;
using System.Security.Cryptography;
using System.Text;

namespace PartFinder.Services;

/// <summary>Org-scoped users (invites) in tenant MongoDB.</summary>
public sealed class MongoOrgUserDirectoryService : IOrgUserDirectoryService
{
    public const string CollectionName = "org_app_users";
    public const string OrgAdminDirectoryIdPrefix = "org-admin:";

    private readonly ILocalSetupContext _setupContext;
    private UserDirectoryBindings? _cachedBindings;

    public MongoOrgUserDirectoryService(ILocalSetupContext setupContext)
    {
        _setupContext = setupContext;
    }

    private sealed class UserDirectoryBindings
    {
        public required IMongoCollection<OrgAppUserDoc> AppUsers { get; init; }
        public required IMongoCollection<BsonDocument> Admins { get; init; }
    }

    public async Task<IReadOnlyList<OrgAppUserSummary>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        _cachedBindings = null;
        _setupContext.Refresh();
        var orgCode = _setupContext.OrgCode?.Trim();
        if (!string.IsNullOrEmpty(orgCode))
        {
            await SetupStatePersistence.SyncOrgDatabaseFromBackendAsync(orgCode, cancellationToken)
                .ConfigureAwait(false);
            _setupContext.Refresh();

            var (apiOk, apiErr, apiUsers) = await SetupApiClient.ListOrgUsersAsync(orgCode, cancellationToken)
                .ConfigureAwait(false);
            if (apiOk && apiUsers is { Count: > 0 })
            {
                return apiUsers;
            }

            if (!string.IsNullOrWhiteSpace(apiErr))
            {
                DebugLogClient.Post("warn", "MongoOrgUserDirectoryService.ListUsersAsync.api", apiErr);
            }
        }

        try
        {
            return await ListUsersFromResolvedDirectoryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DebugLogClient.Post("warn", "MongoOrgUserDirectoryService.ListUsersAsync", ex.Message);
            return Array.Empty<OrgAppUserSummary>();
        }
    }

    public async Task<IReadOnlyList<OrgAppUserSummary>> ListUsersFromTenantUriAsync(
        string tenantMongoUri,
        string orgCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantMongoUri))
        {
            return Array.Empty<OrgAppUserSummary>();
        }

        var code = orgCode?.Trim();
        foreach (var bindings in CreateBindingCandidates(tenantMongoUri.Trim(), code))
        {
            try
            {
                var list = await bindings.AppUsers.Find(FilterDefinition<OrgAppUserDoc>.Empty)
                    .SortByDescending(d => d.InvitedAtUtc)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (list.Count > 0)
                {
                    return list.Select(Map).ToList();
                }
            }
            catch (Exception ex)
            {
                DebugLogClient.Post("warn", "MongoOrgUserDirectoryService.ListUsersFromTenantUriAsync", ex.Message);
            }
        }

        return Array.Empty<OrgAppUserSummary>();
    }

    private async Task<IReadOnlyList<OrgAppUserSummary>> ListUsersFromResolvedDirectoryAsync(
        CancellationToken cancellationToken)
    {
        var bindings = await EnsureBindingsAsync(cancellationToken).ConfigureAwait(false);
        if (bindings is null)
        {
            return Array.Empty<OrgAppUserSummary>();
        }

        var list = await bindings.AppUsers.Find(FilterDefinition<OrgAppUserDoc>.Empty)
            .SortByDescending(d => d.InvitedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var appUsers = list.Select(Map).ToList();
        var adminSummaries = await ListOrgAdminSummariesAsync(bindings, appUsers, cancellationToken)
            .ConfigureAwait(false);
        return adminSummaries
            .Concat(appUsers)
            .OrderByDescending(u => u.InvitedAtUtc)
            .ToList();
    }

    private async Task<UserDirectoryBindings?> EnsureBindingsAsync(CancellationToken cancellationToken)
    {
        if (_cachedBindings is not null)
        {
            return _cachedBindings;
        }

        _setupContext.Refresh();
        var orgCode = _setupContext.OrgCode?.Trim();
        var candidates = new List<string>();

        if (!string.IsNullOrEmpty(orgCode))
        {
            var (status, _) = await SetupApiClient.StatusAsync(orgCode, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(status?.OrgDatabaseUri))
            {
                candidates.Add(status.OrgDatabaseUri.Trim());
            }
        }

        if (_setupContext.TryGetTenantMongoUri(out var local) && !string.IsNullOrWhiteSpace(local))
        {
            candidates.Add(local);
        }

        UserDirectoryBindings? fallback = null;
        foreach (var raw in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var bindings in CreateBindingCandidates(raw, orgCode))
            {
                fallback ??= bindings;
                if (await DirectoryHasUsersAsync(bindings, cancellationToken).ConfigureAwait(false))
                {
                    _cachedBindings = bindings;
                    return bindings;
                }
            }
        }

        _cachedBindings = fallback;
        return fallback;
    }

    private static IEnumerable<UserDirectoryBindings> CreateBindingCandidates(string raw, string? orgCode)
    {
        UserDirectoryBindings? directory = null;
        UserDirectoryBindings? tenant = null;

        try
        {
            var normalized = MongoConnectionStringUtil.Normalize(raw);
            var client = new MongoClient(normalized);
            var userDb = MongoConnectionStringUtil.GetUserDirectoryDatabase(client, normalized);
            directory = new UserDirectoryBindings
            {
                AppUsers = userDb.GetCollection<OrgAppUserDoc>(CollectionName),
                Admins = userDb.GetCollection<BsonDocument>(MongoTenantBootstrap.OrgAdminCollection),
            };
        }
        catch
        {
            // Try tenant path below.
        }

        if (directory is not null)
        {
            yield return directory;
        }

        if (string.IsNullOrWhiteSpace(orgCode))
        {
            yield break;
        }

        try
        {
            var tenantCs = MongoConnectionStringUtil.Prepare(raw, orgCode);
            var tenantClient = new MongoClient(tenantCs);
            var tenantDb = MongoConnectionStringUtil.GetTenantDatabase(tenantClient, tenantCs, orgCode);
            tenant = new UserDirectoryBindings
            {
                AppUsers = tenantDb.GetCollection<OrgAppUserDoc>(CollectionName),
                Admins = tenantDb.GetCollection<BsonDocument>(MongoTenantBootstrap.OrgAdminCollection),
            };
        }
        catch
        {
            yield break;
        }

        if (tenant is not null)
        {
            yield return tenant;
        }
    }

    private static async Task<bool> DirectoryHasUsersAsync(
        UserDirectoryBindings bindings,
        CancellationToken cancellationToken)
    {
        var adminCount = await bindings.Admins.CountDocumentsAsync(
                FilterDefinition<BsonDocument>.Empty,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (adminCount > 0)
        {
            return true;
        }

        var appCount = await bindings.AppUsers.CountDocumentsAsync(
                FilterDefinition<OrgAppUserDoc>.Empty,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return appCount > 0;
    }

    public async Task<OrgAppUserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var bindings = await EnsureBindingsAsync(cancellationToken).ConfigureAwait(false);
            if (bindings is null)
            {
                return null;
            }

            var coll = bindings.AppUsers;

            var em = email.Trim();
            if (string.IsNullOrEmpty(em))
            {
                return null;
            }

            var doc = await coll.Find(x => x.EmailNormalized == NormalizeEmail(em))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            return doc is null ? null : Map(doc);
        }
        catch (Exception ex)
        {
            DebugLogClient.Post("warn", "MongoOrgUserDirectoryService.FindByEmailAsync", ex.Message);
            return null;
        }
    }

    public async Task<DateTime?> GetOrgAdminCreatedAtUtcAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bindings = await EnsureBindingsAsync(cancellationToken).ConfigureAwait(false);
            if (bindings is null)
            {
                return null;
            }

            var coll = bindings.Admins;

            var em = email.Trim();
            if (string.IsNullOrEmpty(em))
            {
                return null;
            }

            var normalized = NormalizeEmail(em);
            var doc = await coll.Find(Builders<BsonDocument>.Filter.Eq("email", normalized))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (doc is null)
            {
                var adminCount = await coll.CountDocumentsAsync(
                        FilterDefinition<BsonDocument>.Empty,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (adminCount == 1)
                {
                    doc = await coll.Find(FilterDefinition<BsonDocument>.Empty)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            return doc is null ? null : ReadDocumentCreatedUtc(doc);
        }
        catch (Exception ex)
        {
            DebugLogClient.Post("warn", "MongoOrgUserDirectoryService.GetOrgAdminCreatedAtUtcAsync", ex.Message);
            return null;
        }
    }

    public async Task<DateTime?> GetTenantBootstrapDateUtcAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var cs = MongoConnectionStringUtil.Prepare(raw, _setupContext.OrgCode);
            var client = new MongoClient(cs);
            var db = MongoConnectionStringUtil.GetTenantDatabase(client, cs, _setupContext.OrgCode);
            var meta = db.GetCollection<BsonDocument>("_partfinder_setup");
            var doc = await meta.Find(Builders<BsonDocument>.Filter.Eq("_id", "bootstrap"))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (doc is null)
            {
                return null;
            }

            if (doc.TryGetValue("bootstrappedAt", out var boot) && boot.IsValidDateTime)
            {
                return boot.ToUniversalTime();
            }

            return ReadDocumentCreatedUtc(doc);
        }
        catch (Exception ex)
        {
            DebugLogClient.Post("warn", "MongoOrgUserDirectoryService.GetTenantBootstrapDateUtcAsync", ex.Message);
            return null;
        }
    }

    private static DateTime? ReadDocumentCreatedUtc(BsonDocument doc)
    {
        if (doc.TryGetValue("createdAt", out var created) && created.IsValidDateTime)
        {
            return created.ToUniversalTime();
        }

        if (doc.TryGetValue("_id", out var id) && id.IsObjectId)
        {
            return id.AsObjectId.CreationTime.ToUniversalTime();
        }

        return null;
    }

    public Task EnsureInvitedUserRecordAsync(
        string name,
        string email,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        string temporaryPassword,
        TemplatePermissionsDto? templatePermissions = null,
        MasterDataPermissionsDto? masterDataPermissions = null,
        CancellationToken cancellationToken = default)
        => EnsureInvitedUserRecordWithBindingsAsync(
            EnsureBindingsAsync(cancellationToken),
            name,
            email,
            role,
            partsAllTemplates,
            allowedTemplateIds,
            temporaryPassword,
            templatePermissions,
            masterDataPermissions,
            cancellationToken);

    public Task EnsureInvitedUserRecordForTenantUriAsync(
        string tenantMongoUri,
        string orgCode,
        string name,
        string email,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        string temporaryPassword,
        TemplatePermissionsDto? templatePermissions = null,
        MasterDataPermissionsDto? masterDataPermissions = null,
        CancellationToken cancellationToken = default)
    {
        IMongoCollection<OrgAppUserDoc>? coll = null;
        if (!string.IsNullOrWhiteSpace(tenantMongoUri))
        {
            coll = CreateBindingCandidates(tenantMongoUri.Trim(), orgCode?.Trim())
                .Select(b => b.AppUsers)
                .FirstOrDefault();
        }

        return EnsureInvitedUserRecordInternalAsync(
            coll,
            name,
            email,
            role,
            partsAllTemplates,
            allowedTemplateIds,
            temporaryPassword,
            templatePermissions,
            masterDataPermissions,
            cancellationToken);
    }

    private static async Task EnsureInvitedUserRecordWithBindingsAsync(
        Task<UserDirectoryBindings?> bindingsTask,
        string name,
        string email,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        string temporaryPassword,
        TemplatePermissionsDto? templatePermissions,
        MasterDataPermissionsDto? masterDataPermissions,
        CancellationToken cancellationToken)
    {
        var bindings = await bindingsTask.ConfigureAwait(false);
        await EnsureInvitedUserRecordInternalAsync(
            bindings?.AppUsers,
            name,
            email,
            role,
            partsAllTemplates,
            allowedTemplateIds,
            temporaryPassword,
            templatePermissions,
            masterDataPermissions,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureInvitedUserRecordInternalAsync(
        IMongoCollection<OrgAppUserDoc>? coll,
        string name,
        string email,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        string temporaryPassword,
        TemplatePermissionsDto? templatePermissions,
        MasterDataPermissionsDto? masterDataPermissions,
        CancellationToken cancellationToken)
    {
        if (coll is null)
        {
            return;
        }

        var em = email.Trim();
        if (string.IsNullOrEmpty(em) || string.IsNullOrWhiteSpace(temporaryPassword))
        {
            return;
        }

        var normalized = NormalizeEmail(em);
        var existing = await coll.Find(x => x.EmailNormalized == normalized)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return;
        }

        var doc = new OrgAppUserDoc
        {
            Name = name.Trim(),
            Email = em,
            EmailNormalized = normalized,
            Role = role.Trim(),
            PartsAllTemplates = partsAllTemplates,
            AllowedTemplateIds = allowedTemplateIds
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            TemplatePermissions = templatePermissions,
            MasterDataPermissions = masterDataPermissions,
            TemporaryPasswordHash = HashPassword(temporaryPassword),
            InvitedAtUtc = DateTime.UtcNow,
        };

        await coll.InsertOneAsync(doc, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<InviteUserResult> InviteUserAsync(
        string name,
        string email,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        TemplatePermissionsDto? templatePermissions = null,
        MasterDataPermissionsDto? masterDataPermissions = null,
        CancellationToken cancellationToken = default)
    {
        var bindings = await EnsureBindingsAsync(cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException(
                           "No tenant database is configured. Complete setup and ensure setup-state.json contains dbUri.");
        var coll = bindings.AppUsers;

        var em = email.Trim();
        if (string.IsNullOrEmpty(em))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var existing = await coll.Find(x => x.EmailNormalized == NormalizeEmail(em))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            throw new InvalidOperationException("A user with this email is already invited.");
        }

        var tempPassword = GenerateTemporaryPassword();
        var passwordHash = HashPassword(tempPassword);

        var doc = new OrgAppUserDoc
        {
            Name = name.Trim(),
            Email = em,
            EmailNormalized = NormalizeEmail(em),
            Role = role.Trim(),
            PartsAllTemplates = partsAllTemplates,
            AllowedTemplateIds = allowedTemplateIds
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            TemplatePermissions = templatePermissions,
            MasterDataPermissions = masterDataPermissions,
            TemporaryPasswordHash = passwordHash,
            InvitedAtUtc = DateTime.UtcNow,
        };

        await coll.InsertOneAsync(doc, cancellationToken: cancellationToken).ConfigureAwait(false);

        var orgCode = _setupContext.OrgCode?.Trim() ?? string.Empty;
        var emailSent = false;
        string? emailError = null;
        try
        {
            InviteEmailClient.SendInvite(
                em,
                doc.Name,
                orgCode,
                doc.Role,
                tempPassword);
            emailSent = true;
        }
        catch (Exception ex)
        {
            emailError = ex.Message;
        }

        return new InviteUserResult
        {
            Email = em,
            OrganizationCode = orgCode,
            TemporaryPassword = tempPassword,
            EmailSent = emailSent,
            EmailError = emailError,
        };
    }

    public async Task<bool> ValidateInviteCredentialsAsync(
        string email,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        var bindings = await EnsureBindingsAsync(cancellationToken).ConfigureAwait(false);
        if (bindings is null)
        {
            return false;
        }

        var coll = bindings.AppUsers;
        var em = email.Trim();
        if (string.IsNullOrEmpty(em) || string.IsNullOrWhiteSpace(temporaryPassword))
        {
            return false;
        }

        var doc = await coll.Find(x => x.EmailNormalized == NormalizeEmail(em))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (doc is null)
        {
            return false;
        }

        return VerifyPassword(temporaryPassword, doc.TemporaryPasswordHash);
    }

    public async Task<bool> UpdateUserAsync(
        string id,
        string name,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        TemplatePermissionsDto? templatePermissions = null,
        MasterDataPermissionsDto? masterDataPermissions = null,
        CancellationToken cancellationToken = default)
    {
        if (id.StartsWith(OrgAdminDirectoryIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var bindings = await EnsureBindingsAsync(cancellationToken).ConfigureAwait(false);
        if (bindings is null || !ObjectId.TryParse(id, out var objectId))
        {
            return false;
        }

        var coll = bindings.AppUsers;
        // Use raw BsonValue fields because TemplatePermissions/MasterDataPermissions
        // are [BsonIgnore] computed properties — the driver cannot build expressions for them.
        BsonValue templatePermsBson = templatePermissions == null
            ? BsonNull.Value
            : templatePermissions.ToBsonDocument();

        BsonValue masterPermsBson = masterDataPermissions == null
            ? BsonNull.Value
            : masterDataPermissions.ToBsonDocument();

        var update = Builders<OrgAppUserDoc>.Update
            .Set(x => x.Name, name.Trim())
            .Set(x => x.Role, role.Trim())
            .Set(x => x.PartsAllTemplates, partsAllTemplates)
            .Set(x => x.AllowedTemplateIds, allowedTemplateIds
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList())
            .Set(x => x.TemplatePermissionsRaw, templatePermsBson)
            .Set(x => x.MasterDataPermissionsRaw, masterPermsBson);

        var result = await coll.UpdateOneAsync(
            x => x.Id == objectId,
            update,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> CompleteInvitedUserActivationAsync(
        string email,
        string newPasswordPlain,
        CancellationToken cancellationToken = default)
    {
        var bindings = await EnsureBindingsAsync(cancellationToken).ConfigureAwait(false);
        if (bindings is null)
        {
            return false;
        }

        var coll = bindings.AppUsers;
        var em = email.Trim();
        if (string.IsNullOrEmpty(em) || string.IsNullOrWhiteSpace(newPasswordPlain))
        {
            return false;
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPasswordPlain, workFactor: 10);
        var update = Builders<OrgAppUserDoc>.Update
            .Set(x => x.TemporaryPasswordHash, string.Empty)
            .Set(x => x.PasswordHash, passwordHash);

        var result = await coll.UpdateOneAsync(
                x => x.EmailNormalized == NormalizeEmail(em),
                update,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0 || result.MatchedCount > 0;
    }

    public async Task<bool> MarkInvitedUserActiveAfterLoginAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var bindings = await EnsureBindingsAsync(cancellationToken).ConfigureAwait(false);
        if (bindings is null)
        {
            return false;
        }

        var coll = bindings.AppUsers;
        var em = email.Trim();
        if (string.IsNullOrEmpty(em))
        {
            return false;
        }

        var update = Builders<OrgAppUserDoc>.Update
            .Set(x => x.TemporaryPasswordHash, string.Empty);

        var result = await coll.UpdateOneAsync(
                x => x.EmailNormalized == NormalizeEmail(em),
                update,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0 || result.MatchedCount > 0;
    }

    public async Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken = default)
    {
        if (id.StartsWith(OrgAdminDirectoryIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var bindings = await EnsureBindingsAsync(cancellationToken).ConfigureAwait(false);
        if (bindings is null || !ObjectId.TryParse(id, out var objectId))
        {
            return false;
        }

        var coll = bindings.AppUsers;
        var result = await coll.DeleteOneAsync(
            x => x.Id == objectId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.DeletedCount > 0;
    }

    public async Task<bool> UpdateOrgAdminDisplayNameAsync(
        string email,
        string name,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return false;
        }

        var bindings = await EnsureBindingsAsync(cancellationToken).ConfigureAwait(false);
        if (bindings is null)
        {
            return false;
        }

        var em = email.Trim();
        if (string.IsNullOrEmpty(em))
        {
            return false;
        }

        var normalized = NormalizeEmail(em);
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("email", normalized),
            Builders<BsonDocument>.Filter.Eq("email", em));
        var update = Builders<BsonDocument>.Update.Set("name", trimmedName);
        var result = await bindings.Admins.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.ModifiedCount > 0 || result.MatchedCount > 0)
        {
            return true;
        }

        var appUser = await bindings.AppUsers.Find(x => x.EmailNormalized == normalized)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (appUser is null)
        {
            return false;
        }

        var appResult = await bindings.AppUsers.UpdateOneAsync(
                x => x.Id == appUser.Id,
                Builders<OrgAppUserDoc>.Update.Set(x => x.Name, trimmedName),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return appResult.ModifiedCount > 0 || appResult.MatchedCount > 0;
    }

    private async Task<IReadOnlyList<OrgAppUserSummary>> ListOrgAdminSummariesAsync(
        UserDirectoryBindings bindings,
        IReadOnlyList<OrgAppUserSummary> appUsers,
        CancellationToken cancellationToken)
    {
        var adminColl = bindings.Admins;

        var appEmails = new HashSet<string>(
            appUsers.Select(u => NormalizeEmail(u.Email)),
            StringComparer.OrdinalIgnoreCase);

        var docs = await adminColl.Find(FilterDefinition<BsonDocument>.Empty)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var summaries = new List<OrgAppUserSummary>();
        foreach (var doc in docs)
        {
            if (!doc.TryGetValue("email", out var emailVal) || !emailVal.IsString)
            {
                continue;
            }

            var email = emailVal.AsString.Trim();
            if (string.IsNullOrEmpty(email))
            {
                continue;
            }

            var normalized = NormalizeEmail(email);
            if (appEmails.Contains(normalized))
            {
                continue;
            }

            var name = doc.TryGetValue("name", out var nameVal) && nameVal.IsString
                ? nameVal.AsString.Trim()
                : string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                name = email;
            }

            var invitedAt = ReadDocumentCreatedUtc(doc) ?? DateTime.UtcNow;
            var hasPassword = doc.TryGetValue("passwordHash", out var hashVal)
                              && hashVal.IsString
                              && !string.IsNullOrWhiteSpace(hashVal.AsString);

            summaries.Add(new OrgAppUserSummary
            {
                Id = OrgAdminDirectoryIdPrefix + normalized,
                Name = name,
                Email = email,
                Role = "Admin",
                PartsAllTemplates = true,
                AllowedTemplateIds = Array.Empty<string>(),
                InvitedAtUtc = invitedAt,
                TemplatePermissions = null,
                MasterDataPermissions = null,
                Status = hasPassword ? "Active" : "Pending",
                IsOrgAdminDirectoryEntry = true,
            });
        }

        return summaries;
    }

    private static OrgAppUserSummary Map(OrgAppUserDoc d)
    {
        var invitedAt = d.InvitedAtUtc;
        if (invitedAt == default && d.Id != default)
        {
            invitedAt = d.Id.CreationTime.ToUniversalTime();
        }

        var hasPermanentPassword = !string.IsNullOrWhiteSpace(d.PasswordHash);
        var pendingInvite = !string.IsNullOrWhiteSpace(d.TemporaryPasswordHash);

        return new OrgAppUserSummary
        {
            Id = d.Id.ToString(),
            Name = d.Name,
            Email = d.Email,
            Role = d.Role,
            PartsAllTemplates = d.PartsAllTemplates,
            AllowedTemplateIds = d.AllowedTemplateIds,
            InvitedAtUtc = invitedAt,
            TemplatePermissions = d.TemplatePermissions,
            MasterDataPermissions = d.MasterDataPermissions,
            Status = hasPermanentPassword || !pendingInvite ? "Active" : "Pending",
            IsOrgAdminDirectoryEntry = false,
        };
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    [BsonIgnoreExtraElements]
    private sealed class OrgAppUserDoc
    {
        public ObjectId Id { get; set; }

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("emailNormalized")]
        public string EmailNormalized { get; set; } = string.Empty;

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Admin or Employee</summary>
        [BsonElement("role")]
        public string Role { get; set; } = "Employee";

        [BsonElement("partsAllTemplates")]
        public bool PartsAllTemplates { get; set; }

        [BsonElement("allowedTemplateIds")]
        public List<string> AllowedTemplateIds { get; set; } = [];

        [BsonElement("temporaryPasswordHash")]
        public string TemporaryPasswordHash { get; set; } = string.Empty;

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [BsonElement("invitedAtUtc")]
        public DateTime InvitedAtUtc { get; set; }

        [BsonElement("templatePermissions")]
        public BsonValue? TemplatePermissionsRaw { get; set; }

        [BsonIgnore]
        public TemplatePermissionsDto? TemplatePermissions
        {
            get
            {
                if (TemplatePermissionsRaw == null || !TemplatePermissionsRaw.IsBsonDocument) return null;
                try { return BsonSerializer.Deserialize<TemplatePermissionsDto>(TemplatePermissionsRaw.AsBsonDocument); }
                catch { return null; }
            }
            set => TemplatePermissionsRaw = value == null ? BsonNull.Value : value.ToBsonDocument();
        }

        [BsonElement("masterDataPermissions")]
        public BsonValue? MasterDataPermissionsRaw { get; set; }

        [BsonIgnore]
        public MasterDataPermissionsDto? MasterDataPermissions
        {
            get
            {
                if (MasterDataPermissionsRaw == null || !MasterDataPermissionsRaw.IsBsonDocument) return null;
                try { return BsonSerializer.Deserialize<MasterDataPermissionsDto>(MasterDataPermissionsRaw.AsBsonDocument); }
                catch { return null; }
            }
            set => MasterDataPermissionsRaw = value == null ? BsonNull.Value : value.ToBsonDocument();
        }
    }

    private static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        Span<char> chars = stackalloc char[12];
        Span<byte> bytes = stackalloc byte[12];
        RandomNumberGenerator.Fill(bytes);
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return chars.ToString();
    }

    private static string HashPassword(string password)
    {
        const int iterations = 120_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);
        return $"pbkdf2${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var parts = stored.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], "pbkdf2", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations < 10_000)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
