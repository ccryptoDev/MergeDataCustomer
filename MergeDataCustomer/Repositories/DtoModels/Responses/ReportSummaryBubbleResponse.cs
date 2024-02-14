namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class ReportSummaryBubbleResponse
    {
        public string LowerLimit { get; set; }
        public string UpperLimit { get; set; }
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }
}
