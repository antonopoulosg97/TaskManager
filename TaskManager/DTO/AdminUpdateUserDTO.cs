using System.ComponentModel.DataAnnotations;

namespace TaskManager.DTO
{
    public record AdminUpdateUserDTO : UpdateUserDTO
    {
       

        [Required(ErrorMessage = "The {0} field is required")]
        public int? RoleId { get; set; }
    }
}
