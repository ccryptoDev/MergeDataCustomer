using MergeDataCustomer.Helpers.Configuration;
using MergeDataImporter.Application.Interfaces;
using MergeDataImporter.Repositories.Contracts;
using MergeDataImporter.Repositories.DtoModels.Requests.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MergeDataCustomer.Application.Controllers
{
    [ApiController]
    [ApiExplorerSettings(GroupName = "Layer4")]
    [ApiVersion(ApiVersioning.CurrentVersion)]
    [Route("api/identity/[controller]")]
    public class TokenController : ControllerBase
    {
        private readonly ITokenService _identityService;

        public TokenController(ITokenService identityService,
                               ICurrentUserService currentUserService)
        {
            _identityService = identityService;
        }

        [HttpPost("Login")]
        public async Task<ActionResult> Get(TokenRequest model)
        {
            var response = await _identityService.LoginAsync(model);
            if (!response.Succeeded)
                return Unauthorized(response);

            return Ok(response);
        }

        [HttpPost("RefreshToken")]
        public async Task<ActionResult> Refresh([FromBody] RefreshTokenRequest model)
        {
            var response = await _identityService.GetRefreshTokenAsync(model);
            if (!response.Succeeded)
                return Unauthorized(response);

            return Ok(response);
        }
    }
}
