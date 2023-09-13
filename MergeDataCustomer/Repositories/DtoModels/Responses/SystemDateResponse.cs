namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class SystemDateResponse
    {
        public long ClientId { get; set; }
        public DateTime CurrentDate { get; set; }
        public int YearsBackward { get; set; }

        public DateTime LastAccUpdate { get; set; }
        public DateTime LastFIUpdate { get; set; }
    }
}
