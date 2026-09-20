using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Migrations
{
    /// <inheritdoc />
    public partial class FixHoaDonFilteredUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_hoa_don_HopDongId_Thang_Nam",
                table: "hoa_don");

            migrationBuilder.CreateIndex(
                name: "IX_hoa_don_HopDongId_Thang_Nam",
                table: "hoa_don",
                columns: new[] { "HopDongId", "Thang", "Nam" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_hoa_don_HopDongId_Thang_Nam",
                table: "hoa_don");

            migrationBuilder.CreateIndex(
                name: "IX_hoa_don_HopDongId_Thang_Nam",
                table: "hoa_don",
                columns: new[] { "HopDongId", "Thang", "Nam" },
                unique: true);
        }
    }
}
