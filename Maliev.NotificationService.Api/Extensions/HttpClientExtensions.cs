using Maliev.NotificationService.Api.Services.External;

namespace Microsoft.Extensions.Hosting;

public static class NotificationHttpClientExtensions
{
    public static IHostApplicationBuilder AddNotificationCustomerServiceClient(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient<ICustomerServiceClient, CustomerServiceClient>((sp, client) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var explicitUrl = configuration["Services:CustomerService:BaseUrl"];

            if (!string.IsNullOrEmpty(explicitUrl))
            {
                client.BaseAddress = new Uri(explicitUrl);
            }
            else
            {
                client.BaseAddress = new Uri($"http://CustomerService");
            }

            client.Timeout = TimeSpan.FromSeconds(90);
        })
        .AddServiceDiscovery();

        return builder;
    }
}
