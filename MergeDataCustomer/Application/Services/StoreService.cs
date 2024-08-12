using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataCustomer.Repositories.DtoModels.Requests;
using MergeDataEntities.Schemas.Public;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;
using Microsoft.EntityFrameworkCore;
using MergeDataEntities.Enums;
using MergeDataImporter.Repositories.Contracts;
using MergeDataCustomer.Repositories.DtoModels.Responses.AgGrid;
using MergeDataCustomer.Helpers;

namespace MergeDataCustomer.Application.Services
{
    public class StoreService
    {
        private readonly RawContext _context;
        private readonly IMapper _mapper;
        private readonly ICurrentUserService _currentUserService;

        public StoreService(RawContext context,
                             IMapper mapper,
                             ICurrentUserService currentUserService)
        {
            _context = context;
            _mapper = mapper;
            _currentUserService = currentUserService;
        }

        public async Task<Result<AgGridObject>> GetAllAgGrid(long clientId)
        {
            List<Store> stores = _context.Stores.Where(x => x.ClientId == clientId && x.IsActive).ToList();

            var result = _mapper.Map<List<StoreResponse>>(stores);

            var casted = AgGridFormatter.FormatResponse(result.Select(x => (dynamic)x).ToList());

            return await Result<AgGridObject>.SuccessAsync(casted);
        }

        public async Task<Result<List<StoreResponse>>> GetAll(long clientId)
        {
            List<Store> stores = _context.Stores.Where(x => x.ClientId == clientId && x.IsActive).ToList();

            var result = _mapper.Map<List<StoreResponse>>(stores);

            return await Result<List<StoreResponse>>.SuccessAsync(result);
        }

        public async Task<Result<StoreResponse>> Get(long storeId, long clientId)
        {
            var store = _context.Stores.FirstOrDefault(x => x.StoreId == storeId && x.ClientId == clientId && x.IsActive);

            var result = _mapper.Map<StoreResponse>(store);

            return await Result<StoreResponse>.SuccessAsync(result);
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

        public async Task<Result> AddAsync(StoreCreationRequest request)
        {
            var client = _context.Clients.FirstOrDefault(x => x.ClientId == request.ClientId);
            if(client == null)
                return (Result)await Result.FailAsync("Client not found for given ClientId");

            var store = _mapper.Map<Store>(request);

            store.StoreId = _context.Stores.Max(x => x.StoreId) + 1;
            store.StoreInternalId = "";
            store.FileInternalId = "";
            store.CityId = 1; //TODO: figure out what to do with this
            store.IsActive = false;
            store.CreatedOn = DateTime.UtcNow;
            store.CreatedBy = _currentUserService.UserName;

            await _context.Stores.AddAsync(store);
            await _context.SaveChangesAsync();

            return (Result)await Result.SuccessAsync("Store successfully created");
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

        public async Task<Result> UpdateAsync(long storeId, long clientId, StoreUpdateRequest request)
        {
            var currentRecord = _context.Stores.FirstOrDefault(x => x.StoreId == storeId);
            var client = _context.Clients.FirstOrDefault(x => x.ClientId == clientId);

            if (client == null || currentRecord == null)
                return (Result)await Result.FailAsync("Client or Store not found for given id");

            currentRecord.Name = request.Name;
            currentRecord.ShortName = request.ShortName;
            currentRecord.AbbrName = request.AbbrName;
            currentRecord.Address = request.Address;
            currentRecord.Zip = request.Zip;
            currentRecord.DmsId = request.DmsId;
            currentRecord.CmsId = request.CmsId;
            currentRecord.CrmId = request.CrmId;
            currentRecord.ErpId = request.ErpId;

            currentRecord.ModifiedOn = DateTime.UtcNow;
            currentRecord.ModifiedBy = _currentUserService.UserName;

            _context.Stores.Update(currentRecord);
            await _context.SaveChangesAsync();

            return (Result)await Result.SuccessAsync("Store successfully updated");
        }

        public async Task<Result> ChangeStatusAsync(long storeId, long clientId, bool active)
        {
            var store = await _context.Stores.FirstOrDefaultAsync(x => x.StoreId == storeId && x.ClientId == clientId && x.IsActive != active);

            if (store == null)
                return await Result<bool>.FailAsync("Store not found");

            store.IsActive = active;

            _context.Stores.Update(store);
            await _context.SaveChangesAsync();

            return (Result)await Result.SuccessAsync("Store status updated successfully");
        }

        public async Task<Result<bool>> DeleteStoreGroup(long storeGroupId)
        {
            var storeGroup = await _context.StoreGroups.FirstOrDefaultAsync(x => x.Id == storeGroupId);

            if (storeGroup == null)
                return await Result<bool>.FailAsync("Store Group not found");

            storeGroup.IsActive = false;
           
            _context.StoreGroups.Update(storeGroup);
            await _context.SaveChangesAsync();

            return await Result<bool>.SuccessAsync(true);
        }
    }
}
