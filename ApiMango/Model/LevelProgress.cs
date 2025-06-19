using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiMango.Model
{
    public class LevelProgress
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LevelBuildIndex { get; set; }

        [Required]
        public int StarsCollected { get; set; }

        [Required]
        public int Score { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; }
    }
}