using HardwareInformation;
using NethermindNode.Core.Helpers;
using NethermindNode.NotionDataStructures;
using NethermindNode.Tests.Enums;
using NethermindNode.Tests.Helpers;
using Notion.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
public class SyncTimeMonitor : BaseTest
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);
    DateTime startTime = DateTime.MinValue;

    int MaxWaitTimeForSyncToComplete = 36 * 60 * 60 * 1000; //1,5day

    [TestCase(12)]
    [Category("PerfMonitoring")]
    public void MonitorSyncTimesOfStagesInSnapSync(int repeatCount)
    {
        Logger.Info("***Starting test: MonitorSyncTimesOfStagesInSnapSync --- syncType: SnapSync***");
        Dictionary<int, List<MetricStage>> results = new Dictionary<int, List<MetricStage>>();
        Dictionary<int, double> totals = new Dictionary<int, double>();

        for (int i = 0; i < repeatCount; i++)
        {
            List<MetricStage> stagesToMonitor = new List<MetricStage>()
            {
                new MetricStage(){ Stage = Stages.FastHeaders },
                new MetricStage(){ Stage = Stages.BeaconHeaders },
                //new MetricStage(){ Stage = Stages.FastSync },
                new MetricStage(){ Stage = Stages.SnapSync },
                new MetricStage(){ Stage = Stages.StateNodes },
                new MetricStage(){ Stage = Stages.FastBodies },
                new MetricStage(){ Stage = Stages.FastReceipts }
            };

            while (DockerCommands.CheckIfDockerContainerIsCreated("execution-client", Logger) == false)
            {
                Logger.Info("Waiting for Execution to be started.");
                Thread.Sleep(5000);
            }

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
                Thread.Sleep(1000);
            }
            var endDateTime = DateTime.UtcNow;
            sw.Stop();

            //Calculate Totals
            foreach (var monitoringStage in stagesToMonitor)
            {
                monitoringStage.Total = monitoringStage.EndTime - monitoringStage.StartTime;
            }

            //Add values to dictionary
            results.Add(i, stagesToMonitor);
            totals.Add(i, sw.Elapsed.TotalSeconds);

            NodeStop();

            AddRecordToNotion(stagesToMonitor);

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

        //Generate averaged result
        List<MetricStage> averagedResult = new List<MetricStage>()
            {
                new MetricStage(){
                    Stage = Stages.FastHeaders,
                    Total = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.FastHeaders).Select(y => y.Total)).Average(),
                    StartTime = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.FastHeaders).Select(y => y.StartTime)).Min()
                },
                new MetricStage(){
                    Stage = Stages.BeaconHeaders,
                    Total = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.BeaconHeaders).Select(y => y.Total)).Average(),
                    StartTime = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.BeaconHeaders).Select(y => y.StartTime)).Min()
                },
                //new MetricStage(){ 
                //    Stage = Stages.FastSync,
                //    Total = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.FastSync).Select(y => y.Total)).Average()
                //},
                new MetricStage(){
                    Stage = Stages.SnapSync,
                    Total = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.SnapSync).Select(y => y.Total)).Average(),
                    StartTime = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.SnapSync).Select(y => y.StartTime)).Min()
                },
                new MetricStage(){
                    Stage = Stages.StateNodes,
                    Total = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.StateNodes).Select(y => y.Total)).Average(),
                    StartTime = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.StateNodes).Select(y => y.StartTime)).Min()
                },
                new MetricStage(){
                    Stage = Stages.FastBodies,
                    Total = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.FastBodies).Select(y => y.Total)).Average(),
                    StartTime = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.FastBodies).Select(y => y.StartTime)).Min()
                },
                new MetricStage(){
                    Stage = Stages.FastReceipts,
                    Total = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.FastReceipts).Select(y => y.Total)).Average(),
                    StartTime = results.SelectMany(x => x.Value.Where(y => y.Stage == Stages.FastReceipts).Select(y => y.StartTime)).Min()
                }
            };

        AddRecordToNotion(averagedResult, results.Count);

        NodeStart();
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
        DockerCommands.StopDockerContainer("execution-client", Logger);
        while (!DockerCommands.GetDockerContainerStatus("execution-client", Logger).Contains("exited"))
        {
            Logger.Info($"Waiting for execution-client docker status to be \"exited\". Current status: {DockerCommands.GetDockerContainerStatus("execution-client", Logger)}");
            Thread.Sleep(30000);
        }
    }

    private void NodeStart()
    {
        DockerCommands.StartDockerContainer("execution-client", Logger);
    }

    private void NodeResync()
    {
#if DEBUG
        var path = DockerCommands.GetDockerDetails("execution-client", " range .Mounts }}{{ if eq .Destination \"/nethermind/data\" }}{{ .Source }}{{ end }}{{ end ", Logger).Trim();
        CommandExecutor.RemoveDirectory(path + "/nethermind_db", Logger);
#else
        CommandExecutor.RemoveDirectory("/root/execution-data/nethermind_db", Logger);
#endif

        //Restarting Node - freshSync
        NodeStart();
    }

    private void AddRecordToNotion(List<MetricStage> result, int numberOfProbes = 0)
    {
        Regex pattern = new Regex(@"--config=(?<network>\w+)|--Metrics.NodeName=(?<nodeName>\w+)");

        //Get data to csv format
        var nethermindImage = DockerCommands.GetImageName("execution-client", Logger).Trim();
        var consensusImage = DockerCommands.GetImageName("consensus-client", Logger).Trim();
        var executionCmd = DockerCommands.GetDockerDetails("execution-client", ".Config.Cmd", Logger);
        Match cmdExecutionMatch = pattern.Match(executionCmd);
        var nethermindNodeName = cmdExecutionMatch.Groups["nodeName"].Value.Split(' ').ToList();
        var network = cmdExecutionMatch.Groups["network"].Value;

        long oneGb = 1073741824;
        MachineInformation info = MachineInformationGatherer.GatherInformation();

#if DEBUG
        var path = DockerCommands.GetDockerDetails("execution-client", " range .Mounts }}{{ if eq .Destination \"/nethermind/data\" }}{{ .Source }}{{ end }}{{ end ", Logger).Trim();
        DirectoryInfo dirInfo = new DirectoryInfo(path + "/nethermind_db");
#else
        DirectoryInfo dirInfo = new DirectoryInfo("/root/execution-data/nethermind_db");
#endif
        long dirSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length) / oneGb;

        foreach (var monitoringStage in result)
        {
            //Send all data to Notion
            NotionHelper notionHelper = new NotionHelper(ConfigurationHelper.Configuration["AuthToken"]);

            var date = new NotionDate(startTime);

            var properties = new Dictionary<string, PropertyValue>
                    {
                        { "Run Id",                 new NotionTitle(ConfigurationHelper.Configuration["GitHubWorkflowId"]) },
                        { "Date Of Execution",      new NotionDate(monitoringStage.StartTime!.Value) },
                        { "Nethermind Image",       new NotionText(nethermindImage) },
                        { "Consensus Layer Client", new NotionText(consensusImage) },
                        { "Stage",                  new NotionText(monitoringStage.Stage.ToString()) },
                        { "Total Time",             new NotionNumber(monitoringStage.Total?.TotalSeconds / 60) },
                        { "Network",                new NotionText(network) },
                        { "CPU",                    new NotionText(info.Cpu.Name.ToString()) },
                        { "RAM",                    new NotionText("RAMSticks count: " + info.RAMSticks.Count + ", Total Memory: " + info.RAMSticks.Sum(x => (decimal)x.Capacity / oneGb).ToString()) },
                        { "DbSize",                 new NotionText(dirSize.ToString()) },
                        { "Probe count",            new NotionNumber(numberOfProbes) },
                    };

            PagesCreateParameters record = new PagesCreateParameters()
            {
                Properties = properties,
                Parent = new DatabaseParentInput()
                {
                    DatabaseId = ConfigurationHelper.Configuration["SyncTimesDatabaseId"]
                }
            };

            notionHelper.AddRecord(record);
        }
    }

}
