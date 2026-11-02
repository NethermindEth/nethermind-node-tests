// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace NethermindNode.Core.RpcResponses;

public class EthBlockResponse : IRpcResponse
{
    public int Id { get; set; }
    public EthBlockResult? Result { get; set; }
}

public class EthBlockResult
{
    public string Number { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string ParentHash { get; set; } = string.Empty;
}
