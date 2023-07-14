using MergeDataCustomer.Helpers.Configuration;
using MergeDataImporter.Application.Interfaces;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Contracts;
using MergeDataImporter.Repositories.DtoModels.Requests.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MergeDataCustomer.Controllers
{
    [ApiController]
    [Route("api/identity/[controller]")]
    [ApiExplorerSettings(GroupName = "Layer2")]
    [ApiVersion(ApiVersioning.CurrentVersion)]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ICurrentUserService _currentUser;

        public AccountController(IAccountService accountService, ICurrentUserService currentUser)
        {
            _accountService = accountService;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Update Profile
        /// </summary>
        /// <param name="model"></param>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Account.Edit)]
        [HttpPut("Update")]
        public async Task<ActionResult> UpdateProfile(UpdateProfileRequest model)
        {
            var response = await _accountService.UpdateProfileAsync(model, _currentUser.UserId);
            return Ok(response);
        }

        /// <summary>
        /// Change Password
        /// </summary>
        /// <param name="model"></param>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Account.Edit)]
        [HttpPut("ChangePassword")]
        public async Task<ActionResult> ChangePassword(ChangePasswordRequest model)
        {
            var response = await _accountService.ChangePasswordAsync(model, _currentUser.UserId);
            return Ok(response);
        }
    }
}
