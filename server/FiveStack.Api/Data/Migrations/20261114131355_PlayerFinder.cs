using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiveStack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlayerFinder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActiveAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Agents",
                table: "AspNetUsers",
                column: "Agents")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Languages",
                table: "AspNetUsers",
                column: "Languages")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Modes",
                table: "AspNetUsers",
                column: "Modes")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PlayTimes",
                table: "AspNetUsers",
                column: "PlayTimes")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Roles",
                table: "AspNetUsers",
                column: "Roles")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Servers",
                table: "AspNetUsers",
                column: "Servers")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Agents",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Languages",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Modes",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PlayTimes",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Roles",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Servers",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastActiveAt",
                table: "AspNetUsers");
        }
    }
}
