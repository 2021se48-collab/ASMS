using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Enums;

public class LibraryInventoryHomeworkEnumTests
{
    [Fact]
    public void Library_inventory_and_homework_enum_values_match_database_values()
    {
        Assert.Equal(0, (int)BookTransactionStatus.Issued);
        Assert.Equal(1, (int)BookTransactionStatus.Returned);
        Assert.Equal(2, (int)BookTransactionStatus.Lost);

        Assert.Equal(0, (int)InventoryCondition.Good);
        Assert.Equal(1, (int)InventoryCondition.Damaged);
        Assert.Equal(2, (int)InventoryCondition.Missing);
        Assert.Equal(3, (int)InventoryCondition.UnderMaintenance);
        Assert.Equal(4, (int)InventoryCondition.Retired);

        Assert.Equal(0, (int)InventoryMovementType.StockIn);
        Assert.Equal(1, (int)InventoryMovementType.StockOut);
        Assert.Equal(2, (int)InventoryMovementType.MarkedDamaged);
        Assert.Equal(3, (int)InventoryMovementType.MarkedMissing);
        Assert.Equal(4, (int)InventoryMovementType.SentForMaintenance);
        Assert.Equal(5, (int)InventoryMovementType.ReturnedFromMaintenance);

        Assert.Equal(0, (int)HomeworkSubmissionStatus.NotSubmitted);
        Assert.Equal(1, (int)HomeworkSubmissionStatus.Submitted);
        Assert.Equal(2, (int)HomeworkSubmissionStatus.Late);
        Assert.Equal(3, (int)HomeworkSubmissionStatus.Reviewed);
    }
}
