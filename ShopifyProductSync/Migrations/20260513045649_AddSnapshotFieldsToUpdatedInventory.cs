using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopifyProductSync.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotFieldsToUpdatedInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationName",
                table: "UpdatedInventories",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "UpdatedInventories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "ShopifyLocationId",
                table: "UpdatedInventories",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationName",
                table: "UpdatedInventories");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "UpdatedInventories");

            migrationBuilder.DropColumn(
                name: "ShopifyLocationId",
                table: "UpdatedInventories");
        }
    }
}
