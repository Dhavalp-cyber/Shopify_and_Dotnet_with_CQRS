using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopifyProductSync.Migrations
{
    /// <inheritdoc />
    public partial class AddShopifyProductIdToUpdatedInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ShopifyProductId",
                table: "UpdatedInventories",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShopifyProductId",
                table: "UpdatedInventories");
        }
    }
}
