using System.ComponentModel.DataAnnotations;

namespace TaskManager.DTO
{
    public record UpdateUserDTO
    {
        

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
