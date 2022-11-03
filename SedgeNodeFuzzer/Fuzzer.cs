using CommandLine;
using SedgeNodeFuzzer.Commands;
using SedgeNodeFuzzer.Helpers;

//while(DockerCommands.CheckIfDockerContainerIsCreated("execution-client") == false)
//{
//    Console.WriteLine(DateTime.Now + ": Waiting for Execution to be started.");
//    Thread.Sleep(5000);
//}
Parser.Default.ParseArguments<FuzzerCommand>(args).WithParsed(t => t.Execute());