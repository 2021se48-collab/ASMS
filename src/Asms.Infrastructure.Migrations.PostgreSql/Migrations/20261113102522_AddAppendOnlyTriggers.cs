using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asms.Infrastructure.Migrations.PostgreSql.Migrations
{
    /// <summary>
    /// These tables are append-only; APPEND_ONLY_TABLE is a code, never display text.
    /// </summary>
    public partial class AddAppendOnlyTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION asms_block_changes() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'APPEND_ONLY_TABLE';
                END;
                $$ LANGUAGE plpgsql;
                """);
            migrationBuilder.Sql("""CREATE TRIGGER "TR_Payments_NoChange" BEFORE UPDATE OR DELETE ON "Payments" FOR EACH ROW EXECUTE FUNCTION asms_block_changes();""");
            migrationBuilder.Sql("""CREATE TRIGGER "TR_AuditLogs_NoChange" BEFORE UPDATE OR DELETE ON "AuditLogs" FOR EACH ROW EXECUTE FUNCTION asms_block_changes();""");
            migrationBuilder.Sql("""CREATE TRIGGER "TR_InventoryMovements_NoChange" BEFORE UPDATE OR DELETE ON "InventoryMovements" FOR EACH ROW EXECUTE FUNCTION asms_block_changes();""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "TR_Payments_NoChange" ON "Payments";""");
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "TR_AuditLogs_NoChange" ON "AuditLogs";""");
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "TR_InventoryMovements_NoChange" ON "InventoryMovements";""");
            migrationBuilder.Sql("""DROP FUNCTION IF EXISTS asms_block_changes();""");
        }
    }
}
