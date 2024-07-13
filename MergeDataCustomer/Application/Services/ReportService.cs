using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Requests;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Enums;
using MergeDataEntities.Helpers;
using MergeDataEntities.Schemas.Public;
using MergeDataEntities.Schemas.Reports;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;
using MergeDataImporter.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Office.Interop.Excel;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MergeDataCustomer.Application.Services
{
    public class ReportService
    {
        private readonly RawContext _context;
        private readonly IMapper _mapper;
        private readonly ICurrentUserService _currentUserService;

        public ReportService(RawContext context,
                             IMapper mapper,
                             ICurrentUserService currentUserService)
        {
            _context = context;
            _mapper = mapper;
            _currentUserService = currentUserService;
        }

        private async Task<bool> UserIsAdministrator(string userId)
        {
            var userRoles = await _context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
            var rolesOfUser = await _context.Roles.Where(r => userRoles.Select(ur => ur.RoleId).Contains(r.Id)).ToListAsync();
            bool isAdministrator = rolesOfUser.Any(r => r.Name == "Administrator");

            return isAdministrator;
        }

        public async Task<Result<List<Section>>> GetSections(string userId)
        {
            List<Section> sections = new List<Section>();
            bool isAdministrator = await UserIsAdministrator(userId);

            if (isAdministrator)
            {
                sections = await _context.Sections.Where(s => s.IsActive).ToListAsync();
            }
            else //if the user isnt admin, we retrive only the sections that the user has access to
            {
                var userSections = await _context.UserSections.Where(us => us.UserId == userId).Select(x => x.SectionId).ToListAsync();
                sections = await _context.Sections.Where(s => s.IsActive && userSections.Contains(s.Id)).ToListAsync();
            }

            return await Result<List<Section>>.SuccessAsync(sections);
        }

        public async Task<Result<List<SubSection>>> GetSubSections(long sectionId, string userId)
        {
            List<SubSection> subSections = new List<SubSection>();
            bool isAdministrator = await UserIsAdministrator(userId);

            if (isAdministrator)
            {
                subSections = await _context.SubSections.Where(s => s.IsActive && s.SectionId == sectionId).ToListAsync();
            }
            else //if the user isnt admin, we retrive only the subSections that the user has access to
            {
                var userSubSections = await _context.UserSubSections.Where(uss => uss.UserId == userId).Select(x => x.SubSectionId).ToListAsync();
                subSections = await _context.SubSections.Where(s => s.IsActive && userSubSections.Contains(s.Id) && s.SectionId == sectionId).ToListAsync();
            }

            return await Result<List<SubSection>>.SuccessAsync(subSections);
        }

        public async Task<Result<List<ReportSummaryResponse>>> GetReportListBySubSection(long subSectionId, long clientId, long storeId, string userId, List<string>? period, TargetOption target)
        {
            List<Report> reports = new List<Report>();
            List<Report> baseReports = new List<Report>();

            List<long> subSectionReportIds = _context.SubSectionReports.Where(x => x.SubSectionId == subSectionId).Select(x => x.ReportId).ToList();

            bool isAdministrator = await UserIsAdministrator(userId);

            if (isAdministrator)
            {
                reports = _context.Reports.Where(r => r.IsActive && subSectionReportIds.Contains(r.Id) && r.ClientId == clientId).ToList();
                baseReports = _context.Reports.Where(r => r.IsActive && subSectionReportIds.Contains(r.Id) && r.ClientId == null).ToList();
            }
            else//if the user isnt admin, we retrive only the reports that the user has access to
            { 
                var userReports = await _context.UserReports.Where(ur => ur.UserId == userId).Select(x => x.ReportId).ToListAsync();
                reports = _context.Reports.Where(r => r.IsActive && userReports.Contains(r.Id) && subSectionReportIds.Contains(r.Id) && r.ClientId == clientId).ToList();
                baseReports = _context.Reports.Where(r => r.IsActive && userReports.Contains(r.Id) && subSectionReportIds.Contains(r.Id) && r.ClientId == null).ToList();
            }

            //we add the base reports to the list of available reports
            reports.AddRange(baseReports);

            if (period == null) //by default, current month as period
                period.Add($"{DateTime.Today.Year}-{DateTime.Today.Month}");

            var dataBlocks = new List<ReportSummaryResponse>();

            foreach (var report in reports)
            {
                var reportSummaryRecord = await _context.ReportSummaries.FirstOrDefaultAsync(x => x.ReportId == report.Id && x.Main && x.IsActive);

                if (reportSummaryRecord != null) //custom main summary of report
                {
                    report.SummaryStyle = reportSummaryRecord.Style; //jic the report structure has another summary style

                    ReportSummaryResponse reportSummary = GetSummaryOfReport(reportSummaryRecord.Style, 
                                                                             report,
                                                                             target,
                                                                             clientId,
                                                                             storeId,
                                                                             reportSummaryRecord.LinesQty, 
                                                                             reportSummaryRecord.TargetColumns, 
                                                                             reportSummaryRecord.ColumnTitles, 
                                                                             reportSummaryRecord.CalcMode, 
                                                                             reportSummaryRecord.Position,
                                                                             period[0],
                                                                             null);

                    reportSummary.ReportConfig.Name = reportSummaryRecord.Name;

                    dataBlocks.Add(reportSummary);
                }
                else //default summary
                {
                    ReportSummaryResponse reportSummary = GetSummaryOfReport(report.SummaryStyle, report, target, clientId, storeId, 4, "1", "", "", "", period[0], null);
                    dataBlocks.Add(reportSummary);
                }
            }

            return await Result<List<ReportSummaryResponse>>.SuccessAsync(dataBlocks);
        }

        public async Task<Result<ReportSummaryResponse>> GetUpdatedReportSummary(long reportId, long clientId, long storeId, List<string>? period, TargetOption target, List<int>? selectedOptions)
        {
            if (period == null) //by default, current month as period
                period.Add($"{DateTime.Today.Year}-{DateTime.Today.Month}");

            var report = await _context.Reports.FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive);

            var reportSummaryRecord = await _context.ReportSummaries.FirstOrDefaultAsync(x => x.ReportId == report.Id && x.Main && x.IsActive);
            
            ReportSummaryResponse reportSummary = new ReportSummaryResponse();

            if (reportSummaryRecord != null) //custom main summary of report
            {
                report.SummaryStyle = reportSummaryRecord.Style; //jic the report structure has another summary style

                reportSummary = GetSummaryOfReport(reportSummaryRecord.Style,
                                                    report,
                                                    target,
                                                    clientId,
                                                    storeId,
                                                    reportSummaryRecord.LinesQty,
                                                    reportSummaryRecord.TargetColumns,
                                                    reportSummaryRecord.ColumnTitles,
                                                    reportSummaryRecord.CalcMode,
                                                    reportSummaryRecord.Position,
                                                    period[0],
                                                    selectedOptions);

                reportSummary.ReportConfig.Name = reportSummaryRecord.Name;
            }
            else //default summary
            {
                reportSummary = GetSummaryOfReport(report.SummaryStyle, report, target, clientId, storeId, 4, "1", "", "", "", period[0], selectedOptions);
            }

            return await Result<ReportSummaryResponse>.SuccessAsync(reportSummary);
        }

        private ReportSummaryResponse GetSummaryOfReport(string summaryStyle, Report report, TargetOption target, long clientId, long storeId, int linesQty, string targetColumns, string columnTitles, string calcMode, string position, string period, List<int>? selectedOptions)
        {
            ReportSummaryResponse reportSummary = new ReportSummaryResponse();

            reportSummary.ReportConfig = _mapper.Map<ReportConfigResponse>(report);

            View viewOfReport = _context.Views.FirstOrDefault(x => x.ReportId == report.Id);
            if (viewOfReport != null)
            {
                reportSummary.ReportConfig.ViewId = viewOfReport.ViewId;
                reportSummary.ReportConfig.strWhere = viewOfReport.strWhere;
                reportSummary.ReportConfig.States = viewOfReport.States;
                reportSummary.ReportConfig.MessageBoard = viewOfReport.MessageBoard;
            }

            if (position != "")
                reportSummary.ReportConfig.Style = $"position:{position}";

            switch (summaryStyle)
            {
                case "bubble":
                case "quad":
                    //INVENTORY REPORTS mostly
                    //INFO: this summary styles doesn't use LinesQty because it always returns 4 lines

                    if (report.Name.ToLower().Contains("cit") || report.Name.ToLower().Contains("in transit")) //PATCH for NADA: only for CIT report
                    {
                        var currentDate = _context.Clients.FirstOrDefault(x => x.ClientId == clientId)?.LastUpdateDate;

                        var queryResult = _context.ContractsInTransitSqlResponses.FromSqlRaw<ContractsInTransitSqlResponse>($@"
                                SELECT
                                    CASE
                                        WHEN ""Days"" < 5 THEN '1-Less than 5'
                                        WHEN ""Days"" BETWEEN 5 AND 10 THEN '2-5 to 10'
                                        WHEN ""Days"" BETWEEN 11 AND 20 THEN '3-11 to 20'
                                        ELSE '4-21+'
                                    END AS ""DaysRange"",
                                    COUNT(""Control1"") AS ""Count"",
                                    SUM(""Postamount"") AS ""TotalPostamount""
                                FROM
                                    (
                                        SELECT
                                            ""Control1"",
                                            ('{currentDate.Value.Year}-{currentDate.Value.Month}-{currentDate.Value.Day}'::date - MIN(""Gldate"")) AS ""Days"",
                                            SUM(""Postamount"") AS ""Postamount""
                                        FROM
                                            normalized.""NormalizedGldets"" NGL
                                        LEFT JOIN
                                            normalized.""NormalizedSales"" NS ON
                                            NGL.""ClientId"" = NS.""ClientId"" AND
                                            NGL.""StoreId"" = NS.""StoreId"" AND
                                            TRIM(REPLACE(NS.""StockNumber"", '#', '')) = NGL.""Control1""
                                        WHERE
                                            ""Glacct"" in ('205','20500','20530','20535','20550','20555') AND
                                            NGL.""ClientId"" = {clientId} AND
                                            NGL.""StoreId"" IN ({storeId})
                                        GROUP BY
                                            ""Control1""
                                        HAVING
                                            SUM(""Postamount"") <> 0
                                    ) AS subquery
                                GROUP BY
                                    ""DaysRange""
                                ORDER BY
                                    ""DaysRange""")
                                .ToList();

                        List<ReportLineValue> rlvalues = new List<ReportLineValue>();

                        reportSummary.ReportConfig.Columns = new List<string> { "Range", "Count", "Amount" };

                        foreach (var row in queryResult)
                        {
                            ReportLineValue newRlv = new ReportLineValue();
                            newRlv.Column1 = row.DaysRange.Substring(2);

                            newRlv.Column2 = row.Count?.ToString();
                            newRlv.Column3 = $"${row.TotalPostamount?.ToString("0.00")}";

                            rlvalues.Add(newRlv);
                        }

                        reportSummary.ReportLines = new List<ReportLineResponse>();
                        foreach (var rlv in rlvalues)
                        {
                            List<string> values = new List<string>() { rlv.Column1, rlv.Column2, rlv.Column3 };

                            reportSummary.ReportLines.Add(new ReportLineResponse
                            {
                                Values = values
                            });
                        }
                    }
                    else
                    {
                        DateTime? lastUpdateDate = _context.Clients.FirstOrDefault(x => x.ClientId == clientId)?.LastUpdateDate;
                        var today = lastUpdateDate != null ? lastUpdateDate : DateTime.UtcNow;

                        var ranges = new List<(string Lower, string Upper)>
                        {
                            ("0", "30"),
                            ("31", "90"),
                            ("91", "180"),
                            ("181", "365")
                        };

                        var lastGeneratedPeriod = _context.ReportLineValues
                                                    .OrderBy(x => x.Period)
                                                    .FirstOrDefault(rlv => rlv.ReportLine.ReportId == report.Id && rlv.StoreId == storeId && rlv.IsActive)?
                                                    .Period;

                        List<ReportLineValue> repLinesAuxAux = new List<ReportLineValue>();

                        if(lastGeneratedPeriod != null) //due to inventory data doesnt filter by date, we only look for the most up to date data of it
                            repLinesAuxAux = _context.ReportLineValues
                                                    .Where(rlv => rlv.StoreId == storeId &&
                                                                  rlv.Period == lastGeneratedPeriod &&
                                                                  rlv.ReportLine.ReportId == report.Id &&
                                                                  rlv.IsActive)
                                                    .ToList();

                        var columnsInv = targetColumns.Split(",");

                        var repLinesAux = repLinesAuxAux.Select(rlv => new { 
                            date = rlv.GetType().GetProperty($"Column{columnsInv[0]}").GetValue(rlv, null)?.ToString(),
                            price = rlv.GetType().GetProperty($"Column{columnsInv[1]}").GetValue(rlv, null)?.ToString()
                        }).ToList();

                        List<int> repLines = new List<int>();
                        decimal[] accumulatedAmts = new decimal[4];
                        foreach(var record in repLinesAux)
                        {
                            int days = 0;

                            DateTime dateTime;
                            DateTime.TryParseExact(record.date, "M/dd/yyyy hh:mm:ss tt", CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out dateTime);

                            if(dateTime != DateTime.MinValue)
                                days = today.Value.Subtract(dateTime).Days;

                            if(days <= 30)
                                accumulatedAmts[0] += Convert.ToDecimal(record.price);
                            else if(days > 30 && days <= 90)
                                accumulatedAmts[1] += Convert.ToDecimal(record.price);
                            else if(days > 90 && days <= 180)
                                accumulatedAmts[2] += Convert.ToDecimal(record.price);
                            else if(days > 180 && days <= 365)
                                accumulatedAmts[3] += Convert.ToDecimal(record.price);

                            repLines.Add(days);
                        }


                        if (summaryStyle == "bubble")
                        {
                            reportSummary.ReportConfig.Columns = new List<string> { "Range", "Count", "Amount" };

                            reportSummary.ReportLines = ranges.Select((r, idx) => new ReportLineResponse
                            {
                                Values = new List<string> { $"{r.Lower} - {r.Upper} {columnTitles}", repLines.Count(day => day >= Convert.ToInt32(r.Lower) && day <= Convert.ToInt32(r.Upper)).ToString(), $"${accumulatedAmts[idx]}" }
                            }).ToList();
                        }
                        else
                        {
                            reportSummary.ReportConfig.Columns = new List<string> { "Title", "Count" };

                            reportSummary.ReportLines = ranges.Select(r => new ReportLineResponse
                            {
                                Values = new List<string> { $"{r.Lower} - {r.Upper} {columnTitles}", repLines.Count(day => day >= Convert.ToInt32(r.Lower) && day <= Convert.ToInt32(r.Upper)).ToString() }
                            }).ToList();
                        }
                    }

                    break;
                case "classic":
                case "dual":
                    var columns = targetColumns.Split(",");
                    var titles = columnTitles.Split(",");

                    reportSummary.ReportConfig.Columns = new List<string> { titles[0] };
                    if(titles.Length > 1)
                        reportSummary.ReportConfig.Columns = titles.ToList();

                    List<ReportLineValue> rlvs = new List<ReportLineValue>();
                    List<ReportLineValue> rlvsVariance = new List<ReportLineValue>();

                    if (report.ReportType == ReportType.Accounting)
                    {
                        var keyReportLines = _context.ReportLines.Where(x => x.ReportId == report.Id && x.KeyLine).OrderBy(x => x.Order).ToList();

                        reportSummary.ReportConfig.Columns = keyReportLines.Select(x => x.Name).ToList();

                        var keyReportLineIds = keyReportLines.Select(x => x.Id).ToList();

                        rlvs = _context.ReportLineValues.Where(x => keyReportLineIds.Contains(x.ReportLineId) &&
                                                                    x.StoreId == storeId && 
                                                                    x.Period == period && 
                                                                    x.IsActive)
                                                        .OrderBy(x => x.ReportLine.Order)
                                                        .Take(keyReportLines.Count()) //Accounting type of report doesnt use LinesQty, it'll use the qty of keylines found
                                                        .ToList();

                        var periodConfig = GetPeriodParts(period);
                        var targetPeriodCompare = period;
                        switch (target)
                        {
                            case TargetOption.PriorMonth:
                                targetPeriodCompare = $"{Convert.ToInt32(periodConfig.PeriodParts[0]) - periodConfig.ZeroToSubtract}-{periodConfig.PriorMonth.ToString().PadLeft(2, '0')}";
                                break;
                            case TargetOption.SameMonthLastYear:
                                targetPeriodCompare = $"{Convert.ToInt32(periodConfig.PeriodParts[0]) - 1}-{periodConfig.PeriodParts[1]}";
                                break;
                            case TargetOption.ThreeMonthsAverage:
                                //TODO: NOT IMPLEMENTED YET
                                break;
                        }
                        rlvsVariance = _context.ReportLineValues.Where(x => keyReportLineIds.Contains(x.ReportLineId) &&
                                                                            x.Period == targetPeriodCompare &&
                                                                            x.IsActive)
                                                    .Take(keyReportLines.Count())
                                                    .ToList();

                    }
                    else if (report.ReportType == ReportType.Repeat)
                    {
                        var reportLine = _context.ReportLines.FirstOrDefault(rl => rl.ReportId == report.Id);
                        
                        if(reportLine != null)
                        {
                            switch (calcMode)
                            {
                                case "count_non_empty":
                                    //INFO: this special calc mode doesn't use LinesQty because it always returns 1 line
                                    var lineValues = _context.ReportLineValues.Where(x => x.ReportLineId == reportLine.Id &&
                                                                                          x.Period == period &&
                                                                                          x.IsActive)
                                                                    .ToList();

                                    ReportLineValue newRlv = new ReportLineValue();
                                    int i = 1;
                                    foreach (var x in columns)
                                    {
                                        //now I count the quantity of lineValues that are not empty in the column number x
                                        var count = lineValues.Count(lv => lv.GetType().GetProperty($"Column{x}").GetValue(lv, null)?.ToString() != "");
                                        newRlv.GetType().GetProperty($"Column{i}").SetValue(newRlv, count.ToString());
                                        i++;
                                    }
                                    rlvs.Add(newRlv);

                                    break;
                                default:
                                    rlvs = _context.ReportLineValues.Where(x => x.ReportLineId == reportLine.Id &&
                                                                                x.Period == period &&
                                                                                x.IsActive)
                                                                    .Take(linesQty)
                                                                    .ToList();

                                    if (report.Name.ToLower().Contains("f&i manager")) //PATCH for NADA (*32j4nh5)
                                        reportSummary.ReportConfig.Columns = rlvs.Select(x => x.GetType().GetProperty($"Column{columns[0]}").GetValue(x, null)?.ToString()).ToList();

                                    break;
                            }
                        }

                    }

                    reportSummary.ReportLines = new List<ReportLineResponse>();
                    int j = 0;
                    foreach(var rlv in rlvs)
                    {
                        List<string> values = new List<string>();
                        foreach(var x in columns)
                        {
                            if (x == columns.First() && report.Name.ToLower().Contains("f&i manager")) //PATCH for NADA (*32j4nh5)
                                continue; //skip first loop with salesman name

                            var rlvVal = rlv.GetType().GetProperty($"Column{x}").GetValue(rlv, null)?.ToString();
                            values.Add(rlvVal ?? "");

                            if(report.ReportType == ReportType.Accounting)
                            {
                                string rlvVceVal = "0";
                                try { rlvVceVal = rlvsVariance[j].GetType().GetProperty($"Column{x}").GetValue(rlvsVariance[j], null)?.ToString(); } catch { }
                                if(rlvVceVal == null)
                                    rlvVceVal = "0";

                                double[] doubleValues = new double[2];

                                if(Double.TryParse(rlvVal, out doubleValues[0]) && Double.TryParse(rlvVceVal, out doubleValues[1]))
                                {
                                    if (doubleValues[1] == 0)
                                        values.Add($"{(doubleValues[0] != 0 ? 200 : 0)}%");
                                    else
                                        values.Add($"{((((doubleValues[0] - doubleValues[1]) / Math.Abs(doubleValues[1])) * 100) + 100).ToString("0.00")}%");
                                }
                                else
                                    values.Add("0%");
                            }
                        }

                        if(report.ReportType == ReportType.Accounting)
                            reportSummary.ReportLines.Add(new ReportLineResponse { Name = reportSummary.ReportConfig.Columns[j], Values = values });
                        else if (report.Name.ToLower().Contains("f&i manager")) //PATCH for NADA (*32j4nh5)
                            reportSummary.ReportLines.Add(new ReportLineResponse { Name = reportSummary.ReportConfig.Columns[j], Values = values });
                        else
                            reportSummary.ReportLines.Add(new ReportLineResponse { Values = values });
                        
                        j++;
                    }

                    break;
                case "classic_selectable":
                    var columnsCS = targetColumns.Split(",");
                    var titlesCS = columnTitles.Split(",");

                    if (report.ReportType == ReportType.Accounting && !report.Name.ToLower().Contains("product"))
                    {
                        var keyReportLines = _context.ReportLines.Where(x => x.ReportId == report.Id && x.KeyLine).ToList();

                        var keyReportLineIds = keyReportLines.Select(x => x.Id).ToList();

                        List<ReportLineValue> rlvsAux = new List<ReportLineValue>();
                        reportSummary.ReportLines = new List<ReportLineResponse>();

                        switch (calcMode)
                        {
                            case "top_bottom":
                                reportSummary.ReportConfig.Columns = new List<string> { $"Top {linesQty},Last {linesQty}" };

                                var selectedMetric = selectedOptions?.LastOrDefault();
                                if (selectedMetric == null)
                                    selectedMetric = 1;

                                var periodParts = period.Split("-");
                                var priorMonth = Convert.ToInt32(periodParts[1]) - 1;
                                int zeroToSubtract = 0;
                                if (priorMonth == 0)
                                {
                                    priorMonth = 12;
                                    zeroToSubtract = 1;
                                }

                                switch (selectedMetric) //TODO: fix filter top/last (is returning always the same values)
                                {
                                    case 1: //Top N
                                        rlvsAux = _context.ReportLineValues.Where(x => keyReportLineIds.Contains(x.ReportLineId) && x.Period == period)
                                                                           .Take(linesQty)
                                                                           .ToList();

                                        break;
                                    case 2: //Last N
                                        rlvsAux = _context.ReportLineValues.Where(x => keyReportLineIds.Contains(x.ReportLineId) && x.Period == period)
                                                                           .Take(linesQty)
                                                                           .ToList();

                                        break;
                                }
                                break;
                        }

                        int z = 0;
                        foreach (var krl in keyReportLines)
                        {
                            reportSummary.ReportLines.Add(new ReportLineResponse
                            {
                                Values = new List<string>() { krl.Name, rlvsAux.ElementAtOrDefault(z) != null ? rlvsAux[z].GetType().GetProperty($"Column{targetColumns}").GetValue(rlvsAux[z], null)?.ToString() : "0" }
                            });
                            z++;
                        }
                    }
                    else if (report.ReportType == ReportType.Repeat && report.Name.ToLower().Contains("models"))  //PATCH for NADA: only for models report
                    {
                        var retailCarGrossProfitLine = _context.ReportLines.FirstOrDefault(x => x.Name.Equals("Retail Car Gross Profit"));
                        var retailTruckGrossProfitLine = _context.ReportLines.FirstOrDefault(x => x.Name.Equals("Retail Truck Gross Profit"));

                        if (retailCarGrossProfitLine == null || retailTruckGrossProfitLine == null)
                            return reportSummary; //doesnt make sense to look for data because we didnt find the necessary lines to filter

                        var periodParts = period.Split("-");
                        var priorMonth = (Convert.ToInt32(periodParts[1]) - 1).ToString();
                        if(priorMonth.Length == 1)
                            priorMonth = $"0{priorMonth}";
                        
                        int zeroToSubtract = 0;
                        if (priorMonth.Equals("00"))
                        {
                            priorMonth = "12";
                            zeroToSubtract = 1;
                        }

                        var queryStr = $@"
                                WITH WorkingDays AS (
                                    SELECT COUNT(*) AS WorkingDaysCount
                                    FROM public.""DateDimension""
                                    WHERE year = {periodParts[0]} AND month = {periodParts[1]} AND is_working_day = true
                                ),
                                InventoryAggregates AS (
                                    SELECT 
                                        Trim(NI.""SaleAcc"") as ""SaleAcc"", 
                                        NI.""StoreId"", 
                                        NI.""ClientId"",
                                        COUNT(NI.""StockNumber"") AS ""InvCnt""
                                    FROM 
                                        normalized.""NormalizedInventories"" NI 
                                    GROUP BY 
                                        NI.""SaleAcc"", NI.""StoreId"", NI.""ClientId""
                                ),
                                TempMDCode AS (
                                    SELECT unnest(string_to_array(trim(""Value""), ''',''')) AS ""MDCode""
                                    FROM dev_reports.""ReportLineCellCalcs""
                                    WHERE ""ReportLineId"" = {retailCarGrossProfitLine.Id} AND ""ColumnIndex"" = 1
                                )
                                SELECT 
                                    'Cars' as ""Model"",
                                    -- Add your calculations here, using NGL and IA (InventoryAggregates)
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""Amountmtd"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""AmountPm"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""AmountPy"",
		 
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Countmtd"" ELSE 0 END) AS ""Countmtd"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Countmtd"" ELSE 0 END) AS ""CountPm"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Countmtd"" ELSE 0 END) AS ""CountPy"",
		 
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{period}'  
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""Grossmtd"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' 
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""GrossPm"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{period}' 
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""GrossPy"",
		 
	                                  CASE 
                                        WHEN SUM(CASE WHEN NGL.""Fsdate"" = '{period}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) = 0 
                                        THEN 0 
                                        ELSE SUM(CASE WHEN NGL.""Fsdate"" = '{period}' THEN NGL.""Amountmtd"" ELSE 0 END) / 
                                             SUM(CASE WHEN NGL.""Fsdate"" = '{period}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) 
                                    END AS ""pvr"",
                                    CASE 
                                        WHEN SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) = 0 
                                        THEN 0 
                                        ELSE SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' THEN NGL.""Amountmtd"" ELSE 0 END) / 
                                             SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) 
                                    END AS ""pvrPm"",
                                    CASE 
                                        WHEN SUM(CASE WHEN NGL.""Fsdate"" = '{period}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) = 0 
                                        THEN 0 
                                        ELSE SUM(CASE WHEN NGL.""Fsdate"" = '{period}' THEN NGL.""Amountmtd"" ELSE 0 END) / 
                                             SUM(CASE WHEN NGL.""Fsdate"" = '{period}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) 
                                    END AS ""PvrPy"",
		 
	                                 SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) - 
     	                                SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) AS ""CountPmT"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) - 
     	                                SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) AS ""CountPyT"",
                    
	                                CASE
		                                WHEN SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' and left(NGL.""MDCode"",4) = '0004'  THEN NGL.""Countmtd"" ELSE 0 END) = 0 THEN 0
		                                ELSE CAST(SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004'  THEN NGL.""Countmtd"" ELSE 0 END) AS decimal) / 
			                                SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END)
		                                END AS ""CountPmV"",
	                                CASE
		                                WHEN SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) = 0 THEN 0
		                                ELSE CAST(SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) AS decimal) / 
			                                SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END)
		                                END AS ""CountPyV"",
                            
	                                Sum(""InvCnt"") as ""InvCnt"",
                                    (CAST(SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) AS decimal) / 
    	                                WD.WorkingDaysCount) AS ""AvgSalesRate"",
	                                CASE 
		                                WHEN (CAST(SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) AS decimal) / 
			                                WD.WorkingDaysCount) = 0 THEN 0
		                                ELSE Count(""InvCnt"") / (CAST(SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004'
			                                THEN NGL.""Countmtd"" ELSE 0 END) AS decimal) / WD.WorkingDaysCount)
	                                END AS ""DaysSupply""	 
                                FROM 
                                    normalized.""NormalizedGlhistories"" NGL
                                    LEFT OUTER JOIN InventoryAggregates IA
                                        ON NGL.""Glacct"" = IA.""SaleAcc"" 
                                        AND NGL.""StoreId"" = IA.""StoreId"" 
                                        AND NGL.""ClientId"" = IA.""ClientId""
                                    INNER JOIN TempMDCode TMC
                                        ON NGL.""MDCode"" = TMC.""MDCode""
                                    CROSS JOIN WorkingDays WD
                                WHERE 
                                    NGL.""StoreId"" = {storeId}  
                                    AND NGL.""ClientId"" = {clientId}  
                                    AND NGL.""Fsdate"" IN ('{period}', '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}', '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}')
                                GROUP BY 
                                    WD.WorkingDaysCount
                                UNION ALL
                                SELECT 
                                    'Trucks' as ""Model"",
                                    -- Your calculations for Trucks...
	                                 SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""Amountmtd"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""AmountPm"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""AmountPy"",
		 
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Countmtd"" ELSE 0 END) AS ""Countmtd"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Countmtd"" ELSE 0 END) AS ""CountPm"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' and left(NGL.""MDCode"",4) = '0004' 
		                                 THEN NGL.""Countmtd"" ELSE 0 END) AS ""CountPy"",
		 
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{period}'  
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""Grossmtd"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' 
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""GrossPm"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' 
		                                 THEN NGL.""Amountmtd"" ELSE 0 END) AS ""GrossPy"",
		 
	                                  CASE 
                                        WHEN SUM(CASE WHEN NGL.""Fsdate"" = '{period}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) = 0 
                                        THEN 0 
                                        ELSE SUM(CASE WHEN NGL.""Fsdate"" = '{period}' THEN NGL.""Amountmtd"" ELSE 0 END) / 
                                             SUM(CASE WHEN NGL.""Fsdate"" = '{period}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) 
                                    END AS ""pvr"",
                                    CASE 
                                        WHEN SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) = 0 
                                        THEN 0 
                                        ELSE SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' THEN NGL.""Amountmtd"" ELSE 0 END) / 
                                             SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) 
                                    END AS ""pvrPm"",
                                    CASE 
                                        WHEN SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) = 0 
                                        THEN 0 
                                        ELSE SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' THEN NGL.""Amountmtd"" ELSE 0 END) / 
                                             SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' AND left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) 
                                    END AS ""PvrPy"",
		 
	                                 SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) - 
     	                                SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) AS ""CountPmT"",
                                     SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) - 
     	                                SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) AS ""CountPyT"",
                    
	                                CASE
		                                WHEN SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' and left(NGL.""MDCode"",4) = '0004'  THEN NGL.""Countmtd"" ELSE 0 END) = 0 THEN 0
		                                ELSE CAST(SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004'  THEN NGL.""Countmtd"" ELSE 0 END) AS decimal) / 
			                                SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END)
		                                END AS ""CountPmV"",
	                                CASE
		                                WHEN SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) = 0 THEN 0
		                                ELSE CAST(SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) AS decimal) / 
			                                SUM(CASE WHEN NGL.""Fsdate"" = '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END)
		                                END AS ""CountPyV"",
                            
	                                Sum(""InvCnt"") as ""InvCnt"",
                                    (CAST(SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) AS decimal) / 
    	                                WD.WorkingDaysCount) AS ""AvgSalesRate"",
	                                CASE 
		                                WHEN (CAST(SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004' THEN NGL.""Countmtd"" ELSE 0 END) AS decimal) / 
			                                WD.WorkingDaysCount) = 0 THEN 0
		                                ELSE Count(""InvCnt"") / (CAST(SUM(CASE WHEN NGL.""Fsdate"" = '{period}' and left(NGL.""MDCode"",4) = '0004'
			                                THEN NGL.""Countmtd"" ELSE 0 END) AS decimal) / WD.WorkingDaysCount)
	                                END AS ""DaysSupply""	 
                                FROM 
                                    normalized.""NormalizedGlhistories"" NGL
                                    LEFT OUTER JOIN InventoryAggregates IA
                                        ON NGL.""Glacct"" = IA.""SaleAcc"" 
                                        AND NGL.""StoreId"" = IA.""StoreId"" 
                                        AND NGL.""ClientId"" = IA.""ClientId""
                                    INNER JOIN (
                                        SELECT unnest(string_to_array(trim(""Value""), ''',''')) AS ""MDCode""
                                        FROM dev_reports.""ReportLineCellCalcs""
                                        WHERE ""ReportLineId"" = {retailTruckGrossProfitLine.Id} AND ""ColumnIndex"" = 1
                                    ) AS TempMDCodeTrucks
                                        ON NGL.""MDCode"" = TempMDCodeTrucks.""MDCode""
                                    CROSS JOIN WorkingDays WD
                                WHERE 
                                    NGL.""StoreId"" = {storeId} 
                                    AND NGL.""ClientId"" = {clientId} 
                                    AND NGL.""Fsdate"" IN ('{period}', '{Convert.ToInt32(periodParts[0]) - zeroToSubtract}-{priorMonth}', '{Convert.ToInt32(periodParts[0]) - 1}-{periodParts[1]}')
                                GROUP BY 
                                    WD.WorkingDaysCount;";

                        var queryResult = _context.ModelSqlResponses.FromSqlRaw<ModelSqlResponse>(queryStr)
                            .ToList(); //TODO: check if queryResult has rows

                        List<ReportLineValue> rlvalues = new List<ReportLineValue>();

                        reportSummary.ReportConfig.Columns = new List<string> { columnTitles };

                        var selectedMetric = selectedOptions?.LastOrDefault();
                        if (selectedMetric == null)
                            selectedMetric = 1;

                        ModelSqlResponse definitive = new ModelSqlResponse();

                        switch (titlesCS[selectedMetric.Value - 1])
                        {
                            case "All":
                                definitive = new ModelSqlResponse
                                {
                                    Model = "All",
                                    Amountmtd = queryResult.Sum(x => x.Amountmtd),
                                    AmountPm = queryResult.Sum(x => x.AmountPm),
                                    AmountPy = queryResult.Sum(x => x.AmountPy),
                                    Countmtd = queryResult.Sum(x => x.Countmtd),
                                    CountPm = queryResult.Sum(x => x.CountPm),
                                    CountPy = queryResult.Sum(x => x.CountPy),
                                    Grossmtd = queryResult.Sum(x => x.Grossmtd),
                                    GrossPm = queryResult.Sum(x => x.GrossPm),
                                    GrossPy = queryResult.Sum(x => x.GrossPy),
                                    pvr = queryResult.Sum(x => x.pvr),
                                    pvrPm = queryResult.Sum(x => x.pvrPm),
                                    pvrPy = queryResult.Sum(x => x.pvrPy),
                                    CountPmT = queryResult.Sum(x => x.CountPmT),
                                    CountPyT = queryResult.Sum(x => x.CountPyT),
                                    CountPmV = queryResult.Average(x => x.CountPmV),
                                    CountPyV = queryResult.Average(x => x.CountPyV),
                                    InvCnt = queryResult.Sum(x => x.InvCnt),
                                    AvgSalesRate = queryResult.Average(x => x.AvgSalesRate),
                                    DaysSupply = queryResult.Average(x => x.DaysSupply)
                                };

                                break;
                            default:
                                definitive = queryResult.First(x => x.Model == titlesCS[selectedMetric.Value - 1]);

                                break;
                        }

                        var definitiveGrossmtd = definitive.Grossmtd != 0 ? definitive.Grossmtd : 1;
                        var definitiveUnitsmtd = definitive.Countmtd != 0 ? definitive.Countmtd : 1;

                        switch (target)
                        {
                            case TargetOption.PriorMonth:
                            case TargetOption.ThreeMonthsAverage://TODO: NOT IMPLEMENTED YET, so it returns same as PM
                                decimal unitsComparisonPm = (decimal)(definitive.CountPm != 0 ? (definitive.CountPm * 100 / definitiveUnitsmtd) : 100);

                                if (unitsComparisonPm > 0)
                                    reportSummary.ReportConfig.Description = $"&arrow_up; {unitsComparisonPm.ToString()}% Compared to {definitive.CountPm} prior month";
                                else if (unitsComparisonPm < 0)
                                    reportSummary.ReportConfig.Description = $"&arrow_down; {unitsComparisonPm.ToString()}% Compared to {definitive.CountPm} prior month";
                                else
                                    reportSummary.ReportConfig.Description = $"No variance compared to prior month";

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = "Units",
                                    Column2 = definitive.Countmtd.ToString(),
                                    Column3 = (definitive.CountPmV * 100).ToString()
                                });

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = "Gross",
                                    Column2 = definitive.Grossmtd.ToString(),
                                    //Column3 = definitive.GrossPm != 0 ? (definitive.GrossPm * 100 / definitiveGrossmtd - 100).ToString() : "100"
                                    Column3 = definitive.GrossPm != 0 ? (definitive.GrossPm * 100 / definitiveGrossmtd).ToString() : "200"
                                });

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = "PVR",
                                    Column2 = definitive.pvr.ToString(),
                                    //Column3 = definitive.pvrPm != 0 ? (definitive.pvr * 100 / definitive.pvrPm - 100).ToString() : "100"
                                    Column3 = definitive.pvrPm != 0 ? (definitive.pvr * 100 / definitive.pvrPm).ToString() : "200"
                                });

                                ModelSqlResponse cars = new ModelSqlResponse() { CountPmT = 0, CountPmV = 0, DaysSupply = 0 };
                                ModelSqlResponse trucks = new ModelSqlResponse() { CountPmT = 0, CountPmV = 0, DaysSupply = 0 };
                                try
                                {
                                    cars = queryResult[0];
                                    trucks = queryResult[1];
                                } catch { }

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = titlesCS[1], //Cars
                                    Column2 = cars.Countmtd.ToString(),
                                    Column3 = (cars.CountPmV * 100).ToString()
                                });

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = titlesCS[2], //Trucks
                                    Column2 = trucks.Countmtd.ToString(),
                                    Column3 = (trucks.CountPmV * 100).ToString()
                                });

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = "Days Supply",
                                    Column2 = definitive.DaysSupply.ToString()
                                });

                                break;
                            case TargetOption.SameMonthLastYear:
                                decimal unitsComparisonPy = (decimal)(definitive.CountPy != 0 ? (definitive.CountPy * 100 / definitiveUnitsmtd) : 100);

                                if (unitsComparisonPy > 0)
                                    reportSummary.ReportConfig.Description = $"&arrow_up; {unitsComparisonPy.ToString()}% Compared to {definitive.CountPy} last year";
                                else if (unitsComparisonPy < 0)
                                    reportSummary.ReportConfig.Description = $"&arrow_down; {unitsComparisonPy.ToString()}% Compared to {definitive.CountPy} last year";
                                else
                                    reportSummary.ReportConfig.Description = $"No variance compared to same month last year";

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = "Units",
                                    Column2 = definitive.Countmtd.ToString(),
                                    Column3 = (definitive.CountPyV * 100).ToString()
                                });

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = "Gross",
                                    Column2 = definitive.Grossmtd.ToString(),
                                    //Column3 = definitive.GrossPy != 0 ? (definitive.GrossPm * 100 / definitive.GrossPy - 100).ToString() : "100"
                                    Column3 = definitive.GrossPy != 0 ? (definitive.GrossPm * 100 / definitive.GrossPy).ToString() : "200"
                                });

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = "PVR",
                                    Column2 = definitive.pvr.ToString(),
                                    //Column3 = definitive.pvrPy != 0 ? (definitive.pvr * 100 / definitive.pvrPy - 100).ToString() : "100"
                                    Column3 = definitive.pvrPy != 0 ? (definitive.pvr * 100 / definitive.pvrPy).ToString() : "200"
                                });

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = titlesCS[1], //Cars
                                    Column2 = queryResult[0].Countmtd.ToString(),
                                    Column3 = (queryResult[0].CountPyV * 100).ToString()
                                });

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = titlesCS[2], //Trucks
                                    Column2 = queryResult[1].Countmtd.ToString(),
                                    Column3 = (queryResult[1].CountPyV * 100).ToString()
                                });

                                rlvalues.Add(new ReportLineValue()
                                {
                                    Column1 = "Days Supply",
                                    Column2 = definitive.DaysSupply.ToString(),
                                    Column3 = ""
                                });

                                break;
                        }
                        

                        reportSummary.ReportLines = new List<ReportLineResponse>();
                        foreach (var rlv in rlvalues)
                        {
                            List<string> values = new List<string>() { rlv.Column1, rlv.Column2, rlv.Column3 };

                            reportSummary.ReportLines.Add(new ReportLineResponse
                            {
                                Values = values
                            });
                        }
                    }
                    else if (report.ReportType == ReportType.Accounting && report.Name.ToLower().Contains("product"))  //PATCH for NADA: only for models report
                    {
                        var periodParts = period.Split("-");
                        var priorMonth = Convert.ToInt32(periodParts[1]) - 1;
                        var daysOfMonth = DateTime.DaysInMonth(Convert.ToInt32(periodParts[0]), Convert.ToInt32(periodParts[1]));
                        int zeroToSubtract = 0;
                        if (priorMonth == 0)
                        {
                            priorMonth = 12;
                            zeroToSubtract = 1;
                        }

                        var queryResult = _context.ProductSqlResponses.FromSqlRaw<ProductSqlResponse>($@"
                                SELECT 
			                            1 as ""item"",
			                            'Finance Reserve' as ""lineDesc"",
                                        SUM(COALESCE(""FinanceReserve"", 0)) AS ""Gross"",
                                        COUNT(CASE WHEN ""FinanceReserve"" IS NOT NULL AND ""FinanceReserve"" <> 0 THEN 1 END) AS ""Count"",
                                        CASE 
                                            WHEN COUNT(""StockNumber"") > 0 THEN ROUND(COUNT(CASE WHEN ""FinanceReserve"" IS NOT NULL 
                                                AND ""FinanceReserve"" <> 0 THEN 1 END)::decimal / COUNT(""StockNumber"") * 100, 2)
                                            ELSE 0 
                                        END AS ""Penetration""
        
                                        FROM normalized.""NormalizedSales"" NS
                                        WHERE 
                                        NS.""StoreId"" in ( {storeId} ) 
                                        AND NS.""ClientId"" = {clientId} 
                                        AND NS.""DealDate"" BETWEEN '{periodParts[0]}-{periodParts[1]}-01' AND '{periodParts[0]}-{periodParts[1]}-{daysOfMonth}'
                                        and NS.""Condition"" = 'New' 
	                            UNION
	                            SELECT 
			                            2 as ""item"",
			                            'Aftermarket Products' as ""lineDesc"",
                                        SUM(COALESCE(""AftermarketIncome"", 0)) AS ""Gross"",
                                        COUNT(CASE WHEN ""AftermarketIncome"" IS NOT NULL AND ""AftermarketIncome"" <> 0 THEN 1 END) AS ""Count"",
                                        CASE 
                                            WHEN COUNT(""StockNumber"") > 0 THEN ROUND(COUNT(CASE WHEN ""AftermarketIncome"" IS NOT NULL 
                                                AND ""AftermarketIncome"" <> 0 THEN 1 END)::decimal / COUNT(""StockNumber"") * 100, 2)
                                            ELSE 0 
                                        END AS ""Pentration""
        
                                        FROM normalized.""NormalizedSales"" NS
                                        WHERE 
                                        NS.""StoreId"" in ( {storeId} ) 
                                        AND NS.""ClientId"" = {clientId}  
                                        AND NS.""DealDate"" BETWEEN '{periodParts[0]}-{periodParts[1]}-01' AND '{periodParts[0]}-{periodParts[1]}-{daysOfMonth}'
                                        and NS.""Condition"" = 'New' 
	                            UNION
			                            Select 3 as ""item"",
			                            'Chargebacks' as ""lineDesc"",
		                            sum(""Column1""::float) as ""Gross"", sum(""Column3""::float) as ""Count"", 0 as ""Penetration""
		                            from dev_reports.""ReportLineValues""
		                            where ""Period"" = '{period}' 
		                            and ""StoreId"" in ( {storeId} ) and ""ClientId"" = {clientId}
		                            and ""ReportLineId"" in (
			                            Select ""Id""
				                            from dev_reports.""ReportLines""
				                            where ""ReportId"" = 5 and ""Name"" like '%Chargebacks%'
		                            )
                                ORDER BY ""item"";")
                            .ToList();

                        List<ReportLineValue> rlvalues = new List<ReportLineValue>();

                        reportSummary.ReportConfig.Columns = new List<string> { columnTitles };

                        var selectedMetric = selectedOptions?.LastOrDefault();
                        if (selectedMetric == null)
                            selectedMetric = 1;

                        foreach (var row in queryResult)
                        {
                            ReportLineValue newRlv = new ReportLineValue();
                            newRlv.Column1 = row.lineDesc;

                            switch (titlesCS[selectedMetric.Value - 1])
                            {
                                case "Gross":
                                    newRlv.Column2 = row.Gross?.ToString("0.00");
                                    newRlv.Column3 = Convert.ToString(new Random().Next(90,120));
                                    break;
                                case "Units":
                                    newRlv.Column2 = row.Count?.ToString();
                                    newRlv.Column3 = Convert.ToString(new Random().Next(90, 120));
                                    break;
                                case "Penetration":
                                    newRlv.Column2 = row.Penetration?.ToString("0.00");
                                    newRlv.Column3 = Convert.ToString(new Random().Next(90, 120));
                                    break;
                            }

                            rlvalues.Add(newRlv);
                        }

                        reportSummary.ReportLines = new List<ReportLineResponse>();
                        foreach (var rlv in rlvalues)
                        {
                            List<string> values = new List<string>() { rlv.Column1, rlv.Column2, rlv.Column3 };

                            reportSummary.ReportLines.Add(new ReportLineResponse
                            {
                                Values = values
                            });
                        }
                    }

                    break;
                case "persons_list":
                    var columnsPL = targetColumns.Split(",");
                    var titlesPL = columnTitles.Split(",");

                    var rl_repLineVals = _context.ReportLineValues
                        .Where(rlv => rlv.ReportLine.ReportId == report.Id && rlv.Period == period)
                        .ToList();

                    reportSummary.ReportConfig.Columns = new List<string> { titlesPL[0] };
                    if (titlesPL.Length > 1)
                        reportSummary.ReportConfig.Columns = titlesPL.ToList();

                    List<ReportLineValue> pl_rlvs = new List<ReportLineValue>();
                    List<ReportLineValue> pl_rlvsVariance = new List<ReportLineValue>();

                    if (report.ReportType == ReportType.Repeat && report.Name.ToLower().Contains("f&i manager"))
                    {
                        var reportLine = _context.ReportLines.FirstOrDefault(rl => rl.ReportId == report.Id);

                        if (reportLine != null)
                        {
                            pl_rlvs = _context.ReportLineValues.Where(x => x.ReportLineId == reportLine.Id &&
                                                                           x.Period == period &&
                                                                           x.StoreId == storeId &&
                                                                           x.IsActive)
                                                                    .Take(linesQty)
                                                                    .ToList();

                            reportSummary.ReportConfig.Columns = new List<string>();
                        }

                        reportSummary.ReportLines = new List<ReportLineResponse>();
                        foreach (var rlv in pl_rlvs)
                        {
                            List<string> values = new List<string>();
                            foreach (var x in columnsPL)
                            {
                                if (x == columnsPL.First()) //first loop: adding name initials
                                {
                                    string name = rlv.GetType().GetProperty($"Column{x}").GetValue(rlv, null)?.ToString();
                                    if (!string.IsNullOrEmpty(name) && !name.Contains("Not Available"))
                                    {
                                        string[] words = name.Split(' ');
                                        string initials = "";

                                        foreach (string word in words)
                                        {
                                            if (!string.IsNullOrEmpty(word))
                                                initials += word[0];
                                        }

                                        values.Add(initials);
                                    }
                                    else if(!string.IsNullOrEmpty(name) && name.Contains("Not Available"))
                                        values.Add("-");
                                    else
                                        values.Add("");
                                }

                                var rlvVal = rlv.GetType().GetProperty($"Column{x}").GetValue(rlv, null)?.ToString();
                                values.Add(rlvVal ?? "");
                            }

                            reportSummary.ReportLines.Add(new ReportLineResponse { Values = values });
                        }
                    }
                    else if (report.ReportType == ReportType.Repeat && report.Name.ToLower().Contains("salesperson")) //PATCH for NADA: only for salesperson reports
                    {
                        string condition = "New";
                        if (report.Description.ToLower().Contains("used"))
                            condition = "Used";

                        string order = "ASC";
                        if (selectedOptions != null && selectedOptions.FirstOrDefault() == 2) //'Last 3' came selected
                            order = "DESC";

                        var periodParts = period.Split("-");
                        var priorMonth = Convert.ToInt32(periodParts[1]) - 1;
                        int zeroToSubtract = 0;
                        if (priorMonth == 0)
                        {
                            priorMonth = 12;
                            zeroToSubtract = 1;
                        }

                        var queryResult = _context.SalespersonSqlResponses.FromSqlRaw<SalespersonSqlResponse>($@"
                                SELECT 
                                    sub.""rankU"", sub.""rankS"", sub.""rankG"", sub.""empId"", 
                                    COALESCE(NULLIF(sub.""Name"", ''), sub.""empId"" || ' - Missing') as ""Name"",
                                    sub.""Deal"", sub.""pmDeal"", sub.""pyDeal"",
                                    COALESCE(sub.""Deal"", 0) - COALESCE(sub.""pmDeal"", 0) AS ""DealPmT"",
                                    sub.""Deal""::decimal / NULLIF(sub.""pmDeal"", 0) * 100 AS ""DealPmV"",
                                    COALESCE(sub.""Deal"", 0) - COALESCE(sub.""pyDeal"", 0) AS ""DealPyT"",
                                    sub.""Deal""::decimal / NULLIF(sub.""pyDeal"", 0) * 100 AS ""DealPyV"",
                    
                                    sub.""Sales"", sub.""pmSales"", sub.""pySales"",
                                    COALESCE(sub.""Sales"", 0) - COALESCE(sub.""pmSales"", 0) AS ""SalesPmT"",
                                    sub.""Sales""::decimal / NULLIF(sub.""pmSales"", 0) * 100 AS ""SalesPmV"",
                                    COALESCE(sub.""Sales"", 0) - COALESCE(sub.""pySales"", 0) AS ""SalesPyT"",
                                    sub.""Sales""::decimal / NULLIF(sub.""pySales"", 0) * 100 AS ""SalesPyV"",
                    
                                    sub.""Gross"", sub.""pmGross"", sub.""pyGross"",
                                    COALESCE(sub.""Gross"", 0) - COALESCE(sub.""pmGross"", 0) AS ""GrossPmT"",
                                    sub.""Gross""::decimal / NULLIF(sub.""pmGross"", 0) * 100 AS ""GrossPmV"",
                                    COALESCE(sub.""Gross"", 0) - COALESCE(sub.""pyGross"", 0) AS ""GrossPyT"",
                                    sub.""Gross""::decimal / NULLIF(sub.""pyGross"", 0) * 100 AS ""GrossPyV""
                                FROM
            	                    (
                	                    Select
					                    ""SalesmanNo"" as ""empId"", ""SalesmanName"" as ""Name"", 
            
                                        ROW_NUMBER() OVER (ORDER BY COUNT(""DealNo"") desc) AS ""rankU"",
                                        ROW_NUMBER() OVER (ORDER BY SUM(""Price"") desc) AS ""rankS"",
                                        ROW_NUMBER() OVER (ORDER BY SUM(""Price"" - ""Cost"") desc) AS ""rankG"",
            
                                        Count (Case when EXTRACT(MONTH FROM NS.""DealDate"") = {periodParts[1]}  
                                                                and EXTRACT(YEAR FROM NS.""DealDate"") = {periodParts[0]} then
                                                                (""DealNo"") end) as ""Deal"",
                                        Count (Case when EXTRACT(MONTH FROM NS.""DealDate"") = {priorMonth}
                                                                and EXTRACT(YEAR FROM NS.""DealDate"") = {Convert.ToInt32(periodParts[0]) - zeroToSubtract} then
                                                                (""DealNo"") end) as ""pmDeal"", 
                                        Count (Case when EXTRACT(MONTH FROM NS.""DealDate"") = {periodParts[0]}  
                                                                and EXTRACT(YEAR FROM NS.""DealDate"") = {Convert.ToInt32(periodParts[0]) - 1} then
                                                                (""DealNo"") end) as ""pyDeal"", 
            
                                        Sum (Case when EXTRACT(MONTH FROM NS.""DealDate"") = {periodParts[1]}  
                                                                and EXTRACT(YEAR FROM NS.""DealDate"") = {periodParts[0]} then
                                                                (""Price"") end) as ""Sales"",
                                        Sum (Case when EXTRACT(MONTH FROM NS.""DealDate"") = {priorMonth}  
                                                                and EXTRACT(YEAR FROM NS.""DealDate"") = {Convert.ToInt32(periodParts[0]) - zeroToSubtract} then
                                                                (""Price"") end) as ""pmSales"", 
                                        Sum (Case when EXTRACT(MONTH FROM NS.""DealDate"") = {periodParts[1]}  
                                                                and EXTRACT(YEAR FROM NS.""DealDate"") = {Convert.ToInt32(periodParts[0]) - 1} then
                                                                (""Price"") end) as ""pySales"", 
                                            
                                        Sum (Case when EXTRACT(MONTH FROM NS.""DealDate"") = {periodParts[1]}  
                                                                and EXTRACT(YEAR FROM NS.""DealDate"") = {periodParts[0]} then
                                                                (""HouseGross"" + ""BackEndGross"") end) as ""Gross"",
                                        Sum (Case when EXTRACT(MONTH FROM NS.""DealDate"") = {priorMonth}  
                                                                and EXTRACT(YEAR FROM NS.""DealDate"") = {Convert.ToInt32(periodParts[0]) - zeroToSubtract} then
                                                                (""HouseGross"" + ""BackEndGross"") end) as ""pmGross"", 
                                        Sum (Case when EXTRACT(MONTH FROM NS.""DealDate"") = {periodParts[1]}  
                                                                and EXTRACT(YEAR FROM NS.""DealDate"") = {Convert.ToInt32(periodParts[0]) - 1} then
                                                                (""HouseGross"" + ""BackEndGross"") end) as ""pyGross""
            
                                            FROM normalized.""NormalizedSales"" NS
                                            Where NS.""ClientId"" = {clientId} and NS.""StoreId"" = {storeId} and NS.""Condition"" = '{condition}'  
                                            Group by ""SalesmanNo"", ""SalesmanName"" 
                                    ) AS sub
            
            	                order by ""rankU"" {order} LIMIT {linesQty}")
                            .ToList();

                        List<ReportLineValue> rlvalues = new List<ReportLineValue>();

                        switch (calcMode)
                        {
                            case "top_bottom":
                                reportSummary.ReportConfig.Columns = new List<string> { $"Top {linesQty},Last {linesQty}", columnTitles };

                                var selectedMetric = selectedOptions?.LastOrDefault();
                                if (selectedMetric == null)
                                    selectedMetric = 1;

                                foreach (var row in queryResult)
                                {
                                    ReportLineValue newRlv = new ReportLineValue();

                                    if (!string.IsNullOrEmpty(row.Name) && !row.Name.Contains("Not Available"))
                                    {
                                        string[] words = row.Name.Split(' ');
                                        string initials = "";

                                        foreach (string word in words)
                                        {
                                            if (!string.IsNullOrEmpty(word))
                                                initials += word[0];
                                        }

                                        newRlv.Column1 = initials;
                                    }
                                    else if (!string.IsNullOrEmpty(row.Name) && row.Name.Contains("Not Available"))
                                        newRlv.Column1 = "-";
                                    else
                                        newRlv.Column1 = "";

                                    newRlv.Column2 = row.Name;

                                    switch (titlesPL[selectedMetric.Value - 1])
                                    {
                                        case "Units":
                                            switch (target)
                                            {
                                                case TargetOption.PriorMonth:
                                                    newRlv.Column3 = row.Deal?.ToString();
                                                    newRlv.Column4 = row.DealPmV?.ToString();
                                                    break;
                                                case TargetOption.SameMonthLastYear:
                                                    newRlv.Column3 = row.pyDeal?.ToString();
                                                    newRlv.Column4 = row.DealPyV?.ToString();
                                                    break;
                                                case TargetOption.ThreeMonthsAverage:
                                                    //TODO: NOT IMPLEMENTED YET. showing PM by default
                                                    newRlv.Column3 = row.Deal?.ToString();
                                                    newRlv.Column4 = row.DealPmV?.ToString();
                                                    break;
                                            }
                                            break;
                                        case "Sales":
                                            switch (target)
                                            {
                                                case TargetOption.PriorMonth:
                                                    newRlv.Column3 = row.Sales?.ToString();
                                                    newRlv.Column4 = row.SalesPmV?.ToString();
                                                    break;
                                                case TargetOption.SameMonthLastYear:
                                                    newRlv.Column3 = row.pySales?.ToString();
                                                    newRlv.Column4 = row.SalesPyV?.ToString();
                                                    break;
                                                case TargetOption.ThreeMonthsAverage:
                                                    //TODO: NOT IMPLEMENTED YET. showing PM by default
                                                    newRlv.Column3 = row.Sales?.ToString();
                                                    newRlv.Column4 = row.SalesPmV?.ToString();
                                                    break;
                                            }
                                            break;
                                        case "Gross":
                                            switch (target)
                                            {
                                                case TargetOption.PriorMonth:
                                                    newRlv.Column3 = row.Gross?.ToString();
                                                    newRlv.Column4 = row.GrossPmV?.ToString();
                                                    break;
                                                case TargetOption.SameMonthLastYear:
                                                    newRlv.Column3 = row.pyGross?.ToString();
                                                    newRlv.Column4 = row.GrossPyV?.ToString();
                                                    break;
                                                case TargetOption.ThreeMonthsAverage:
                                                    //TODO: NOT IMPLEMENTED YET. showing PM by default
                                                    newRlv.Column3 = row.Gross?.ToString();
                                                    newRlv.Column4 = row.GrossPmV?.ToString();
                                                    break;
                                            }
                                            break;
                                    }

                                    rlvalues.Add(newRlv);
                                }

                                reportSummary.ReportLines = new List<ReportLineResponse>();
                                foreach (var rlv in rlvalues)
                                {
                                    List<string> values = new List<string>() { rlv.Column1, rlv.Column2, rlv.Column3, rlv.Column4 };

                                    reportSummary.ReportLines.Add(new ReportLineResponse
                                    {
                                        Values = values
                                    });
                                }
                                break;
                        }

                    }

                    //order by amount of sales
                    reportSummary.ReportLines = reportSummary.ReportLines.OrderByDescending(x => Convert.ToDouble(x.Values[2]?.Replace("$", ""))).ToList();

                    break;
                case "graphic_selectable":
                    //TODO: implement grapihc summary type for Models summary

                    break;
                case "card":
                case "target":
                    //it doesnt have summary body
                    break;
            }

            return reportSummary;
        }

        private PeriodHelperResponse GetPeriodParts(string period)
        {
            PeriodHelperResponse response = new PeriodHelperResponse();

            response.PeriodParts = period.Split("-");
            response.PriorMonth = Convert.ToInt32(response.PeriodParts[1]) - 1;
            response.ZeroToSubtract = 0;
            if (response.PriorMonth == 0)
            {
                response.PriorMonth = 12;
                response.ZeroToSubtract = 1;
            }

            return response;
        }

        /// <summary>
        /// It proceses the YYYY periods and return a list of YYYY-MM periods.
        /// </summary>
        public List<string> PreparePeriods(List<string> periods)
        {
            List<string> defPeriods = new List<string>();

            //YYYY periods will be converted to 12 periods of YYYY-MM
            foreach(string period in periods)
            {
                if (Regex.IsMatch(period, @"^\d{4}$"))
                    for (int i = 1; i <= 12; i++)
                        defPeriods.Add(period + "-" + i.ToString("00"));
            }

            return defPeriods;
        }

        public Report GetReportConfig(long reportId, long clientId)
        {
            var report = _context.Reports.FirstOrDefault(r => r.IsActive && r.Id == reportId && (r.ClientId == clientId || r.ClientId == null));

            return report;
        }

        public List<long> GetAllStoreIds(long clientId)
        {
            var listOfStoreIds = _context.Stores.Where(x => x.IsActive && x.ClientId == clientId).Select(x => x.StoreId).ToList();

            return listOfStoreIds;
        }

        public async Task<Result<ReportDetailResponse>> GetReport(GetReportRequest request, bool periodsAreYmd)
        {
            var report = await _context.Reports.FirstOrDefaultAsync(r => r.IsActive && r.Id == request.ReportId && (r.ClientId == request.ClientId || r.ClientId == null));
            if (report == null)
                return await Result<ReportDetailResponse>.FailAsync("Report not found");

            ReportConfigResponse rcr = _mapper.Map<ReportConfigResponse>(report);

            List<string> columns = new List<string>();
            for (int i = 1; i <= report.ColumnsUsed; i++)
                columns.Add((string)report.GetType().GetProperty("Column" + i.ToString()).GetValue(report, null));
            
            rcr.Columns = columns;

            View viewOfReport = _context.Views.FirstOrDefault(x => x.ReportId == request.ReportId);
            if (viewOfReport != null)
            {
                rcr.ViewId = viewOfReport.ViewId;
                rcr.strWhere = viewOfReport.strWhere;
                rcr.States = viewOfReport.States;
                rcr.MessageBoard = viewOfReport.MessageBoard;
            }


            List<DateTime> periodsYmd = new List<DateTime>();
            if (periodsAreYmd)
            {
                foreach (string period in request.Period)
                    periodsYmd.Add(DateTime.ParseExact(period, "yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
            
            var reportLines = _context.ReportLines.Where(rl => rl.IsActive && rl.ReportId == report.Id)
                                                  .OrderBy(rl => rl.Order)
                                                  .ToList();

            var reportLineValues = await _context.ReportLineValues.Where(rlv => 
                                                                   rlv.ReportLine.ReportId == report.Id &&
                                                                   rlv.ClientId == request.ClientId &&
                                                                   request.Period.Contains(rlv.Period) &&
                                                                   rlv.IsActive)
                                                            .ToListAsync();

            //the storeId filter is only made when the report is no splitted by store
            if (!report.SplitByStore)
                reportLineValues = reportLineValues.Where(x => request.StoreId.Contains(x.StoreId.Value)).ToList();
            

            if (reportLineValues.Count() == 0) //no data for given period
            {
                ReportDetailResponse noDataRdr = new ReportDetailResponse()
                {
                    ReportConfig = rcr,
                    ReportLines = new List<ReportLineResponse>()
                };

                return await Result<ReportDetailResponse>.SuccessAsync(noDataRdr);
            }


            List<long> storeIds = new List<long>();
            if (report.ReportType == ReportType.Accounting && report.SplitByStore)
            {
                //this is a special case report, so we need to look for the lines without filtering by store
                reportLineValues = _context.ReportLineValues.Where(rlv =>
                                                                   rlv.ReportLine.ReportId == report.Id &&
                                                                   rlv.ClientId == request.ClientId &&
                                                                   request.Period.Contains(rlv.Period) &&
                                                                   rlv.IsActive)
                                                            .ToList();

                string splitByStoreIds = reportLineValues.First(x => x.SplitByStoreIds != null)?.SplitByStoreIds;
                storeIds = splitByStoreIds.Split(',').Select(long.Parse).ToList();

                List<Store> storesNeeded = await _context.Stores.Where(s => storeIds.Contains(s.StoreId)).ToListAsync();

                foreach(Store str in storesNeeded)
                {
                    for (int i = 1; i <= report.ColumnsUsed; i++)
                        rcr.Columns.Add((string)report.GetType().GetProperty("Column" + i.ToString()).GetValue(report, null) + " - " + str.AbbrName);
                }
            }


            if (report.ReportType == ReportType.Repeat) //if the report is repeat, we need to make a copy of the reportLine for each existing reportLineValue
            {
                for (int z = 0; z < reportLineValues.Count-1; z++)
                    reportLines.Add(reportLines.ElementAt(0));

                //creating last line for totals
                ReportLine lastLine = new ReportLine();
                ReportLine lineToCopy = reportLines.First();

                lastLine.Id = lineToCopy.Id;
                lastLine.ClientId = lineToCopy.ClientId;
                lastLine.ReportId = lineToCopy.ReportId;
                lastLine.Style = "Total";
                lastLine.Visible = true;
                lastLine.IsActive = true;
                lastLine.Order = lineToCopy.Order + 10;
                for (int i = 1; i <= report.ColumnsUsed; i++)
                    lastLine.GetType().GetProperty("Type" + i.ToString()).SetValue(lastLine, lineToCopy.GetType().GetProperty("Type" + i.ToString()).GetValue(lineToCopy, null));
                
                reportLines.Add(lastLine);
            }
            

            if (periodsAreYmd)
                reportLineValues = reportLineValues.Where(rlv => rlv.CreatedOn >= periodsYmd[0] && rlv.CreatedOn <= periodsYmd[1]).ToList(); //TODO: review this filter. doesnt make sense
            else
                reportLineValues = reportLineValues.Where(rlv => request.Period.Contains(rlv.Period)).ToList();

            List<ReportLineResponse> rlr = _mapper.Map<List<ReportLineResponse>>(reportLines);

            //if the report is repeat, we need the following two variables to calculate and retrieve the final total line
            ReportLineValue totalLineValue = new ReportLineValue();
            bool lastTotalLineAdded = false;

            rlr.ForEach(r =>
            {
                if(r.Style != "Title" || lastTotalLineAdded==true)
                {
                    var currentLineValue = reportLineValues.First(rlv => rlv.ReportLineId == r.Id);
                    reportLineValues.Remove(currentLineValue); //so in th repeat type report, the next loop will get the next reportLineValue and not the same
                    var currentLine = reportLines.First(rl => rl.Id == r.Id);

                    r.StoreId = currentLineValue.StoreId;
                    r.Period = currentLineValue.Period;

                    List<string> column_values = new List<string>();
                    List<string> column_formats = new List<string>();

                    int iAux = 1;
                    for (int i = 1; i <= report.ColumnsUsed * (storeIds.Count() + 1); i++)
                    {
                        string cellValue = (string)currentLineValue.GetType().GetProperty("Column" + i.ToString()).GetValue(currentLineValue, null);
                        string cellFormat = (string)currentLine.GetType().GetProperty("Type" + iAux.ToString()).GetValue(currentLine, null);

                        //if the report is repeat, we calculate the final total line as it loop through the reportLineValues
                        if (report.ReportType == ReportType.Repeat)
                        {
                            var accumulatedTotal = (string)totalLineValue.GetType().GetProperty("Column" + i.ToString()).GetValue(totalLineValue, null);
                            if(string.IsNullOrEmpty(accumulatedTotal) || string.IsNullOrWhiteSpace(accumulatedTotal))
                                accumulatedTotal = "0";

                            if(string.IsNullOrEmpty(cellValue) || string.IsNullOrWhiteSpace(cellValue))
                            {
                                totalLineValue.GetType().GetProperty("Column" + i.ToString()).SetValue(totalLineValue, "");
                            }
                            else if (cellFormat.Equals("dollar") || cellFormat.Equals("dollar_v") || cellFormat.Equals("double") || cellFormat.Equals("double_v") ||  cellFormat.Equals("indented_number") || cellFormat.Equals("indented_number_v") || cellFormat.Equals("percentage") || cellFormat.Equals("percentage_v"))
                            {
                                var newTotal = Convert.ToDecimal(accumulatedTotal) + Convert.ToDecimal(cellValue);
                                totalLineValue.GetType().GetProperty("Column" + i.ToString()).SetValue(totalLineValue, newTotal.ToString());
                            }
                            else if(cellFormat.Equals("integer_dollar") || cellFormat.Equals("integer_dollar_v") || cellFormat.Equals("integer") || cellFormat.Equals("integer_v"))
                            {
                                var newTotal = Convert.ToInt32(accumulatedTotal) + Convert.ToInt32(cellValue);
                                totalLineValue.GetType().GetProperty("Column" + i.ToString()).SetValue(totalLineValue, newTotal.ToString());
                            }
                            else
                            {
                                totalLineValue.GetType().GetProperty("Column" + i.ToString()).SetValue(totalLineValue, "");
                            }
                        }

                        if(cellFormat.Equals("daysToCurrentDate")) //special case cell format
                        {
                            cellFormat = "text";
                            DateTime today = DateTime.Today;
                            DateTime date = new DateTime();
                            try { date = Convert.ToDateTime(cellValue); } catch { }

                            if (cellValue == null || cellValue.Equals("") || date == new DateTime(0001, 01, 01))
                                cellValue = "";
                            else
                            {
                                int daysDiff = Convert.ToInt32((today - date).TotalDays);
                                if (daysDiff <= 0)
                                    daysDiff = 1;

                                cellValue = daysDiff.ToString();
                            }
                        }

                        column_values.Add(cellValue);
                        column_formats.Add(cellFormat);

                        if(iAux >= report.ColumnsUsed) //the typeFormats of the cols must be repeated for each repeated column
                            iAux = 1;
                        iAux++;
                    }

                    r.Values = column_values;
                    r.TypeFormats = column_formats;

                    //in the last iteration, we add the total line to the reportLineValues
                    if (report.ReportType == ReportType.Repeat && reportLineValues.Count == 0 && !lastTotalLineAdded)
                    {
                        totalLineValue.ReportLineId = r.Id;
                        totalLineValue.Period = r.Period;

                        reportLineValues.Add(totalLineValue);
                        lastTotalLineAdded = true;
                    }
                }
            });

            //internal report summaries load
            var reportSummaries = await _context.ReportSummaries.Where(x => x.ReportId == report.Id && !x.Main && x.IsActive).OrderBy(x => x.Order).ToListAsync();
            
            List<ReportSummaryResponse> rs = new List<ReportSummaryResponse>();

            foreach(var rep_sum in reportSummaries)
            {
                ReportSummaryResponse reportSummary = GetSummaryOfReport(rep_sum.Style,
                                                                         report,
                                                                         request.Target,
                                                                         request.ClientId,
                                                                         request.StoreId.FirstOrDefault(),
                                                                         rep_sum.LinesQty, 
                                                                         rep_sum.TargetColumns, 
                                                                         rep_sum.ColumnTitles, 
                                                                         rep_sum.CalcMode, 
                                                                         rep_sum.Position, 
                                                                         request.Period.First(),
                                                                         null);

                reportSummary.ReportConfig.SummaryStyle = rep_sum.Style;
                reportSummary.ReportConfig.Name = rep_sum.Name;
                reportSummary.ReportConfig.Description = rep_sum.Description;
                reportSummary.ReportConfig.Order = rep_sum.Order;

                rs.Add(reportSummary);
            }

            rs = rs.OrderBy(x => x.ReportConfig.Order).ToList();


            ReportDetailResponse rdr = new ReportDetailResponse()
            {
                ReportConfig = rcr,
                ReportLines = rlr,
                ReportSummaries = rs
            };

            return await Result<ReportDetailResponse>.SuccessAsync(rdr);
        }

        public async Task<Result<ReportDetailResponse>> GetReportByStore(GetReportRequest request, bool periodsAreYmd)
        {
            var report = await _context.Reports.FirstOrDefaultAsync(r => r.IsActive && r.Id == request.ReportId && r.ClientId == request.ClientId);
            if (report == null)
                return await Result<ReportDetailResponse>.FailAsync("Report not found");

            ReportConfigResponse rcr = _mapper.Map<ReportConfigResponse>(report);

            //if the storeId list comes empty, we fill it with all the stores of the client
            if (request.StoreId == null || request.StoreId.Count() == 0)
                request.StoreId = _context.Stores.Where(s => s.ClientId == request.ClientId && s.IsActive).Select(s => s.StoreId).ToList();

            var storeNames = await _context.Stores.Where(s => request.StoreId.Contains(s.StoreId)).Select(x => x.Name).ToListAsync();

            List<string> columns = new List<string>();
            foreach (var store in storeNames)
                columns.Add($"{store}");            
            rcr.Columns = columns;

            List<DateTime> periodsYmd = new List<DateTime>();
            if (periodsAreYmd)
            {
                foreach (string period in request.Period)
                    periodsYmd.Add(DateTime.ParseExact(period, "yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            var reportLines = _context.ReportLines.Where(rl => rl.IsActive && rl.ReportId == report.Id)
                                                  .OrderBy(rl => rl.Order)
                                                  .ToList();

            var firstPart = _context.ReportLineValues
                                .Where(rlv => request.StoreId.Contains(rlv.StoreId.Value) &&
                                        request.Period.Contains(rlv.Period) &&
                                        rlv.IsActive &&
                                        rlv.ReportLine.ReportId == report.Id)
                                .ToList();

            if (periodsAreYmd)
                firstPart = firstPart.Where(rlv => rlv.CreatedOn >= periodsYmd[0] && rlv.CreatedOn <= periodsYmd[1]).ToList();
            else
                firstPart = firstPart.Where(rlv => request.Period.Contains(rlv.Period)).ToList();

            var reportLineValues = firstPart.GroupBy(rlv => new { rlv.StoreId, rlv.ReportLineId })
                                    .AsEnumerable() //to be able to use body statement on Select
                                    .Select(periodGroup => new ReportLineValueAggrByStore
                                    {
                                        StoreId = periodGroup.First().StoreId.Value,
                                        ReportLineId = periodGroup.First().ReportLineId,
                                        ColumnValue = periodGroup.Sum(x => Convert.ToDouble(x.GetType().GetProperty("Column" + report.AggrByColumnIdx).GetValue(x, null))).ToString()
                                    })
                                    .ToList();


            List<ReportLineResponse> rlr = _mapper.Map<List<ReportLineResponse>>(reportLines);
            rlr.ForEach(r =>
            {
                var currentLineValues = reportLineValues.Where(rlv => rlv.ReportLineId == r.Id);

                List<string> column_values = new List<string>();
                List<string> column_formats = new List<string>();

                foreach (var storeId in request.StoreId)
                {
                    var currentLineValue = currentLineValues.FirstOrDefault(rlv => rlv.StoreId == storeId);
                    r.Period = "Mixed";

                    column_values.Add(currentLineValue?.ColumnValue ?? "0");
                    column_formats.Add("");
                }

                r.Values = column_values;
            });

            ReportDetailResponse rdr = new ReportDetailResponse()
            {
                ReportConfig = rcr,
                ReportLines = rlr
            };

            return await Result<ReportDetailResponse>.SuccessAsync(rdr);
        }

        public async Task<Result<ReportDetailResponse>> GetReportByMonth(GetReportRequest request)
        {
            var report = await _context.Reports.FirstOrDefaultAsync(r => r.IsActive && r.Id == request.ReportId && r.ClientId == request.ClientId);
            if (report == null)
                return await Result<ReportDetailResponse>.FailAsync("Report not found");

            ReportConfigResponse rcr = _mapper.Map<ReportConfigResponse>(report);

            if(request.ByTrend != null && request.ByTrend.Value)
            {
                request.Period = new List<string>();

                var currentPeriodDate = DateTime.Now;
                for (int i = 0; i < 24; i++)
                {
                    request.Period.Add(currentPeriodDate.ToString("yyyy-MM"));
                    currentPeriodDate = currentPeriodDate.AddMonths(-1);
                }
            }

            List<string> columns = new List<string>();
            for (int i = 1; i <= request.Period.Count; i++)
            {
                var periodParts = request.Period[i - 1].Split('-');

                string monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(Convert.ToInt32(periodParts[1]));
                columns.Add($"{monthName} {periodParts[0].Substring(2, 2)}");
            }
            rcr.Columns = columns;


            var reportLines = _context.ReportLines.Where(rl => rl.IsActive && rl.ReportId == report.Id)
                                                  .OrderBy(rl => rl.Order)
                                                  .ToList();

            //if the storeId list comes empty, we fill it with all the stores of the client
            if (request.StoreId == null || request.StoreId.Count() == 0)
                request.StoreId = _context.Stores.Where(s => s.ClientId == request.ClientId && s.IsActive).Select(s => s.StoreId).ToList();


            var firstPart = _context.ReportLineValues
                                .Where(rlv => request.StoreId.Contains(rlv.StoreId.Value) &&
                                        request.Period.Contains(rlv.Period) &&
                                        rlv.IsActive &&
                                        rlv.ReportLine.ReportId == report.Id)
                                .GroupBy(rlv => new { rlv.Period, rlv.ReportLineId })
                                .ToList();

            var reportLineValues = firstPart.AsEnumerable() //to be able to use body statement on Select
                                    .Select(periodGroup => new ReportLineValueAggrByMonth
                                    {
                                        Period = periodGroup.First().Period,
                                        ReportLineId = periodGroup.First().ReportLineId,
                                        ColumnValue = periodGroup.Sum(x => Convert.ToDouble(x.GetType().GetProperty("Column" + report.AggrByColumnIdx).GetValue(x, null))).ToString()
                                    })
                                    .ToList();


            List<ReportLineResponse> rlr = _mapper.Map<List<ReportLineResponse>>(reportLines);
            rlr.ForEach(r =>
            {
                var currentLineValues = reportLineValues.Where(rlv => rlv.ReportLineId == r.Id);

                List<string> column_values = new List<string>();
                List<string> column_formats = new List<string>();

                foreach (var period in request.Period)
                {
                    var currentLineValue = currentLineValues.FirstOrDefault(rlv => rlv.Period == period);
                    r.Period = "Mixed";

                    column_values.Add(currentLineValue?.ColumnValue ?? "0");
                    column_formats.Add("");
                }

                r.Values = column_values;               
            });

            ReportDetailResponse rdr = new ReportDetailResponse()
            {
                ReportConfig = rcr,
                ReportLines = rlr
            };

            return await Result<ReportDetailResponse>.SuccessAsync(rdr);
        }

        /// <summary>
        /// It generates a hardcoded report structure for the drilldown of a report line or account. Once we have real data we need to adapt this service to work dynamically.
        /// </summary>
        public async Task<Result<ReportDetailResponse>> GetLineDrilldown(GetLineDrilldownRequest request)
        {
            if (request.Level == 1 && request.ReportLineId != null)
            {
                var reportLine = _context.ReportLines.FirstOrDefault(r => r.Id == request.ReportLineId);
                if (reportLine == null)
                    return await Result<ReportDetailResponse>.FailAsync("Report line not found");

                ReportConfigResponse rcr = new ReportConfigResponse();

                rcr.Name = reportLine.Name;
                rcr.Description = $"Drilldown level 1 of {reportLine.Name} report line";
                rcr.Visible = true;
                rcr.Style = "";

                List<string> columns = new List<string>() { "ACCT", "GL Desc", "ACC Type", "MTD", "YTD", "CNT MTD", "CNT YTD" };
                rcr.Columns = columns;

                List<ReportLineResponse> rlr = new List<ReportLineResponse>();
                for (int i=0; i<4; i++)
                {
                    rlr.Add(new ReportLineResponse()
                    {
                        Order = 10+(i*10),
                        Name = "Hardcoded line level 1",
                        TypeFormats = i != 3 ? new List<string>() { "string", "string", "string", "dollar", "dollar", "integer", "integer" } : new List<string>() { "string", "dollar", "string", "integer" },
                        StoreId = reportLine.StoreId.Value,
                        Visible = true,
                        Drillable = true,
                        Style = i != 3 ? "" : "final",
                        Values = i != 3 ? new List<string>() { "43012", "Jag Xj USD RTL-non Certified", "S", "220000", "4250200", "5", (100+i).ToString() } : new List<string>() { "Total Amount", "3153", "Total Count", "68" }
                    });
                }

                ReportDetailResponse rdr = new ReportDetailResponse()
                {
                    ReportConfig = rcr,
                    ReportLines = rlr
                };

                return await Result<ReportDetailResponse>.SuccessAsync(rdr);
            }
            else if(request.Level == 2 && request.AccountNo != null)
            {
                ReportConfigResponse rcr = new ReportConfigResponse();

                rcr.Name = request.AccountNo;
                rcr.Description = $"Drilldown level 2 of {request.AccountNo} account";
                rcr.Visible = true;
                rcr.Style = "";

                List<string> columns = new List<string>() { "Posting Description", "GL Date", "Control 1", "Reference ID", "Post Amount" };
                rcr.Columns = columns;

                List<string> types = new List<string>() { "string", "string", "string", "string", "dollar" };

                List<ReportLineResponse> rlr = new List<ReportLineResponse>();
                for (int i = 0; i < 4; i++)
                {
                    rlr.Add(new ReportLineResponse()
                    {
                        Order = 10 + (i * 10),
                        Name = "Hardcoded line level 1",
                        TypeFormats = i != 3 ? new List<string>() { "string", "string", "string", "string", "dollar" } : new List<string>() { "string", "dollar", "string", "integer" },
                        Visible = true,
                        Drillable = true,
                        Style = i != 3 ? "" : "final",
                        Values = i != 3 ? new List<string>() { "Sale Vehicle Invoice", $"11/{(15+i)}/2022", "855235", "152399", "25240" } : new List<string>() { "Total Amount", "-218094", "Total Count", "6" }
                    });
                }

                ReportDetailResponse rdr = new ReportDetailResponse()
                {
                    ReportConfig = rcr,
                    ReportLines = rlr
                };

                return await Result<ReportDetailResponse>.SuccessAsync(rdr);
            }
            else
            {
                return await Result<ReportDetailResponse>.FailAsync("Invalid parameters. If level == 1, reportLineId shouldn't be null. If level == 2, accountNo shouldn't be null.");
            }
        }
    }
}
