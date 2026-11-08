using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskManager.DTO;
using TaskManager.Models;

namespace TaskManager.Security
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public TokenDTO GenerateToken(User user)
        {
            
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("Jwt:ExpiresInMinutes"));

            
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Uuid.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, user.Role.Name)
            };

            
            claims.AddRange(user.Role.Capabilities.Select(c => new Claim("capability", c.Name)));

            
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                Expires = expiresAt,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            
            return new TokenDTO
            {
                Token = new JsonWebTokenHandler().CreateToken(descriptor),
                ExpiresAt = expiresAt
            };
        }
    }
}