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
    // DTO для запроса, требующего токен
    public class TokenRequest { public string Token { get; set; } }

    // DTO для ответа с прогрессом игрока
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

    // DTO для запроса на сохранение прогресса
    public class SaveProgressRequest
    {
        public string Token { get; set; }
        public int LevelBuildIndex { get; set; }
        public int StarsCollected { get; set; }
        public int Score { get; set; }
    }

    // DTO для ответа таблицы лидеров
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

        private int? ValidateToken(string token)
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
                return int.Parse(jwtToken.Claims.First(x => x.Type == "id").Value);
            }
            catch { return null; }
        }

        [HttpPost("get-progress")]
        public async Task<IActionResult> GetPlayerProgress([FromBody] TokenRequest request)
        {
            var userId = ValidateToken(request.Token);
            if (userId == null) return Unauthorized("Invalid token");

            var user = await _context.Users
                .Include(u => u.LevelProgresses)
                .FirstOrDefaultAsync(u => u.Id == userId);

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

        [HttpPost("save-progress")]
        public async Task<IActionResult> SavePlayerProgress([FromBody] SaveProgressRequest request)
        {
            var userId = ValidateToken(request.Token);
            if (userId == null) return Unauthorized("Invalid token");

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null) return NotFound("User not found");

            var existingProgress = await _context.LevelProgresses
                .FirstOrDefaultAsync(lp => lp.UserId == userId && lp.LevelBuildIndex == request.LevelBuildIndex);

            if (existingProgress == null)
            {
                _context.LevelProgresses.Add(new LevelProgress
                {
                    UserId = userId.Value,
                    LevelBuildIndex = request.LevelBuildIndex,
                    StarsCollected = request.StarsCollected,
                    Score = request.Score
                });
            }
            else
            {
                // Обновляем, только если результат лучше (больше звезд или очков)
                if (request.StarsCollected > existingProgress.StarsCollected ||
                   (request.StarsCollected == existingProgress.StarsCollected && request.Score > existingProgress.Score))
                {
                    existingProgress.StarsCollected = request.StarsCollected;
                    existingProgress.Score = request.Score;
                }
            }

            await _context.SaveChangesAsync();

            // Пересчитываем TotalScore пользователя, суммируя лучшие результаты по каждому уровню
            user.TotalScore = await _context.LevelProgresses
                                    .Where(lp => lp.UserId == userId)
                                    .SumAsync(lp => lp.Score);

            await _context.SaveChangesAsync();

            return Ok(new { success = true, totalScore = user.TotalScore });
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard()
        {
            var leaderboard = await _context.Users
                .OrderByDescending(u => u.TotalScore)
                .Take(3) // Топ-3
                .Select(u => new LeaderboardEntryResponse
                {
                    Login = u.Login,
                    TotalScore = u.TotalScore
                })
                .ToListAsync();

            return Ok(leaderboard);
        }
    }
}