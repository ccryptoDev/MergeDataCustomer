using MergeDataCustomer.Application.Services;
using MergeDataCustomer.Helpers.Configuration;
using MergeDataCustomer.Repositories.DtoModels.Requests;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataImporter.Helpers.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MergeDataCustomer.Application.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "Layer3")]
    [ApiVersion(ApiVersioning.CurrentVersion)]
    public class ClientController : ControllerBase
    {
        private readonly ClientService _clientService;
        public ClientController(ClientService clientService)
        {
            _clientService = clientService;
        }

        [Route("GetClients")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpGet]
        public async Task<IActionResult> GetClients()
        {
            var auxResult = await _clientService.GetClients();
            List<ClientResponse> result = auxResult.Data;

            return Ok(result);
        }

        [Route("GetClient/{id}")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpGet]
        public async Task<IActionResult> GetClient(long id)
        {
            var auxResult = await _clientService.GetClient(id);
            ClientResponse result = auxResult.Data;

            return Ok(result);
        }

        [Route("CreateClient")]
        [Authorize(Policy = Permissions.User.Create)]
        [HttpPost]
        public async Task<IActionResult> CreateClient([FromBody] ClientRequest newClient)
        {
            var auxResult = await _clientService.CreateClient(newClient);
            ClientResponse result = auxResult.Data;

            return CreatedAtAction(nameof(CreateClient), new { id = result.Id }, result);
        }

        [Route("UpdateClient/{id}")]
        [Authorize(Policy = Permissions.User.Edit)]
        [HttpPut]
        public async Task<IActionResult> UpdateClient([FromRoute] long id, [FromBody] ClientRequest updatedClient)
        {
            var auxResult = await _clientService.UpdateClient(id, updatedClient);
            ClientResponse result = auxResult.Data;

            return Ok(result);
        }

        [Route("UpdateClientStatus/{id}/{isActive}")]
        [Authorize(Policy = Permissions.User.Edit)]
        [HttpPut]
        public async Task<IActionResult> UpdateClient([FromRoute] long id, [FromRoute] bool isActive)
        {
            var auxResult = await _clientService.UpdateClientStatus(id, isActive);
            ClientResponse result = auxResult.Data;

            return Ok(result);
        }
    }
}
