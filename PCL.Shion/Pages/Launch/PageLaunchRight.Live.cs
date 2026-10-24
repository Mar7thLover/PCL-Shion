using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Shion.Pages.Homepage;
using PCL.Shion.Services;

namespace PCL.Shion.Pages.Launch;

public partial class PageLaunchRight
{
    private const string homepageLivePatchFileName = "CustomLive.json";
    private const string homepageLiveSupportFileName = "CustomLive.supported.json";

    // 外部工具写入补丁文件，因此把可修改的属性限定在明确的列表内
    private static readonly Dictionary<string, string> _homepageLiveAllowedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = "Text",
        ["title"] = "Title",
        ["info"] = "Info",
        ["tooltip"] = "ToolTip",
        ["visibility"] = "Visibility",
        ["isEnabled"] = "IsEnabled",
        ["opacity"] = "Opacity"
    };

    private FileSystemWatcher? _homepageLiveWatcher;
    private DispatcherTimer? _homepageLivePatchTimer;

    private void _EnsureHomepageLiveWatcher()
    {
        if (_homepageLiveWatcher != null) return;
        if (App.Services.Settings.UiCustomType != 1) return;

        try
        {
            var directory = _GetHomepageLiveDirectory();
            Directory.CreateDirectory(directory);
            _WriteHomepageLiveSupportMarker(directory);

            _homepageLiveWatcher = new FileSystemWatcher(directory, homepageLivePatchFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };
            _homepageLiveWatcher.Changed += (_, _) => _QueueHomepageLivePatchApply();
            _homepageLiveWatcher.Created += (_, _) => _QueueHomepageLivePatchApply();
            _homepageLiveWatcher.Renamed += (_, _) => _QueueHomepageLivePatchApply();
            _homepageLiveWatcher.EnableRaisingEvents = true;
            _QueueHomepageLivePatchApply();
        }
        catch (Exception ex)
        {
            HomepageSupport.Log(ex, "[Page] Failed to start custom homepage live patch watcher", LogEventLevel.Debug);
        }
    }

    private void _DisposeHomepageLiveWatcher()
    {
        try
        {
            _homepageLiveWatcher?.Dispose();
        }
        catch (Exception ex)
        {
            HomepageSupport.Log(ex, "[Page] Failed to dispose custom homepage live patch watcher", LogEventLevel.Debug);
        }

        _homepageLiveWatcher = null;

        try
        {
            if (_homepageLivePatchTimer != null)
            {
                _homepageLivePatchTimer.Stop();
                _homepageLivePatchTimer.Tick -= _HomepageLivePatchTimerTick;
                _homepageLivePatchTimer = null;
            }
        }
        catch (Exception ex)
        {
            HomepageSupport.Log(ex, "[Page] Failed to dispose custom homepage live patch debounce timer", LogEventLevel.Debug);
        }

        _DeleteHomepageLiveSupportMarker();
    }

    private void _QueueHomepageLivePatchApply()
    {
        HomepageSupport.RunInUi(() =>
        {
            _homepageLivePatchTimer ??= new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _homepageLivePatchTimer.Tick -= _HomepageLivePatchTimerTick;
            _homepageLivePatchTimer.Tick += _HomepageLivePatchTimerTick;
            _homepageLivePatchTimer.Stop();
            _homepageLivePatchTimer.Start();
        });
    }

    private void _HomepageLivePatchTimerTick(object? sender, EventArgs e)
    {
        _homepageLivePatchTimer?.Stop();
        _ApplyHomepageLivePatchesFromFile();
    }

    private void _ApplyHomepageLivePatchesFromFile()
    {
        if (PanCustom.Children.Count == 0) return;
        if (App.Services.Settings.UiCustomType != 1) return;

        var file = Path.Combine(_GetHomepageLiveDirectory(), homepageLivePatchFileName);
        if (!File.Exists(file)) return;

        try
        {
            var token = JsonNode.Parse(_ReadHomepageLivePatchFile(file),
                new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                })!;
            foreach (var patch in _EnumerateHomepageLivePatches(token))
                _ApplyHomepageLivePatch(patch);
        }
        catch (Exception ex)
        {
            HomepageSupport.Log(ex, "[Page] Failed to apply custom homepage live patches", LogEventLevel.Debug);
        }
    }

    private static string _ReadHomepageLivePatchFile(string file)
    {
        Exception? lastException = null;
        for (var i = 0; i < 3; i++)
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                lastException = ex;
                Thread.Sleep(50);
            }
        }

        throw lastException ?? new IOException("Unable to read custom homepage live patch file.");
    }

    private static string _GetHomepageLiveDirectory()
    {
        return Path.Combine(HomepageSupport.ExeDirectory, "PCL");
    }

    private static void _WriteHomepageLiveSupportMarker(string directory)
    {
        try
        {
            var marker = new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true })
            {
                ["processId"] = Environment.ProcessId,
                ["processPath"] = Environment.ProcessPath ?? "",
                ["patchFile"] = homepageLivePatchFileName,
                ["startedAt"] = DateTime.Now.ToString("O", CultureInfo.InvariantCulture)
            };
            File.WriteAllText(Path.Combine(directory, homepageLiveSupportFileName), marker.ToJsonString());
        }
        catch (Exception ex)
        {
            HomepageSupport.Log(ex, "[Page] Failed to write custom homepage live patch support marker", LogEventLevel.Debug);
        }
    }

    private static void _DeleteHomepageLiveSupportMarker()
    {
        try
        {
            var file = Path.Combine(_GetHomepageLiveDirectory(), homepageLiveSupportFileName);
            if (!File.Exists(file)) return;

            var marker = (JsonObject)JsonNode.Parse(_ReadHomepageLivePatchFile(file),
                new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                })!;
            if (marker["processId"]?.GetValue<int>() == Environment.ProcessId)
                File.Delete(file);
        }
        catch (Exception ex)
        {
            HomepageSupport.Log(ex, "[Page] Failed to delete custom homepage live patch support marker", LogEventLevel.Debug);
        }
    }

    private static IEnumerable<JsonObject> _EnumerateHomepageLivePatches(JsonNode token)
    {
        if (token is JsonObject obj)
        {
            if (obj["patches"] is JsonArray patches)
            {
                foreach (var patch in patches.OfType<JsonObject>())
                    yield return patch;
                yield break;
            }

            if (_TryGetString(obj, "target", "tag", "name") != null)
            {
                yield return obj;
                yield break;
            }

            foreach (var property in obj)
            {
                if (property.Value is not JsonObject patch) continue;
                patch = (JsonObject)JsonNode.Parse(patch.ToJsonString(),
                    new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                    new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    })!;
                patch["target"] ??= property.Key;
                yield return patch;
            }
        }
        else if (token is JsonArray array)
        {
            foreach (var patch in array.OfType<JsonObject>())
                yield return patch;
        }
    }

    private void _ApplyHomepageLivePatch(JsonObject patch)
    {
        var target = _TryGetString(patch, "target", "tag", "name");
        if (string.IsNullOrWhiteSpace(target)) return;

        foreach (var element in _FindElementsByTag(PanCustom, target))
            _ApplyHomepageLivePatchToElement(element, patch);
    }

    private void _ApplyHomepageLivePatchToElement(Control element, JsonObject patch)
    {
        _SetPropertyIfPresent(element, patch, "text", "Text");
        _SetPropertyIfPresent(element, patch, "title", "Title");
        _SetPropertyIfPresent(element, patch, "info", "Info");
        _SetPropertyIfPresent(element, patch, "tooltip", "ToolTip");
        _SetPropertyIfPresent(element, patch, "toolTip", "ToolTip");
        _SetPropertyIfPresent(element, patch, "visibility", "Visibility");
        _SetPropertyIfPresent(element, patch, "isEnabled", "IsEnabled");
        _SetPropertyIfPresent(element, patch, "opacity", "Opacity");

        if (patch["properties"] is JsonObject properties)
        {
            foreach (var property in properties)
                _TrySetElementProperty(element, property.Key, property.Value?.ToString() ?? "");
        }

        var childrenXaml = _TryGetString(patch, "childrenXaml", "ChildrenXaml");
        if (!string.IsNullOrEmpty(childrenXaml) && element is Panel panel)
            _ReplacePanelChildren(panel, childrenXaml);
    }

    private static void _SetPropertyIfPresent(Control element, JsonObject patch, string jsonName, string propertyName)
    {
        if (patch.TryGetPropertyValue(jsonName, out var value))
            _TrySetElementProperty(element, propertyName, value?.ToString() ?? "");
    }

    private static bool _TrySetElementProperty(Control element, string propertyName, string value)
    {
        if (!_homepageLiveAllowedProperties.TryGetValue(propertyName, out var allowedPropertyName))
        {
            HomepageSupport.Log($"[Page] Skipped unsupported live patch property {propertyName}");
            return false;
        }

        propertyName = allowedPropertyName;
        var trimmedValue = value.Trim();
        try
        {
            // Avalonia 中没有与 WPF 同名的 Visibility 与 ToolTip 属性，分别映射到 IsVisible 与 ToolTip.Tip
            switch (propertyName)
            {
                case "ToolTip":
                    ToolTip.SetTip(element, value);
                    return true;
                case "Visibility" when trimmedValue.Equals("Visible", StringComparison.OrdinalIgnoreCase):
                    element.IsVisible = true;
                    return true;
                case "Visibility" when trimmedValue.Equals("Collapsed", StringComparison.OrdinalIgnoreCase)
                                       || trimmedValue.Equals("Hidden", StringComparison.OrdinalIgnoreCase):
                    element.IsVisible = false;
                    return true;
                case "Visibility":
                    return false;
            }

            var property = element.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanWrite) return false;

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object convertedValue;
            if (propertyType == typeof(string))
                convertedValue = value;
            else if (propertyType == typeof(object))
                convertedValue = value;
            else if (propertyType == typeof(bool) && bool.TryParse(trimmedValue, out var boolValue))
                convertedValue = boolValue;
            else if (propertyType == typeof(int) && int.TryParse(trimmedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                convertedValue = intValue;
            else if (propertyType == typeof(double) && double.TryParse(trimmedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
                convertedValue = doubleValue;
            else if (propertyType.IsEnum && Enum.TryParse(propertyType, trimmedValue, true, out var enumValue))
                convertedValue = enumValue!;
            else
                return false;

            property.SetValue(element, convertedValue);
            return true;
        }
        catch (Exception ex)
        {
            HomepageSupport.Log(ex, $"[Page] Failed to set live patch property {propertyName}", LogEventLevel.Debug);
            return false;
        }
    }

    private static void _ReplacePanelChildren(Panel panel, string childrenXaml)
    {
        var content = HomepageSupport.StripXmlns(HomepageSupport.ArgumentReplace(childrenXaml));
        if (HomepageSupport.LoadXaml(HomepageSupport.WrapPanel(content)) is not Panel parsedPanel) return;

        var children = parsedPanel.Children.OfType<Control>().ToList();
        parsedPanel.Children.Clear();
        panel.Children.Clear();
        foreach (var child in children)
            panel.Children.Add(child);
    }

    private static IEnumerable<Control> _FindElementsByTag(Control root, string tag) =>
        root.GetSelfAndVisualDescendants().OfType<Control>()
            .Where(element => string.Equals(element.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static string? _TryGetString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetPropertyValue(name, out var value))
                return value?.ToString();
        }

        return null;
    }
}
