namespace Klinova.Web.Tests;

internal static class HttpClientExtensions
{
    /// <summary>Posts an HTML form (application/x-www-form-urlencoded) and disposes the request content.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string url, IEnumerable<KeyValuePair<string, string>> form)
    {
        using var content = new FormUrlEncodedContent(form);
        return await client.PostAsync(url, content);
    }
}
