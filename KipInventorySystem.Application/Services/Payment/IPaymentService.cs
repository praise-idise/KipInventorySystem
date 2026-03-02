using KipInventorySystem.Application.Services.Payment.DTOs;

namespace KipInventorySystem.Application.Services.Payment;

public interface IPaymentService
{
    Task<PaymentIntentResponseDTO> CreatePaymentIntentAsync(CreatePaymentIntentDTO dto, CancellationToken cancellationToken);
    Task<PaymentIntentResponseDTO> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken);
    Task<bool> CancelPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken);
    Task<string> CreateCustomerAsync(CreateCustomerDTO dto, CancellationToken cancellationToken);
    Task<bool> RefundPaymentAsync(RefundDTO dto, CancellationToken cancellationToken);
}
