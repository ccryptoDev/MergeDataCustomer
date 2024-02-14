namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class SystemDateResponse
    {
        public long ClientId { get; set; }
        public int YearsBackward { get; set; }
        public DateTime LastUpdateDate { get; set; }
    }
}
