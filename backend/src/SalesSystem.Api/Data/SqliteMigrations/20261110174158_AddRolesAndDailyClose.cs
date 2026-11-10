using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SalesSystem.Api.Data.SqliteMigrations
{
    /// <inheritdoc />
    public partial class AddRolesAndDailyClose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyCloses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BusinessDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ExpectedCash = table.Column<double>(type: "REAL", precision: 18, scale: 2, nullable: false),
                    ExpectedCard = table.Column<double>(type: "REAL", precision: 18, scale: 2, nullable: false),
                    CountedCash = table.Column<double>(type: "REAL", precision: 18, scale: 2, nullable: false),
                    CountedCard = table.Column<double>(type: "REAL", precision: 18, scale: 2, nullable: false),
                    OrdersCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RentalPaymentsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ClosedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCloses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyCloses_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "DisplayName", "Name" },
                values: new object[,]
                {
                    { 3, "مدير العمليات", "OperationsManager" },
                    { 4, "محاسب", "Accountant" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_BusinessDate",
                table: "DailyCloses",
                column: "BusinessDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_ClosedByUserId",
                table: "DailyCloses",
                column: "ClosedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyCloses");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4);
        }
    }
}
