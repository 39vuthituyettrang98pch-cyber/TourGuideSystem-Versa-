using UserMobile.Models;

namespace UserMobile.Services;

public interface IApiService
{
    Uri? BaseAddress { get; }
    Task<ApiResponse<T>> GetAsync<T>(string route, CancellationToken cancellationToken = default);
    Task<ApiResponse<T>> PostAsync<T>(string route, object body, CancellationToken cancellationToken = default);
    Task<ApiResponse<T>> PutAsync<T>(string route, object body, CancellationToken cancellationToken = default);
}
