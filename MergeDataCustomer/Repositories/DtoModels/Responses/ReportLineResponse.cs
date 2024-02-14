using System.ComponentModel.DataAnnotations;

namespace MergeDataCustomer.Repositories.DtoModels.Responses
{
    public class ReportLineResponse
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public string Name { get; set; }
        public string NameStyle { get; set; }

        /// <summary>
        /// Possible 'Style' values:
        /// -final (meaning final and row of the report)
        /// -highlighted (the style of this row is like the final one, but could be any row on the table)
        /// </summary>
        public string Style { get; set; }

        public bool Visible { get; set; }
        public bool Drillable { get; set; }

        public long? StoreId { get; set; }

        [StringLength(7)] //Format: MM-YYYY
        public string Period { get; set; }


        /// <summary>
        /// Possible 'TypeFormats' values:
        /// text, subtitle, dollar, dollar_v, double, double_v, integer, integer_v, indented_number, percentage, percentege_v, icon, image, link
        /// 
        /// the TypeFormats that finishes with “_v”, are variances of the type indicated, meaning that must be shown as variances
        /// (with upper arrow, neutral icon/color or lower arrow).
        /// --
        /// we can send up to 2 possible value of valuesTypeFormat in for one same column, separated by “,” or “|” (comma or pipe).
        /// Comma: the column will have 2 values (also separated by comma), and they must be placed one aside of the other.
        /// Pipe: the column will have 2 values (also separated by pipe), and they must be placed one over the other.
        /// </summary>
        public List<string> TypeFormats { get; set; }

        public List<string> Values { get; set; }
    }
}
