using System.ComponentModel.DataAnnotations.Schema;

namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class StoreGroupResponse
    {
        public long StoreGroupId { get; set; }
        public long ClientId { get; set; }

        public string Name { get; set; } = null!;
        public int StoreGroupLevel { get; set; }

        public bool? IsActive { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
}
