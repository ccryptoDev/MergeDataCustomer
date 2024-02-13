using MergeDataEntities.Enums;

namespace MergeDataCustomer.Repositories.DtoModels.Requests
{
    public class GetReportSummaryRequest
    {
        public long ReportId { get; set; }
        public long ClientId { get; set; }
        public List<long>? StoreId { get; set; }
        public TargetOption Target { get; set; }
        public List<string> Period { get; set; }

        //if the summary has selects with different options, here should come the selected options by index, separated by comma
        public List<int>? SelectedOptions { get; set; }
    }
}
