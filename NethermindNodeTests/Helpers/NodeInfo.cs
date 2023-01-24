using NethermindNode.Core.Helpers;
using NethermindNode.Tests.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNode.Helpers
{
    public static class NodeInfo
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

        public static string GetCurrentStage(NLog.Logger logger)
        {
            var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("debug_getSyncStage", "", "http://localhost:8545", logger);
            string output;
            if (commandResult.Result == null)
            {
                output = "WaitingForConnection";
            }
            else
            {
                var value = JsonConvert.DeserializeObject(commandResult.Result.Item1);
                if (value == null)
                {
                    output = "WaitingForConnection";
                }
                else
                {
                    output = ((dynamic)value).result.currentStage.ToString();
                }
            }

            logger.Info("Current stage is: " + output);
            return output;
        }

        public static List<Stages> GetCurrentStages(NLog.Logger logger)
        {
            List<Stages> result = new List<Stages>();
            var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("debug_getSyncStage", "", "http://localhost:8545", logger);
            string output = commandResult.Result == null ? "WaitingForConnection" : ((dynamic)JsonConvert.DeserializeObject(commandResult.Result.Item1)).result.currentStage.ToString();
            foreach (string stage in output.Split(','))
            {
                bool parsed = Enum.TryParse(stage.Trim(), out Stages parsedStage);
                if (parsed)
                {
                    result.Add(parsedStage);
                }
            }

            logger.Info(TestContext.CurrentContext.Test.MethodName + " ||| " + "Current stage is: " + output);
            return result;
        }
    }
}
