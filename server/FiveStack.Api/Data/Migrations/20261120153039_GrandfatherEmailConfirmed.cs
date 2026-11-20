using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiveStack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class GrandfatherEmailConfirmed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Email confirmation became required for password sign-in. Accounts made before that keep working.
            migrationBuilder.Sql("""UPDATE "AspNetUsers" SET "EmailConfirmed" = true WHERE NOT "IsDemo" AND "Email" IS NOT NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which accounts were confirmed before isn't recorded.
        }
    }
}
