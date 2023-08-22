using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Requests;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Schemas.Reports;
using MergeDataEntities.Schemas.Public;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;
using MergeDataImporter.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Globalization;
using static MergeDataImporter.Helpers.Generic.Permissions;
using System.Text.RegularExpressions;
using MergeDataImporter.Repositories.DtoModels.Requests.Identity;

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

        public async Task<Result<List<ReportContractResponse>>> GetReportListBySubSection(long subSectionId, long clientId, string userId)
        {
            List<Report> reports = new List<Report>();
            bool isAdministrator = await UserIsAdministrator(userId);

            if (isAdministrator)
            {
                reports = _context.Reports.Where(r => r.IsActive && r.SubSectionId == subSectionId && r.ClientId == clientId).ToList();
            }
            else//if the user isnt admin, we retrive only the reports that the user has access to
            { 
                var userReports = await _context.UserReports.Where(ur => ur.UserId == userId).Select(x => x.ReportId).ToListAsync();
                reports = _context.Reports.Where(r => r.IsActive && userReports.Contains(r.Id) && r.SubSectionId == subSectionId && r.ClientId == clientId).ToList();
            }

            var defResponse = _mapper.Map<List<ReportContractResponse>>(reports);

            return await Result<List<ReportContractResponse>>.SuccessAsync(defResponse);
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

        public async Task<Result<ReportDetailResponse>> GetReport(GetReportRequest request, bool periodsAreYmd)
        {
            var report = await _context.Reports.FirstOrDefaultAsync(r => r.IsActive && r.Id == request.ReportId && r.ClientId == request.ClientId);
            if (report == null)
                return await Result<ReportDetailResponse>.FailAsync("Report not found");

            ReportConfigResponse rcr = _mapper.Map<ReportConfigResponse>(report);

            List<string> columns = new List<string>();
            for (int i = 1; i <= report.ColumnsUsed; i++)
                columns.Add((string)report.GetType().GetProperty("Column" + i.ToString()).GetValue(report, null));
            
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
            var reportLineValues = _context.ReportLineValues.Where(rlv => request.StoreId.Contains(rlv.StoreId) &&
                                                                   rlv.IsActive &&
                                                                   rlv.ReportLine.ReportId == report.Id)
                                                            .ToList();

            if(periodsAreYmd)
                reportLineValues = reportLineValues.Where(rlv => rlv.CreatedOn >= periodsYmd[0] && rlv.CreatedOn <= periodsYmd[1]).ToList();
            else
                reportLineValues = reportLineValues.Where(rlv => request.Period.Contains(rlv.Period)).ToList();


            if (reportLineValues.Count == 0)
                return await Result<ReportDetailResponse>.FailAsync("No data for given period");

            List<ReportLineResponse> rlr = _mapper.Map<List<ReportLineResponse>>(reportLines);
            rlr.ForEach(r =>
            {
                var currentLineValue = reportLineValues.First(rlv => rlv.ReportLineId == r.Id);
                var currentLine = reportLines.First(rl => rl.Id == r.Id);

                r.StoreId = currentLineValue.StoreId;
                r.Period = currentLineValue.Period;

                List<string> column_values = new List<string>();
                List<string> column_formats = new List<string>();
                for (int i = 1; i <= report.ColumnsUsed; i++)
                {
                    column_values.Add((string)currentLineValue.GetType().GetProperty("Column" + i.ToString()).GetValue(currentLineValue, null));
                    column_formats.Add((string)currentLine.GetType().GetProperty("Type" + i.ToString()).GetValue(currentLine, null));
                }

                r.Values = column_values;
                r.TypeFormats = column_formats;
            });

            ReportDetailResponse rdr = new ReportDetailResponse()
            {
                ReportConfig = rcr,
                ReportLines = rlr
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
                                .Where(rlv => request.StoreId.Contains(rlv.StoreId) &&
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
                                        StoreId = periodGroup.First().StoreId,
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
                                .Where(rlv => request.StoreId.Contains(rlv.StoreId) &&
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

        public async Task<Result<ReportSummaryResponse>> GetReportSummary(ReportSummaryRequest request)
        {
            var report = await _context.Reports.FirstOrDefaultAsync(r => r.IsActive && r.Id == request.ReportId && r.ClientId == request.ClientId);
            if (report == null)
                return await Result<ReportSummaryResponse>.FailAsync("Report not found");

            return await Result<ReportSummaryResponse>.SuccessAsync(_mapper.Map<ReportSummaryResponse>(report));
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
                        StoreId = reportLine.StoreId,
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
