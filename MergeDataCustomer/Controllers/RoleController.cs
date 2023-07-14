using AutoMapper;
using MergeDataCustomer.Helpers.Configuration;
using MergeDataEntities.Identity;
using MergeDataImporter.Application.Interfaces;
using MergeDataImporter.Application.Mappings;
using MergeDataImporter.Application.Services;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;
using MergeDataImporter.Repositories.Contracts;
using MergeDataImporter.Repositories.DtoModels.Requests.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MergeDataCustomer.Controllers
{
    [ApiController]
    [Route("api/identity/[controller]")]
    [ApiVersion(ApiVersioning.CurrentVersion)]
    [ApiExplorerSettings(GroupName = "Layer2")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(RoleManager<Role> roleManager,
                            UserManager<User> userManager,
                            ICurrentUserService currentUserService,
                            RawContext context)
        {
            var mapperConfig = new MapperConfiguration(mc => {
                mc.AddProfile(new RoleProfile());
                mc.AddProfile(new RoleClaimProfile());
            });
            Mapper mapper = new Mapper(mapperConfig);

            IRoleClaimService _roleClaimService = new RoleClaimService(mapper,
                                                     currentUserService,
                                                     context);

            _roleService = new RoleService(roleManager,
                                            mapper,
                                            userManager,
                                            _roleClaimService,
                                            currentUserService);
        }

        /// <summary>
        /// Get All Roles (basic, admin etc.)
        /// </summary>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Role.View)]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var roles = await _roleService.GetAllAsync();
            return Ok(roles);
        }

        /// <summary>
        /// Add a Role
        /// </summary>
        /// <param name="request"></param>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Role.Create)]
        [HttpPost("Create")]
        public async Task<IActionResult> Post(RoleRequest request)
        {
            var response = await _roleService.SaveAsync(request);
            return Ok(response);
        }

        /// <summary>
        /// Delete a Role
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Role.Delete)]
        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var response = await _roleService.DeleteAsync(id);
            return Ok(response);
        }

        /// <summary>
        /// Get Permissions By Role Id
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns>Status 200 Ok</returns>
        [Authorize(Policy = Permissions.Role.View)]
        [HttpGet("GetPermissions/{roleId}")]
        public async Task<IActionResult> GetPermissionsByRoleId([FromRoute] string roleId)
        {
            var response = await _roleService.GetAllPermissionsAsync(roleId);
            return Ok(response);
        }

        /// <summary>
        /// Edit a Role Claim
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [Authorize(Policy = Permissions.Role.Edit)]
        [HttpPut("UpdatePermissions/update")]
        public async Task<IActionResult> Update(PermissionRequest model)
        {
            var response = await _roleService.UpdatePermissionsAsync(model);
            return Ok(response);
        }
    }
}
