namespace MergeDataCustomer.Repositories.DtoModels.Requests
{
    public class ReportByStoreRequest
    {
        public long ReportId { get; set; }
        public long ClientId { get; set; }
        public long StoreId { get; set; }
        public List<string> Period { get; set; }
    }
}
