using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PramosClearing.Market.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "exchanges",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchanges", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "cryptos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Network = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaxSupply = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cryptos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cryptos_assets_Id",
                        column: x => x.Id,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "etfs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnderlyingIndex = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalExpenseRatio = table.Column<decimal>(type: "decimal(8,6)", precision: 8, scale: 6, nullable: false),
                    MarketIdentifier = table.Column<string>(type: "nvarchar(42)", maxLength: 42, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_etfs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_etfs_assets_Id",
                        column: x => x.Id,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MarketIdentifier = table.Column<string>(type: "nvarchar(42)", maxLength: 42, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stocks_assets_Id",
                        column: x => x.Id,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cryptos_network",
                table: "cryptos",
                column: "Network");

            migrationBuilder.CreateIndex(
                name: "ix_cryptos_symbol",
                table: "cryptos",
                column: "Symbol",
                unique: true,
                filter: "[Symbol] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_etfs_exchange",
                table: "etfs",
                column: "Exchange");

            migrationBuilder.CreateIndex(
                name: "ix_etfs_symbol_exchange",
                table: "etfs",
                columns: new[] { "Symbol", "Exchange" },
                unique: true,
                filter: "[Symbol] IS NOT NULL AND [Exchange] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_exchanges_country",
                table: "exchanges",
                column: "Country");

            migrationBuilder.CreateIndex(
                name: "ix_stocks_exchange",
                table: "stocks",
                column: "Exchange");

            migrationBuilder.CreateIndex(
                name: "ix_stocks_symbol_exchange",
                table: "stocks",
                columns: new[] { "Symbol", "Exchange" },
                unique: true,
                filter: "[Symbol] IS NOT NULL AND [Exchange] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cryptos");

            migrationBuilder.DropTable(
                name: "currencies");

            migrationBuilder.DropTable(
                name: "etfs");

            migrationBuilder.DropTable(
                name: "exchanges");

            migrationBuilder.DropTable(
                name: "stocks");

            migrationBuilder.DropTable(
                name: "assets");
        }
    }
}
