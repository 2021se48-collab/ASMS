using Asms.Domain.Common;

namespace Asms.Domain.Library;

/// <summary>
/// Copy counts change only through the book methods.
/// </summary>
public class Book : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string? Author { get; set; }

    public string? Isbn { get; set; }

    public Guid? CategoryId { get; set; }

    public string? Language { get; set; }

    public int TotalCopies { get; private set; }

    public int AvailableCopies { get; private set; }

    public string? ShelfLocation { get; set; }

    public void AddCopies(int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        TotalCopies += count;
        AvailableCopies += count;
    }

    public void Checkout()
    {
        if (AvailableCopies < 1)
        {
            throw new InvalidOperationException();
        }

        AvailableCopies--;
    }

    public void Return()
    {
        if (AvailableCopies >= TotalCopies)
        {
            throw new InvalidOperationException();
        }

        AvailableCopies++;
    }
}
