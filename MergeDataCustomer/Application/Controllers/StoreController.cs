using MergeDataCustomer.Application.Services;
using MergeDataCustomer.Helpers.Configuration;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataCustomer.Repositories.DtoModels.Requests;
using MergeDataImporter.Helpers.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MergeDataCustomer.Application.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "Layer3")]
    [ApiVersion(ApiVersioning.CurrentVersion)]
    public class StoreController : ControllerBase
    {
        private readonly StoreService _storeService;

        public StoreController(StoreService storeService)
        {
            _storeService = storeService;
        }

        [Route("GetStores")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpGet]
        public async Task<IActionResult> GetStores(long clientId)
        {
            var auxResult = await _storeService.GetStores(clientId);
            List<StoreResponse> result = auxResult.Data;

            return Ok(result);
        }

        [Route("GetStoreGroups")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpGet]
        public async Task<IActionResult> GetStoreGroups(long clientId)
        {
            var auxResult = await _storeService.GetStoreGroups(clientId);
            List<StoreGroupResponse> result = auxResult.Data;

            return Ok(result);
        }

        [Route("CreateStoreGroup")]
        [Authorize(Policy = Permissions.User.Create)]
        [HttpPost]
        public async Task<IActionResult> CreateStoreGroup([FromBody] StoreGroupCreationRequest newStoreGroup)
        {
            var auxResult = await _storeService.CreateStoreGroup(newStoreGroup);
            StoreGroupResponse result = auxResult.Data;

            return CreatedAtAction(nameof(GetStoreGroups), new { id = result.StoreGroupId }, result);
        }

        [Route("DeleteStoreGroup/{id}")]
        [Authorize(Policy = Permissions.User.Delete)]
        [HttpDelete]
        public async Task<IActionResult> DeleteStoreGroup(long id)
        {
            var result = await _storeService.DeleteStoreGroup(id);

            if (result.Succeeded)
                return NoContent();
            else
                return NotFound();
        }
    }
}
