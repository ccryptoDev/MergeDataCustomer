using MergeDataEntities.Enums;

namespace MergeDataCustomer.Repositories.DtoModels.Requests
{
    public class GetReportListBySubSectionRequest
    {
        public long SubSectionId { get; set; }
        public long ClientId { get; set; }
        public List<long>? StoreId { get; set; }
        public string UserId { get; set; }
        public TargetOption Target { get; set; }
        public List<string> Period { get; set; }
    }
}
