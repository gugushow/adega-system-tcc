using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace AdegaSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3ProductBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BatchId",
                table: "PurchaseItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    PurchaseId = table.Column<int>(type: "int", nullable: true),
                    BatchNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    QuantityReceived = table.Column<int>(type: "int", nullable: false),
                    QuantityAvailable = table.Column<int>(type: "int", nullable: false),
                    ManufacturingDate = table.Column<DateTime>(type: "date", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "date", nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductBatches_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductBatches_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_BatchId",
                table: "StockMovements",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_BatchId",
                table: "PurchaseItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ExpirationDate",
                table: "ProductBatches",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ProductId_ExpirationDate",
                table: "ProductBatches",
                columns: new[] { "ProductId", "ExpirationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_PurchaseId",
                table: "ProductBatches",
                column: "PurchaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseItems_ProductBatches_BatchId",
                table: "PurchaseItems",
                column: "BatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_ProductBatches_BatchId",
                table: "StockMovements",
                column: "BatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Escrito à mão (o EF não gera isto): produtos que já têm estoque antes da Fase 3
            // ganham um lote "SALDO-INICIAL" com esse saldo, sem validade e com o custo atual.
            // Assim a regra "CurrentStock = soma dos lotes" já vale desde o primeiro dia.
            // Depois, o saldo pode ser corrigido pela tela de Estoque (ajuste) se for preciso.
            migrationBuilder.Sql(
                """
                INSERT INTO ProductBatches
                    (ProductId, PurchaseId, BatchNumber, QuantityReceived, QuantityAvailable, ManufacturingDate, ExpirationDate, UnitCost, CreatedAt)
                SELECT Id, NULL, 'SALDO-INICIAL', CurrentStock, CurrentStock, NULL, NULL, CostPrice, UTC_TIMESTAMP(6)
                FROM Products
                WHERE CurrentStock > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseItems_ProductBatches_BatchId",
                table: "PurchaseItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_ProductBatches_BatchId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_BatchId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseItems_BatchId",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "PurchaseItems");
        }
    }
}
