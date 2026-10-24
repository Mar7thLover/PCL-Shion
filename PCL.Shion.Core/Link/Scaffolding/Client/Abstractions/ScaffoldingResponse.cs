using System;

namespace PCL.Shion.Core.Link.Scaffolding.Client.Abstractions;

public record ScaffoldingResponse(byte Status, ReadOnlyMemory<byte> Body);