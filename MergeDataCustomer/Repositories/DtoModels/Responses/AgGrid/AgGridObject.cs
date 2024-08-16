namespace MergeDataCustomer.Repositories.DtoModels.Responses.AgGrid
{
    public class AgGridObject
    {
        public List<AgGridColumn> Columns { get; set; }
        public List<dynamic> ReportLines { get; set; }
    }
}
