using MergeDataEntities.Enums;

namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class ReportConfigResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Visible { get; set; }
        public string Style { get; set; }
        public string SummaryStyle { get; set; }
        public ReportType ReportType { get; set; }
        public int ReportCategoryId { get; set; }
        public long? ClientId { get; set; }


        public bool ByTrendEnabled { get; set; }
        public bool ByStoreEnabled { get; set; }
        public bool StoreSelectorEnabled { get; set; }

        public bool DaySelectorEnabled { get; set; }
        public bool MonthSelectorEnabled { get; set; }
        public bool YearSelectorEnabled { get; set; }

        public bool MtdYtdEnabled { get; set; }
        public bool PriorMonthEnabled { get; set; }
        public bool DrilldownEnabled { get; set; }

        public long ViewId { get; set; }
        public string? strWhere { get; set; }
        public string? States { get; set; }
        public string? MessageBoard { get; set; }

        public string SourceOfReport { get; set; } //it can be the temporary php api, or our api

        public List<string> Columns { get; set; }
        public int Order { get; set; }

        public string? CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime ModifiedOn { get; set; }
    }
}
