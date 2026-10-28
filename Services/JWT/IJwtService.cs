using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Services.JWT
{
    public interface IJwtService
    {
        string GenerateAccessToken(User user, string orgRole = "", int orgId = 0);
        string GenerateRefreshToken();
        ClaimsPrincipal? ValidateToken(string token);
        int GetUserIdFromToken(string token);
    }
}
