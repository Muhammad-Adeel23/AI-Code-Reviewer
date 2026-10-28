using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Models.Entities;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Services.JWT
{
    public class JwtService: IJwtService
    {
        private readonly IConfiguration _config;
        private readonly string _secret;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expiryMinutes;
        public JwtService(IConfiguration config)
        {
            _config = config;
            _secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret missing in appsettings");
            _issuer = config["Jwt:Issuer"] ?? "AICodeReviewer";
            _audience = config["Jwt:Audience"] ?? "AICodeReviewer";
            _expiryMinutes = int.Parse(config["Jwt:ExpiryMinutes"] ?? "60");
        }
        public string GenerateAccessToken(User user, string orgRole = "", int orgId = 0)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("userId", user.Id.ToString()),
            new Claim("role", user.Role),
            new Claim("orgRole",orgRole),   // ← ADD
            new Claim("orgId",orgId.ToString()), // ← ADD  
            new Claim("gitHubLogin",   user.GitHubLogin ?? ""), // ← ADD
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
        };

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ─── GENERATE REFRESH TOKEN ───────────────────────────────────────────────

        public string GenerateRefreshToken()
        {
            // Cryptographically secure random 64 bytes → base64 string
            var randomBytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(randomBytes);
        }

        // ─── VALIDATE TOKEN ───────────────────────────────────────────────────────

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_secret);

                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero   // No tolerance for expired tokens
                }, out _);

                return principal;
            }
            catch
            {
                return null;
            }
        }

        // ─── GET USER ID FROM TOKEN ───────────────────────────────────────────────

        public int GetUserIdFromToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "userId");
            return int.TryParse(userIdClaim?.Value, out var id) ? id : 0;
        }
    }
}
