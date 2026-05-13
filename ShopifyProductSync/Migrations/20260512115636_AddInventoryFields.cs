using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopifyProductSync.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventoryQuantity",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ShopifyInventoryItemId",
                table: "Products",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ShopifyLocationId",
                table: "Products",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InventoryQuantity",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShopifyInventoryItemId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShopifyLocationId",
                table: "Products");
        }
    }
}
