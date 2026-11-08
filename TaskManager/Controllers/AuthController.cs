using Microsoft.AspNetCore.Mvc;
using TaskManager.DTO;
using TaskManager.Services;

namespace TaskManager.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;

        public AuthController(IUserService userService)
        {
            _userService = userService;
        }


        [HttpPost("register/user")]
        [ProducesResponseType(typeof(UserReadOnlyDTO),StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]

        public async Task<IActionResult> RegisterUser([FromBody] SignUpDTO signupDTO)
        {
            var createdUser = await _userService.SignUpAsync(signupDTO);

            return CreatedAtAction(
                actionName: nameof(UsersController.GetUserByUuid),
                controllerName: "Users",
                routeValues: new { uuid = createdUser.Uuid },
                value: createdUser
                );
                
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(TokenDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<TokenDTO>> Login([FromBody] SignInDTO signInDTO)
        {
            TokenDTO token = await _userService.SignInAsync(signInDTO);
            return Ok(token);
        }
           
    }
}
