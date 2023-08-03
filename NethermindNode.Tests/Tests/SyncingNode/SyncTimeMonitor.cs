﻿using NethermindNode.Core.Helpers;
using NethermindNode.NotionDataStructures;
using NethermindNode.Tests.Enums;
using NethermindNode.Tests.Helpers;
using Notion.Client;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
public class SyncTimeMonitor : BaseTest
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

    int MaxWaitTimeForSyncToComplete = 72 * 60 * 60 * 1000; //3 days

    private bool _isSnapSync;
    private bool _isNonValidator;

    [SetUp]
    public void Setup()
    {
        _isSnapSync = Environment.GetEnvironmentVariable("isSnapSync") != null
            ? Convert.ToBoolean(Environment.GetEnvironmentVariable("isSnapSync"))
            : true;

        _isNonValidator = Environment.GetEnvironmentVariable("isNonValidator") != null
            ? Convert.ToBoolean(Environment.GetEnvironmentVariable("isNonValidator"))
            : false;
    }


    [Description("Single monitoring of current sync")]
    [Category("PerfMonitoring")]
    [Test]
    public void MonitorSyncTimesOfStages()
    {
        Logger.Info("***Starting test: MonitorSyncTimesOfStages***");

        DateTime startTime = DateTime.MinValue;

        List<MetricStage> stagesToMonitor = new List<MetricStage>()
        {
            new MetricStage(){ Stage = Stages.FastHeaders },
            new MetricStage(){ Stage = Stages.BeaconHeaders },
            _isSnapSync ? new MetricStage(){ Stage = Stages.SnapSync } : null,
            new MetricStage(){ Stage = Stages.StateNodes },
            !_isNonValidator ? new MetricStage(){ Stage = Stages.FastBodies } : null,
            !_isNonValidator ? new MetricStage(){ Stage = Stages.FastReceipts } : null
        }.Where(stage => stage != null).ToList();

        NodeInfo.WaitForNodeToBeReady(Logger);
        double totalExecutionTime = MonitorStages(startTime, stagesToMonitor);

        //Calculate Totals
        foreach (var monitoringStage in stagesToMonitor)
        {
            monitoringStage.Total = monitoringStage.EndTime - monitoringStage.StartTime;
        }

        WriteReportToFile(totalExecutionTime, stagesToMonitor);
    }

    [TestCase(12)]
    [Description("To be used when testing mulitple resyncs + metrics")]
    [Category("PerfMonitoringWithResync")]
    public void MonitorSyncTimesOfStagesInSnapSync(int repeatCount)
    {
        long timeStamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

        Logger.Info("***Starting test: MonitorSyncTimesOfStagesInSnapSync --- syncType: SnapSync***");
        Dictionary<int, List<MetricStage>> results = new Dictionary<int, List<MetricStage>>();
        Dictionary<int, double> totals = new Dictionary<int, double>();
        DateTime startTime = DateTime.MinValue;

        for (int i = 0; i < repeatCount; i++)
        {
            List<MetricStage> stagesToMonitor = new List<MetricStage>()
            {
                new MetricStage(){ Stage = Stages.FastHeaders },
                new MetricStage(){ Stage = Stages.BeaconHeaders },
                new MetricStage(){ Stage = Stages.SnapSync },
                new MetricStage(){ Stage = Stages.StateNodes },
                new MetricStage(){ Stage = Stages.FastBodies },
                new MetricStage(){ Stage = Stages.FastReceipts }
            };

            NodeInfo.WaitForNodeToBeReady(Logger);
            double totalExecutionTime = MonitorStages(startTime, stagesToMonitor);

            //Calculate Totals
            foreach (var monitoringStage in stagesToMonitor)
            {
                monitoringStage.Total = monitoringStage.EndTime - monitoringStage.StartTime;
            }

            //Add values to dictionary
            results.Add(i, stagesToMonitor);
            totals.Add(i, totalExecutionTime);

            NodeStop();

            AddRecordToNotion(timeStamp, stagesToMonitor, startTime, ConfigurationHelper.Configuration["SyncTimesDetailedDatabaseId"]);

            if (i + 1 < repeatCount)
                NodeResync();

            Logger.Info($"Starting a FreshSync. Remaining fresh syncs to be executed: {repeatCount - i - 1}");
        }

        //Find longest and shortest runs and remove them
        var minTotalId = totals.MinBy(x => x.Value).Key;
        var maxTotalId = totals.MaxBy(x => x.Value).Key;

        results.Remove(minTotalId);
        results.Remove(maxTotalId);
        totals.Remove(minTotalId);
        totals.Remove(maxTotalId);

        var stageMetrics = new List<Stages> {
            Stages.FastHeaders,
            Stages.BeaconHeaders,
            Stages.SnapSync,
            Stages.StateNodes,
            Stages.FastBodies,
            Stages.FastReceipts
        };

        var averagedResult = stageMetrics.Select(stage =>
        {
            var stageValues = results.SelectMany(x => x.Value.Where(y => y.Stage == stage));
            return new MetricStage
            {
                Stage = stage,
                Total = stageValues.Select(x => x.Total).Average(),
                StartTime = stageValues.Min(x => x.StartTime)
            };
        }).ToList();

        AddRecordToNotion(timeStamp, averagedResult, startTime, ConfigurationHelper.Configuration["SyncTimesSummaryDatabaseId"], results.Count);

        NodeStart();
    }

    //Monitor sync stages and returns total time in seconds of how long Sync Procedure took
    private double MonitorStages(DateTime startTime, List<MetricStage> stagesToMonitor)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        var startDateTime = DateTime.UtcNow;
        if (startTime == DateTime.MinValue)
            startTime = startDateTime;

        while (stagesToMonitor.Any(x => x.EndTime == null))
        {
            var currentStages = NodeInfo.GetCurrentStages(Logger);
            if (currentStages.Count == 0 || currentStages.Contains(Stages.Disconnected) || currentStages.Contains(Stages.None))
            {
                Thread.Sleep(1000);
                continue;
            }

            //Need to have any check for maximum sync time - if more than MaxWaitTimeForSyncToComplete then something must have gone wrong and we will fail test
            if (sw.ElapsedMilliseconds > MaxWaitTimeForSyncToComplete)
            {
                sw.Stop();
                throw new AssertionException("Timout while waiting for sync to complete.");
            }

            //Process current stages
            foreach (var stage in currentStages)
            {
                //Set StartTime for stages which appeared for first time
                var monitoringStage = stagesToMonitor.FirstOrDefault(x => x.Stage == stage);
                if (monitoringStage != null && monitoringStage.StartTime == null)
                {
                    monitoringStage.StartTime = DateTime.UtcNow;
                }

                //If for any reason stage appeared again and have not null EndTime, we should reset EndTime
                if (monitoringStage != null && monitoringStage.EndTime != null)
                {
                    monitoringStage.EndTime = null;
                }
            }

            //If any stage dissapeared and have StartTime set, then we can treat it as completed
            foreach (var monitoringStage in stagesToMonitor.Where(x => x.StartTime != null && x.EndTime == null))
            {
                if (!currentStages.Contains(monitoringStage.Stage))
                {
                    monitoringStage.EndTime = DateTime.UtcNow;
                }
            }

            // Check if the EndTime of the last item is set - this may mean that for some reason sync ended but some of the stages did not have EndTime
            if (stagesToMonitor.Last().EndTime != null)
            {
                break;
            }

            Thread.Sleep(1000);
        }
        var endDateTime = DateTime.UtcNow;
        sw.Stop();

        return sw.Elapsed.TotalSeconds;
    }

    private void AddRecordToNotion(long startTimeOfRun, List<MetricStage> result, DateTime startTime, string databaseId, int numberOfProbes = 0)
    {
        Regex pattern = new Regex(@"--config=(?<network>\w+)|--Metrics.NodeName=(?<nodeName>\w+)");

        //Get data to csv format
        var nethermindImage = DockerCommands.GetImageName("execution-client", Logger).Trim();
        var consensusImage = DockerCommands.GetImageName("consensus-client", Logger).Trim();
        var executionCmd = DockerCommands.GetDockerDetails("execution-client", "{{.Config.Cmd}}", Logger);
        Match cmdExecutionMatch = pattern.Match(executionCmd);
        var nethermindNodeName = cmdExecutionMatch.Groups["nodeName"].Value.Split(' ').ToList();
        var network = cmdExecutionMatch.Groups["network"].Value;

        decimal oneGb = 1073741824;

        var path = GetExecutionDataPath();
        DirectoryInfo dirInfo = new DirectoryInfo(path + "/nethermind_db");
        decimal dirSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length) / oneGb;

        foreach (var monitoringStage in result)
        {
            //Send all data to Notion
            NotionHelper notionHelper = new NotionHelper(ConfigurationHelper.Configuration["AuthToken"]);

            var date = new NotionDate(startTime);

            var properties = new Dictionary<string, PropertyValue>
                    {
                        { "Run Id",                 new NotionTitle(startTimeOfRun.ToString()) },
                        { "Date Of Execution",      new NotionDate(monitoringStage.StartTime!.Value) },
                        { "Nethermind Image",       new NotionText(nethermindImage) },
                        { "Consensus Layer Client", new NotionText(consensusImage) },
                        { "Stage",                  new NotionText(monitoringStage.Stage.ToString()) },
                        { "Total Time",             new NotionNumber(monitoringStage.Total?.TotalSeconds / 60) },
                        { "Network",                new NotionText(network) },
                        { "DbSize",                 new NotionText(dirSize.ToString("0.00")) },
                        { "Probe count",            new NotionNumber(numberOfProbes) },
                    };

            PagesCreateParameters record = new PagesCreateParameters()
            {
                Properties = properties,
                Parent = new DatabaseParentInput()
                {
                    DatabaseId = databaseId
                }
            };

            notionHelper.AddRecord(record);
        }
    }

    internal class MetricStage
    {
        public Stages Stage { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Total { get; set; }
    }

    private void NodeStop()
    {
        //Stopping and clearing EL
        DockerCommands.StopDockerContainer("sedge-execution-client", Logger);
        while (!DockerCommands.GetDockerContainerStatus("sedge-execution-client", Logger).Contains("exited"))
        {
            Logger.Debug($"Waiting for sedge-execution-client docker status to be \"exited\". Current status: {DockerCommands.GetDockerContainerStatus("sedge-execution-client", Logger)}");
            Thread.Sleep(30000);
        }
    }

    private void NodeStart()
    {
        DockerCommands.StartDockerContainer("sedge-execution-client", Logger);
    }

    private void NodeResync()
    {
        var path = GetExecutionDataPath();
        CommandExecutor.RemoveDirectory(path + "/nethermind_db", Logger);

        //Restarting Node - freshSync
        NodeStart();
    }

    private string GetExecutionDataPath()
    {
        return DockerCommands.GetDockerDetails("execution-client", "{{ range .Mounts }}{{ if eq .Destination \\\"/nethermind/data\\\" }}{{ .Source }}{{ end }}{{ end }}", Logger).Trim(); ;
    }

    private void WriteReportToFile(double totalExecutionTime, List<MetricStage> stagesToMonitor)
    {
        // Initialize a StringBuilder for our report.
        StringBuilder reportBuilder = new StringBuilder();

        // Add the total execution time to the report.
        reportBuilder.AppendLine($"TOTAL SYNC TIME: {TimeSpan.FromSeconds(totalExecutionTime).ToString(@"d\.hh\:mm\:ss")}");

        // Add the time for each stage to the report.
        foreach (var stage in stagesToMonitor)
        {
            reportBuilder.AppendLine($"{stage.Stage}: {stage.Total?.ToString(@"d\.hh\:mm\:ss")}");
        }

        // Write the report to a text file.
        System.IO.File.WriteAllText("syncTimeReport.txt", reportBuilder.ToString());
    }
}