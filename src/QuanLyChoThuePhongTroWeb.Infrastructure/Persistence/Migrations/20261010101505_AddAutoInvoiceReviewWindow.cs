using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoInvoiceReviewWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HanPhanHoi",
                table: "hoa_don",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoGioPhanHoiHoaDon",
                table: "chi_nhanh",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.CreateIndex(
                name: "IX_hoa_don_TrangThaiPhatHanh_HanPhanHoi",
                table: "hoa_don",
                columns: new[] { "TrangThaiPhatHanh", "HanPhanHoi" },
                filter: "\"HanPhanHoi\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChiNhanh_SoGioPhanHoiHoaDon",
                table: "chi_nhanh",
                sql: "\"SoGioPhanHoiHoaDon\" BETWEEN 1 AND 168");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_hoa_don_TrangThaiPhatHanh_HanPhanHoi",
                table: "hoa_don");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChiNhanh_SoGioPhanHoiHoaDon",
                table: "chi_nhanh");

            migrationBuilder.DropColumn(
                name: "HanPhanHoi",
                table: "hoa_don");

            migrationBuilder.DropColumn(
                name: "SoGioPhanHoiHoaDon",
                table: "chi_nhanh");
        }
    }
}
