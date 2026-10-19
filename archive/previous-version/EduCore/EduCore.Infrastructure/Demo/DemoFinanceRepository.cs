using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;

namespace EduCore.Infrastructure.Demo;

public class DemoFinanceRepository : IFinanceRepository
{
    private readonly List<InvoiceDto> _invoices = new();
    private readonly List<PaymentDto> _payments = new();

    public DemoFinanceRepository()
    {
        Seed();
    }

    private void Seed()
    {
        var tenantA = "educore-academy";
        var tenantB = "greenfield-college";

        _invoices.Add(new InvoiceDto { Id = Guid.NewGuid(), StudentId = Guid.NewGuid(), StudentName = "StudentA1", Category = "Tuition", Amount = 1000, Paid = 500, Date = DateTime.UtcNow.AddDays(-30), Status = "Partially Paid", TenantId = tenantA });
        _invoices.Add(new InvoiceDto { Id = Guid.NewGuid(), StudentId = Guid.NewGuid(), StudentName = "StudentA2", Category = "Registration", Amount = 200, Paid = 200, Date = DateTime.UtcNow.AddDays(-40), Status = "Paid", TenantId = tenantA });
        _invoices.Add(new InvoiceDto { Id = Guid.NewGuid(), StudentId = Guid.NewGuid(), StudentName = "StudentB1", Category = "Tuition", Amount = 1200, Paid = 0, Date = DateTime.UtcNow.AddDays(-90), Status = "Outstanding", TenantId = tenantB });

        _payments.Add(new PaymentDto { Id = Guid.NewGuid(), InvoiceId = _invoices[0].Id, StudentId = _invoices[0].StudentId, Amount = 500, Date = DateTime.UtcNow.AddDays(-15), TenantId = tenantA });
        _payments.Add(new PaymentDto { Id = Guid.NewGuid(), InvoiceId = _invoices[1].Id, StudentId = _invoices[1].StudentId, Amount = 200, Date = DateTime.UtcNow.AddDays(-35), TenantId = tenantA });
    }

    public Task<IEnumerable<InvoiceDto>> GetInvoicesAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var q = _invoices.AsEnumerable();
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(i => i.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<IEnumerable<PaymentDto>> GetPaymentsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var q = _payments.AsEnumerable();
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(p => p.TenantId == tenantId);
        return Task.FromResult(q);
    }

    public Task<InvoiceDto> CreateInvoiceAsync(InvoiceDto invoice, CancellationToken cancellationToken = default)
    {
        invoice.Id = Guid.NewGuid();
        _invoices.Add(invoice);
        return Task.FromResult(invoice);
    }

    public Task<PaymentDto> RecordPaymentAsync(PaymentDto payment, CancellationToken cancellationToken = default)
    {
        payment.Id = Guid.NewGuid();
        _payments.Add(payment);
        var inv = _invoices.FirstOrDefault(i => i.Id == payment.InvoiceId);
        if (inv != null)
        {
            inv.Paid += payment.Amount;
            inv.Status = inv.Paid >= inv.Amount ? "Paid" : "Partially Paid";
        }
        return Task.FromResult(payment);
    }
}
