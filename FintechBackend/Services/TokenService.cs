using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FintechBackend.Models;
using Microsoft.IdentityModel.Tokens;

namespace FintechBackend.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly SymmetricSecurityKey _key;

    public TokenService(IConfiguration config)
    {
        _config = config;

        // Fetch the secret key set via dotnet user-secrets
        var secret = _config["JwtSettings:Secret"] 
            ?? throw new InvalidOperationException("JWT Secret Key is missing in configuration.");

        // SecurityKey requires a byte array
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public string CreateToken(User user)
    {
        // 1. Define the Claims (the payload payload stored inside the token)
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
            new Claim(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role)
        };

        // 2. Define Signing Credentials using HMAC SHA-512
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

        // 3. Describe the Token Parameters
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(60), // Set token lifespan (e.g., 60 minutes)
            SigningCredentials = creds,
            Issuer = _config["JwtSettings:Issuer"] ?? "FintechBackend",
            Audience = _config["JwtSettings:Audience"] ?? "FintechFrontend"
        };

        // 4. Create and return the encoded JWT string
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}