using Maliev.NotificationService.Api.Services.External;

namespace Microsoft.Extensions.Hosting;

public static class NotificationHttpClientExtensions
{
    public static IHostApplicationBuilder AddNotificationCustomerServiceClient(this IHostApplicationBuilder builder)
    {
        builder.AddAuthenticatedServiceClient<ICustomerServiceClient, CustomerServiceClient>(
            "CustomerService",
            sourceServiceName: "notification");

        return builder;
    }
}
