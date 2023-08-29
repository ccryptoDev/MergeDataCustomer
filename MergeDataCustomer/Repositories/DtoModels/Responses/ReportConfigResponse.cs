namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class ReportConfigResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Visible { get; set; }
        public string Style { get; set; }

        public long? ClientId { get; set; }

        public List<string> Columns { get; set; }
    }
}
