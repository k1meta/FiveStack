using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiveStack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Squads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SquadPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HostId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Region = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Server = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    RankMin = table.Column<int>(type: "integer", nullable: true),
                    RankMax = table.Column<int>(type: "integer", nullable: true),
                    NeededRoles = table.Column<string[]>(type: "text[]", nullable: false),
                    NeededAgents = table.Column<string[]>(type: "text[]", nullable: false),
                    SlotsOpen = table.Column<int>(type: "integer", nullable: false),
                    MicRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SquadPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SquadPosts_AspNetUsers_HostId",
                        column: x => x.HostId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JoinRequests",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinRequests", x => new { x.PostId, x.UserId });
                    table.ForeignKey(
                        name: "FK_JoinRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JoinRequests_SquadPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "SquadPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_UserId",
                table: "JoinRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SquadPosts_ExpiresAt",
                table: "SquadPosts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SquadPosts_HostId",
                table: "SquadPosts",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_SquadPosts_NeededRoles",
                table: "SquadPosts",
                column: "NeededRoles")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JoinRequests");

            migrationBuilder.DropTable(
                name: "SquadPosts");
        }
    }
}
