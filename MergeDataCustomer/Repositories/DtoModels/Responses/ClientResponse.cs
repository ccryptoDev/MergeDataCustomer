namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class ClientResponse
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime LastUpdateDate { get; set; }
        public bool IsActive { get; set; }
        public int StoresCount { get; set; }
    }
}
