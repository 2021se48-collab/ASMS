using Asms.Domain.Enums;

namespace Asms.Domain.Finance;

/// <summary>
/// Calculates fee totals from invoices and payment records.
/// </summary>
public static class FeeCalculator
{
    /// <summary>
    /// Calculates the amount due after discounts and scholarships.
    /// </summary>
    public static decimal CalculateNetAmount(decimal amount, decimal discount, decimal scholarship)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (discount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(discount));
        }

        if (scholarship < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scholarship));
        }

        if (discount > amount - scholarship)
        {
            throw new ArgumentOutOfRangeException(nameof(discount), "Discount and scholarship exceed the amount.");
        }

        return amount - discount - scholarship;
    }

    /// <summary>
    /// Calculates payments minus reversals.
    /// </summary>
    public static decimal CalculatePaidTotal(IEnumerable<Payment> payments)
    {
        if (payments is null)
        {
            throw new ArgumentNullException(nameof(payments));
        }

        var total = 0m;
        foreach (var payment in payments)
        {
            if (payment.Kind == PaymentKind.Payment)
            {
                total += payment.Amount;
            }
            else if (payment.Kind == PaymentKind.Reversal)
            {
                total -= payment.Amount;
            }
        }

        return total;
    }

    /// <summary>
    /// Calculates the balance, which can be negative for an overpayment.
    /// </summary>
    public static decimal CalculateBalance(decimal netAmount, IEnumerable<Payment> payments)
    {
        return netAmount - CalculatePaidTotal(payments);
    }

    /// <summary>
    /// Determines invoice status from its balance and cancellation state.
    /// </summary>
    public static InvoiceStatus DetermineStatus(decimal netAmount, decimal paidTotal, bool isCancelled)
    {
        if (isCancelled)
        {
            return InvoiceStatus.Cancelled;
        }

        if (paidTotal >= netAmount && netAmount > 0)
        {
            return InvoiceStatus.Paid;
        }

        if (paidTotal > 0)
        {
            return InvoiceStatus.PartiallyPaid;
        }

        return InvoiceStatus.Issued;
    }
}
