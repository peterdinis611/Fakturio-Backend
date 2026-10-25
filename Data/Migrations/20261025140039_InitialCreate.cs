using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fakturio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LegalForm = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Ico = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Dic = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IcDph = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    IsVatPayer = table.Column<bool>(type: "INTEGER", nullable: false),
                    CommercialRegister = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    InvoiceEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Website = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ContactPerson = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Street = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false, defaultValue: "SK"),
                    BankIban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    BankBic = table.Column<string>(type: "TEXT", maxLength: 11, nullable: true),
                    BankName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DefaultDueDays = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultVatRate = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    DefaultCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    InvoicePrefix = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    DefaultPaymentMethod = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, defaultValue: "bank_transfer"),
                    DefaultConstantSymbol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    DefaultInvoiceNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    InvoiceFooter = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LogoUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Ico = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Dic = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IcDph = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Street = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false, defaultValue: "SK"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CustomerIco = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    CustomerDic = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    CustomerIcDph = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    CustomerEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CustomerStreet = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CustomerCity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CustomerPostalCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    CustomerCountry = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false, defaultValue: "SK"),
                    SellerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SellerIco = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    SellerDic = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    SellerIcDph = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    SellerStreet = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SellerCity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SellerPostalCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    SellerCountry = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PaidAt = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CancelReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CancelledAt = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    LastReminderSentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReminderCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    VariableSymbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ConstantSymbol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    SpecificSymbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PaymentMethod = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BankIban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    BankBic = table.Column<string>(type: "TEXT", maxLength: 11, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Subtotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    VatTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "ks"),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    VatRate = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Ico",
                table: "Customers",
                column: "Ico");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId",
                table: "InvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DueDate",
                table: "Invoices",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_IssueDate",
                table: "Invoices",
                column: "IssueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Number",
                table: "Invoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanySettings");

            migrationBuilder.DropTable(
                name: "InvoiceItems");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
