using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Schemas.Public;

namespace MergeDataCustomer.Application.Mappings
{
    public class ClientProfile : Profile
    {
        public ClientProfile()
        {
            CreateMap<Client, ClientResponse>()
                .ForMember(x => x.Id, opt => opt.MapFrom(src => src.ClientId))
                .ForMember(x => x.StoresCount, opt => opt.MapFrom(src => src.Stores.Count));
        }
    }
}
