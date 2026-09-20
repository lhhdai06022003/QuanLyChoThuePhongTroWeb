using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddThongBaoTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "thong_bao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NguoiDungId = table.Column<int>(type: "integer", nullable: true),
                    TieuDe = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    NoiDung = table.Column<string>(type: "text", nullable: false),
                    LinhVuc = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LinkDieuHuong = table.Column<string>(type: "text", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_thong_bao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_thong_bao_nguoi_dung_NguoiDungId",
                        column: x => x.NguoiDungId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_thong_bao_NguoiDungId",
                table: "thong_bao",
                column: "NguoiDungId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "thong_bao");
        }
    }
}
