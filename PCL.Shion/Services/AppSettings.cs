using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Services;

/// <summary>
/// 启动器设置，保存在 <c>%APPDATA%\PCL Shion\settings.json</c>（不与 PCL CE 共用目录）。
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string MinecraftRoot { get; set; } = MinecraftFolder.GetDefault();
    public string PlayerName { get; set; } = "";
    public string? SelectedInstance { get; set; }
    public int MaxMemoryMb { get; set; } = 4096;

    private static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL Shion", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 设置只是便利功能，写入失败不应影响启动
        }
    }
}
