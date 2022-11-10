using CommandLine;
using SedgeNodeFuzzer.Commands;
using SedgeNodeFuzzer.Helpers;

NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

while (DockerCommands.CheckIfDockerContainerIsCreated("execution-client", Logger) == false)
{
    Logger.Info("Waiting for Execution to be started.");
    Thread.Sleep(5000);
}
Parser.Default.ParseArguments<FuzzerCommand>(args).WithParsed(t => t.Execute());