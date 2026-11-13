using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asms.Infrastructure.Migrations.Sqlite.Migrations
{
    /// <summary>
    /// These tables are append-only; APPEND_ONLY_TABLE is a code, never display text.
    /// </summary>
    public partial class AddAppendOnlyTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""CREATE TRIGGER TR_Payments_NoUpdate BEFORE UPDATE ON "Payments" BEGIN SELECT RAISE(ABORT, 'APPEND_ONLY_TABLE'); END;""");
            migrationBuilder.Sql("""CREATE TRIGGER TR_Payments_NoDelete BEFORE DELETE ON "Payments" BEGIN SELECT RAISE(ABORT, 'APPEND_ONLY_TABLE'); END;""");
            migrationBuilder.Sql("""CREATE TRIGGER TR_AuditLogs_NoUpdate BEFORE UPDATE ON "AuditLogs" BEGIN SELECT RAISE(ABORT, 'APPEND_ONLY_TABLE'); END;""");
            migrationBuilder.Sql("""CREATE TRIGGER TR_AuditLogs_NoDelete BEFORE DELETE ON "AuditLogs" BEGIN SELECT RAISE(ABORT, 'APPEND_ONLY_TABLE'); END;""");
            migrationBuilder.Sql("""CREATE TRIGGER TR_InventoryMovements_NoUpdate BEFORE UPDATE ON "InventoryMovements" BEGIN SELECT RAISE(ABORT, 'APPEND_ONLY_TABLE'); END;""");
            migrationBuilder.Sql("""CREATE TRIGGER TR_InventoryMovements_NoDelete BEFORE DELETE ON "InventoryMovements" BEGIN SELECT RAISE(ABORT, 'APPEND_ONLY_TABLE'); END;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "TR_Payments_NoUpdate";""");
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "TR_Payments_NoDelete";""");
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "TR_AuditLogs_NoUpdate";""");
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "TR_AuditLogs_NoDelete";""");
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "TR_InventoryMovements_NoUpdate";""");
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "TR_InventoryMovements_NoDelete";""");
        }
    }
}
