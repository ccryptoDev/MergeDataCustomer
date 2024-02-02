using CsvHelper;
using MergeDataCustomer.Application.Services;
using MergeDataCustomer.Helpers.Configuration;
using MergeDataCustomer.Repositories.DtoModels.Requests;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Schemas.Public;
using MergeDataEntities.Schemas.Reports;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MergeDataCustomer.Application.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "Layer3")]
    [ApiVersion(ApiVersioning.CurrentVersion)]
    public class ReportController : ControllerBase
    {
        private readonly ReportService _reportService;
        private readonly RawContext _context;

        public ReportController(ReportService reportService, RawContext context)
        {
            _reportService = reportService;
            _context = context;
        }

        [Route("GetSections")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpGet]
        public async Task<IActionResult> GetSections(string userId)
        {
            var auxResult = await _reportService.GetSections(userId);
            List<Section> result = auxResult.Data;

            return Ok(result);
        }

        [Route("GetSubSections")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpGet]
        public async Task<IActionResult> GetSubSections(long sectionId, string userId)
        {
            var auxResult = await _reportService.GetSubSections(sectionId, userId);
            List<SubSection> result = auxResult.Data;

            return Ok(result);
        }

        [Route("GetReportListBySubSection")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpPost]
        public async Task<IActionResult> GetReportListBySubSection(GetReportListBySubSectionRequest request)
        {
            var result = await _reportService.GetReportListBySubSection(request.SubSectionId, request.ClientId, request.StoreId.First(), request.UserId, request.Period, request.Target);

            return Ok(result);
        }

        [Route("GetUpdatedReportSummary")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpPost]
        public async Task<IActionResult> GetUpdatedReportSummary(GetReportSummaryRequest request)
        {
            var result = await _reportService.GetUpdatedReportSummary(request.ReportId, request.ClientId, request.StoreId.First(), request.Period, request.Target, request.SelectedOptions);

            return Ok(result);
        }

        [Route("GetCSVReport")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpPost]
        public async Task<IActionResult> GetCSVReport(GetReportRequest request)
        {
            var reportResult = await GetReport(request) as OkObjectResult;

            if (reportResult == null || !(reportResult.Value is ReportDetailResponse))
            {
                return BadRequest("Failed to generate report");
            }

            var reportData = reportResult.Value as ReportDetailResponse;

            var csvBuilder = new StringBuilder();
            var stringWriter = new StringWriter(csvBuilder);

            using (var csvWriter = new CsvWriter(stringWriter, CultureInfo.InvariantCulture))
            {
                if(reportData.ReportConfig.Columns.Count > 0 && reportData.ReportLines.Count > 0)
                {
                    foreach (var header in reportData.ReportConfig.Columns) //headers
                    {
                        csvWriter.WriteField(header);
                    }
                    csvWriter.NextRecord();

                    foreach (var record in reportData.ReportLines) //records
                    {
                        foreach (var field in record.Values) //fields
                        {
                            csvWriter.WriteField(field.TrimEnd());
                        }   
                        csvWriter.NextRecord();
                    }
                }
                else //PATCH for demo: special case of getting data directly from the normalized tables
                {
                    //first and last day of period 
                    var period = request.Period.First().Split("-");
                    DateOnly firstDay = new DateOnly(Convert.ToInt32(period[0]), Convert.ToInt32(period[1]), 1);
                    DateOnly lastDay = new DateOnly(Convert.ToInt32(period[0]), Convert.ToInt32(period[1]), DateTime.DaysInMonth(Convert.ToInt32(period[0]), Convert.ToInt32(period[1])));

                    IEnumerable<Object> normalizedData = new List<Object>();

                    switch (reportData.ReportConfig.Name)
                    {
                        case string a when a.Contains("Service"):
                            normalizedData = _context.NormalizedServices.Where(x => x.ClientId == request.ClientId &&
                                                                                     request.StoreId.Contains(x.StoreId) && 
                                                                                     x.DateClosed >= firstDay && x.DateClosed <= lastDay)
                                                                        .ToList();
                            break;
                        case string a when a.Contains("Sale"):
                            normalizedData = _context.NormalizedSales.Where(x => x.ClientId == request.ClientId &&
                                                                                     request.StoreId.Contains(x.StoreId) &&
                                                                                     x.DealDate >= firstDay && x.DealDate <= lastDay)
                                                                      .ToList();

                            break;
                    }

                    if (normalizedData.ToList().Count > 0)
                    {
                        foreach (PropertyInfo prop in normalizedData.First().GetType().GetProperties())
                        {
                            if (prop.Name != "IsActive" && prop.Name != "CreatedBy" && prop.Name != "CreatedOn" && prop.Name != "ModifiedBy" && prop.Name != "ModifiedOn" && prop.Name != "Id" && prop.Name != "ClientId" && prop.Name != "StoreId" && prop.Name != "Client" && prop.Name != "Store" && //generic fields
                                prop.Name != "FirstName" && prop.Name != "MiddleName" && prop.Name != "Lastname" && prop.Name != "Birthdate" && prop.Name != "Address1" && prop.Name != "Address2" && prop.Name != "City" && prop.Name != "State" && prop.Name != "WorkPhone" && prop.Name != "Phone" && prop.Name != "WorkPhoneExt" && //service fields
                                prop.Name != "CustomerId" && prop.Name != "LastName" && prop.Name != "BirthDate" && prop.Name != "Address" && prop.Name != "AddressInfo" && prop.Name != "Email") //sales fields
                                csvWriter.WriteField(prop.Name);
                        }
                        csvWriter.NextRecord();

                        foreach (var record in normalizedData) //records
                        {
                            foreach (PropertyInfo prop in record.GetType().GetProperties())
                            {
                                //avoid adding nonsense fields
                                if (prop.Name != "IsActive" && prop.Name != "CreatedBy" && prop.Name != "CreatedOn" && prop.Name != "ModifiedBy" && prop.Name != "ModifiedOn" && prop.Name != "Id" && prop.Name != "ClientId" && prop.Name != "StoreId" && prop.Name != "Client" && prop.Name != "Store" && //generic fields
                                prop.Name != "FirstName" && prop.Name != "MiddleName" && prop.Name != "Lastname" && prop.Name != "Birthdate" && prop.Name != "Address1" && prop.Name != "Address2" && prop.Name != "City" && prop.Name != "State" && prop.Name != "WorkPhone" && prop.Name != "Phone" && prop.Name != "WorkPhoneExt" && //service fields
                                prop.Name != "CustomerId" && prop.Name != "LastName" && prop.Name != "BirthDate" && prop.Name != "Address" && prop.Name != "AddressInfo" && prop.Name != "Email") //sales fields
                                {
                                    var celldata = record.GetType().GetProperty(prop.Name)?.GetValue(record)?.ToString()?.Trim();
                                    csvWriter.WriteField(celldata);
                                }
                            }
                            csvWriter.NextRecord();
                        }
                    }
                }
            }

            return File(new MemoryStream(Encoding.UTF8.GetBytes(csvBuilder.ToString())), "text/csv", "Report.csv");
        }


        [Route("GetReport")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpPost]
        public async Task<IActionResult> GetReport(GetReportRequest request)
        {
            if (request.Period.Count() > 1) //remove duplicate periods
                request.Period = request.Period.Distinct().ToList();
            else if(request.Period.Count() == 0 && !request.ByTrend.Value)
                return BadRequest("At least one period is required to get report content.");

            int periodsFormat = 0;

            if(!request.ByTrend.Value) //if byTrend is required, we dont waste resources on validating the periods
            {
                if (Regex.IsMatch(request.Period.First(), @"^\d{4}$"))
                {
                    periodsFormat = 1;
                    foreach (var period in request.Period)
                        if (!Regex.IsMatch(period, @"^\d{4}$"))
                            return BadRequest("All the periods must be the same format. If the first one is YYYY, all of them should follow the same format.");
                }
                if (Regex.IsMatch(request.Period.First(), @"^\d{4}-\d{2}$"))
                {
                    periodsFormat = 2;
                    foreach (var period in request.Period)
                        if (!Regex.IsMatch(period, @"^\d{4}-\d{2}$"))
                            return BadRequest("All the periods must be the same format. If the first one is YYYY-MM, all of them should follow the same format.");
                }
                if (Regex.IsMatch(request.Period.First(), @"^\d{4}-\d{2}-\d{2}$"))
                {
                    periodsFormat = 3;
                    foreach (var period in request.Period)
                        if (!Regex.IsMatch(period, @"^\d{4}-\d{2}-\d{2}$"))
                            return BadRequest("All the periods must be the same format. If the first one is YYYY-MM-DD, all of them should follow the same format.");
                }
            }
            else
                periodsFormat = 2;

            var result = new Result<ReportDetailResponse>();
            bool anyPath = false;

            Report reportConfig = _reportService.GetReportConfig(request.ReportId, request.ClientId);

            //if the report is "splitted by store", we add all the stores to the request
            if (reportConfig.SplitByStore && (request.StoreId == null || request.StoreId.Count == 0 || (request.StoreId.Count == 1 && request.StoreId.First() == 0)))
                request.StoreId = _reportService.GetAllStoreIds(request.ClientId);


            if ((request.ByTrend == null || !request.ByTrend.Value) &&
               request.StoreId != null && request.StoreId.Count == 1 &&
               (((periodsFormat == 1 || periodsFormat == 2) && request.Period.Count == 1) || (periodsFormat == 3 && request.Period.Count == 2)) ||
               reportConfig.SplitByStore) //one period if yyyy or yyyy-mm format, or 2 periods if ymd format
            {
                if(periodsFormat == 1) //if the period(s) are in YYYY format, we transform them to 12 separated periods in YYYY-MM 
                    request.Period = _reportService.PreparePeriods(request.Period);

                anyPath = true;

                //whole report
                result = await _reportService.GetReport(request, periodsFormat == 3);
            }
            else if((request.ByTrend == null || !request.ByTrend.Value) &&
               (request.StoreId == null || request.StoreId.Count == 0 || request.StoreId.Count > 1) &&
               (((periodsFormat == 1 || periodsFormat == 2) && request.Period.Count == 1) || (periodsFormat == 3 && request.Period.Count == 2)))
            {
                if (periodsFormat == 1)
                    request.Period = _reportService.PreparePeriods(request.Period);

                anyPath = true;

                //by store
                result = await _reportService.GetReportByStore(request, periodsFormat == 3);
            }
            else if ((request.ByTrend != null && request.ByTrend.Value) || //specifically requested by trend
                (request.StoreId == null || request.StoreId.Count == 0 || request.StoreId.Count > 1) && 
                request.Period.Count > 1)
            {
                if(periodsFormat != 2)
                    return BadRequest("Invalid period format. It must be YYYY-MM when trying to get report By Month or By Trend.");

                anyPath = true;

                //by month or trend
                result = await _reportService.GetReportByMonth(request);
            }

            if(!anyPath)
                return BadRequest("The data combination specified doesn't belongs to any allowed form of the report (Whole report - By Store - By Month/Trend). Review the possible scenarios consumption for this endpoint.");

            if (result.Succeeded)
                return Ok(result.Data);
            else
                return BadRequest(result.Messages);
        }

        //[Route("GetReportSummaries")]
        //[Authorize(Policy = Permissions.User.View)]
        //[HttpPost]
        //public async Task<IActionResult> GetReportSummaries(int subSectionID)
        //{
        //    var result = await _reportService.GetReportSummaries(subSectionID);

        //    return Ok(result);
        //}

        [Route("GetLineDrilldown")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpPost]
        public async Task<IActionResult> GetLineDrilldown(GetLineDrilldownRequest request)
        {
            var result = await _reportService.GetLineDrilldown(request);
            if(result.Succeeded)
                return Ok(result);

            return BadRequest(result);
        }
    }
}
