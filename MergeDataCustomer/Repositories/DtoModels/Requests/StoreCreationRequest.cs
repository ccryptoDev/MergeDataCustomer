using System.ComponentModel.DataAnnotations.Schema;

namespace MergeDataCustomer.Repositories.DtoModels.Requests
{
    public class StoreCreationRequest
    {
        public long ClientId { get; set; }

        public string Name { get; set; } = null!;
        public string ShortName { get; set; }
        public string AbbrName { get; set; }

        public string Address { get; set; } = null!;
        public string Zip { get; set; } = null!;


        public long? DmsId { get; set; } = null;
        public long? CmsId { get; set; } = null;
        public long? CrmId { get; set; } = null;
        public long? ErpId { get; set; } = null;


        public DateTime StartDate { get; set; }
    }
}
