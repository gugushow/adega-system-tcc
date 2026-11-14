using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AdegaSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Barcode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Brand = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Volume = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    AlcoholContent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CostPrice = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MinimumStock = table.Column<int>(type: "int", nullable: false),
                    CurrentStock = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Active", "CreatedAt", "Description", "Name" },
                values: new object[,]
                {
                    { 1, true, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Cerveja" },
                    { 2, true, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Vinho" },
                    { 3, true, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Whisky" },
                    { 4, true, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Gin" },
                    { 5, true, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Vodka" },
                    { 6, true, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Energético" },
                    { 7, true, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Refrigerante" },
                    { 8, true, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Água" },
                    { 9, true, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Gelo" },
                    { 10, true, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Outros" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
