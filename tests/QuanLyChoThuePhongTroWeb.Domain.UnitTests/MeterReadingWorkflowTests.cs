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
        public void DichVuDienNuoc_RefusesNegativeReadings_And_AllowsAdjustmentWhenDaDuyet()
        {
            var record = new DichVuDienNuocCuaPhong
            {
                ChiSoDienCu = 0m,
                ChiSoNuocCu = 0m
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => record.CapNhatChiSo(chiSoDienMoi: -1m, chiSoNuocMoi: 10m));
            Assert.Throws<ArgumentOutOfRangeException>(() => record.CapNhatChiSo(chiSoDienMoi: 10m, chiSoNuocMoi: -1m));

            record.CapNhatChiSo(chiSoDienMoi: 50m, chiSoNuocMoi: 20m);
            record.GuiDuyet();
            record.Duyet(nguoiDuyetId: 10);

            // Domain entity permits updating reading for DaDuyet (lock check is in Application service)
            record.CapNhatChiSo(chiSoDienMoi: 60m, chiSoNuocMoi: 25m);
            Assert.Equal(60m, record.ChiSoDienMoi);
            Assert.Equal(25m, record.ChiSoNuocMoi);
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

        [Fact]
        public void AnhChiSoDongHo_GiaTriAIGoiY_DoesNotChangeOfficialReading_AndNewImageIsNotOfficial()
        {
            var anh = CreateImage();

            anh.GhiNhanKetQuaAI(giaTriGoiY: 150.5m, doTinCay: 0.95);

            Assert.Equal(150.5m, anh.GiaTriAIGoiY);
            Assert.Null(anh.GiaTriXacNhan);
            Assert.Null(anh.NguoiXacNhanId);
            Assert.Null(anh.NgayXacNhan);
            Assert.False(anh.DuocChonLamChiSoChinhThuc);
        }

        [Fact]
        public void AnhChiSoDongHo_ZeroValue_IsValidForAIAndConfirmation()
        {
            var anh = CreateImage();

            anh.GhiNhanKetQuaAI(giaTriGoiY: 0m, doTinCay: 0.90);
            Assert.Equal(0m, anh.GiaTriAIGoiY);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DocDuoc, anh.TrangThaiXuLy);

            anh.XacNhan(giaTriXacNhan: 0m, nguoiXacNhanId: 10, ghiChu: "Công tơ ban đầu số 0");
            Assert.Equal(0m, anh.GiaTriXacNhan);
            Assert.True(anh.DuocChonLamChiSoChinhThuc);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, anh.TrangThaiXuLy);
        }

        [Fact]
        public void AnhChiSoDongHo_NegativeValues_And_OutOfRangeConfidence_AreRejected()
        {
            var anh = CreateImage();

            Assert.Throws<ArgumentOutOfRangeException>(() => anh.GhiNhanKetQuaAI(giaTriGoiY: -0.001m, doTinCay: 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => anh.GhiNhanKetQuaAI(giaTriGoiY: 100m, doTinCay: -0.01));
            Assert.Throws<ArgumentOutOfRangeException>(() => anh.GhiNhanKetQuaAI(giaTriGoiY: 100m, doTinCay: 1.01));

            var validAnh = CreateImage();
            validAnh.GhiNhanKetQuaAI(giaTriGoiY: 100m, doTinCay: 0.8);
            Assert.Throws<ArgumentOutOfRangeException>(() => validAnh.XacNhan(giaTriXacNhan: -1m, nguoiXacNhanId: 10));
        }

        [Fact]
        public void AnhChiSoDongHo_Retry_FromKhongDocDuocOrLoi_ReturnsToDangXuLy_AndCanRecordNewAIResult()
        {
            var anh = CreateImage();
            anh.GhiNhanKetQuaAI(giaTriGoiY: null, doTinCay: null, thongBaoLoi: "Không nhận diện được số");
            Assert.Equal(TrangThaiXuLyAnhChiSo.KhongDocDuoc, anh.TrangThaiXuLy);

            anh.ThuLaiXuLy();
            Assert.Equal(TrangThaiXuLyAnhChiSo.DangXuLy, anh.TrangThaiXuLy);
            Assert.Null(anh.ThongBaoLoi);

            anh.GhiNhanKetQuaAI(giaTriGoiY: 175.2m, doTinCay: 0.92);
            Assert.Equal(175.2m, anh.GiaTriAIGoiY);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DocDuoc, anh.TrangThaiXuLy);

            var anhLoi = CreateImage();
            anhLoi.GhiNhanLoiXuLy("Timeout kết nối AI");
            Assert.Equal(TrangThaiXuLyAnhChiSo.Loi, anhLoi.TrangThaiXuLy);

            anhLoi.ThuLaiXuLy();
            Assert.Equal(TrangThaiXuLyAnhChiSo.DangXuLy, anhLoi.TrangThaiXuLy);
        }

        [Fact]
        public void AnhChiSoDongHo_Retry_FromInvalidStates_ThrowsInvalidOperationException()
        {
            var anhMoi = CreateImage();
            Assert.Throws<InvalidOperationException>(() => anhMoi.ThuLaiXuLy());

            var anhDaXacNhan = CreateImage();
            anhDaXacNhan.GhiNhanKetQuaAI(giaTriGoiY: 120m, doTinCay: 0.9);
            anhDaXacNhan.XacNhan(120m, 10);
            Assert.Throws<InvalidOperationException>(() => anhDaXacNhan.ThuLaiXuLy());
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
