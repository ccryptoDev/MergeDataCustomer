namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class ReportSummaryResponse
    {
        public ReportConfigResponse ReportConfig { get; set; }
        public List<ReportLineResponse> ReportLines { get; set; }
    }
}
