using System.ComponentModel.DataAnnotations;

namespace ApiMango.Request
{
    public class LoginRequest
    {
        [Required]
        public string Login { get; set; }

        [Required]
        public string Password { get; set; }
    }
}