using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Schemas.Public;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace MergeDataCustomer.Application.Services
{
    public class StoreGroupService
    {
        private readonly RawContext _context;
        private readonly IMapper _mapper;
        public StoreGroupService(RawContext context,
                             IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<Result<List<StoreGroupResponse>>> GetStoreGroups(long clientId)
        {           
            List<StoreGroup> storeGroups = _context.StoreGroups.Where(x => x.ClientId == clientId && x.IsActive).ToList();

            var result = _mapper.Map<List<StoreGroupResponse>>(storeGroups);

            return await Result<List<StoreGroupResponse>>.SuccessAsync(result);
        }

        public async Task<Result<StoreGroupResponse>> CreateStoreGroup(StoreGroupResponse newStoreGroup)
        {
            var storeGroup = _mapper.Map<StoreGroup>(newStoreGroup);

            _context.StoreGroups.Add(storeGroup);
            await _context.SaveChangesAsync();

            var result = _mapper.Map<StoreGroupResponse>(storeGroup);
            return await Result<StoreGroupResponse>.SuccessAsync(result);
        }

        public async Task<Result<bool>> DeleteStoreGroup(long storeGroupId)
        {
            var storeGroup = await _context.StoreGroups.FirstOrDefaultAsync(x => x.Id == storeGroupId);

            if (storeGroup == null)
                return await Result<bool>.FailAsync("Store Group not found");

            _context.StoreGroups.Remove(storeGroup);
            await _context.SaveChangesAsync();

            return await Result<bool>.SuccessAsync(true);
        }
    }
}
