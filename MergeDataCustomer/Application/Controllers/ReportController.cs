using AutoMapper;
using MergeDataCustomer.Application.Services;
using MergeDataCustomer.Helpers.Configuration;
using MergeDataCustomer.Repositories.DtoModels.Requests;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Schemas.Public;
using MergeDataImporter.Helpers.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Text.RegularExpressions;

namespace MergeDataCustomer.Application.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "Layer4")]
    [ApiVersion(ApiVersioning.CurrentVersion)]
    public class ReportController : ControllerBase
    {
        private readonly ReportService _reportService;

        public ReportController(ReportService reportService)
        {
            _reportService = reportService;
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
        [HttpGet]
        public async Task<IActionResult> GetReportListBySubSection(long subSectionId, long clientId, string userId)
        {
            var result = await _reportService.GetReportListBySubSection(subSectionId, clientId, userId);

            return Ok(result);
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

            if((request.ByTrend == null || !request.ByTrend.Value) &&
               request.StoreId != null && request.StoreId.Count == 1 &&
               (((periodsFormat == 1 || periodsFormat == 2) && request.Period.Count == 1) || (periodsFormat == 3 && request.Period.Count == 2))) //one period if yyyy or yyyy-mm format, or 2 periods if ymd format
            {
                if(periodsFormat == 1)
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

        [Route("GetReportSummary")]
        [Authorize(Policy = Permissions.User.View)]
        [HttpPost]
        public async Task<IActionResult> GetReportSummary([FromForm] ReportSummaryRequest request)
        {
            var result = await _reportService.GetReportSummary(request);

            return Ok(result);
        }

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
