using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Schemas.Reports;

namespace MergeDataCustomer.Application.Mappings
{
    public class ReportProfile : Profile
    {
        public ReportProfile()
        {
            CreateMap<Report, ReportConfigResponse>().ReverseMap();
            CreateMap<Report, ReportContractResponse>().ReverseMap();
            CreateMap<Report, ReportBasicDataResponse>().ReverseMap();
            CreateMap<ReportLine, ReportLineResponse>().ReverseMap();
        }
    }
}
