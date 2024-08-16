namespace MergeDataCustomer.Repositories.DtoModels.Responses.AgGrid
{
    public class AgGridColumn
    {
        public int ColIndex { get; set; }
        public bool Visible { get; set; }
        public string HeaderName { get; set; }
        public string Field { get; set; }
        public int Flex { get; set; }
        public string ValueFormatter { get; set; }
        public string Type { get; set; }
        public string CellRenderer { get; set; }
        public string Filter { get; set; }
        public bool DmVisible { get; set; }
        public string TotalRow { get; set; }
        public string RowGroup { get; set; }
        public bool Hide { get; set; }
        public string AggFunc { get; set; }
    }
}
