using KipInventorySystem.Application.Services.Payment;
using KipInventorySystem.Application.Services.Payment.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using static KipInventorySystem.Shared.Models.AppSettings;

namespace KipInventorySystem.Infrastructure.Integrations.Stripe;

internal class StripeService(IOptions<StripeSettings> stripeSettings, ILogger<StripeService> logger) : IPaymentService
{
    private readonly StripeSettings _stripeSettings = stripeSettings.Value;

    public async Task<PaymentIntentResponseDTO> CreatePaymentIntentAsync(CreatePaymentIntentDTO dto, CancellationToken cancellationToken)
    {
        try
        {
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            var options = new PaymentIntentCreateOptions
            {
                Amount = dto.Amount,
                Currency = dto.Currency,
                Description = dto.Description,
                Metadata = dto.Metadata,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                },
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options, cancellationToken: cancellationToken);

            logger.LogInformation("Payment intent created successfully. ID: {PaymentIntentId}", paymentIntent.Id);

            return new PaymentIntentResponseDTO
            {
                Id = paymentIntent.Id,
                ClientSecret = paymentIntent.ClientSecret,
                Amount = paymentIntent.Amount,
                Currency = paymentIntent.Currency,
                Status = paymentIntent.Status
            };
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error while creating payment intent");
            throw;
        }
    }

    public async Task<PaymentIntentResponseDTO> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        try
        {
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId, cancellationToken: cancellationToken);

            return new PaymentIntentResponseDTO
            {
                Id = paymentIntent.Id,
                ClientSecret = paymentIntent.ClientSecret,
                Amount = paymentIntent.Amount,
                Currency = paymentIntent.Currency,
                Status = paymentIntent.Status
            };
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error while retrieving payment intent {PaymentIntentId}", paymentIntentId);
            throw;
        }
    }

    public async Task<bool> CancelPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        try
        {
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            var service = new PaymentIntentService();
            var paymentIntent = await service.CancelAsync(paymentIntentId, cancellationToken: cancellationToken);

            logger.LogInformation("Payment intent cancelled successfully. ID: {PaymentIntentId}", paymentIntentId);

            return paymentIntent.Status == "canceled";
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error while cancelling payment intent {PaymentIntentId}", paymentIntentId);
            throw;
        }
    }

    public async Task<string> CreateCustomerAsync(CreateCustomerDTO dto, CancellationToken cancellationToken)
    {
        try
        {
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            var options = new CustomerCreateOptions
            {
                Email = dto.Email,
                Name = dto.Name,
                Metadata = dto.Metadata,
            };

            var service = new CustomerService();
            var customer = await service.CreateAsync(options, cancellationToken: cancellationToken);

            logger.LogInformation("Customer created successfully. ID: {CustomerId}", customer.Id);

            return customer.Id;
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error while creating customer");
            throw;
        }
    }

    public async Task<bool> RefundPaymentAsync(RefundDTO dto, CancellationToken cancellationToken)
    {
        try
        {
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            var options = new RefundCreateOptions
            {
                PaymentIntent = dto.PaymentIntentId,
                Amount = dto.Amount,
                Reason = dto.Reason,
            };

            var service = new RefundService();
            var refund = await service.CreateAsync(options, cancellationToken: cancellationToken);

            logger.LogInformation("Refund processed successfully. ID: {RefundId}", refund.Id);

            return refund.Status == "succeeded";
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error while processing refund for payment intent {PaymentIntentId}", dto.PaymentIntentId);
            throw;
        }
    }
}
