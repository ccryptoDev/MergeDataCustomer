namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class ReportDetailResponse
    {
        public ReportConfigResponse ReportConfig { get; set; }
        public List<ReportLineResponse> ReportLines { get; set; }
    }
}
