using CommandLine;
using SedgeNodeFuzzer.Commands;

Parser.Default.ParseArguments<FuzzerCommand>(args).WithParsed(t => t.Execute());