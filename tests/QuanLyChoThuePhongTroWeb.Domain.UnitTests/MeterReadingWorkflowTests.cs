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

        [Fact]
        public void AnhChiSoDongHo_MoiTaiLenOrDangXuLy_CannotBeConfirmed_ThrowsInvalidOperationException()
        {
            var anhMoi = CreateImage();
            Assert.Throws<InvalidOperationException>(() => anhMoi.XacNhan(100m, 1));
            Assert.Throws<InvalidOperationException>(() => anhMoi.XacNhanThuCong(100m, 1, "Ly do"));

            anhMoi.BatDauXuLy();
            Assert.Throws<InvalidOperationException>(() => anhMoi.XacNhan(100m, 1));
            Assert.Throws<InvalidOperationException>(() => anhMoi.XacNhanThuCong(100m, 1, "Ly do"));
        }

        [Fact]
        public void AnhChiSoDongHo_XacNhanThuCong_RequiresReason_AndAllowsKhongDocDuocOrLoi()
        {
            var anhKhongDocDuoc = CreateImage();
            anhKhongDocDuoc.GhiNhanKetQuaAI(null, null, "Mờ số");
            Assert.Throws<ArgumentException>(() => anhKhongDocDuoc.XacNhanThuCong(150m, 1, ""));
            Assert.Throws<ArgumentException>(() => anhKhongDocDuoc.XacNhanThuCong(150m, 1, "   "));

            anhKhongDocDuoc.XacNhanThuCong(150m, 1, "Đọc trực tiếp tại đồng hồ");
            Assert.Equal(150m, anhKhongDocDuoc.GiaTriXacNhan);
            Assert.Equal("Đọc trực tiếp tại đồng hồ", anhKhongDocDuoc.GhiChuXacNhan);
            Assert.True(anhKhongDocDuoc.DuocChonLamChiSoChinhThuc);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, anhKhongDocDuoc.TrangThaiXuLy);

            var anhLoi = CreateImage();
            anhLoi.GhiNhanLoiXuLy("Lỗi OCR");
            anhLoi.XacNhanThuCong(160m, 1, "Nhập tay");
            Assert.Equal(160m, anhLoi.GiaTriXacNhan);
            Assert.True(anhLoi.DuocChonLamChiSoChinhThuc);
        }

        [Fact]
        public void AnhChiSoDongHo_HuyChonChinhThuc_ClearsOfficialFlag_AndSetsDaThayThe()
        {
            var anh = CreateImage();
            anh.GhiNhanKetQuaAI(150m, 0.9);
            anh.XacNhan(150m, 1);
            Assert.True(anh.DuocChonLamChiSoChinhThuc);

            anh.HuyChonChinhThuc();
            Assert.False(anh.DuocChonLamChiSoChinhThuc);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaThayThe, anh.TrangThaiXuLy);
        }

        [Fact]
        public void DichVuDienNuocCuaPhong_DuyetLai_AllowedOnDaDuyet_UpdatesApproverAndNotes()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = 1,
                Thang = 9,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };

            period.GuiDuyet();
            period.Duyet(1, "Duyệt lần 1");
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
            Assert.Equal("Duyệt lần 1", period.GhiChuDuyet);

            // Duyệt lại khi đã duyệt
            period.DuyetLai(2, "Duyệt lần 2 sau khi sửa");
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
            Assert.Equal(2, period.NguoiDuyetId);
            Assert.Equal("Duyệt lần 2 sau khi sửa", period.GhiChuDuyet);

            // Duyệt() gọi lại trên DaDuyet phải ném exception
            Assert.Throws<InvalidOperationException>(() => period.Duyet(3));
        }

        [Fact]
        public void BatDauXuLy_SetsNgayXuLy_AndSetsDangXuLy()
        {
            var anh = CreateImage();
            var before = DateTime.UtcNow;
            anh.BatDauXuLy();
            var after = DateTime.UtcNow;

            Assert.Equal(TrangThaiXuLyAnhChiSo.DangXuLy, anh.TrangThaiXuLy);
            Assert.NotNull(anh.NgayXuLy);
            Assert.True(anh.NgayXuLy >= before && anh.NgayXuLy <= after);
        }

        [Fact]
        public void ThuLaiXuLy_Extended_StaleRules_WorkAsExpected()
        {
            var now = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);
            var timeout = TimeSpan.FromMinutes(10);

            // 1. DangXuLy chưa quá hạn => Từ chối
            var anhChuaQuaHan = CreateImage();
            anhChuaQuaHan.BatDauXuLy();
            anhChuaQuaHan.NgayXuLy = now.AddMinutes(-5);
            Assert.Throws<InvalidOperationException>(() => anhChuaQuaHan.ThuLaiXuLy(now, timeout));

            // 2. DangXuLy đã quá hạn => Cho phép
            var anhQuaHan = CreateImage();
            anhQuaHan.BatDauXuLy();
            anhQuaHan.NgayXuLy = now.AddMinutes(-15);
            anhQuaHan.ThuLaiXuLy(now, timeout);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DangXuLy, anhQuaHan.TrangThaiXuLy);
            Assert.Equal(now, anhQuaHan.NgayXuLy);

            // 3. KhongDocDuoc hoặc Loi => Cho phép ngay
            var anhLoi = CreateImage();
            anhLoi.GhiNhanLoiXuLy("Lỗi OCR");
            anhLoi.ThuLaiXuLy(now, timeout);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DangXuLy, anhLoi.TrangThaiXuLy);
            Assert.Null(anhLoi.ThongBaoLoi);

            // 4. DaXacNhan, DuocChonLamChiSoChinhThuc, DaThayThe, IsDeleted => Từ chối
            var anhDaXacNhan = CreateImage();
            anhDaXacNhan.GhiNhanKetQuaAI(100m, 0.9);
            anhDaXacNhan.XacNhan(100m, 1);
            Assert.Throws<InvalidOperationException>(() => anhDaXacNhan.ThuLaiXuLy(now, timeout));

            var anhDaThayThe = CreateImage();
            anhDaThayThe.GhiNhanKetQuaAI(100m, 0.9);
            anhDaThayThe.XacNhan(100m, 1);
            anhDaThayThe.HuyChonChinhThuc();
            Assert.Throws<InvalidOperationException>(() => anhDaThayThe.ThuLaiXuLy(now, timeout));
        }

        [Fact]
        public void GhiNhanLoiXuLy_And_GhiNhanKetQuaAI_TruncatesThongBaoLoiTo1000Chars()
        {
            var longMsg = new string('A', 5000);

            var anhLoi = CreateImage();
            anhLoi.GhiNhanLoiXuLy(longMsg);
            Assert.NotNull(anhLoi.ThongBaoLoi);
            Assert.Equal(1000, anhLoi.ThongBaoLoi.Length);

            var anhKetQua = CreateImage();
            anhKetQua.GhiNhanKetQuaAI(null, null, longMsg);
            Assert.NotNull(anhKetQua.ThongBaoLoi);
            Assert.Equal(1000, anhKetQua.ThongBaoLoi.Length);
        }

        [Fact]
        public void AnhChiSoDongHo_NgayXuLy_IsTruncatedToMicroseconds_AndAlwaysIncreasesEvenWithSameTimestamp()
        {
            var nowWithNanoseconds = new DateTime(2026, 9, 27, 10, 30, 0, DateTimeKind.Utc).AddTicks(12345); // 12345 ticks = 1234.5 us
            var anh = CreateImage();
            anh.TrangThaiXuLy = TrangThaiXuLyAnhChiSo.Loi;

            // Lần 1
            anh.ThuLaiXuLy(nowWithNanoseconds, TimeSpan.FromMinutes(10));
            var firstStamp = anh.NgayXuLy!.Value;
            Assert.Equal(0, firstStamp.Ticks % 10); // Phải làm tròn về micro giây (chia hết cho 10 ticks)

            // Giả lập trạng thái lỗi để thử lại lần 2 với cùng timestamp
            anh.TrangThaiXuLy = TrangThaiXuLyAnhChiSo.Loi;
            anh.ThuLaiXuLy(nowWithNanoseconds, TimeSpan.FromMinutes(10));
            var secondStamp = anh.NgayXuLy!.Value;
            Assert.Equal(0, secondStamp.Ticks % 10);
            Assert.True(secondStamp > firstStamp); // Dấu mới phải luôn lớn hơn hẳn dấu cũ

            // Giả lập trạng thái lỗi để thử lại lần 3 vẫn với cùng timestamp
            anh.TrangThaiXuLy = TrangThaiXuLyAnhChiSo.Loi;
            anh.ThuLaiXuLy(nowWithNanoseconds, TimeSpan.FromMinutes(10));
            var thirdStamp = anh.NgayXuLy!.Value;
            Assert.Equal(0, thirdStamp.Ticks % 10);
            Assert.True(thirdStamp > secondStamp); // Tiếp tục tăng dần
        }

        [Fact]
        public void SuaGiaTriXacNhan_Succeeds_AndSetsAllProperties()
        {
            var anh = CreateImage();
            anh.TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc;
            anh.XacNhan(100m, nguoiXacNhanId: 1, ghiChu: "Số gốc");

            anh.SuaGiaTriXacNhan(120m, nguoiSuaId: 2, lyDo: "Khách kiểm tra lại đồng hồ");

            Assert.Equal(120m, anh.GiaTriXacNhan);
            Assert.Equal(2, anh.NguoiXacNhanId);
            Assert.NotNull(anh.NgayXacNhan);
            Assert.Equal("[Sửa 100 → 120] Khách kiểm tra lại đồng hồ", anh.GhiChuXacNhan);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, anh.TrangThaiXuLy);
            Assert.True(anh.DuocChonLamChiSoChinhThuc);
        }

        [Fact]
        public void SuaGiaTriXacNhan_Throws_WhenStateIsNotDaXacNhan_OrNotOfficial_OrDeleted()
        {
            var anh1 = CreateImage();
            anh1.TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc; // Chưa xác nhận
            Assert.Throws<InvalidOperationException>(() => anh1.SuaGiaTriXacNhan(120m, 2, "Lý do"));

            var anh2 = CreateImage();
            anh2.TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc;
            anh2.XacNhan(100m, 1);
            anh2.DuocChonLamChiSoChinhThuc = false; // Không chính thức
            Assert.Throws<InvalidOperationException>(() => anh2.SuaGiaTriXacNhan(120m, 2, "Lý do"));

            var anh3 = CreateImage();
            anh3.TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc;
            anh3.XacNhan(100m, 1);
            anh3.IsDeleted = true; // Đã xóa
            Assert.Throws<InvalidOperationException>(() => anh3.SuaGiaTriXacNhan(120m, 2, "Lý do"));
        }

        [Fact]
        public void SuaGiaTriXacNhan_Throws_WhenReasonEmpty_NegativeValue_SameValue_OrInvalidActor()
        {
            var anh = CreateImage();
            anh.TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc;
            anh.XacNhan(100m, 1);

            Assert.Throws<ArgumentException>(() => anh.SuaGiaTriXacNhan(120m, 2, "   "));
            Assert.Throws<ArgumentOutOfRangeException>(() => anh.SuaGiaTriXacNhan(-1m, 2, "Lý do"));
            Assert.Throws<ArgumentOutOfRangeException>(() => anh.SuaGiaTriXacNhan(120m, 0, "Lý do"));
            Assert.Throws<InvalidOperationException>(() => anh.SuaGiaTriXacNhan(100m, 2, "Lý do trùng số"));
        }

        [Fact]
        public void SuaGiaTriXacNhan_TruncatesNoteTo1000Chars()
        {
            var anh = CreateImage();
            anh.TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc;
            anh.XacNhan(100m, 1);

            var longReason = new string('X', 2000);
            anh.SuaGiaTriXacNhan(150m, 2, longReason);

            Assert.NotNull(anh.GhiChuXacNhan);
            Assert.Equal(1000, anh.GhiChuXacNhan.Length);
            Assert.StartsWith("[Sửa 100 → 150] ", anh.GhiChuXacNhan);
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
