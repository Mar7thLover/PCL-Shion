using System.Text.Json.Serialization;

// ReSharper disable InconsistentNaming

namespace PCL.Shion.Core.Link.Scaffolding.Client.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlayerKind
{
    HOST,
    GUEST
}