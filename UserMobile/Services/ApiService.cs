using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using UserMobile.Models;

namespace UserMobile.Services;

public sealed class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenStore _tokenStore;

    public ApiService(HttpClient httpClient, IAccessTokenStore tokenStore)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task<ApiResponse<T>> GetAsync<T>(
        string route,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<T>(HttpMethod.Get, route, null, cancellationToken);
    }

    public Task<ApiResponse<T>> PostAsync<T>(
        string route,
        object body,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<T>(HttpMethod.Post, route, body, cancellationToken);
    }

    private async Task<ApiResponse<T>> SendAsync<T>(
        HttpMethod method,
        string route,
        object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, route);
            var accessToken = await _tokenStore.GetAsync();
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }

            if (body != null)
                request.Content = JsonContent.Create(body);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                _tokenStore.Clear();

            return await ReadResponseAsync<T>(response, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"API connection failed: {_httpClient.BaseAddress}{route}. {exception}");

            return new ApiResponse<T>
            {
                Success = false,
#if DEBUG
                Message = $"Không thể kết nối tới {_httpClient.BaseAddress}. " +
                          "Hãy bảo đảm AdminWeb đang chạy đúng cổng 5297."
#else
                Message = "Không thể kết nối tới máy chủ."
#endif
            };
        }
    }

    private static async Task<ApiResponse<T>> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(
                cancellationToken: cancellationToken);
            if (result is not null)
                return result;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
        {
            System.Diagnostics.Debug.WriteLine($"Invalid API response: {exception}");
        }

        return new ApiResponse<T>
        {
            Success = false,
            Message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Phiên đăng nhập đã hết hạn.",
                HttpStatusCode.NotFound => "Không tìm thấy API được yêu cầu.",
                _ => response.ReasonPhrase ?? "Máy chủ trả về dữ liệu không hợp lệ."
            }
        };
    }
}
