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

    private readonly ILocalSetupContext _setupContext;

    public MongoOrgUserDirectoryService(ILocalSetupContext setupContext)
    {
        _setupContext = setupContext;
    }

    private IMongoCollection<OrgAppUserDoc>? TryGetCollection()
    {
        if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cs = MongoConnectionStringUtil.Normalize(raw);
        var url = new MongoUrl(cs);
        var client = new MongoClient(cs);
        var db = client.GetDatabase(url.DatabaseName ?? "admin");
        return db.GetCollection<OrgAppUserDoc>(CollectionName);
    }

    public async Task<IReadOnlyList<OrgAppUserSummary>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection();
        if (coll is null)
        {
            return Array.Empty<OrgAppUserSummary>();
        }

        var list = await coll.Find(FilterDefinition<OrgAppUserDoc>.Empty)
            .SortByDescending(d => d.InvitedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return list.Select(Map).ToList();
    }

    public async Task<OrgAppUserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection();
        if (coll is null)
        {
            return null;
        }

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
        var coll = TryGetCollection()
                   ?? throw new InvalidOperationException(
                       "No tenant database is configured. Complete setup and ensure setup-state.json contains dbUri.");

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
        var coll = TryGetCollection();
        if (coll is null)
        {
            return false;
        }

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
        var coll = TryGetCollection();
        if (coll is null || !ObjectId.TryParse(id, out var objectId))
        {
            return false;
        }

        var update = Builders<OrgAppUserDoc>.Update
            .Set(x => x.Name, name.Trim())
            .Set(x => x.Role, role.Trim())
            .Set(x => x.PartsAllTemplates, partsAllTemplates)
            .Set(x => x.AllowedTemplateIds, allowedTemplateIds
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList())
            .Set(x => x.TemplatePermissions, templatePermissions)
            .Set(x => x.MasterDataPermissions, masterDataPermissions);

        var result = await coll.UpdateOneAsync(
            x => x.Id == objectId,
            update,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection();
        if (coll is null || !ObjectId.TryParse(id, out var objectId))
        {
            return false;
        }

        var result = await coll.DeleteOneAsync(
            x => x.Id == objectId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.DeletedCount > 0;
    }

    private static OrgAppUserSummary Map(OrgAppUserDoc d) =>
        new()
        {
            Id = d.Id.ToString(),
            Name = d.Name,
            Email = d.Email,
            Role = d.Role,
            PartsAllTemplates = d.PartsAllTemplates,
            AllowedTemplateIds = d.AllowedTemplateIds,
            InvitedAtUtc = d.InvitedAtUtc,
<<<<<<< HEAD
            Status = string.IsNullOrWhiteSpace(d.TemporaryPasswordHash) ? "Active" : "Pending",
=======
            TemplatePermissions = d.TemplatePermissions,
            MasterDataPermissions = d.MasterDataPermissions,
>>>>>>> 7fce8f5 (feat: implement granular user access control and permissions management system for organization templates and master data)
        };

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
