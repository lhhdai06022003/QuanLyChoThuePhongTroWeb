using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddYeuCauSuCo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "nguoi_thue",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "yeu_cau_su_co",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhongTroId = table.Column<int>(type: "integer", nullable: false),
                    NguoiThueId = table.Column<int>(type: "integer", nullable: false),
                    TieuDe = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MoTa = table.Column<string>(type: "text", nullable: false),
                    HinhAnhUrl = table.Column<string>(type: "text", nullable: true),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    NgayGui = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayXuLy = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChiPhiSuaChua = table.Column<double>(type: "double precision", nullable: false),
                    CongVaoHoaDon = table.Column<bool>(type: "boolean", nullable: false),
                    GhiChuAdmin = table.Column<string>(type: "text", nullable: true),
                    LyDoTuChoi = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yeu_cau_su_co", x => x.Id);
                    table.ForeignKey(
                        name: "FK_yeu_cau_su_co_nguoi_thue_NguoiThueId",
                        column: x => x.NguoiThueId,
                        principalTable: "nguoi_thue",
                        principalColumn: "NguoiThueId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_yeu_cau_su_co_phong_tro_PhongTroId",
                        column: x => x.PhongTroId,
                        principalTable: "phong_tro",
                        principalColumn: "PhongTroId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_su_co_NguoiThueId",
                table: "yeu_cau_su_co",
                column: "NguoiThueId");

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_su_co_PhongTroId",
                table: "yeu_cau_su_co",
                column: "PhongTroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "yeu_cau_su_co");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "nguoi_thue",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
