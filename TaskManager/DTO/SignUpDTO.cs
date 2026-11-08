
using System.ComponentModel.DataAnnotations;

namespace TaskManager.DTO
{
    public record SignUpDTO
    {
        [Required(ErrorMessage = "The {0} field is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Username must be between 2 and 50 characters.")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        [RegularExpression(@"(?=.*?[A-Z])(?=.*?[a-z])(?=.*?\d)(?=.*?\W)^.{8,}$",
            ErrorMessage = "Password must contain at least one uppercase, one lowercase, " +
            "one digit, and one special character")]
        [StringLength(72, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 72 characters.")]
        public string? Password { get; set; }

        [Required(ErrorMessage = "The {0} field is required")]
        [StringLength(30, MinimumLength = 2, ErrorMessage = "Firstname must be between 2 and 30 characters.")]
        public string? Firstname { get; set; }

        [Required(ErrorMessage = "The {0} field is required")]
        [StringLength(40, MinimumLength = 2, ErrorMessage = "Lastname must be between 2 and 40 characters.")]
        public string? Lastname { get; set; }

        
        [StringLength(15, MinimumLength = 10, ErrorMessage = "Phone number must be at least 10 characters and not exceed 15 characters")]
        public string? PhoneNumber { get; set; }

        
    }
}
