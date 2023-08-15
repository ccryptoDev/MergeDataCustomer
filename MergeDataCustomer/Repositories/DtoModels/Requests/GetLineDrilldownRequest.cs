namespace MergeDataCustomer.Repositories.DtoModels.Requests
{
    public class GetLineDrilldownRequest
    {
        public int Level { get; set; }
        public long? ReportLineId { get; set; }
        public string? AccountNo { get; set; }
        public List<long>? StoreId { get; set; }
        public List<string> Period { get; set; }
    }
}
