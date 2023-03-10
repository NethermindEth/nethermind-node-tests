using Hardware.Info;
using NethermindNode.Core.Helpers;
using NethermindNode.NotionDataStructures;
using NethermindNode.Tests.Enums;
using NethermindNode.Tests.Helpers;
using Notion.Client;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
public class SyncTimeMonitor : BaseTest
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

    int MaxWaitTimeForSyncToComplete = 36 * 60 * 60 * 1000; //1,5day

    [TestCase(12)]
    [Category("PerfMonitoring")]
    public void MonitorSyncTimesOfStagesInSnapSync(int repeatCount)
    {
        IHardwareInfo hardwareInfo = new HardwareInfo();
        hardwareInfo.RefreshAll();
        Logger.Info(hardwareInfo.OperatingSystem);

        Logger.Info(hardwareInfo.MemoryStatus);

        foreach (var hardware in hardwareInfo.BatteryList)
            Logger.Info(hardware);

        foreach (var hardware in hardwareInfo.BiosList)
            Logger.Info(hardware);

        foreach (var cpu in hardwareInfo.CpuList)
        {
            Logger.Info(cpu);

            foreach (var cpuCore in cpu.CpuCoreList)
                Logger.Info(cpuCore);
        }

        foreach (var drive in hardwareInfo.DriveList)
        {
            Logger.Info(drive);

            foreach (var partition in drive.PartitionList)
            {
                Logger.Info(partition);

                foreach (var volume in partition.VolumeList)
                    Logger.Info(volume);
            }
        }

        foreach (var hardware in hardwareInfo.KeyboardList)
            Logger.Info(hardware);

        foreach (var hardware in hardwareInfo.MemoryList)
            Logger.Info(hardware);

        foreach (var hardware in hardwareInfo.MonitorList)
            Logger.Info(hardware);

        foreach (var hardware in hardwareInfo.MotherboardList)
            Logger.Info(hardware);

        foreach (var hardware in hardwareInfo.MouseList)
            Logger.Info(hardware);

        foreach (var hardware in hardwareInfo.NetworkAdapterList)
            Logger.Info(hardware);

        foreach (var hardware in hardwareInfo.PrinterList)
            Logger.Info(hardware);

        foreach (var hardware in hardwareInfo.SoundDeviceList)
            Logger.Info(hardware);

        foreach (var hardware in hardwareInfo.VideoControllerList)
            Logger.Info(hardware);

        foreach (var address in HardwareInfo.GetLocalIPv4Addresses(NetworkInterfaceType.Ethernet, OperationalStatus.Up))
            Logger.Info(address);

        foreach (var address in HardwareInfo.GetLocalIPv4Addresses(NetworkInterfaceType.Wireless80211))
            Logger.Info(address);

        foreach (var address in HardwareInfo.GetLocalIPv4Addresses(OperationalStatus.Up))
            Logger.Info(address);

        foreach (var address in HardwareInfo.GetLocalIPv4Addresses())
            Logger.Info(address);


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
            //test log
            Logger.Info(GetExecutionDataPath());
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

            AddRecordToNotion(stagesToMonitor, startTime);

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

        AddRecordToNotion(averagedResult, startTime, results.Count);

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
            Thread.Sleep(1000);
        }
        var endDateTime = DateTime.UtcNow;
        sw.Stop();

        return sw.Elapsed.TotalSeconds;
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
        var path = GetExecutionDataPath();
        CommandExecutor.RemoveDirectory(path + "/nethermind_db", Logger);

        //Restarting Node - freshSync
        NodeStart();
    }

    private string GetExecutionDataPath()
    {
        return DockerCommands.GetDockerDetails("execution-client", "{{ range .Mounts }}{{ if eq .Destination \\\"/nethermind/data\\\" }}{{ .Source }}{{ end }}{{ end }}", Logger).Trim(); ;
    }

    private void AddRecordToNotion(List<MetricStage> result, DateTime startTime, int numberOfProbes = 0)
    {
        Regex pattern = new Regex(@"--config=(?<network>\w+)|--Metrics.NodeName=(?<nodeName>\w+)");

        //Get data to csv format
        var nethermindImage = DockerCommands.GetImageName("execution-client", Logger).Trim();
        var consensusImage = DockerCommands.GetImageName("consensus-client", Logger).Trim();
        var executionCmd = DockerCommands.GetDockerDetails("execution-client", "{{.Config.Cmd}}", Logger);
        Match cmdExecutionMatch = pattern.Match(executionCmd);
        var nethermindNodeName = cmdExecutionMatch.Groups["nodeName"].Value.Split(' ').ToList();
        var network = cmdExecutionMatch.Groups["network"].Value;

        long oneGb = 1073741824;
        //MachineInformation info = MachineInformationGatherer.GatherInformation();

        var path = GetExecutionDataPath();
        DirectoryInfo dirInfo = new DirectoryInfo(path + "/nethermind_db");
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
                        //{ "CPU",                    new NotionText(info.Cpu.Name.ToString()) },
                        //{ "RAM",                    new NotionText("RAMSticks count: " + info.RAMSticks.Count + ", Total Memory: " + info.RAMSticks.Sum(x => (decimal)x.Capacity / oneGb).ToString()) },
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