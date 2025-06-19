using System.ComponentModel.DataAnnotations;
using ApiMango.Model;
using Microsoft.EntityFrameworkCore;

[Index(nameof(Login), IsUnique = true)]
[Index(nameof(Token), IsUnique = true)]
public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Login { get; set; }

    [Required]
    [MaxLength(64)]
    public string PasswordHash { get; set; }

    [Required]
    public string Token { get; set; }

    public DateTime? TokenExpiry { get; set; }

    public int TotalScore { get; set; } = 0;

    public ICollection<LevelProgress> LevelProgresses { get; set; } = new List<LevelProgress>();
}