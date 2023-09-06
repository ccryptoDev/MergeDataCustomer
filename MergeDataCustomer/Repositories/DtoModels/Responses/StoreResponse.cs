using System.ComponentModel.DataAnnotations.Schema;

namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class StoreResponse
    {
        public long StoreId { get; set; }
        public long ClientId { get; set; }

        public string Name { get; set; } = null!;
        public string ShortName { get; set; }
        public string AbbrName { get; set; }

        public DateTime StartDate { get; set; }
    }
}
