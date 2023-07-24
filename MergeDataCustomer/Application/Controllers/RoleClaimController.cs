using AutoMapper;
using MergeDataCustomer.Helpers.Configuration;
using MergeDataImporter.Application.Interfaces;
using MergeDataImporter.Application.Mappings;
using MergeDataImporter.Application.Services;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;
using MergeDataImporter.Repositories.Contracts;
using MergeDataImporter.Repositories.DtoModels.Requests.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MergeDataCustomer.Application.Controllers
{
    [ApiController]
    [ApiExplorerSettings(GroupName = "Layer4")]
    [ApiVersion(ApiVersioning.CurrentVersion)]
    [Route("api/identity/[controller]")]
    public class RoleClaimController : ControllerBase
    {
        private readonly IRoleClaimService _roleClaimService;

        public RoleClaimController(ICurrentUserService currentUserService,
                                   RawContext context)
        {
            var mapperConfig = new MapperConfiguration(mc =>
            {
                mc.AddProfile(new RoleProfile());
                mc.AddProfile(new RoleClaimProfile());
            });
            Mapper mapper = new Mapper(mapperConfig);

            _roleClaimService = new RoleClaimService(mapper,
                                                     currentUserService,
                                                     context);
        }

        /// <summary>
        /// Get All Role Claims(e.g. Product Create Permission)
        /// </summary>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Role.View)]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var roleClaims = await _roleClaimService.GetAllAsync();
            return Ok(roleClaims);
        }

        /// <summary>
        /// Get All Role Claims By Id
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Role.View)]
        [HttpGet("GetAllByRoleId/{roleId}")]
        public async Task<IActionResult> GetAllByRoleId([FromRoute] string roleId)
        {
            var response = await _roleClaimService.GetAllByRoleIdAsync(roleId);
            return Ok(response);
        }

        /// <summary>
        /// Add a Role Claim
        /// </summary>
        /// <param name="request"></param>
        /// <returns>Status 200 OK </returns>
        [Authorize(Policy = Permissions.Role.Create)]
        [HttpPost("Create")]
        public async Task<IActionResult> Post(RoleClaimRequest request)
        {
            var response = await _roleClaimService.SaveAsync(request);
            return Ok(response);
        }

        /// <summary>
        /// Delete a Role Claim
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Role.Delete)]
        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _roleClaimService.DeleteAsync(id);
            return Ok(response);
        }
    }
}
