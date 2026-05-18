using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PartFinder.Models;

/// <summary>
/// Represents a single login session for a user device.
/// Stored in MongoDB "user_sessions" collection.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class LoginSessionRecord
{
    [BsonId]
    public ObjectId MongoId { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [BsonElement("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    [BsonElement("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [BsonElement("location")]
    public string Location { get; set; } = string.Empty;

    [BsonElement("loginTime")]
    public DateTime LoginTime { get; set; } = DateTime.UtcNow;

    [BsonElement("lastActivityTime")]
    public DateTime LastActivityTime { get; set; } = DateTime.UtcNow;

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("userAgent")]
    public string UserAgent { get; set; } = "PartFinder-WinUI/1.0";

    [BsonElement("osVersion")]
    public string OsVersion { get; set; } = string.Empty;
}
