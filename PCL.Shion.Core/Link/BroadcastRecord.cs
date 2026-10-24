using System;
using System.Net;

namespace PCL.Shion.Core.Link;

public record BroadcastRecord(string Desc, IPEndPoint Address, DateTime FoundAt);