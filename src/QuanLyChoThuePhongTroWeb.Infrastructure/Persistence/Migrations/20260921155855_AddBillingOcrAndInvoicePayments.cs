using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingOcrAndInvoicePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_dich_vu_dien_nuoc_cua_phong_PhongTroId",
                table: "dich_vu_dien_nuoc_cua_phong");

            migrationBuilder.AlterColumn<decimal>(
                name: "ChiPhiSuaChua",
                table: "yeu_cau_su_co",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "GiaThue",
                table: "phong_tro",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "SoTienThanhToan",
                table: "lich_su_thanh_toan",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<int>(
                name: "MinhChungThanhToanHoaDonId",
                table: "lich_su_thanh_toan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayXacNhan",
                table: "lich_su_thanh_toan",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TienThuePhong",
                table: "hop_dong",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "TienCocPhong",
                table: "hop_dong",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "TongTien",
                table: "hoa_don",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<bool>(
                name: "ChoPhepThanhToanMotPhan",
                table: "hoa_don",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "HanThanhToan",
                table: "hoa_don",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayChot",
                table: "hoa_don",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayGui",
                table: "hoa_don",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NguoiChotId",
                table: "hoa_don",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SoTienThanhToanToiThieu",
                table: "hoa_don",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrangThaiPhatHanh",
                table: "hoa_don",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "DonGiaNuoc",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "DonGiaDien",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "ChiSoNuocMoi",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "ChiSoNuocCu",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "ChiSoDienMoi",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "ChiSoDienCu",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<string>(
                name: "GhiChuDuyet",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayDuyet",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NguoiDuyetId",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NguoiTaoId",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrangThaiGhiNhan",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "GiaDichVu",
                table: "dich_vu_chi_nhanh",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "TongTien",
                table: "chi_tiet_hoa_don",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "SoLuong",
                table: "chi_tiet_hoa_don",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "DonGia",
                table: "chi_tiet_hoa_don",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.CreateTable(
                name: "anh_chi_so_dong_ho",
                columns: table => new
                {
                    AnhChiSoDongHoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DichVuDienNuocCuaPhongId = table.Column<int>(type: "integer", nullable: false),
                    LoaiDongHo = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    PublicId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NguoiGuiId = table.Column<int>(type: "integer", nullable: true),
                    NgayGui = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrangThaiXuLy = table.Column<int>(type: "integer", nullable: false),
                    GiaTriAIGoiY = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    DoTinCay = table.Column<double>(type: "double precision", nullable: true),
                    ThongBaoLoi = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NgayXuLy = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GiaTriXacNhan = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    NguoiXacNhanId = table.Column<int>(type: "integer", nullable: true),
                    NgayXacNhan = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GhiChuXacNhan = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DuocChonLamChiSoChinhThuc = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anh_chi_so_dong_ho", x => x.AnhChiSoDongHoId);
                    table.CheckConstraint("CK_AnhChiSoDongHo_ChiSoChinhThuc", "NOT \"DuocChonLamChiSoChinhThuc\" OR (\"GiaTriXacNhan\" IS NOT NULL AND \"NguoiXacNhanId\" IS NOT NULL AND \"NgayXacNhan\" IS NOT NULL)");
                    table.CheckConstraint("CK_AnhChiSoDongHo_DoTinCay", "\"DoTinCay\" IS NULL OR (\"DoTinCay\" >= 0 AND \"DoTinCay\" <= 1)");
                    table.CheckConstraint("CK_AnhChiSoDongHo_GiaTri", "(\"GiaTriAIGoiY\" IS NULL OR \"GiaTriAIGoiY\" >= 0) AND (\"GiaTriXacNhan\" IS NULL OR \"GiaTriXacNhan\" >= 0)");
                    table.ForeignKey(
                        name: "FK_anh_chi_so_dong_ho_dich_vu_dien_nuoc_cua_phong_DichVuDienNu~",
                        column: x => x.DichVuDienNuocCuaPhongId,
                        principalTable: "dich_vu_dien_nuoc_cua_phong",
                        principalColumn: "DichVuDienNuocCuaPhongId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_anh_chi_so_dong_ho_nguoi_dung_NguoiGuiId",
                        column: x => x.NguoiGuiId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_anh_chi_so_dong_ho_nguoi_dung_NguoiXacNhanId",
                        column: x => x.NguoiXacNhanId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "lich_su_trang_thai_hoa_don",
                columns: table => new
                {
                    LichSuTrangThaiHoaDonId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HoaDonId = table.Column<int>(type: "integer", nullable: false),
                    TrangThaiPhatHanhCu = table.Column<int>(type: "integer", nullable: true),
                    TrangThaiPhatHanhMoi = table.Column<int>(type: "integer", nullable: false),
                    TrangThaiThanhToanCu = table.Column<int>(type: "integer", nullable: true),
                    TrangThaiThanhToanMoi = table.Column<int>(type: "integer", nullable: false),
                    NguoiThucHienId = table.Column<int>(type: "integer", nullable: true),
                    NgayThucHien = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LyDo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lich_su_trang_thai_hoa_don", x => x.LichSuTrangThaiHoaDonId);
                    table.ForeignKey(
                        name: "FK_lich_su_trang_thai_hoa_don_hoa_don_HoaDonId",
                        column: x => x.HoaDonId,
                        principalTable: "hoa_don",
                        principalColumn: "HoaDonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "yeu_cau_thanh_toan_hoa_don",
                columns: table => new
                {
                    YeuCauThanhToanHoaDonId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HoaDonId = table.Column<int>(type: "integer", nullable: false),
                    MaYeuCau = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SoTien = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NoiDungChuyenKhoan = table.Column<string>(type: "text", nullable: false),
                    HanThanhToan = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    NguoiTaoId = table.Column<int>(type: "integer", nullable: true),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yeu_cau_thanh_toan_hoa_don", x => x.YeuCauThanhToanHoaDonId);
                    table.ForeignKey(
                        name: "FK_yeu_cau_thanh_toan_hoa_don_hoa_don_HoaDonId",
                        column: x => x.HoaDonId,
                        principalTable: "hoa_don",
                        principalColumn: "HoaDonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lich_su_trang_thai_yeu_cau_thanh_toan_hoa_don",
                columns: table => new
                {
                    LichSuTrangThaiYeuCauThanhToanHoaDonId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YeuCauThanhToanHoaDonId = table.Column<int>(type: "integer", nullable: false),
                    TrangThaiCu = table.Column<int>(type: "integer", nullable: true),
                    TrangThaiMoi = table.Column<int>(type: "integer", nullable: false),
                    NguoiThucHienId = table.Column<int>(type: "integer", nullable: true),
                    NgayThucHien = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LyDo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lich_su_trang_thai_yeu_cau_thanh_toan_hoa_don", x => x.LichSuTrangThaiYeuCauThanhToanHoaDonId);
                    table.ForeignKey(
                        name: "FK_lich_su_trang_thai_yeu_cau_thanh_toan_hoa_don_yeu_cau_thanh~",
                        column: x => x.YeuCauThanhToanHoaDonId,
                        principalTable: "yeu_cau_thanh_toan_hoa_don",
                        principalColumn: "YeuCauThanhToanHoaDonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "minh_chung_thanh_toan_hoa_don",
                columns: table => new
                {
                    MinhChungThanhToanHoaDonId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YeuCauThanhToanHoaDonId = table.Column<int>(type: "integer", nullable: false),
                    HinhAnhUrl = table.Column<string>(type: "text", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MaGiaoDichNganHang = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SoTienKhaiBao = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NgayChuyenKhaiBao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrangThaiDoiChieu = table.Column<int>(type: "integer", nullable: false),
                    NguoiDoiChieuId = table.Column<int>(type: "integer", nullable: true),
                    NgayDoiChieu = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LyDoTuChoi = table.Column<string>(type: "text", nullable: true),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_minh_chung_thanh_toan_hoa_don", x => x.MinhChungThanhToanHoaDonId);
                    table.ForeignKey(
                        name: "FK_minh_chung_thanh_toan_hoa_don_yeu_cau_thanh_toan_hoa_don_Ye~",
                        column: x => x.YeuCauThanhToanHoaDonId,
                        principalTable: "yeu_cau_thanh_toan_hoa_don",
                        principalColumn: "YeuCauThanhToanHoaDonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_thanh_toan_MaGiaoDich",
                table: "lich_su_thanh_toan",
                column: "MaGiaoDich",
                unique: true,
                filter: "\"MaGiaoDich\" IS NOT NULL AND \"MaGiaoDich\" <> '' AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_thanh_toan_MinhChungThanhToanHoaDonId",
                table: "lich_su_thanh_toan",
                column: "MinhChungThanhToanHoaDonId",
                unique: true,
                filter: "\"MinhChungThanhToanHoaDonId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_dich_vu_dien_nuoc_cua_phong_PhongTroId_Thang_Nam",
                table: "dich_vu_dien_nuoc_cua_phong",
                columns: new[] { "PhongTroId", "Thang", "Nam" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_anh_chi_so_dong_ho_DichVuDienNuocCuaPhongId_LoaiDongHo",
                table: "anh_chi_so_dong_ho",
                columns: new[] { "DichVuDienNuocCuaPhongId", "LoaiDongHo" },
                unique: true,
                filter: "\"DuocChonLamChiSoChinhThuc\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_anh_chi_so_dong_ho_NguoiGuiId",
                table: "anh_chi_so_dong_ho",
                column: "NguoiGuiId");

            migrationBuilder.CreateIndex(
                name: "IX_anh_chi_so_dong_ho_NguoiXacNhanId",
                table: "anh_chi_so_dong_ho",
                column: "NguoiXacNhanId");

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_trang_thai_hoa_don_HoaDonId",
                table: "lich_su_trang_thai_hoa_don",
                column: "HoaDonId");

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_trang_thai_yeu_cau_thanh_toan_hoa_don_YeuCauThanhTo~",
                table: "lich_su_trang_thai_yeu_cau_thanh_toan_hoa_don",
                column: "YeuCauThanhToanHoaDonId");

            migrationBuilder.CreateIndex(
                name: "IX_minh_chung_thanh_toan_hoa_don_YeuCauThanhToanHoaDonId",
                table: "minh_chung_thanh_toan_hoa_don",
                column: "YeuCauThanhToanHoaDonId");

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_thanh_toan_hoa_don_HoaDonId",
                table: "yeu_cau_thanh_toan_hoa_don",
                column: "HoaDonId");

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_thanh_toan_hoa_don_MaYeuCau",
                table: "yeu_cau_thanh_toan_hoa_don",
                column: "MaYeuCau",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_lich_su_thanh_toan_minh_chung_thanh_toan_hoa_don_MinhChungT~",
                table: "lich_su_thanh_toan",
                column: "MinhChungThanhToanHoaDonId",
                principalTable: "minh_chung_thanh_toan_hoa_don",
                principalColumn: "MinhChungThanhToanHoaDonId",
                onDelete: ReferentialAction.SetNull);
            migrationBuilder.Sql("UPDATE hoa_don SET \"TrangThaiPhatHanh\" = 2 WHERE \"TrangThaiPhatHanh\" = 0;");
            migrationBuilder.Sql("UPDATE dich_vu_dien_nuoc_cua_phong SET \"TrangThaiGhiNhan\" = 2 WHERE \"TrangThaiGhiNhan\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lich_su_thanh_toan_minh_chung_thanh_toan_hoa_don_MinhChungT~",
                table: "lich_su_thanh_toan");

            migrationBuilder.DropTable(
                name: "anh_chi_so_dong_ho");

            migrationBuilder.DropTable(
                name: "lich_su_trang_thai_hoa_don");

            migrationBuilder.DropTable(
                name: "lich_su_trang_thai_yeu_cau_thanh_toan_hoa_don");

            migrationBuilder.DropTable(
                name: "minh_chung_thanh_toan_hoa_don");

            migrationBuilder.DropTable(
                name: "yeu_cau_thanh_toan_hoa_don");

            migrationBuilder.DropIndex(
                name: "IX_lich_su_thanh_toan_MaGiaoDich",
                table: "lich_su_thanh_toan");

            migrationBuilder.DropIndex(
                name: "IX_lich_su_thanh_toan_MinhChungThanhToanHoaDonId",
                table: "lich_su_thanh_toan");

            migrationBuilder.DropIndex(
                name: "IX_dich_vu_dien_nuoc_cua_phong_PhongTroId_Thang_Nam",
                table: "dich_vu_dien_nuoc_cua_phong");

            migrationBuilder.DropColumn(
                name: "MinhChungThanhToanHoaDonId",
                table: "lich_su_thanh_toan");

            migrationBuilder.DropColumn(
                name: "NgayXacNhan",
                table: "lich_su_thanh_toan");

            migrationBuilder.DropColumn(
                name: "ChoPhepThanhToanMotPhan",
                table: "hoa_don");

            migrationBuilder.DropColumn(
                name: "HanThanhToan",
                table: "hoa_don");

            migrationBuilder.DropColumn(
                name: "NgayChot",
                table: "hoa_don");

            migrationBuilder.DropColumn(
                name: "NgayGui",
                table: "hoa_don");

            migrationBuilder.DropColumn(
                name: "NguoiChotId",
                table: "hoa_don");

            migrationBuilder.DropColumn(
                name: "SoTienThanhToanToiThieu",
                table: "hoa_don");

            migrationBuilder.DropColumn(
                name: "TrangThaiPhatHanh",
                table: "hoa_don");

            migrationBuilder.DropColumn(
                name: "GhiChuDuyet",
                table: "dich_vu_dien_nuoc_cua_phong");

            migrationBuilder.DropColumn(
                name: "NgayDuyet",
                table: "dich_vu_dien_nuoc_cua_phong");

            migrationBuilder.DropColumn(
                name: "NguoiDuyetId",
                table: "dich_vu_dien_nuoc_cua_phong");

            migrationBuilder.DropColumn(
                name: "NguoiTaoId",
                table: "dich_vu_dien_nuoc_cua_phong");

            migrationBuilder.DropColumn(
                name: "TrangThaiGhiNhan",
                table: "dich_vu_dien_nuoc_cua_phong");

            migrationBuilder.AlterColumn<double>(
                name: "ChiPhiSuaChua",
                table: "yeu_cau_su_co",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "GiaThue",
                table: "phong_tro",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "SoTienThanhToan",
                table: "lich_su_thanh_toan",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "TienThuePhong",
                table: "hop_dong",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "TienCocPhong",
                table: "hop_dong",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "TongTien",
                table: "hoa_don",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "DonGiaNuoc",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "DonGiaDien",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "ChiSoNuocMoi",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<double>(
                name: "ChiSoNuocCu",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<double>(
                name: "ChiSoDienMoi",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<double>(
                name: "ChiSoDienCu",
                table: "dich_vu_dien_nuoc_cua_phong",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<double>(
                name: "GiaDichVu",
                table: "dich_vu_chi_nhanh",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "TongTien",
                table: "chi_tiet_hoa_don",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "SoLuong",
                table: "chi_tiet_hoa_don",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<double>(
                name: "DonGia",
                table: "chi_tiet_hoa_don",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "IX_dich_vu_dien_nuoc_cua_phong_PhongTroId",
                table: "dich_vu_dien_nuoc_cua_phong",
                column: "PhongTroId");
        }
    }
}
