using System;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Domain.UnitTests
{
    public class MeterReadingWorkflowTests
    {
        [Fact]
        public void DichVuDienNuoc_DefaultState_IsNhap()
        {
            var record = new DichVuDienNuocCuaPhong();
            Assert.Equal(TrangThaiGhiNhan.Nhap, record.TrangThaiGhiNhan);
        }

        [Fact]
        public void DichVuDienNuoc_Workflow_Nhap_To_ChoDuyet_To_DaDuyet_Succeeds()
        {
            var record = new DichVuDienNuocCuaPhong
            {
                PhongTroId = 1,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 70m
            };

            record.GuiDuyet();
            Assert.Equal(TrangThaiGhiNhan.ChoDuyet, record.TrangThaiGhiNhan);

            record.Duyet(nguoiDuyetId: 10, ghiChu: "Chỉ số hợp lệ");
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, record.TrangThaiGhiNhan);
            Assert.Equal(10, record.NguoiDuyetId);
            Assert.NotNull(record.NgayDuyet);
            Assert.Equal("Chỉ số hợp lệ", record.GhiChuDuyet);
        }

        [Fact]
        public void DichVuDienNuoc_TuChoi_RequiresReason_AndSetsStateToTuChoi()
        {
            var record = new DichVuDienNuocCuaPhong
            {
                TrangThaiGhiNhan = TrangThaiGhiNhan.ChoDuyet
            };

            Assert.Throws<ArgumentException>(() => record.TuChoi(nguoiDuyetId: 10, lyDo: ""));

            record.TuChoi(nguoiDuyetId: 10, lyDo: "Ảnh mờ không nhìn rõ công tơ");
            Assert.Equal(TrangThaiGhiNhan.TuChoi, record.TrangThaiGhiNhan);
            Assert.Equal(10, record.NguoiDuyetId);
            Assert.Equal("Ảnh mờ không nhìn rõ công tơ", record.GhiChuDuyet);
        }

        [Fact]
        public void DichVuDienNuoc_RefusesNewReading_BelowPreviousReading()
        {
            var record = new DichVuDienNuocCuaPhong
            {
                ChiSoDienCu = 100m,
                ChiSoNuocCu = 50m
            };

            var exDien = Assert.Throws<InvalidOperationException>(() =>
                record.CapNhatChiSo(chiSoDienMoi: 99m, chiSoNuocMoi: 60m));
            Assert.Contains("điện", exDien.Message, StringComparison.OrdinalIgnoreCase);

            var exNuoc = Assert.Throws<InvalidOperationException>(() =>
                record.CapNhatChiSo(chiSoDienMoi: 110m, chiSoNuocMoi: 49m));
            Assert.Contains("nước", exNuoc.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AnhChiSoDongHo_GhiNhanKetQuaAiDocDuoc_StoresOneResultOnImage()
        {
            var anh = CreateImage();

            anh.GhiNhanKetQuaAI(giaTriGoiY: 150.5m, doTinCay: 0.95);

            Assert.Equal(150.5m, anh.GiaTriAIGoiY);
            Assert.Equal(0.95, anh.DoTinCay);
            Assert.NotNull(anh.NgayXuLy);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DocDuoc, anh.TrangThaiXuLy);
        }

        [Fact]
        public void AnhChiSoDongHo_AiKhongDocDuoc_CanRequireNewImage()
        {
            var anh = CreateImage();

            anh.GhiNhanKetQuaAI(giaTriGoiY: null, doTinCay: null, thongBaoLoi: "Ảnh mờ");
            anh.DanhDauCanChupLai();

            Assert.Null(anh.GiaTriAIGoiY);
            Assert.Equal("Ảnh mờ", anh.ThongBaoLoi);
            Assert.Equal(TrangThaiXuLyAnhChiSo.CanChupLai, anh.TrangThaiXuLy);
        }

        [Fact]
        public void AnhChiSoDongHo_NhanVienCanXacNhanGiaTriKhacAiGoiY()
        {
            var anh = CreateImage();
            anh.GhiNhanKetQuaAI(giaTriGoiY: 150.5m, doTinCay: 0.80);

            anh.XacNhan(giaTriXacNhan: 151m, nguoiXacNhanId: 10, ghiChu: "Đọc trực tiếp trên ảnh");

            Assert.Equal(151m, anh.GiaTriXacNhan);
            Assert.Equal(10, anh.NguoiXacNhanId);
            Assert.NotNull(anh.NgayXacNhan);
            Assert.Equal("Đọc trực tiếp trên ảnh", anh.GhiChuXacNhan);
            Assert.True(anh.DuocChonLamChiSoChinhThuc);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, anh.TrangThaiXuLy);
        }

        [Fact]
        public void AnhChiSoDongHo_DaXacNhan_CannotBeConfirmedAgain()
        {
            var anh = CreateImage();
            anh.GhiNhanKetQuaAI(giaTriGoiY: 150.5m, doTinCay: 0.95);
            anh.XacNhan(giaTriXacNhan: 150.5m, nguoiXacNhanId: 10);

            Assert.Throws<InvalidOperationException>(() =>
                anh.XacNhan(giaTriXacNhan: 151m, nguoiXacNhanId: 11));
        }

        private static AnhChiSoDongHo CreateImage()
        {
            return new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "https://example.com/meter.jpg"
            };
        }
    }
}
