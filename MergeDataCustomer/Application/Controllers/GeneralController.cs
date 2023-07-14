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
    public class GeneralController : ControllerBase
    {
        private readonly GeneralService _generalService;

        public GeneralController(GeneralService generalService)
        {
            _generalService = generalService;
        }

        [Route("GetSystemDate")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpGet]
        public async Task<IActionResult> GetSystemDate(long clientId)
        {
            var result = await _generalService.GetSystemDate(clientId);

            return Ok(result);
        }
    }
}
