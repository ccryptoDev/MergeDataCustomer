namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class PeriodHelperResponse
    {
        public string[] PeriodParts { get; set; }
        public int PriorMonth { get; set; }
        public int ZeroToSubtract { get; set; }
    }
}
