﻿namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class ReportSummaryResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Visible { get; set; }
        public string Style { get; set; }

        public string? CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
