namespace EduCore.Application.DTOs;

public class InvoiceDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty; // Paid, Partially Paid, Outstanding, Overdue
    public string TenantId { get; set; } = string.Empty;
}

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid StudentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
