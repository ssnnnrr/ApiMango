using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Text;
using ApiMango.DataBaseContext;
using ApiMango.Model;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ApiMango.Request;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace ApiMango.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var user = _context.Users.FirstOrDefault(u => u.Login == request.Login && u.PasswordHash == request.Password);

            if (user == null)
            {
                return Unauthorized("Invalid credentials");
            }

            return Ok(new { token = user.Token, totalScore = user.TotalScore });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (_context.Users.Any(u => u.Login == request.Login))
                return BadRequest("Username already exists");

            var token = GenerateJwtTokenWithLogin(request.Login);

            var user = new User
            {
                Login = request.Login,
                PasswordHash = request.Password,
                TotalScore = 0,
                Token = token,
                TokenExpiry = DateTime.UtcNow.AddYears(1)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { token, totalScore = user.TotalScore });
        }
        
        private string GenerateJwtTokenWithLogin(string login)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim("login", login) }),
                Expires = DateTime.UtcNow.AddYears(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}