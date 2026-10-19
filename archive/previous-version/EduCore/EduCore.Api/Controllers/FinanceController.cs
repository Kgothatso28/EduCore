using EduCore.Application.Interfaces;
using EduCore.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace EduCore.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class FinanceController : ControllerBase
{
    private readonly IFinanceRepository _finance;

    public FinanceController(IFinanceRepository finance)
    {
        _finance = finance;
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var inv = await _finance.GetInvoicesAsync(tenant);
        return Ok(inv);
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var p = await _finance.GetPaymentsAsync(tenant);
        return Ok(p);
    }

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice([FromBody] InvoiceDto invoice)
    {
        invoice.TenantId ??= User.FindFirst("tenant_id")?.Value ?? invoice.TenantId;
        var created = await _finance.CreateInvoiceAsync(invoice);
        return CreatedAtAction(nameof(GetInvoices), new { id = created.Id }, created);
    }

    [HttpPost("payments")]
    public async Task<IActionResult> RecordPayment([FromBody] PaymentDto payment)
    {
        payment.TenantId ??= User.FindFirst("tenant_id")?.Value ?? payment.TenantId;
        var created = await _finance.RecordPaymentAsync(payment);
        return CreatedAtAction(nameof(GetPayments), new { id = created.Id }, created);
    }
}
