using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Schemas.Public;

namespace MergeDataCustomer.Application.Mappings
{
    public class StoreGroupProfile : Profile
    {
        public StoreGroupProfile()
        {
            CreateMap<StoreGroup, StoreGroupResponse>().ReverseMap();
        }
    }
}
