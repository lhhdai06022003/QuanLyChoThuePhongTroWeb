using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class AutoInvoiceReadingPolicyTests
    {
        private static readonly AutoInvoiceOptions Options = new();
        private static readonly DateTime T0 = new(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc);

        internal static AutoInvoiceMeterImage Img(
            int id,
            LoaiDongHo loai,
            TrangThaiXuLyAnhChiSo status = TrangThaiXuLyAnhChiSo.DocDuoc,
            decimal? ai = null,
            double? tinCay = 0.95,
            decimal? xacNhan = null,
            bool official = false,
            DateTime? ngayGui = null)
        {
            return new AutoInvoiceMeterImage(id, loai, status, ai, tinCay, xacNhan, official, ngayGui ?? T0);
        }

        internal static AutoInvoiceRoomContext Ctx(
            IReadOnlyList<AutoInvoiceMeterImage>? images = null,
            TrangThaiGhiNhan? status = TrangThaiGhiNhan.Nhap,
            IReadOnlyList<AutoInvoiceConsumption>? truoc = null,
            IReadOnlyList<AutoInvoiceContractRef>? contracts = null,
            bool coHoaDon = false,
            bool kyTruocChuaChot = false,
            int? khachId = 5,
            string? email = null,
            int? periodId = 1,
            decimal dienCu = 100,
            decimal dienMoi = 0,
            decimal nuocCu = 50,
            decimal nuocMoi = 0,
            decimal donGiaDien = 3500m,
            decimal donGiaNuoc = 15000m,
            string tenChiNhanh = "Chi nhánh A")
        {
            return new AutoInvoiceRoomContext
            {
                PhongTroId = 101,
                SoPhong = "101",
                ChiNhanhId = 1,
                TenChiNhanh = tenChiNhanh,
                SoDienThoaiChiNhanh = "0900000000",
                Thang = 10,
                Nam = 2026,
                DichVuDienNuocCuaPhongId = periodId,
                TrangThaiGhiNhan = status,
                ChiSoDienCu = dienCu,
                ChiSoDienMoi = dienMoi,
                ChiSoNuocCu = nuocCu,
                ChiSoNuocMoi = nuocMoi,
                DonGiaDien = donGiaDien,
                DonGiaNuoc = donGiaNuoc,
                Images = images ?? Array.Empty<AutoInvoiceMeterImage>(),
                TieuThuCacKyTruoc = truoc ?? Array.Empty<AutoInvoiceConsumption>(),
                HopDongHieuLuc = contracts ?? new[] { new AutoInvoiceContractRef(10, 7) },
                CoHoaDonHoatDong = coHoaDon,
                KyTruocChuaChot = kyTruocChuaChot,
                NguoiDungKhachId = khachId,
                EmailKhach = email
            };
        }

        private static AutoInvoiceRoomContext GoodCtx(Action<List<AutoInvoiceMeterImage>>? tweak = null, int? khachId = 5, string? email = null)
        {
            var images = new List<AutoInvoiceMeterImage>
            {
                Img(1, LoaiDongHo.Dien, ai: 120m),
                Img(2, LoaiDongHo.Nuoc, ai: 60m)
            };
            tweak?.Invoke(images);
            return Ctx(images, khachId: khachId, email: email);
        }

        // ---------- EvaluateMeter ----------

        [Theory]
        [InlineData(0.89, AutoInvoiceReason.DoTinCayThap)]
        [InlineData(0.90, null)]
        [InlineData(0.95, null)]
        public void Meter_ConfidenceThreshold(double tinCay, AutoInvoiceReason? expected)
        {
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 120m, tinCay: tinCay) });

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options);

            Assert.Equal(expected, d.LyDo);
            if (expected == null)
            {
                Assert.Equal(1, d.AnhTuXacNhanId);
                Assert.Equal(120m, d.ChiSoMoi);
            }
        }

        [Fact]
        public void Meter_NullConfidence_IsLowConfidence()
        {
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 120m, tinCay: null) });

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options);

            Assert.Equal(AutoInvoiceReason.DoTinCayThap, d.LyDo);
        }

        [Theory]
        [InlineData(TrangThaiXuLyAnhChiSo.KhongDocDuoc)]
        [InlineData(TrangThaiXuLyAnhChiSo.Loi)]
        [InlineData(TrangThaiXuLyAnhChiSo.CanChupLai)]
        public void Meter_UnreadableStatuses_AreKhongDocDuoc(TrangThaiXuLyAnhChiSo status)
        {
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, status, ai: 120m) });

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options);

            Assert.Equal(AutoInvoiceReason.KhongDocDuoc, d.LyDo);
        }

        [Fact]
        public void Meter_DocDuocWithoutAiValue_IsKhongDocDuoc()
        {
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: null) });

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options);

            Assert.Equal(AutoInvoiceReason.KhongDocDuoc, d.LyDo);
        }

        [Theory]
        [InlineData(TrangThaiXuLyAnhChiSo.MoiTaiLen)]
        [InlineData(TrangThaiXuLyAnhChiSo.DangXuLy)]
        public void Meter_StillProcessing_IsThieuChiSo(TrangThaiXuLyAnhChiSo status)
        {
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, status) });

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options);

            Assert.Equal(AutoInvoiceReason.ThieuChiSo, d.LyDo);
        }

        [Fact]
        public void Meter_NoImage_IsThieuChiSo()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, Ctx(), Options);

            Assert.Equal(AutoInvoiceReason.ThieuChiSo, d.LyDo);
        }

        [Fact]
        public void Meter_OtherMeterImagesAreIgnored()
        {
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Nuoc, ai: 60m) });

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options);

            Assert.Equal(AutoInvoiceReason.ThieuChiSo, d.LyDo);
        }

        [Fact]
        public void Meter_OnlyNewestImageIsConsidered_OlderGoodImageIgnored()
        {
            var images = new[]
            {
                Img(2, LoaiDongHo.Dien, ai: 118m, tinCay: 0.5, ngayGui: T0.AddHours(1)),
                Img(1, LoaiDongHo.Dien, ai: 120m, tinCay: 0.99, ngayGui: T0)
            };

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, Ctx(images), Options);

            Assert.Equal(AutoInvoiceReason.DoTinCayThap, d.LyDo);
        }

        [Fact]
        public void Meter_SameTimestamp_HigherIdWins()
        {
            var images = new[]
            {
                Img(1, LoaiDongHo.Dien, ai: 120m, tinCay: 0.99),
                Img(2, LoaiDongHo.Dien, ai: 121m, tinCay: 0.5)
            };

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, Ctx(images), Options);

            Assert.Equal(AutoInvoiceReason.DoTinCayThap, d.LyDo);
        }

        [Fact]
        public void Meter_ReplacedImagesAreSkipped()
        {
            var images = new[]
            {
                Img(2, LoaiDongHo.Dien, TrangThaiXuLyAnhChiSo.DaThayThe, ai: 118m, tinCay: 0.5, ngayGui: T0.AddHours(1)),
                Img(1, LoaiDongHo.Dien, ai: 120m, tinCay: 0.99, ngayGui: T0)
            };

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, Ctx(images), Options);

            Assert.Null(d.LyDo);
            Assert.Equal(1, d.AnhTuXacNhanId);
        }

        [Fact]
        public void Meter_NewValueBelowOldValue_IsChiSoGiam()
        {
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 95m) });

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options);

            Assert.Equal(AutoInvoiceReason.ChiSoGiam, d.LyDo);
            Assert.Equal(95m, d.ChiSoMoi);
            Assert.Equal(100m, d.ChiSoCu);
            Assert.Contains("nhỏ hơn", d.MoTa);
        }

        [Fact]
        public void Meter_EqualToOldValue_IsAccepted()
        {
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 100m) });

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options);

            Assert.Null(d.LyDo);
        }

        [Fact]
        public void Meter_ExactlyThreeTimesAverage_IsAccepted()
        {
            var truoc = new[] { new AutoInvoiceConsumption(10, 5), new AutoInvoiceConsumption(10, 5), new AutoInvoiceConsumption(10, 5) };
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 130m) }, truoc: truoc);

            Assert.Null(AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options).LyDo);
        }

        [Fact]
        public void Meter_SmallestUnitAboveThreeTimesAverage_IsRejected()
        {
            var truoc = new[] { new AutoInvoiceConsumption(10, 5), new AutoInvoiceConsumption(10, 5), new AutoInvoiceConsumption(10, 5) };
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 130.001m) }, truoc: truoc);

            Assert.Equal(AutoInvoiceReason.TieuThuBatThuong, AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options).LyDo);
        }

        [Fact]
        public void Meter_NoPreviousPeriod_ElectricityCapIs500()
        {
            var ok = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 600m) });
            var bad = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 601m) });

            Assert.Null(AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ok, Options).LyDo);
            Assert.Equal(AutoInvoiceReason.TieuThuBatThuong, AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, bad, Options).LyDo);
        }

        [Fact]
        public void Meter_NoPreviousPeriod_WaterCapIs30()
        {
            var ok = Ctx(new[] { Img(1, LoaiDongHo.Nuoc, ai: 80m) });
            var bad = Ctx(new[] { Img(1, LoaiDongHo.Nuoc, ai: 81m) });

            Assert.Null(AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Nuoc, ok, Options).LyDo);
            Assert.Equal(AutoInvoiceReason.TieuThuBatThuong, AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Nuoc, bad, Options).LyDo);
        }

        [Fact]
        public void Meter_ZeroAverage_UsesCapInsteadOfZeroLimit()
        {
            var truoc = new[] { new AutoInvoiceConsumption(0, 0), new AutoInvoiceConsumption(0, 0) };
            var ok = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 150m) }, truoc: truoc);
            var bad = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 601m) }, truoc: truoc);

            Assert.Null(AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ok, Options).LyDo);
            Assert.Equal(AutoInvoiceReason.TieuThuBatThuong, AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, bad, Options).LyDo);
        }

        [Fact]
        public void Meter_UsesAtMostConfiguredNumberOfPreviousPeriods()
        {
            // Kỳ thứ 4 (1000) bị bỏ qua: trung bình 3 kỳ đầu = 10, giới hạn 30.
            var truoc = new[]
            {
                new AutoInvoiceConsumption(10, 0), new AutoInvoiceConsumption(10, 0),
                new AutoInvoiceConsumption(10, 0), new AutoInvoiceConsumption(1000, 0)
            };
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 131m) }, truoc: truoc);

            Assert.Equal(AutoInvoiceReason.TieuThuBatThuong, AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options).LyDo);
        }

        [Fact]
        public void Meter_ApprovedPeriodValue_StillChecksAbnormalConsumption_AndNeverAutoConfirms()
        {
            var truoc = new[] { new AutoInvoiceConsumption(10, 0) };
            var bad = Ctx(status: TrangThaiGhiNhan.DaDuyet, truoc: truoc, dienMoi: 140);
            var ok = Ctx(status: TrangThaiGhiNhan.DaDuyet, truoc: truoc, dienMoi: 130);

            var dBad = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, bad, Options);
            var dOk = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ok, Options);

            Assert.Equal(AutoInvoiceReason.TieuThuBatThuong, dBad.LyDo);
            Assert.Null(dOk.LyDo);
            Assert.Null(dOk.AnhTuXacNhanId);
        }

        [Fact]
        public void Meter_OfficialImage_UsesConfirmedValue_WithoutAutoConfirm_ButChecksConsumption()
        {
            var truoc = new[] { new AutoInvoiceConsumption(10, 0) };
            var imgOk = Img(1, LoaiDongHo.Dien, TrangThaiXuLyAnhChiSo.DaXacNhan, ai: 999m, tinCay: 0.1, xacNhan: 130m, official: true);
            var imgBad = Img(1, LoaiDongHo.Dien, TrangThaiXuLyAnhChiSo.DaXacNhan, ai: 999m, tinCay: 0.1, xacNhan: 131m, official: true);

            var ok = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, Ctx(new[] { imgOk }, truoc: truoc), Options);
            var bad = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, Ctx(new[] { imgBad }, truoc: truoc), Options);

            Assert.Null(ok.LyDo);
            Assert.Null(ok.AnhTuXacNhanId);
            Assert.Equal(130m, ok.ChiSoMoi);
            Assert.Equal(AutoInvoiceReason.TieuThuBatThuong, bad.LyDo);
        }

        [Fact]
        public void Meter_OfficialImage_IgnoresNewerUnconfirmedImage()
        {
            var images = new[]
            {
                Img(2, LoaiDongHo.Dien, ai: 119m, tinCay: 0.2, ngayGui: T0.AddHours(1)),
                Img(1, LoaiDongHo.Dien, TrangThaiXuLyAnhChiSo.DaXacNhan, xacNhan: 120m, official: true, ngayGui: T0)
            };

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, Ctx(images), Options);

            Assert.Null(d.LyDo);
            Assert.Equal(120m, d.ChiSoMoi);
            Assert.Null(d.AnhTuXacNhanId);
        }

        [Fact]
        public void GhiChuTuXacNhan_FormatsWithVietnameseDecimalComma()
        {
            Assert.Equal("Tự xác nhận, độ tin cậy 0,93", AutoInvoiceReadingPolicy.GhiChuTuXacNhan(0.93));
        }

        [Fact]
        public void LowConfidenceDescription_UsesVietnameseNumbers()
        {
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 120m, tinCay: 0.85) });

            var d = AutoInvoiceReadingPolicy.EvaluateMeter(LoaiDongHo.Dien, ctx, Options);

            Assert.Equal("Điện: độ tin cậy nhận diện thấp (0,85 < 0,90)", d.MoTa);
        }

        [Theory]
        [InlineData(AutoInvoiceReason.ThieuChiSo, true)]
        [InlineData(AutoInvoiceReason.DaCoHoaDon, true)]
        [InlineData(AutoInvoiceReason.KhongCoHopDong, true)]
        [InlineData(AutoInvoiceReason.DoTinCayThap, false)]
        [InlineData(AutoInvoiceReason.LoiKhiTao, false)]
        public void IsSilent_MatchesSpec(AutoInvoiceReason reason, bool expected)
        {
            Assert.Equal(expected, AutoInvoiceReadingPolicy.IsSilent(reason));
        }

        [Theory]
        [InlineData(AutoInvoiceReason.DoTinCayThap, true)]
        [InlineData(AutoInvoiceReason.KhongDocDuoc, true)]
        [InlineData(AutoInvoiceReason.ChiSoGiam, true)]
        [InlineData(AutoInvoiceReason.TieuThuBatThuong, false)]
        [InlineData(AutoInvoiceReason.KyTruocChuaChot, false)]
        [InlineData(AutoInvoiceReason.KhachKhongCoKenhNhan, false)]
        [InlineData(AutoInvoiceReason.LoiKhiTao, false)]
        public void CanBaoKhach_MatchesDecision(AutoInvoiceReason reason, bool expected)
        {
            Assert.Equal(expected, AutoInvoiceReadingPolicy.CanBaoKhach(reason));
        }

        // ---------- EvaluateRoom ----------

        [Fact]
        public void Room_Eligible_WhenEverythingOk()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(GoodCtx(), Options);

            Assert.True(d.DuDieuKien);
            Assert.Empty(d.LyDos);
            Assert.Equal(10, d.HopDong!.HopDongId);
            Assert.Equal(1, d.Dien!.AnhTuXacNhanId);
            Assert.Equal(2, d.Nuoc!.AnhTuXacNhanId);
        }

        [Fact]
        public void Room_NoPeriod_IsThieuChiSo()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(Ctx(periodId: null, status: null), Options);

            Assert.False(d.DuDieuKien);
            Assert.Equal(new[] { AutoInvoiceReason.ThieuChiSo }, d.LyDos);
        }

        [Fact]
        public void Room_ActiveInvoice_IsDaCoHoaDon()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(Ctx(coHoaDon: true), Options);

            Assert.Equal(new[] { AutoInvoiceReason.DaCoHoaDon }, d.LyDos);
        }

        [Fact]
        public void Room_NoContract_IsKhongCoHopDong()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(Ctx(contracts: Array.Empty<AutoInvoiceContractRef>()), Options);

            Assert.Equal(new[] { AutoInvoiceReason.KhongCoHopDong }, d.LyDos);
        }

        [Fact]
        public void Room_TwoContracts_IsNhieuHopDongTrongKy()
        {
            var contracts = new[] { new AutoInvoiceContractRef(10, 7), new AutoInvoiceContractRef(11, 8) };

            var d = AutoInvoiceReadingPolicy.EvaluateRoom(Ctx(contracts: contracts), Options);

            Assert.Equal(new[] { AutoInvoiceReason.NhieuHopDongTrongKy }, d.LyDos);
            Assert.False(AutoInvoiceReadingPolicy.IsSilent(AutoInvoiceReason.NhieuHopDongTrongKy));
        }

        [Fact]
        public void Room_OneMeterMissing_IsThieuChiSo()
        {
            var ctx = GoodCtx(images => images.RemoveAll(i => i.LoaiDongHo == LoaiDongHo.Nuoc));

            var d = AutoInvoiceReadingPolicy.EvaluateRoom(ctx, Options);

            Assert.False(d.DuDieuKien);
            Assert.Equal(new[] { AutoInvoiceReason.ThieuChiSo }, d.LyDos);
        }

        [Fact]
        public void Room_ReportableReasonBeatsMissingMeter()
        {
            var ctx = GoodCtx(images =>
            {
                images.RemoveAll(i => i.LoaiDongHo == LoaiDongHo.Nuoc);
                images[0] = Img(1, LoaiDongHo.Dien, ai: 120m, tinCay: 0.85);
            });

            var d = AutoInvoiceReadingPolicy.EvaluateRoom(ctx, Options);

            Assert.Equal(new[] { AutoInvoiceReason.DoTinCayThap }, d.LyDos);
            Assert.Single(d.MoTaLyDos);
        }

        [Fact]
        public void Room_BothMetersHaveReasons_ListsElectricityFirst()
        {
            var ctx = GoodCtx(images =>
            {
                images[0] = Img(1, LoaiDongHo.Dien, ai: 90m);
                images[1] = Img(2, LoaiDongHo.Nuoc, TrangThaiXuLyAnhChiSo.KhongDocDuoc);
            });

            var d = AutoInvoiceReadingPolicy.EvaluateRoom(ctx, Options);

            Assert.Equal(new[] { AutoInvoiceReason.ChiSoGiam, AutoInvoiceReason.KhongDocDuoc }, d.LyDos);
            Assert.StartsWith("Điện", d.MoTaLyDos[0]);
            Assert.StartsWith("Nước", d.MoTaLyDos[1]);
        }

        [Fact]
        public void Room_PreviousPeriodNotClosed_IsKyTruocChuaChot()
        {
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 120m), Img(2, LoaiDongHo.Nuoc, ai: 60m) }, kyTruocChuaChot: true);

            var d = AutoInvoiceReadingPolicy.EvaluateRoom(ctx, Options);

            Assert.Equal(new[] { AutoInvoiceReason.KyTruocChuaChot }, d.LyDos);
            Assert.Equal("Kỳ 09/2026 trước chưa được chốt", d.MoTaLyDos.Single());
        }

        [Fact]
        public void Room_NoAccountNoEmail_IsKhachKhongCoKenhNhan()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(GoodCtx(khachId: null, email: null), Options);

            Assert.Equal(new[] { AutoInvoiceReason.KhachKhongCoKenhNhan }, d.LyDos);
        }

        [Fact]
        public void Room_OnlyEmail_IsEligible()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(GoodCtx(khachId: null, email: "a@b.vn"), Options);

            Assert.True(d.DuDieuKien);
        }

        [Fact]
        public void Room_OnlyAccount_IsEligible()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(GoodCtx(khachId: 5, email: null), Options);

            Assert.True(d.DuDieuKien);
        }


        // ---------- ThieuDonGia (L1) ----------

        private static AutoInvoiceRoomContext Approved(decimal dienMoi, decimal nuocMoi, decimal donGiaDien, decimal donGiaNuoc,
            bool kyTruocChuaChot = false, TrangThaiGhiNhan? status = TrangThaiGhiNhan.DaDuyet)
            => Ctx(status: status, dienMoi: dienMoi, nuocMoi: nuocMoi, donGiaDien: donGiaDien, donGiaNuoc: donGiaNuoc, kyTruocChuaChot: kyTruocChuaChot);

        [Fact]
        public void Room_ConsumptionWithoutElectricityRate_IsThieuDonGia()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(Approved(130, 50, donGiaDien: 0, donGiaNuoc: 0), Options);

            Assert.False(d.DuDieuKien);
            Assert.Equal(new[] { AutoInvoiceReason.ThieuDonGia }, d.LyDos);
            var moTa = d.MoTaLyDos.Single();
            Assert.Equal("Chi nhánh Chi nhánh A chưa có đơn giá điện cho kỳ 10/2026", moTa);
        }

        [Fact]
        public void Room_ConsumptionWithoutWaterRate_IsThieuDonGia()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(Approved(100, 60, donGiaDien: 0, donGiaNuoc: 0), Options);

            Assert.Equal(new[] { AutoInvoiceReason.ThieuDonGia }, d.LyDos);
            Assert.Equal("Chi nhánh Chi nhánh A chưa có đơn giá nước cho kỳ 10/2026", d.MoTaLyDos.Single());
        }

        [Fact]
        public void Room_ConsumptionWithoutBothRates_IsSingleReasonMentioningBoth()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(Approved(130, 60, donGiaDien: 0, donGiaNuoc: 0), Options);

            Assert.Equal(new[] { AutoInvoiceReason.ThieuDonGia }, d.LyDos);
            Assert.Equal("Chi nhánh Chi nhánh A chưa có đơn giá điện và nước cho kỳ 10/2026", d.MoTaLyDos.Single());
        }

        [Fact]
        public void Room_NoConsumptionAndNoRate_IsEligible()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(Approved(100, 50, donGiaDien: 0, donGiaNuoc: 0), Options);

            Assert.True(d.DuDieuKien);
        }

        [Fact]
        public void Room_ThieuChiSo_WinsOverThieuDonGia()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(Ctx(status: TrangThaiGhiNhan.Nhap, donGiaDien: 0, donGiaNuoc: 0), Options);

            Assert.Equal(AutoInvoiceReason.ThieuChiSo, Assert.Single(d.LyDos));
        }

        [Fact]
        public void Room_ThieuDonGia_WinsOverKyTruocChuaChot()
        {
            var d = AutoInvoiceReadingPolicy.EvaluateRoom(Approved(130, 60, donGiaDien: 0, donGiaNuoc: 0, kyTruocChuaChot: true), Options);

            Assert.Equal(new[] { AutoInvoiceReason.ThieuDonGia }, d.LyDos);
        }

        [Fact]
        public void ThieuDonGia_IsNotSilent_AndNeverNotifiesCustomer()
        {
            Assert.False(AutoInvoiceReadingPolicy.IsSilent(AutoInvoiceReason.ThieuDonGia));
            Assert.False(AutoInvoiceReadingPolicy.CanBaoKhach(AutoInvoiceReason.ThieuDonGia));
        }
    }
}
