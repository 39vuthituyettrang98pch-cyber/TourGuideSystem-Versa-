using UserMobile.Models;

namespace UserMobile.Services;

public interface IApiService
{
    Task<ApiResponse<T>> GetAsync<T>(
        string route,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<T>> PostAsync<T>(
        string route,
        object body,
        CancellationToken cancellationToken = default);
}
