using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Requests;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Schemas.Public;

namespace MergeDataCustomer.Application.Mappings
{
    public class StoreProfile : Profile
    {
        public StoreProfile()
        {
            CreateMap<Store, StoreResponse>().ReverseMap();
            CreateMap<StoreCreationRequest, Store>().ReverseMap();
            CreateMap<StoreUpdateRequest, Store>().ReverseMap();
        }
    }
}
