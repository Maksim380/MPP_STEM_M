using System.Net.Http.Json;
using Greenhouse.Core.Contracts;

namespace Greenhouse.Tests;

internal static class TestHelpers
{
    public static Task<HttpResponseMessage> PostReadingAsync(this HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/readings", body, GreenhouseFactory.Json);

    public static Task<HttpResponseMessage> PostBatchAsync(this HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/readings/batch", body, GreenhouseFactory.Json);

    public static async Task<T> GetAsync<T>(this HttpClient client, string url) =>
        (await client.GetFromJsonAsync<T>(url, GreenhouseFactory.Json))!;

    public static Task<List<ReadingOut>> ReadingsAsync(this HttpClient client, string query = "") =>
        client.GetAsync<List<ReadingOut>>("/api/readings" + query);

    public static Task<List<AlertOut>> AlertsAsync(this HttpClient client, string query = "") =>
        client.GetAsync<List<AlertOut>>("/api/alerts" + query);

    public static async Task<Dictionary<string, LatestReadingOut>> LatestAsync(this HttpClient client) =>
        (await client.GetAsync<List<LatestReadingOut>>("/api/readings/latest")).ToDictionary(x => x.Sensor);
}
