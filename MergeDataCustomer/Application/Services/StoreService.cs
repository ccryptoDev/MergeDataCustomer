using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Schemas.Public;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;

namespace MergeDataCustomer.Application.Services
{
    public class StoreService
    {
        private readonly RawContext _context;
        private readonly IMapper _mapper;

        public StoreService(RawContext context,
                             IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<Result<List<StoreResponse>>> GetStores(long clientId)
        {
            List<Store> stores = _context.Stores.Where(x => x.ClientId == clientId && x.IsActive).ToList();

            var result = _mapper.Map<List<StoreResponse>>(stores);

            return await Result<List<StoreResponse>>.SuccessAsync(result);
        }


    }
}
