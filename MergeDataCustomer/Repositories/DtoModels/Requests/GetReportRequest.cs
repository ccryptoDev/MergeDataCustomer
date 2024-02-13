using MergeDataEntities.Enums;

namespace MergeDataCustomer.Repositories.DtoModels.Requests
{
    public class GetReportRequest
    {
        public long ReportId { get; set; }
        public TargetOption Target { get; set; }
        public long ClientId { get; set; }
        public List<long>? StoreId { get; set; }
        public List<string> Period { get; set; }
        public bool? isYTD { get; set; } = false;
        public bool? ByTrend { get; set; } = false;
    }
}
