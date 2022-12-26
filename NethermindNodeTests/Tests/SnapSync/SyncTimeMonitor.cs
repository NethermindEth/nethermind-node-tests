using NethermindNodeTests.Enums;
using NethermindNodeTests.Helpers;
using Newtonsoft.Json;
using Notion.Client;
using SedgeNodeFuzzer.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Tests.SnapSync
{
    [TestFixture]
    public class SyncTimeMonitor : BaseTest
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

        List<MetricStage> stagesToMonitor = new List<MetricStage>()
            {
                new MetricStage(){ Stage = Stages.FastHeaders },
                new MetricStage(){ Stage = Stages.BeaconHeaders },
                new MetricStage(){ Stage = Stages.FastSync },
                new MetricStage(){ Stage = Stages.SnapSync },
                new MetricStage(){ Stage = Stages.StateNodes },
                new MetricStage(){ Stage = Stages.FastBodies },
                new MetricStage(){ Stage = Stages.FastReceipts }
            };

        int MaxWaitTimeForSyncToComplete = 36 * 60 * 60 * 1000; //1,5day

        [Test]
        [Category("PerfMonitoring")]
        public void MonitorSyncTimesOfStagesInSnapSync()
        {
            Logger.Info("***Starting test: MonitorSyncTimesOfStagesInSnapSync --- syncType: SnapSync***");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var startDateTime = DateTime.UtcNow;

            while (stagesToMonitor.Any(x => x.EndTime == null))
            {
                var currentStages = NodeInfo.GetCurrentStages(Logger);

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

            //Get data to csv format
            var filePath = TestContext.CurrentContext.Test.MethodName + "_" + DockerCommands.GetImageName("execution-client", Logger) + ".csv";

            var nethermindImage = DockerCommands.GetImageName("execution-client", Logger);
            var nethermindNodeName = DockerCommands.GetDockerDetails("execution-client", ".Config.Cmd", Logger).Split(' ').ToList().FirstOrDefault(x => x.Contains("--Metrics.NodeName"))?.Split('-');
            var consensusClient = nethermindNodeName?.Last();
            var network = nethermindNodeName?[nethermindImage.Length - 2];

            using (StreamWriter writer = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write)))
            {
                writer.WriteLine("Date Of Execution,Nethermind Image,Consensus Layer Client,Stage,Start Time,End Time,Total Time");

                foreach (var monitoringStage in stagesToMonitor)
                {
                    writer.WriteLine($"{startDateTime},{nethermindImage},{consensusClient},{monitoringStage.Stage},{monitoringStage.StartTime}, {monitoringStage.EndTime},{monitoringStage.Total}");

                    if (network?.ToLower() == "sepolia")
                    {
                        //Send all data to Notion
                        NotionHelper notionHelper = new NotionHelper();

                        var properties = new Dictionary<string, PropertyValue>
                        {
                            { "Date Of Execution",      new DatePropertyValue() { Date = new Date() { Start = startDateTime } } },
                            { "Nethermind Image",       new RichTextPropertyValue() { RichText = new List<RichTextBase>() { new RichTextText() { PlainText = nethermindImage, Text = new Text() { Content = nethermindImage } } } } },
                            { "Consensus Layer Client", new RichTextPropertyValue() { RichText = new List<RichTextBase>() { new RichTextText() { PlainText = consensusClient, Text = new Text() { Content = consensusClient } } } } },
                            { "Stage",                  new RichTextPropertyValue() { RichText = new List<RichTextBase>() { new RichTextText() { PlainText = monitoringStage.Stage.ToString(), Text = new Text() { Content = monitoringStage.Stage.ToString() } } } } },
                            { "Start Time",             new DatePropertyValue() { Date = new Date() { Start = monitoringStage.StartTime } } },
                            { "End Time",               new DatePropertyValue() { Date = new Date() { Start = monitoringStage.EndTime } } },
                            { "Total Time",             new NumberPropertyValue() { Number = monitoringStage.Total?.TotalSeconds / 60} }
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
                writer.WriteLine($"{startDateTime},{nethermindImage},{consensusClient},FullSync,{startDateTime},{endDateTime},{sw.Elapsed}");

                if (network?.ToLower() == "mainnet")
                {
                    //Send all data to Notion
                    NotionHelper notionHelper = new NotionHelper();

                    var properties = new Dictionary<string, PropertyValue>
                    {
                        { "Date Of Execution",      new DatePropertyValue() { Date = new Date() { Start = startDateTime } } },
                        { "Nethermind Image",       new RichTextPropertyValue() { RichText = new List<RichTextBase>() { new RichTextText() { PlainText = nethermindImage, Text = new Text() { Content = nethermindImage } } } } },
                        { "Consensus Layer Client", new RichTextPropertyValue() { RichText = new List<RichTextBase>() { new RichTextText() { PlainText = consensusClient, Text = new Text() { Content = consensusClient } } } } },
                        { "Stage",                  new RichTextPropertyValue() { RichText = new List<RichTextBase>() { new RichTextText() { PlainText = "FullSync", Text = new Text() { Content = "FullSync" } } } } },
                        { "Start Time",             new DatePropertyValue() { Date = new Date() { Start = startDateTime } } },
                        { "End Time",               new DatePropertyValue() { Date = new Date() { Start = endDateTime } } },
                        { "Total Time",             new NumberPropertyValue() { Number = sw.Elapsed.TotalSeconds / 60} }
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

        private List<Stages> GetCurrentStage()
        {
            List<Stages> result = new List<Stages>();
            var commandResult = CurlExecutor.ExecuteNethermindJsonRpcCommand("debug_getSyncStage", "http://localhost:8545", Logger);
            string output = commandResult.Result == null ? "WaitingForConnection" : ((dynamic)JsonConvert.DeserializeObject(commandResult.Result)).result.currentStage.ToString();
            foreach (string stage in output.Split(','))
            {
                bool parsed = Enum.TryParse(stage.Trim(), out Stages parsedStage);
                if (parsed)
                {
                    result.Add(parsedStage);
                }
            }

            Logger.Info(TestContext.CurrentContext.Test.MethodName + " ||| " + "Current stage is: " + output);
            return result;
        }

        internal class MetricStage
        {
            public Stages Stage { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public TimeSpan? Total { get; set; }
        }
    }
}
