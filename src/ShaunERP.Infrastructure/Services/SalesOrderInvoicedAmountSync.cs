using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services;

/// <summary>
/// Keeps <see cref="SalesOrder.AmountInvoiced"/> and invoiced status in sync with
/// issued (sent / paid) invoices only. Draft and void invoices do not consume
/// the order's billable balance or lock it as invoiced.
/// </summary>
internal static class SalesOrderInvoicedAmountSync
{
    public static bool CountsTowardInvoiced(InvoiceStatus status) =>
        status is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid;

    public static async Task ApplyAsync(ShaunErpDbContext db, SalesOrder order, CancellationToken ct)
    {
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.SalesOrderId == order.Id && i.CompanyId == order.CompanyId)
            .Where(i => CountsTowardInvoiced(i.InvoiceStatus))
            .ToListAsync(ct);

        order.AmountInvoiced = invoices.Sum(i => i.GrandTotal > 0 ? i.GrandTotal : i.Amount);

        if (order.OrderStatus == SalesOrderStatus.Cancelled) return;

        if (order.AmountInvoiced <= 0.01m)
        {
            if (order.OrderStatus is SalesOrderStatus.PartiallyInvoiced or SalesOrderStatus.Invoiced)
                order.OrderStatus = SalesOrderStatus.Confirmed;
        }
        else if (order.AmountInvoiced + 0.01m >= order.GrandTotal)
            order.OrderStatus = SalesOrderStatus.Invoiced;
        else
            order.OrderStatus = SalesOrderStatus.PartiallyInvoiced;
    }
}
