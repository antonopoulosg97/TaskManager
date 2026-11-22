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

        /// <summary>
        /// Registers a new user account. Anyone can register, and new users get the EMPLOYEE role.
        /// </summary>
        /// <param name="signupDTO">The new user's username, password, first name, last name and optional phone number.</param>
        /// <returns>The created user.</returns>
        /// <response code="201">Returns the created user.</response>
        /// <response code="400">If the request body is invalid.</response>
        /// <response code="409">If the username is already taken.</response>
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

        /// <summary>
        /// Authenticates a user and returns a JWT token if credentials are valid.
        /// </summary>
        /// <param name="signInDTO">The user's username and password.</param>
        /// <returns>A JWT token and its expiry time.</returns>
        /// <response code="200">Returns the JWT token and its expiry time.</response>
        /// <response code="400">If the request body is invalid.</response>
        /// <response code="401">If the username or password is incorrect.</response>

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
