using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Shion.Core.Accounts;

/// <summary>
/// 一次账号接口请求的结果。失败时也保留响应正文，便于读出协议错误码。
/// </summary>
internal readonly record struct AccountResponse(HttpStatusCode Status, string Body)
{
    public bool IsSuccess => (int)Status is >= 200 and < 300;

    public JsonNode? Json => AccountHttp.ParseJson(Body);
}

/// <summary>
/// 账号登录共用的 HTTP 与 JSON 辅助方法。
/// </summary>
internal static class AccountHttp
{
    public static async Task<AccountResponse> SendAsync(HttpClient http, HttpMethod method, string url,
        HttpContent? content = null, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrEmpty(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new AccountResponse(response.StatusCode, body);
    }

    public static Task<AccountResponse> PostJsonAsync(HttpClient http, string url, JsonNode body,
        string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        return SendAsync(http, HttpMethod.Post, url, content, bearerToken, cancellationToken);
    }

    public static Task<AccountResponse> PostFormAsync(HttpClient http, string url,
        IEnumerable<KeyValuePair<string, string>> form, CancellationToken cancellationToken = default)
    {
        return SendAsync(http, HttpMethod.Post, url, new FormUrlEncodedContent(form), null, cancellationToken);
    }

    public static JsonNode? ParseJson(string body)
    {
        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? GetString(JsonNode? node, string name)
    {
        return node?[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;
    }

    public static int GetInt(JsonNode? node, string name, int fallback)
    {
        return node?[name] is JsonValue value && value.TryGetValue(out int number) ? number : fallback;
    }

    public static long? GetLong(JsonNode? node, string name)
    {
        return node?[name] is JsonValue value && value.TryGetValue(out long number) ? number : null;
    }

    /// <summary>
    /// 把失败的响应转换为 <see cref="AccountException"/>，错误码取自响应的 <c>error</c> 字段。
    /// </summary>
    public static AccountException ToException(AccountResponse response, string fallbackError)
    {
        var json = response.Json;
        var error = GetString(json, "error") ?? fallbackError;
        var message = GetString(json, "errorMessage") ?? GetString(json, "error_description") ??
                      GetString(json, "errorDescription") ?? $"请求失败（HTTP {(int)response.Status}）。";
        return new AccountException(error, message);
    }

    /// <summary>
    /// 解析令牌端点的响应。授权未完成时返回带 <see cref="TokenResponse.Error"/> 的结果，而不是抛出异常。
    /// </summary>
    public static TokenResponse ParseToken(AccountResponse response)
    {
        var json = response.Json;
        if (json is null)
            return new TokenResponse(null, null, null, null, 0, "invalid_response", $"令牌响应无效（HTTP {(int)response.Status}）。");

        var error = GetString(json, "error");
        return new TokenResponse(
            GetString(json, "access_token"),
            GetString(json, "refresh_token"),
            GetString(json, "id_token"),
            GetString(json, "token_type"),
            GetInt(json, "expires_in", 0),
            error,
            GetString(json, "error_description"));
    }

    public static DeviceCodeInfo? ParseDeviceCode(JsonNode? json)
    {
        var deviceCode = GetString(json, "device_code");
        var userCode = GetString(json, "user_code");
        var verificationUri = GetString(json, "verification_uri");
        if (string.IsNullOrEmpty(deviceCode) || string.IsNullOrEmpty(userCode) || string.IsNullOrEmpty(verificationUri))
            return null;

        return new DeviceCodeInfo(
            deviceCode,
            userCode,
            verificationUri,
            GetString(json, "verification_uri_complete"),
            GetInt(json, "expires_in", 900),
            Math.Max(GetInt(json, "interval", 5), 1));
    }

    public static AccountCandidate? ReadCandidate(JsonNode? node)
    {
        var id = GetString(node, "id");
        var name = GetString(node, "name");
        return string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) ? null : new AccountCandidate(id, name);
    }

    public static List<AccountCandidate> ReadCandidates(JsonNode? node)
    {
        var list = new List<AccountCandidate>();
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (ReadCandidate(item) is { } candidate)
                    list.Add(candidate);
            }
        }

        return list;
    }
}
