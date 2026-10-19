using EduCore.Application.DTOs;

namespace EduCore.Application.Interfaces;

public interface IFinanceRepository
{
    Task<IEnumerable<InvoiceDto>> GetInvoicesAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentDto>> GetPaymentsAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<InvoiceDto> CreateInvoiceAsync(InvoiceDto invoice, CancellationToken cancellationToken = default);
    Task<PaymentDto> RecordPaymentAsync(PaymentDto payment, CancellationToken cancellationToken = default);
}
