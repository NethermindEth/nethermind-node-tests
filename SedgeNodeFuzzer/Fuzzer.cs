using CommandLine;
using SedgeNodeFuzzer.Commands;
using SedgeNodeFuzzer.Helpers;

while(DockerCommands.CheckIfDockerContainerIsCreated("execution") == false)
{
    Console.WriteLine(DateTime.Now + ": Waiting for Execution to be started.");
    Thread.Sleep(60000);
}
Parser.Default.ParseArguments<FuzzerCommand>(args).WithParsed(t => t.Execute());