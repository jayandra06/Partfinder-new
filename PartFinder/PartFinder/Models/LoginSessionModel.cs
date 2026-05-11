using System;

namespace PartFinder.Models;

public sealed class LoginSessionModel
{
    public string IpAddress { get; set; } = "Not Available";
    public string Location { get; set; } = "Not Available";
    public string Coordinates { get; set; } = "Not Available";
    public string Browser { get; set; } = "Not Available";
    public string OsVersion { get; set; } = "Not Available";
    public string DeviceName { get; set; } = "Not Available";
    public string LoginTime { get; set; } = "Not Available";
}
