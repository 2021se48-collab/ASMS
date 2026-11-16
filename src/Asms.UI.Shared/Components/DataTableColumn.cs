namespace Asms.UI.Shared.Components;

public sealed class DataTableColumn<TItem>
{
    public DataTableColumn(
        string headerKey,
        Func<TItem, string?> value,
        Func<TItem, IComparable?>? sortValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerKey);
        ArgumentNullException.ThrowIfNull(value);

        HeaderKey = headerKey;
        Value = value;
        SortValue = sortValue;
    }

    public string HeaderKey { get; }

    public Func<TItem, string?> Value { get; }

    public Func<TItem, IComparable?>? SortValue { get; }
}
