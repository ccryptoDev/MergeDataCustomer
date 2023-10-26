using System.ComponentModel.DataAnnotations.Schema;
using MergeDataEntities.Enums;

namespace MergeDataCustomer.Repositories.DtoModels.Requests
{
    public class StoreGroupCreationRequest
    {
        public StoreGroupLevel StoreGroupLevel { get; set; }
        public long ClientId { get; set; }
        public string Name { get; set; }
        public List<int> StoreIds { get; set; }
    }
}
