using MergeDataCustomer.Application.Services;
using MergeDataCustomer.Helpers.Configuration;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataImporter.Helpers.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MergeDataCustomer.Application.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "Layer4")]
    [ApiVersion(ApiVersioning.CurrentVersion)]
    public class StoreGroupController : ControllerBase
    {
        private readonly StoreGroupService _storeGroupService;

        public StoreGroupController(StoreGroupService storeGroupService)
        {
            _storeGroupService = storeGroupService;
        }

        [Route("GetStoreGroups")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpGet]
        public async Task<IActionResult> GetStoreGroups(long clientId)
        {
            var auxResult = await _storeGroupService.GetStoreGroups(clientId);
            List<StoreGroupResponse> result = auxResult.Data;

            return Ok(result);
        }

        [Route("CreateStoreGroup")]
        [Authorize(Policy = Permissions.User.Edit)]
        [HttpPost]
        public async Task<IActionResult> CreateStoreGroup([FromBody] StoreGroupResponse newStoreGroup)
        {
            var auxResult = await _storeGroupService.CreateStoreGroup(newStoreGroup);
            StoreGroupResponse result = auxResult.Data;

            return CreatedAtAction(nameof(GetStoreGroups), new { id = result.StoreGroupId }, result);
        }

        [Route("DeleteStoreGroup/{id}")]
        [Authorize(Policy = Permissions.User.Edit)]
        [HttpDelete]
        public async Task<IActionResult> DeleteStoreGroup(long id)
        {
            var result = await _storeGroupService.DeleteStoreGroup(id);

            if (result.Succeeded)
                return NoContent();
            else
                return NotFound();
        }
    }
}
