using System.Net.Http.Json;

namespace Maliev.NotificationService.Api.Services.External;

public class CustomerServiceClient : ICustomerServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CustomerServiceClient> _logger;

    public CustomerServiceClient(HttpClient httpClient, ILogger<CustomerServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/customer/v1/customers/{customerId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<CustomerDto>(cancellationToken: cancellationToken);
            }
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Failed to fetch customer {CustomerId}. Status code: {StatusCode}", customerId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customer {CustomerId}", customerId);
        }
        return null;
    }
}
