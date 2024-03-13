# nethermind-node-tests

## Introduction
Welcome to the Nethermind Node Tests repository. This repository has a set of tests which are being executed on Nethermind node with plans to make them usefull for all EL clients.

## About the Repository
The primary focus of this repository is to maintain and execute a set of test cases that are crucial for the proper functioning and synchronization of nodes during different stages of the Nethermind node sync.
As for now tool is tightly coupled with Sedge node creation tool (https://github.com/NethermindEth/sedge). Tests are executing Kills and Gracefull restarts on various sync stages + executing additional actions on synced EL and CL (this part is universal for all ELs so it can "fuzz" others as well).
Repo contains also other usefull tests like:
1. Nethermind Fullpruning start and process validation (https://github.com/NethermindEth/nethermind-node-tests/blob/main/NethermindNode.Tests/Tests/Pruning/JsonRpcPruning.cs)
2. Auto resyncs capability to ensure stability of a node on particular version (https://github.com/NethermindEth/nethermind-node-tests/tree/main/NethermindNode.Tests/Tests/Resyncs)
3. Some basic JsonRPC verification and stress tests (https://github.com/NethermindEth/nethermind-node-tests/tree/main/NethermindNode.Tests/Tests/JsonRpc)

## Getting Started
### Prerequisites
1. Node created with Sedge (https://github.com/NethermindEth/sedge) 
2. Dotnet SDK installed on machine (https://dotnet.microsoft.com/en-us/download)
3. Systemd support on linux distro

### List the prerequisites needed to run these tests.
Tests can be simply started by dotnet test command:
`dotnet test NethermindNodeTests.sln -c Release --logger:"nunit;LogFilePath=%WORKINGDIR%/test-result.xml" --filter CATEGORIES`
There is also a systemd service template created which can be used to inject this into any VM in flight and run in background (tests may be very long running so there is a value of keeping them on background) - https://github.com/NethermindEth/nethermind-node-tests/blob/main/tests.service
