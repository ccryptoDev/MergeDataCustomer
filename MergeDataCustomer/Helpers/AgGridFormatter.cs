using MergeDataCustomer.Repositories.DtoModels.Responses.AgGrid;
using System.Text.RegularExpressions;

namespace MergeDataCustomer.Helpers
{
    public static class AgGridFormatter
    {

        public static AgGridObject FormatResponse(List<dynamic> objects)
        {
            AgGridObject agGridResponse = new AgGridObject();
            agGridResponse.Columns = new List<AgGridColumn>();
            agGridResponse.ReportLines = new List<dynamic>();

            if(objects.FirstOrDefault() == null)
                return agGridResponse;

            var objProperties = objects.First().GetType().GetProperties();
            int i = 0;
            foreach (var property in objProperties)
            {
                AgGridColumn column = new AgGridColumn()
                {
                    ColIndex = i,
                    Visible = !property.Name.Contains("Id") ? true : false,
                    HeaderName = Regex.Replace(property.Name, "([a-z])([A-Z])", "$1 $2"), //add space when uppercase letter appears
                    Field = char.ToLower(property.Name[0]) + property.Name.Substring(1), //we make first letter lowercase to match columns name
                    Flex = 1,
                    ValueFormatter = "",
                    Type = "",
                    CellRenderer = "",
                    Filter = "",
                    DmVisible = true,
                    TotalRow = "",
                    RowGroup = "",
                    Hide = false,
                    AggFunc = ""
                };

                agGridResponse.Columns.Add(column);

                i++;                
            }

            foreach(var obj in objects)
            {
                agGridResponse.ReportLines.Add(obj);
            }

            return agGridResponse;
        }
    }
}
