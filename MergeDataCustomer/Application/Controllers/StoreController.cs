using MergeDataCustomer.Application.Services;
using MergeDataCustomer.Helpers.Configuration;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataCustomer.Repositories.DtoModels.Requests;
using MergeDataImporter.Helpers.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MergeDataCustomer.Repositories.DtoModels.Responses.AgGrid;

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
        public async Task<IActionResult> GetStores(long clientId, bool agGridFormat=false)
        {
            if(agGridFormat)
            {
                var auxResult = await _storeService.GetAllAgGrid(clientId);
                AgGridObject result = auxResult.Data;

                return Ok(result);
            }
            else
            {
                var auxResult = await _storeService.GetAll(clientId);
                List<StoreResponse> result = auxResult.Data;

                return Ok(result);
            }
        }

        [Route("GetStore/{storeId}/{clientId}")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpGet]
        public async Task<IActionResult> GetStore(long storeId, long clientId)
        {
            var auxResult = await _storeService.Get(storeId, clientId);
            StoreResponse result = auxResult.Data;

            return Ok(result);
        }

        [Route("GetStoreGroups/{clientId}")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpGet]
        public async Task<IActionResult> GetStoreGroups(long clientId)
        {
            var auxResult = await _storeService.GetStoreGroups(clientId);
            List<StoreGroupResponse> result = auxResult.Data;

            return Ok(result);
        }

        [Authorize(Policy = Permissions.User.Create)]
        [HttpPost("CreateStore")]
        public async Task<IActionResult> CreateStore(StoreCreationRequest request)
        {
            var result = await _storeService.AddAsync(request);

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

        [Authorize(Policy = Permissions.User.Edit)]
        [HttpPut("UpdateStore/{storeId}/{clientId}")]
        public async Task<IActionResult> Update(long storeId, long clientId, StoreUpdateRequest request)
        {
            var result = await _storeService.UpdateAsync(storeId, clientId, request);

            return Ok(result);
        }

        [Authorize(Policy = Permissions.User.Edit)]
        [HttpPut("UpdateStoreStatus/{storeId}/{clientId}/{isActive}")]
        public async Task<IActionResult> UpdateStoreStatus(long storeId, long clientId, bool isActive)
        {
            var result = await _storeService.ChangeStatusAsync(storeId, clientId, isActive);

            return Ok(result);
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
