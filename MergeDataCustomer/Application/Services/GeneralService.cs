using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;

namespace MergeDataCustomer.Application.Services
{
    public class GeneralService
    {
        private readonly RawContext _context;

        public GeneralService(RawContext context)
        {
            _context = context;
        }

        public async Task<Result<SystemDateResponse>> GetSystemDate(long clientId)
        {
            var clientData = _context.Clients.FirstOrDefault(x => x.ClientId == clientId && x.IsActive);
            if (clientData == null)
                return await Result<SystemDateResponse>.FailAsync("Client not found for given id");

            SystemDateResponse result = new SystemDateResponse()
            {
                ClientId = clientId,
                YearsBackward = clientData.YearsBackward,
                LastUpdateDate = clientData.LastUpdateDate
            };

            return await Result<SystemDateResponse>.SuccessAsync(result);
        }
    }
}
