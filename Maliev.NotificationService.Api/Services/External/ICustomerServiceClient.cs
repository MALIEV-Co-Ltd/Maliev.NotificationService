namespace Maliev.NotificationService.Api.Services.External;

public interface ICustomerServiceClient
{
    Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default);
}

public class CustomerDto
{
    public Guid CustomerId { get; set; }
    public Guid PrincipalId { get; set; }
    public string Email { get; set; } = string.Empty;
}
