using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Schemas.Public;

namespace MergeDataCustomer.Application.Mappings
{
    public class StoreGroupItemProfile : Profile
    {
        public StoreGroupItemProfile() {
            CreateMap<StoreGroupItem, StoreGroupItemResponse>().ReverseMap();
        }
    }
}
