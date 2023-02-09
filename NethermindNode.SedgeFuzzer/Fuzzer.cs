using CommandLine;
using NethermindNode.Core.Helpers;
using NethermindNode.SedgeFuzzer.Commands;

NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
NodeInfo.WaitForNodeToBeReady(Logger);
Parser.Default.ParseArguments<FuzzerCommand>(args).WithParsed(t => t.Execute());