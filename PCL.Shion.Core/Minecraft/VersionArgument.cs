namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// arguments.jvm / arguments.game 中的一项：一组值，附带可选的规则。纯字符串元素的规则为空。
/// </summary>
public sealed record VersionArgument(IReadOnlyList<string> Values, IReadOnlyList<VersionRule> Rules);
