using Asms.Domain.Academics;
using Asms.Domain.Finance;
using Asms.Domain.Schools;
using Asms.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class FeeCategoryConfiguration : IEntityTypeConfiguration<FeeCategory>
{
    public void Configure(EntityTypeBuilder<FeeCategory> builder)
    {
        builder.Property(category => category.Name).IsRequired().HasMaxLength(200);
        builder.Property(category => category.Description).HasMaxLength(2000);
        builder.HasIndex(category => category.SchoolId)
            .HasDatabaseName("IX_FeeCategories_SchoolId");
        builder.HasIndex(category => new { category.SchoolId, category.Name })
            .IsUnique()
            .HasDatabaseName("UX_FeeCategories_SchoolId_Name");
        builder.HasOne<School>().WithMany().HasForeignKey(category => category.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FeeStructureConfiguration : IEntityTypeConfiguration<FeeStructure>
{
    public void Configure(EntityTypeBuilder<FeeStructure> builder)
    {
        builder.Property(structure => structure.Amount).HasPrecision(18, 2);
        builder.Property(structure => structure.Currency).IsRequired().HasMaxLength(3);
        builder.HasIndex(structure => structure.SchoolId)
            .HasDatabaseName("IX_FeeStructures_SchoolId");
        builder.HasIndex(structure => new
            {
                structure.SchoolId,
                structure.FeeCategoryId,
                structure.AcademicYearId,
                structure.GradeId
            })
            .IsUnique()
            .HasDatabaseName("UX_FeeStructures_SchoolId_FeeCategoryId_AcademicYearId_GradeId");
        builder.HasOne<School>().WithMany().HasForeignKey(structure => structure.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FeeCategory>().WithMany().HasForeignKey(structure => structure.FeeCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(structure => structure.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Grade>().WithMany().HasForeignKey(structure => structure.GradeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StudentInvoiceConfiguration : IEntityTypeConfiguration<StudentInvoice>
{
    public void Configure(EntityTypeBuilder<StudentInvoice> builder)
    {
        builder.Property(invoice => invoice.InvoiceNumber).IsRequired().HasMaxLength(50);
        builder.Property(invoice => invoice.Amount).HasPrecision(18, 2);
        builder.Property(invoice => invoice.DiscountAmount).HasPrecision(18, 2);
        builder.Property(invoice => invoice.ScholarshipAmount).HasPrecision(18, 2);
        builder.Property(invoice => invoice.Currency).IsRequired().HasMaxLength(3);
        builder.Property(invoice => invoice.Notes).HasMaxLength(2000);
        builder.HasIndex(invoice => invoice.SchoolId)
            .HasDatabaseName("IX_StudentInvoices_SchoolId");
        builder.HasIndex(invoice => new { invoice.SchoolId, invoice.InvoiceNumber })
            .IsUnique()
            .HasDatabaseName("UX_StudentInvoices_SchoolId_InvoiceNumber");
        builder.HasIndex(invoice => new { invoice.SchoolId, invoice.StudentId })
            .HasDatabaseName("IX_StudentInvoices_SchoolId_StudentId");
        builder.HasOne<School>().WithMany().HasForeignKey(invoice => invoice.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Student>().WithMany().HasForeignKey(invoice => invoice.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(invoice => invoice.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.Currency).IsRequired().HasMaxLength(3);
        builder.Property(payment => payment.ReceiptNumber).IsRequired().HasMaxLength(50);
        builder.Property(payment => payment.Reason).HasMaxLength(2000);
        builder.HasIndex(payment => payment.SchoolId).HasDatabaseName("IX_Payments_SchoolId");
        builder.HasIndex(payment => new { payment.SchoolId, payment.ReceiptNumber })
            .IsUnique()
            .HasDatabaseName("UX_Payments_SchoolId_ReceiptNumber");
        builder.HasIndex(payment => new { payment.SchoolId, payment.StudentInvoiceId })
            .HasDatabaseName("IX_Payments_SchoolId_StudentInvoiceId");
        builder.HasIndex(payment => new { payment.SchoolId, payment.StudentId })
            .HasDatabaseName("IX_Payments_SchoolId_StudentId");
        builder.HasOne<School>().WithMany().HasForeignKey(payment => payment.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StudentInvoice>().WithMany().HasForeignKey(payment => payment.StudentInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Student>().WithMany().HasForeignKey(payment => payment.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payment>().WithMany().HasForeignKey(payment => payment.ReversesPaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.Property(expense => expense.Title).IsRequired().HasMaxLength(200);
        builder.Property(expense => expense.Category).HasMaxLength(200);
        builder.Property(expense => expense.Amount).HasPrecision(18, 2);
        builder.Property(expense => expense.Currency).IsRequired().HasMaxLength(3);
        builder.Property(expense => expense.PaidTo).HasMaxLength(200);
        builder.Property(expense => expense.Notes).HasMaxLength(2000);
        builder.HasIndex(expense => expense.SchoolId).HasDatabaseName("IX_Expenses_SchoolId");
        builder.HasIndex(expense => new { expense.SchoolId, expense.ExpenseDate })
            .HasDatabaseName("IX_Expenses_SchoolId_ExpenseDate");
        builder.HasOne<School>().WithMany().HasForeignKey(expense => expense.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
