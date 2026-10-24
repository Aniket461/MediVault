using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediVault.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace MediVault.Api.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "";

    public string Audience { get; set; } = "";

    public string Key { get; set; } = "";

    public int ExpiresMinutes { get; set; } = 480;
}

public sealed class TokenService(IConfiguration configuration)
{
    public string Create(UserAccount user)
    {
        var options = configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT settings are missing.");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.ExpiresMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
