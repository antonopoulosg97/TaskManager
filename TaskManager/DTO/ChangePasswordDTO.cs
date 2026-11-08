using System.ComponentModel.DataAnnotations;

namespace TaskManager.DTO
{
    public record ChangePasswordDTO
    {
        
        [Required(ErrorMessage = "The {0} field is required.")]
        public string? OldPassword { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        [StringLength(72, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 72 characters.")]
        [RegularExpression(@"(?=.*?[A-Z])(?=.*?[a-z])(?=.*?\d)(?=.*?\W)^.{8,}$",
            ErrorMessage = "Password must contain at least one uppercase, one lowercase, one digit, and one special character")]
        public string? NewPassword { get; set; }
    }
}

