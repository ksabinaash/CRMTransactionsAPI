﻿using CRMTransactions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CRMTransactions.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CallsIntelligenceController : ControllerBase
    {
        private readonly AppDbContext context;

        private readonly ILogger logger;

        private readonly IConfiguration config;

        public CallsIntelligenceController(AppDbContext context, ILogger<CallsIntelligenceController> logger, IConfiguration config)
        {
            this.context = context;

            this.logger = logger;

            this.config = config;
        }

        [HttpGet]
        [Route("GetCallVolumeChartData")]
        public async Task<ActionResult<CallVolumeChart>> GetCallVolumeChartData(DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (fromDate == null)
            {
                fromDate = DateTime.Now.AddDays(Convert.ToDouble(config.GetValue<string>("DefaultReportFromDate")));
            }

            if (toDate == null)
            {
                toDate = DateTime.Now;
            }

            var labs = GetLabs().Result.Value;

            var callTypes = new List<string>() { "Missed", "Incoming", "Outgoing" };

            Dictionary<string, List<ChartMetrics>> callVolumeDictionary = new Dictionary<string, List<ChartMetrics>>();

            foreach (var item in callTypes)
            {
                var callType = new List<ChartMetrics>();

                foreach (var labName in labs)
                {
                    ChartMetrics chartV2 = new ChartMetrics();

                    chartV2.Name = labName;

                    callType.Add(chartV2);
                }

                callVolumeDictionary.Add(item, callType);
            }

            var validCalls = await context.ValidCalls
                                              .Where(s => s.EventTime >= fromDate && s.EventTime <= toDate)
                                              .Select(x => new { x.LabName, x.ValidCallId, x.CallType })
                                              .ToListAsync();

            var missedCalls = await context.MissedCalls
                                           .Where(s => s.EventTime >= fromDate && s.EventTime <= toDate)
                                           .Select(x => new { x.LabName, x.Id, CallType = "Missed" })
                                           .ToListAsync();

            var groupedValidCalls = validCalls.GroupBy(vc => new { vc.LabName, vc.CallType })
                                             .Select(group => new
                                             {
                                                 Metric = group.Key,
                                                 Count = group.Count()
                                             }).ToList();

            var groupedMissedCalls = missedCalls.GroupBy(vc => new { vc.LabName, vc.CallType })
                                            .Select(group => new
                                            {
                                                Metric = group.Key,
                                                Count = group.Count()
                                            }).ToList();

            foreach (var item in groupedMissedCalls)
            {
                if (item.Metric.CallType == null)
                    continue;

                var callType = callVolumeDictionary[item.Metric.CallType] as List<ChartMetrics>;

                callType.Where(m => m.Name.ToUpper() == item.Metric.LabName.ToUpper()).ToList().ForEach(s => s.count = item.Count);
            }

            foreach (var item in groupedValidCalls)
            {
                if (item.Metric.CallType == null)
                    continue;

                var callType = callVolumeDictionary[item.Metric.CallType] as List<ChartMetrics>;

                callType.Where(m => m.Name.ToUpper() == item.Metric.LabName.ToUpper()).ToList().ForEach(s => s.count = item.Count);
            }

            CallVolumeChart response = new CallVolumeChart();

            response.labs = labs;

            response.callTypes = callTypes;

            response.volumeData = callVolumeDictionary;

            response.countData = new List<List<string>>();

            foreach (var item in callVolumeDictionary.Values)
            {
                response.countData.Add(item.Select(m => m.count.ToString()).ToList());
            }

            return response;
        }

        [HttpGet]
        [Route("GetCallPurposeChartData")]
        public async Task<ActionResult<CallPurposeChart>> GetCallPurposeChartData(DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (fromDate == null)
            {
                fromDate = DateTime.Now.AddDays(Convert.ToDouble(config.GetValue<string>("DefaultReportFromDate")));
            }

            if (toDate == null)
            {
                toDate = DateTime.Now;
            }

            var labs = GetLabs().Result.Value;

            var callPurpose = await context.CallPurpose.ToListAsync();

            var purposes = callPurpose.Select(m => m.PurposeoftheCall).ToList();

            Dictionary<string, List<ChartMetrics>> purposeDictionary = new Dictionary<string, List<ChartMetrics>>();

            foreach (var item in purposes)
            {
                var purpose = new List<ChartMetrics>();

                foreach (var lab in labs)
                {
                    ChartMetrics chartV2 = new ChartMetrics();

                    chartV2.Name = lab;

                    purpose.Add(chartV2);
                }

                purposeDictionary.Add(item, purpose);
            }

            var validCalls = await context.ValidCalls
                                              .Where(s => s.EventTime >= fromDate && s.EventTime <= toDate)
                                              .Select(x => new { x.LabName, x.ValidCallId, x.CallPurpose })
                                              .ToListAsync();

            var groupedValidCalls = validCalls.GroupBy(vc => new { vc.LabName, vc.CallPurpose })
                                             .Select(group => new
                                             {
                                                 Metric = group.Key,
                                                 Count = group.Count()
                                             }).ToList();

            foreach (var item in groupedValidCalls)
            {
                var purpose = purposeDictionary[item.Metric.CallPurpose] as List<ChartMetrics>;

                purpose.Where(m => m.Name == item.Metric.LabName).ToList().ForEach(s => s.count = item.Count);
            }

            CallPurposeChart response = new CallPurposeChart();

            response.labs = labs;

            response.purposes = purposeDictionary.Keys.ToList();

            response.purposeData = purposeDictionary;

            response.countData = new List<List<int>>();

            foreach (var item in purposeDictionary.Values)
            {
                response.countData.Add(item.Select(m => m.count).ToList());
            }
            response.sumData = new List<string>();

            foreach (var item in response.countData)
            {
                response.sumData.Add(item.ToList().Sum().ToString());
            }

            return response;
        }

        [HttpGet]
        [Route("GetCallTrendChartData")]
        public async Task<ActionResult<CallTrendChart>> CallTrendChartData([Required] string labName, DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (fromDate == null)
            {
                fromDate = DateTime.Now.AddDays(Convert.ToDouble(config.GetValue<string>("DefaultReportFromDate")));
            }

            if (toDate == null)
            {
                toDate = DateTime.Now;
            }

            CallTrendChart response = new CallTrendChart();

            var labs = GetLabs().Result.Value;

            var months = GetMonths(fromDate.GetValueOrDefault(), toDate.GetValueOrDefault());

            response.labs = labs;

            response.period = months;

            response.labName = labName;

            var callTypes = new List<string>() { "Missed", "Incoming", "Outgoing" };

            response.callTypes = callTypes;

            Dictionary<string, List<ChartMetrics>> callTrendDictionary = new Dictionary<string, List<ChartMetrics>>();

            foreach (var item in months)
            {
                var period = new List<ChartMetrics>();

                foreach (var callType in callTypes)
                {
                    ChartMetrics chartV2 = new ChartMetrics();

                    chartV2.Name = callType;

                    period.Add(chartV2);
                }

                callTrendDictionary.Add(item, period);
            }

            var validCalls = await context.ValidCalls
                                                 .Where(s => s.EventTime >= fromDate && s.EventTime <= toDate && s.LabName.ToUpper() == labName.ToUpper())
                                                 .Select(x => new { x.EventTime, x.ValidCallId, x.CallType })
                                                 .ToListAsync();

            var missedCalls = await context.MissedCalls
                                           .Where(s => s.EventTime >= fromDate && s.EventTime <= toDate && s.LabName.ToUpper() == labName.ToUpper())
                                           .Select(x => new { x.EventTime, x.Id, CallType = "Missed" })
                                           .ToListAsync();

            var groupedValidCalls = validCalls.GroupBy(vc => new { vc.EventTime.Month, vc.EventTime.Year, vc.CallType })
                                             .Select(group => new
                                             {
                                                 Metric = group.Key,
                                                 Count = group.Count()
                                             }).ToList();

            var groupedMissedCalls = missedCalls.GroupBy(vc => new { vc.EventTime.Month, vc.EventTime.Year, vc.CallType })
                                            .Select(group => new
                                            {
                                                Metric = group.Key,
                                                Count = group.Count()
                                            }).ToList();

            foreach (var item in groupedValidCalls)
            {
                var purpose = callTrendDictionary[CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(item.Metric.Month) + " " + item.Metric.Year ] as List<ChartMetrics>;

                purpose.Where(m => m.Name == item.Metric.CallType).ToList().ForEach(s => s.count = item.Count);
            }

            foreach (var item in groupedMissedCalls)
            {
                var purpose = callTrendDictionary[CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(item.Metric.Month) + " " + item.Metric.Year] as List<ChartMetrics>;

                purpose.Where(m => m.Name == item.Metric.CallType).ToList().ForEach(s => s.count = item.Count);
            }

            response.trendData = callTrendDictionary;

            response.countData = new List<List<string>>();

            foreach (var item in callTrendDictionary.Values)
            {
                response.countData.Add(item.Select(m => m.count.ToString()).ToList());
            }

            return response;
        }

        private async Task<ActionResult<List<String>>> GetLabs()
        {
            var ValidCallsLabs = await context.ValidCalls.Select(m => m.LabName).Distinct().ToListAsync();

            var missedCallsLabs = await context.MissedCalls.Select(m => m.LabName).Distinct().ToListAsync();

            List<string> labs = new List<string>();

            labs.AddRange(ValidCallsLabs);

            labs.AddRange(missedCallsLabs);

            labs = labs.Distinct().OrderBy(x => x).ToList();

            return labs;
        }

        private List<string> GetMonths(DateTime from, DateTime to) 
        {
           var start = from;
            var end = to;

            // set end-date to end of month
            end = new DateTime(end.Year, end.Month, DateTime.DaysInMonth(end.Year, end.Month));

            var diff = Enumerable.Range(0, Int32.MaxValue)
                                 .Select(e => start.AddMonths(e))
                                 .TakeWhile(e => e <= end)
                                 .Select(e => e.ToString("MMMM yyyy"));

            var value = diff.ToList();

            return value;
        }
    }
}

