using System.ComponentModel.DataAnnotations;

namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class ReportLineValueAggrByStore
    {
        public long StoreId { get; set; }

        public long ReportLineId { get; set; }
        public string ColumnValue { get; set; }
    }
}
