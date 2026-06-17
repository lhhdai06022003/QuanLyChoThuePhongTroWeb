using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDangKyDichVuNgayKetThucAndMacDinhChiNhanh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MacDinh",
                table: "dich_vu");

            migrationBuilder.AddColumn<bool>(
                name: "MacDinh",
                table: "dich_vu_chi_nhanh",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayKetThuc",
                table: "dang_ky_dich_vu",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MacDinh",
                table: "dich_vu_chi_nhanh");

            migrationBuilder.DropColumn(
                name: "NgayKetThuc",
                table: "dang_ky_dich_vu");

            migrationBuilder.AddColumn<bool>(
                name: "MacDinh",
                table: "dich_vu",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
