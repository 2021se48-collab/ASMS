using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Finance;

/// <summary>
/// Payments are append-only and never edited or deleted. Fix mistakes with reversals.
/// Amounts are positive; the Application layer checks that reversals do not exceed payments.
/// </summary>
public class Payment : BaseEntity
{
    public Guid StudentInvoiceId { get; init; }

    public Guid StudentId { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "AFN";

    public PaymentKind Kind { get; init; }

    public Guid? ReversesPaymentId { get; init; }

    public PaymentMethod Method { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public DateTime PaidAtUtc { get; init; }

    public Guid? ReceivedByUserId { get; init; }

    public string? Reason { get; init; }

    /// <summary>
    /// Creates a payment with a positive amount and receipt number.
    /// </summary>
    public static Payment CreatePayment(
        Guid schoolId,
        Guid invoiceId,
        Guid studentId,
        decimal amount,
        PaymentMethod method,
        string receiptNumber,
        DateTime paidAtUtc,
        Guid? receivedByUserId,
        string currency = "AFN")
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            throw new ArgumentException("Receipt number cannot be empty.", nameof(receiptNumber));
        }

        return new Payment
        {
            SchoolId = schoolId,
            StudentInvoiceId = invoiceId,
            StudentId = studentId,
            Amount = amount,
            Currency = currency,
            Kind = PaymentKind.Payment,
            Method = method,
            ReceiptNumber = receiptNumber,
            PaidAtUtc = paidAtUtc,
            ReceivedByUserId = receivedByUserId
        };
    }

    /// <summary>
    /// Creates a reversal record for an existing payment.
    /// </summary>
    public static Payment CreateReversal(
        Payment original,
        string reason,
        string receiptNumber,
        DateTime reversedAtUtc,
        Guid? userId)
    {
        if (original is null)
        {
            throw new ArgumentNullException(nameof(original));
        }

        if (original.Kind == PaymentKind.Reversal)
        {
            throw new InvalidOperationException("A reversal cannot be reversed.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        }

        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            throw new ArgumentException("Receipt number cannot be empty.", nameof(receiptNumber));
        }

        return new Payment
        {
            SchoolId = original.SchoolId,
            StudentInvoiceId = original.StudentInvoiceId,
            StudentId = original.StudentId,
            Amount = original.Amount,
            Currency = original.Currency,
            Kind = PaymentKind.Reversal,
            ReversesPaymentId = original.Id,
            Method = original.Method,
            ReceiptNumber = receiptNumber,
            PaidAtUtc = reversedAtUtc,
            ReceivedByUserId = userId,
            Reason = reason
        };
    }
}
