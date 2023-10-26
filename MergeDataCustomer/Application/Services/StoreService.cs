using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataCustomer.Repositories.DtoModels.Requests;
using MergeDataEntities.Schemas.Public;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;
using Microsoft.EntityFrameworkCore;
using MergeDataEntities.Enums;

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

        public async Task<Result<List<StoreGroupResponse>>> GetStoreGroups(long clientId)
        {            
            List<StoreGroup> storeGroups = _context.StoreGroups
                .Where(x => x.ClientId == clientId && x.IsActive)
                .Include(sg => sg.StoreGroupItems)
                .ToList();

            var result = _mapper.Map<List<StoreGroupResponse>>(storeGroups);

            return await Result<List<StoreGroupResponse>>.SuccessAsync(result);
        }

        public async Task<Result<StoreGroupResponse>> CreateStoreGroup(StoreGroupCreationRequest request)
        {
            var storeGroup = new StoreGroup
            {
                ClientId = request.ClientId,
                Name = request.Name,
                IsActive = true,
                StoreGroupLevel = (StoreGroupLevel)request.StoreGroupLevel
            };

            await _context.StoreGroups.AddAsync(storeGroup);

            await _context.SaveChangesAsync();

            foreach (var storeIdInt in request.StoreIds)
            {
                var storeGroupItem = new StoreGroupItem
                {
                    StoreId = storeIdInt,
                    StoreGroupId = storeGroup.Id
                };
                await _context.StoreGroupItems.AddAsync(storeGroupItem);
            }

            await _context.SaveChangesAsync();

            var result = _mapper.Map<StoreGroupResponse>(storeGroup);
            result.StoreGroupItems = _context.StoreGroupItems
                                             .Where(sgi => sgi.StoreGroupId == storeGroup.Id)
                                             .Select(sgi => new StoreGroupItemResponse {
                                                 StoreGroupId = sgi.StoreGroupId,
                                                 StoreId = sgi.StoreId
                                             })
                                             .ToList();

            return await Result<StoreGroupResponse>.SuccessAsync(result);
        }

        public async Task<Result<bool>> DeleteStoreGroup(long storeGroupId)
        {
            var storeGroup = await _context.StoreGroups.FirstOrDefaultAsync(x => x.Id == storeGroupId);

            if (storeGroup == null)
                return await Result<bool>.FailAsync("Store Group not found");

            storeGroup.IsActive = false;
           
            await _context.SaveChangesAsync();

            return await Result<bool>.SuccessAsync(true);
        }
    }
}
