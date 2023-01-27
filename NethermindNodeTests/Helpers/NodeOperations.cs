using Microsoft.CSharp.RuntimeBinder;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.Enums;
using NethermindNode.Tests.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNode.Helpers
{
    public static class NodeOperations
    {
        public static bool IsFullySynced(NLog.Logger logger)
        {
            var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("eth_syncing", "", "http://localhost:8545", logger);
            var result = commandResult.Result;
            return result == null ? false : result.Item1.Contains("false");
        }

        public static void WaitForNodeToBeReady(NLog.Logger logger)
        {
            //Waiting for proper start of node
            while (DockerCommands.CheckIfDockerContainerIsCreated("execution-client", logger) == false)
            {
                logger.Info("Waiting for Execution to be started.");
                Thread.Sleep(30000);
            }
        }

        public static List<Stages> GetCurrentStages(NLog.Logger logger)
        {
            List<Stages> result = new List<Stages>();
            var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("debug_getSyncStage", "", "http://localhost:8545", logger);
            string output = "";

            output = commandResult.Result == null ? "WaitingForConnection" : ((dynamic)JsonConvert.DeserializeObject(commandResult.Result.Item1)).result.currentStage.ToString();

            foreach (string stage in output.Split(','))
            {
                bool parsed = Enum.TryParse(stage.Trim(), out Stages parsedStage);
                if (parsed)
                {
                    result.Add(parsedStage);
                }
            }

            logger.Info("Current stage is: " + output);
            return result;
        }

        public static void NodeStop(string containerName, NLog.Logger logger)
        {
            //Stopping and clearing EL
            DockerCommands.StopDockerContainer(containerName, logger);
            while (!DockerCommands.GetDockerContainerStatus(containerName, logger).Contains("exited"))
            {
                logger.Info($"Waiting for {containerName} docker status to be \"exited\". Current status: {DockerCommands.GetDockerContainerStatus(containerName, logger)}");
                Thread.Sleep(30000);
            }
        }

        public static void NodeStart(string containerName, NLog.Logger logger)
        {
            DockerCommands.StartDockerContainer(containerName, logger);
        }

        public static void NodeResync(string containerName, NLog.Logger logger)
        {
#if DEBUG
            var path = DockerCommands.GetDockerDetails(containerName, " range .Mounts }}{{ if eq .Destination \"/nethermind/data\" }}{{ .Source }}{{ end }}{{ end ", logger).Trim();
            CommandExecutor.RemoveDirectory(path + "/nethermind_db", logger);
#else

            CommandExecutor.RemoveDirectory("/root/execution-data/nethermind_db", logger);
#endif
            //Restarting Node - freshSync
            NodeStart(containerName, logger);
        }
    }
}
