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

    }
}
