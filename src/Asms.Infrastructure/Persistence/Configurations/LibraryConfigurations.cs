using Asms.Domain.Library;
using Asms.Domain.Schools;
using Asms.Domain.Staffing;
using Asms.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class BookCategoryConfiguration : IEntityTypeConfiguration<BookCategory>
{
    public void Configure(EntityTypeBuilder<BookCategory> builder)
    {
        builder.Property(category => category.Name).IsRequired().HasMaxLength(200);
        builder.Property(category => category.Description).HasMaxLength(2000);
        builder.HasIndex(category => category.SchoolId)
            .HasDatabaseName("IX_BookCategories_SchoolId");
        builder.HasIndex(category => new { category.SchoolId, category.Name })
            .IsUnique()
            .HasDatabaseName("UX_BookCategories_SchoolId_Name");
        builder.HasOne<School>().WithMany().HasForeignKey(category => category.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.Property(book => book.Title).IsRequired().HasMaxLength(200);
        builder.Property(book => book.Author).HasMaxLength(200);
        builder.Property(book => book.Isbn).HasMaxLength(50);
        builder.Property(book => book.Language).HasMaxLength(10);
        builder.Property(book => book.ShelfLocation).HasMaxLength(200);
        builder.HasIndex(book => book.SchoolId).HasDatabaseName("IX_Books_SchoolId");
        builder.HasIndex(book => new { book.SchoolId, book.Isbn })
            .IsUnique()
            .HasDatabaseName("UX_Books_SchoolId_Isbn");
        builder.HasIndex(book => new { book.SchoolId, book.Title })
            .HasDatabaseName("IX_Books_SchoolId_Title");
        builder.HasOne<School>().WithMany().HasForeignKey(book => book.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BookCategory>().WithMany().HasForeignKey(book => book.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BookTransactionConfiguration : IEntityTypeConfiguration<BookTransaction>
{
    public void Configure(EntityTypeBuilder<BookTransaction> builder)
    {
        builder.Property(transaction => transaction.Notes).HasMaxLength(2000);
        builder.HasIndex(transaction => transaction.SchoolId)
            .HasDatabaseName("IX_BookTransactions_SchoolId");
        builder.HasIndex(transaction => new { transaction.SchoolId, transaction.BookId })
            .HasDatabaseName("IX_BookTransactions_SchoolId_BookId");
        builder.HasIndex(transaction => new { transaction.SchoolId, transaction.Status })
            .HasDatabaseName("IX_BookTransactions_SchoolId_Status");
        builder.HasOne<School>().WithMany().HasForeignKey(transaction => transaction.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Book>().WithMany().HasForeignKey(transaction => transaction.BookId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Student>().WithMany().HasForeignKey(transaction => transaction.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Teacher>().WithMany().HasForeignKey(transaction => transaction.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
