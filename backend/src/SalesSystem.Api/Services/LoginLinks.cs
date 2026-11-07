using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace SalesSystem.Api.Services;

public static class LoginLinks
{
    /// <summary>128 random bits, URL-safe (22 characters).</summary>
    public static string NewSlug() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
}
