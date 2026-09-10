using Microsoft.AspNetCore.Mvc;
using EduCore.Application.Interfaces;

namespace EduCore.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IAttendanceRepository _attendance;
    private readonly IResultRepository _results;
    private readonly IFinanceRepository _finance;

    public ReportsController(IAttendanceRepository attendance, IResultRepository results, IFinanceRepository finance)
    {
        _attendance = attendance;
        _results = results;
        _finance = finance;
    }

    [HttpGet("attendance/summary")]
    public async Task<IActionResult> AttendanceSummary([FromQuery] DateTime? start, [FromQuery] DateTime? end, [FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var s = start ?? DateTime.UtcNow.AddDays(-30);
        var e = end ?? DateTime.UtcNow;
        var summary = await _attendance.GetSummaryAsync(s, e, tenant);
        return Ok(summary);
    }

    [HttpGet("finance/summary")]
    public async Task<IActionResult> FinanceSummary([FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var invoices = await _finance.GetInvoicesAsync(tenant);
        var payments = await _finance.GetPaymentsAsync(tenant);

        var totalInvoiced = invoices.Sum(i => i.Amount);
        var totalPaid = invoices.Sum(i => i.Paid);
        var outstanding = totalInvoiced - totalPaid;

        return Ok(new { totalInvoiced, totalPaid, outstanding, invoiceCount = invoices.Count() });
    }

    [HttpGet("results/summary")]
    public async Task<IActionResult> ResultsSummary([FromQuery] string? tenant)
    {
        tenant ??= User.FindFirst("tenant_id")?.Value;
        var all = await _results.GetAllAsync(tenant);
        var avg = all.Any() ? all.Average(r => r.Percentage) : 0;
        return Ok(new { averagePercentage = avg, count = all.Count() });
    }
}
