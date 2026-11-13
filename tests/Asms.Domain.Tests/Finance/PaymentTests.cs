using System.Runtime.CompilerServices;
using Asms.Domain.Enums;
using Asms.Domain.Finance;

namespace Asms.Domain.Tests.Finance;

public class PaymentTests
{
    [Fact]
    public void CreatePayment_sets_all_fields()
    {
        var schoolId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var paidAtUtc = new DateTime(2026, 11, 13, 9, 30, 0, DateTimeKind.Utc);

        var payment = Payment.CreatePayment(
            schoolId,
            invoiceId,
            studentId,
            125m,
            PaymentMethod.BankTransfer,
            "DEV01-20261113-0007",
            paidAtUtc,
            userId);

        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(schoolId, payment.SchoolId);
        Assert.Equal(invoiceId, payment.StudentInvoiceId);
        Assert.Equal(studentId, payment.StudentId);
        Assert.Equal(125m, payment.Amount);
        Assert.Equal("AFN", payment.Currency);
        Assert.Equal(PaymentKind.Payment, payment.Kind);
        Assert.Null(payment.ReversesPaymentId);
        Assert.Equal(PaymentMethod.BankTransfer, payment.Method);
        Assert.Equal("DEV01-20261113-0007", payment.ReceiptNumber);
        Assert.Equal(paidAtUtc, payment.PaidAtUtc);
        Assert.Equal(userId, payment.ReceivedByUserId);
        Assert.Null(payment.Reason);
    }

    [Fact]
    public void CreatePayment_throws_for_non_positive_amount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePayment(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePayment(-1m));
    }

    [Fact]
    public void CreatePayment_throws_for_empty_receipt_number()
    {
        Assert.Throws<ArgumentException>(() => Payment.CreatePayment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1m,
            PaymentMethod.Cash,
            " ",
            DateTime.UtcNow,
            null));
    }

    [Fact]
    public void CreateReversal_copies_original_fields_and_sets_reversal_fields()
    {
        var original = CreateSamplePayment();
        var reversedAtUtc = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
        var userId = Guid.NewGuid();

        var reversal = Payment.CreateReversal(
            original,
            "Entered in error",
            "DEV01-20261114-0001",
            reversedAtUtc,
            userId);

        Assert.NotEqual(original.Id, reversal.Id);
        Assert.NotEqual(original.ReceiptNumber, reversal.ReceiptNumber);
        Assert.Equal(original.SchoolId, reversal.SchoolId);
        Assert.Equal(original.StudentInvoiceId, reversal.StudentInvoiceId);
        Assert.Equal(original.StudentId, reversal.StudentId);
        Assert.Equal(original.Amount, reversal.Amount);
        Assert.Equal(original.Currency, reversal.Currency);
        Assert.Equal(original.Method, reversal.Method);
        Assert.Equal(PaymentKind.Reversal, reversal.Kind);
        Assert.Equal(original.Id, reversal.ReversesPaymentId);
        Assert.Equal("Entered in error", reversal.Reason);
        Assert.Equal(reversedAtUtc, reversal.PaidAtUtc);
        Assert.Equal(userId, reversal.ReceivedByUserId);
    }

    [Fact]
    public void CreateReversal_throws_for_null_original()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Payment.CreateReversal(null!, "Reason", "receipt", DateTime.UtcNow, null));
    }

    [Fact]
    public void CreateReversal_throws_when_original_is_a_reversal()
    {
        var reversal = Payment.CreateReversal(
            CreateSamplePayment(),
            "Correction",
            "receipt-2",
            DateTime.UtcNow,
            null);

        Assert.Throws<InvalidOperationException>(() =>
            Payment.CreateReversal(reversal, "Correction again", "receipt-3", DateTime.UtcNow, null));
    }

    [Fact]
    public void CreateReversal_throws_for_blank_reason_or_receipt_number()
    {
        var original = CreateSamplePayment();

        Assert.Throws<ArgumentException>(() =>
            Payment.CreateReversal(original, " ", "receipt-2", DateTime.UtcNow, null));
        Assert.Throws<ArgumentException>(() =>
            Payment.CreateReversal(original, "Reason", string.Empty, DateTime.UtcNow, null));
    }

    [Fact]
    public void Finance_properties_are_init_only()
    {
        var propertyNames = new[]
        {
            nameof(Payment.StudentInvoiceId),
            nameof(Payment.StudentId),
            nameof(Payment.Amount),
            nameof(Payment.Currency),
            nameof(Payment.Kind),
            nameof(Payment.ReversesPaymentId),
            nameof(Payment.Method),
            nameof(Payment.ReceiptNumber),
            nameof(Payment.PaidAtUtc),
            nameof(Payment.ReceivedByUserId),
            nameof(Payment.Reason)
        };

        foreach (var propertyName in propertyNames)
        {
            var property = typeof(Payment).GetProperty(propertyName);
            Assert.NotNull(property);
            Assert.NotNull(property!.SetMethod);
            Assert.Contains(
                typeof(IsExternalInit),
                property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers());
        }
    }

    private static Payment CreatePayment(decimal amount)
    {
        return Payment.CreatePayment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            amount,
            PaymentMethod.Cash,
            "receipt",
            DateTime.UtcNow,
            null);
    }

    private static Payment CreateSamplePayment()
    {
        return Payment.CreatePayment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            75m,
            PaymentMethod.MobileMoney,
            "DEV01-20261113-0001",
            new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid(),
            "USD");
    }
}
