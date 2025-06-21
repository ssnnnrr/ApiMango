using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Text;
using ApiMango.DataBaseContext;
using ApiMango.Model;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ApiMango.Controllers
{
    #region DTOs
    public class TokenRequest { public string Token { get; set; } }

    public class PlayerProgressResponse
    {
        public int TotalStars { get; set; }
        public int TotalScore { get; set; }
        public List<LevelProgressDto> Levels { get; set; }
    }

    public class LevelProgressDto
    {
        public int LevelBuildIndex { get; set; }
        public int StarsCollected { get; set; }
        public int Score { get; set; }
    }

    public class SaveProgressRequest
    {
        public string Token { get; set; }
        public int LevelBuildIndex { get; set; }
        public int StarsCollected { get; set; }
        public int Score { get; set; }
    }

    public class LeaderboardEntryResponse
    {
        public string Login { get; set; }
        public int TotalScore { get; set; }
    }
    #endregion

    [ApiController]
    [Route("api/game")]
    public class GameController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public GameController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private string? ValidateAndGetLogin(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                return jwtToken.Claims.First(x => x.Type == "login").Value;
            }
            catch
            {
                return null;
            }
        }

        [HttpPost("get-progress")]
        public async Task<IActionResult> GetPlayerProgress([FromBody] TokenRequest request)
        {
            var userLogin = ValidateAndGetLogin(request.Token);
            if (userLogin == null) return Unauthorized("Invalid token");

            var user = await _context.Users
                .Include(u => u.LevelProgresses)
                .FirstOrDefaultAsync(u => u.Login == userLogin);

            if (user == null) return NotFound("User not found");

            var progresses = user.LevelProgresses.Select(lp => new LevelProgressDto
            {
                LevelBuildIndex = lp.LevelBuildIndex,
                StarsCollected = lp.StarsCollected,
                Score = lp.Score
            }).ToList();

            var response = new PlayerProgressResponse
            {
                Levels = progresses,
                TotalStars = progresses.Sum(p => p.StarsCollected),
                TotalScore = user.TotalScore
            };

            return Ok(response);
        }

        // GameController.cs (сервер)

        [HttpPost("save-progress")]
        public async Task<IActionResult> SavePlayerProgress([FromBody] SaveProgressRequest request)
        {
            var userLogin = ValidateAndGetLogin(request.Token);
            if (userLogin == null) return Unauthorized("Invalid token");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == userLogin);
            if (user == null) return NotFound("User not found");

            var userId = user.Id;
            var existingProgress = await _context.LevelProgresses
                .FirstOrDefaultAsync(lp => lp.UserId == userId && lp.LevelBuildIndex == request.LevelBuildIndex);

            bool hasProgressChanged = false;

            if (existingProgress == null)
            {
                _context.LevelProgresses.Add(new LevelProgress
                {
                    UserId = userId,
                    LevelBuildIndex = request.LevelBuildIndex,
                    StarsCollected = request.StarsCollected,
                    Score = request.Score
                });
                hasProgressChanged = true;
            }
            else
            {
                if (request.Score > existingProgress.Score)
                {
                    existingProgress.Score = request.Score;
                    existingProgress.StarsCollected = request.StarsCollected;
                    hasProgressChanged = true;
                }
            }

            if (hasProgressChanged)
            {
                await _context.SaveChangesAsync();

                user.TotalScore = await _context.LevelProgresses
                                        .Where(lp => lp.UserId == userId)
                                        .SumAsync(lp => lp.Score);

                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, totalScore = user.TotalScore });
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard()
        {
            var leaderboard = await _context.Users
                .OrderByDescending(u => u.TotalScore)
                .Take(3)
                .Select(u => new LeaderboardEntryResponse { Login = u.Login, TotalScore = u.TotalScore })
                .ToListAsync();

            return Ok(leaderboard);
        }
    }
}