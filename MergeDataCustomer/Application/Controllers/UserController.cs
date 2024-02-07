using AutoMapper;
using MergeDataCustomer.Helpers.Configuration;
using MergeDataImporter.Application.Mappings;
using MergeDataImporter.Application.Services;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Contracts;
using MergeDataImporter.Repositories.DtoModels.Requests.Identity;
using MergeDataEntities.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MergeDataImporter.Repositories.Context;

namespace MergeDataCustomer.Application.Controllers
{
    [ApiController]
    [ApiExplorerSettings(GroupName = "Layer3")]
    [ApiVersion(ApiVersioning.CurrentVersion)]
    [Route("api/identity/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController(IMailService mailService,
                            RawContext context,
                            UserManager<User> userManager,
                            RoleManager<Role> roleManager,
                            ICurrentUserService currentUserService)
        {
            var mapperConfig = new MapperConfiguration(mc =>
            {
                mc.AddProfile(new UserProfile());
            });
            Mapper mapper = new Mapper(mapperConfig);

            _userService = new UserService(mailService,
                                            mapper,
                                            context,
                                            userManager,
                                            roleManager,
                                            currentUserService);
        }

        [Authorize(Policy = Permissions.User.View)]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllAsync();
            return Ok(users);
        }

        [Authorize(Policy = Permissions.User.View)]
        [HttpGet("GetById/{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userService.GetAsync(id);
            return Ok(user);
        }

        [HttpPost("Register")]
        [Authorize(Policy = Permissions.User.Create)]
        public async Task<IActionResult> RegisterAsync(RegisterRequest request)
        {
            var origin = Request.Headers["origin"];
            return Ok(await _userService.RegisterAsync(request, origin));
        }

        [HttpPost("ToggleUserStatus")]
        [Authorize(Policy = Permissions.User.Edit)]
        public async Task<IActionResult> ToggleUserStatusAsync(ToggleUserStatusRequest request)
        {
            return Ok(await _userService.ToggleUserStatusAsync(request));
        }

        [HttpGet("GetRoles/{id-user}")]
        [Authorize(Policy = Permissions.Role.View)]
        public async Task<IActionResult> GetRolesAsync(string id)
        {
            var userRoles = await _userService.GetRolesAsync(id);
            return Ok(userRoles);
        }

        [HttpPut("UpdateRoles/{id}")]
        [Authorize(Policy = Permissions.Role.Edit)]
        public async Task<IActionResult> UpdateRolesAsync(UpdateUserRolesRequest request)
        {
            return Ok(await _userService.UpdateRolesAsync(request));
        }

        //[HttpGet("ConfirmEmail")]
        //[AllowAnonymous]
        //public async Task<IActionResult> ConfirmEmailAsync([FromQuery] string userId, [FromQuery] string code)
        //{
        //    return Ok(await _userService.ConfirmEmailAsync(userId, code));
        //}

        [HttpPost("ForgotPassword")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            var origin = Request.Headers["origin"];
            return Ok(await _userService.ForgotPasswordAsync(request, origin));
        }

        [HttpPost("ValidateResetCode")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateResetCode(ResetCodeRequest request)
        {
            return Ok(await _userService.ValidateResetCode(request));
        }

        [HttpPost("ResetPassword")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPasswordAsync(ResetPasswordRequest request)
        {
            return Ok(await _userService.ResetPasswordAsync(request));
        }

        [HttpPost("ChangePassword")]
        [Authorize(Policy = Permissions.User.Edit)]
        public async Task<IActionResult> ChangePassword(Guid userId, ChangePasswordRequest request)
        {
            return Ok(await _userService.ChangePassword(userId, request));
        }

        [HttpPost("AssociateClient")]
        [Authorize(Policy = Permissions.Administrator.Edit)]
        public async Task<IActionResult> AssociateClient(string id, long clientId)
        {
            var result = await _userService.AssociateClient(id, clientId);
            if (result.Succeeded)
                return Ok(result.Messages);

            return BadRequest(result.Messages);
        }

        [HttpPost("DisassociateClient")]
        [Authorize(Policy = Permissions.Administrator.Edit)]
        public async Task<IActionResult> DisassociateClient(string id, long clientId)
        {
            var result = await _userService.DisassociateClient(id, clientId);
            if (result.Succeeded)
                return Ok(result.Messages);

            return BadRequest(result.Messages);
        }

        [HttpPost("AssociateStore")]
        [Authorize(Policy = Permissions.Administrator.Edit)]
        public async Task<IActionResult> AssociateStore(string id, long storeId)
        {
            var result = await _userService.AssociateStore(id, storeId);
            if (result.Succeeded)
                return Ok(result.Messages);

            return BadRequest(result.Messages);
        }

        [HttpPost("DisassociateStore")]
        [Authorize(Policy = Permissions.Administrator.Edit)]
        public async Task<IActionResult> DisassociateStore(string id, long storeId)
        {
            var result = await _userService.DisassociateStore(id, storeId);
            if (result.Succeeded)
                return Ok(result.Messages);

            return BadRequest(result.Messages);
        }
    }
}
