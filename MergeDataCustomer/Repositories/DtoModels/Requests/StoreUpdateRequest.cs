namespace MergeDataCustomer.Repositories.DtoModels.Requests
{
    public class StoreUpdateRequest
    {
        public string Name { get; set; } = null!;
        public string ShortName { get; set; }
        public string AbbrName { get; set; }

        public string Address { get; set; } = null!;
        public string Zip { get; set; } = null!;


        public long? DmsId { get; set; } = null;
        public long? CmsId { get; set; } = null;
        public long? CrmId { get; set; } = null;
        public long? ErpId { get; set; } = null;
    }
}
