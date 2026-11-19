using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QuoteHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Text = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Quotes",
                columns: new[] { "Id", "Author", "Category", "CreatedAtUtc", "IsDeleted", "Text" },
                values: new object[,]
                {
                    { 1, "Katherine Johnson", "Space", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "Like what you do, and then you will do your best." },
                    { 2, "Grace Hopper", "Engineering", new DateTime(2026, 1, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "The most dangerous phrase in the language is, 'We've always done it this way.'" },
                    { 3, "Edsger W. Dijkstra", "Engineering", new DateTime(2026, 1, 3, 0, 0, 0, 0, DateTimeKind.Utc), false, "Simplicity is prerequisite for reliability." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Category",
                table: "Quotes",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Quotes");
        }
    }
}
