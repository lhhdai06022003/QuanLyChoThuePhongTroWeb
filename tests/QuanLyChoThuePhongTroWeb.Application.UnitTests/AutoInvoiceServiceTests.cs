using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;
using static QuanLyChoThuePhongTroWeb.Application.UnitTests.AutoInvoiceReadingPolicyTests;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class AutoInvoiceServiceTests
    {
        private const int Phong = 101;
        private const int Chi = 1;
        private const int Actor = 10;
        private const int Fallback = 20;

        private sealed class Harness
        {
            public List<string> Log { get; } = new();
            public FakeAutoInvoiceStore Store { get; } = new();
            public FakeMeterReadingWorkflowService Workflow { get; } = new();
            public FakeInvoiceIssuanceService Issuance { get; } = new();
            public FakeEmployeeAccessService Access { get; } = new();
            public FakeThongBaoService ThongBao { get; } = new();
            public CapturingLogger<AutoInvoiceService> Logger { get; } = new();
            public List<ConfirmMeterImageRequest> Confirms { get; } = new();
            public AutoInvoiceService Service { get; }

            public Harness(AutoInvoiceRoomContext? ctx, int staff = Actor, bool grantAll = true)
            {
                Store.Log = Log;
                Issuance.Log = Log;
                ThongBao.Log = Log;
                Store.Contexts.Add(ctx);
                Store.FallbackActorId = Fallback;
                Workflow.OnConfirmImage = (req, actor) =>
                {
                    Log.Add($"Confirm:{req.AnhChiSoDongHoId}");
                    Confirms.Add(req);
                    return ServiceResult<MeterImageWorkflowResult>.Ok(new MeterImageWorkflowResult());
                };
                Workflow.OnApproveBatch = (req, actor) =>
                {
                    Log.Add("Approve");
                    return ServiceResult<MeterPeriodsApprovalResult>.Ok(new MeterPeriodsApprovalResult());
                };
                if (grantAll)
                {
                    foreach (var who in new[] { staff, Fallback })
                    {
                        Access.Permissions.Add((who, Chi, EmployeeActionCodes.MeterReview));
                        Access.Permissions.Add((who, Chi, EmployeeActionCodes.InvoiceDraft));
                        Access.Permissions.Add((who, Chi, EmployeeActionCodes.InvoiceSubmit));
                    }
                }

                Service = new AutoInvoiceService(
                    Store, Workflow, Issuance, Access, ThongBao, new AutoInvoiceOptions(), Logger);
            }

            public Task<ServiceResult<AutoInvoiceRunResult>> Run(AutoInvoiceTrigger trigger = AutoInvoiceTrigger.NhanVienTaiAnh, int actor = Actor)
                => Service.RunForRoomAsync(actor, trigger, Phong, 10, 2026);
        }

        private static AutoInvoiceRoomContext Good(IReadOnlyList<AutoInvoiceMeterImage>? images = null)
        {
            return Ctx(images ?? new[]
            {
                Img(1, LoaiDongHo.Dien, ai: 120m, tinCay: 0.95),
                Img(2, LoaiDongHo.Nuoc, ai: 60m, tinCay: 0.97)
            });
        }

        private static AutoInvoiceRoomContext Approved()
        {
            return Ctx(status: TrangThaiGhiNhan.DaDuyet, dienMoi: 120, nuocMoi: 60);
        }

        private static int Index(List<string> log, string entry) => log.IndexOf(entry);

        // ---------- Đường chính ----------

        [Fact]
        public async Task ChiSoHopLe_TaoNhap_KhongBaoKhach()
        {
            var h = new Harness(Good());

            var result = await h.Run();

            Assert.True(result.Success);
            Assert.Equal(AutoInvoiceOutcome.DaTaoNhap, result.Data!.Outcome);
            Assert.Equal(500, result.Data.HoaDonId);
            Assert.Equal(Actor, result.Data.NguoiThucHienId);
            Assert.Empty(result.Data.LyDos);

            var steps = h.Log.Where(x => x is "Confirm:1" or "Confirm:2" or "Approve" or "CreateDrafts" or "Notify:Phong" or "Notify:Khach").ToList();
            Assert.Equal(new[] { "Confirm:1", "Confirm:2", "Approve", "CreateDrafts", "Notify:Phong" }, steps);

            Assert.Equal(2, h.Confirms.Count);
            Assert.Equal(1, h.Confirms[0].AnhChiSoDongHoId);
            Assert.Equal(120m, h.Confirms[0].GiaTriXacNhan);
            Assert.Equal("Tự xác nhận, độ tin cậy 0,95", h.Confirms[0].GhiChu);
            Assert.Equal(2, h.Confirms[1].AnhChiSoDongHoId);
            Assert.Equal(60m, h.Confirms[1].GiaTriXacNhan);
            Assert.Equal("Tự xác nhận, độ tin cậy 0,97", h.Confirms[1].GhiChu);

            var approve = Assert.Single(h.Workflow.ApprovedBatchRequests);
            Assert.Equal(10, approve.Thang);
            Assert.Equal(2026, approve.Nam);
            var item = Assert.Single(approve.DanhSachPhong);
            Assert.Equal(Phong, item.PhongTroId);
            Assert.Equal(MeterReadingSubmissionMode.OfficialImage, item.DienMode);
            Assert.Equal(MeterReadingSubmissionMode.OfficialImage, item.NuocMode);

            var create = Assert.Single(h.Issuance.CreateCalls);
            Assert.Equal(Chi, create.Request.ChiNhanhId);
            Assert.Equal(new[] { Phong }, create.Request.PhongTroIds);
            Assert.Equal(10, create.Request.Thang);
            Assert.Equal(2026, create.Request.Nam);
            Assert.Equal(Actor, create.ActorId);
            // Lịch sử khởi tạo của hóa đơn mang nhãn [TU_DONG].
            Assert.Equal("[TU_DONG] Tạo hóa đơn nháp tự động", create.LyDoKhoiTao);
            Assert.StartsWith(AutoInvoiceHistoryTags.TuDong, create.LyDoKhoiTao);

            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Phong", notice.Kind);
            Assert.Equal(Phong, notice.TargetId);
            Assert.Equal("Phòng 101 kỳ 10/2026: có hóa đơn tự động chờ gửi duyệt", notice.TieuDe);
            Assert.Equal("Hóa đơn HD-500 đã được tạo ở trạng thái nháp. Kiểm tra rồi bấm Gửi duyệt.", notice.NoiDung);
            Assert.Equal("HoaDon", notice.LinhVuc);
            Assert.Equal("/QuanLyNhaTro/QuanLyHoaDon?chiNhanhId=1&thang=10&nam=2026&hoaDonId=500", notice.Link);
            Assert.DoesNotContain(h.ThongBao.Sent, n => n.Kind == "Khach");
        }

        [Fact]
        public async Task Discard_IsCalledFirst_AndAfterEveryStep()
        {
            var h = new Harness(Good());

            await h.Run();

            Assert.Equal("Discard", h.Log[0]);
            foreach (var step in new[] { "Confirm:1", "Confirm:2", "Approve", "CreateDrafts", "Notify:Phong" })
            {
                var i = Index(h.Log, step);
                Assert.True(i >= 0, step);
                Assert.Equal("Discard", h.Log[i + 1]);
            }
        }

        [Fact]
        public async Task OfficialImagesAlready_SkipsConfirmStep()
        {
            var images = new[]
            {
                Img(1, LoaiDongHo.Dien, TrangThaiXuLyAnhChiSo.DaXacNhan, xacNhan: 120m, official: true),
                Img(2, LoaiDongHo.Nuoc, TrangThaiXuLyAnhChiSo.DaXacNhan, xacNhan: 60m, official: true)
            };
            var h = new Harness(Good(images: images));

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DaTaoNhap, result.Data!.Outcome);
            Assert.Empty(h.Confirms);
            Assert.Single(h.Workflow.ApprovedBatchRequests);
        }

        [Fact]
        public async Task PeriodAlreadyApproved_SkipsConfirmAndApprove()
        {
            var h = new Harness(Approved());

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DaTaoNhap, result.Data!.Outcome);
            Assert.Empty(h.Confirms);
            Assert.Empty(h.Workflow.ApprovedBatchRequests);
            Assert.Single(h.Issuance.CreateCalls);
        }

        [Fact]
        public async Task CustomerUpload_UsesFallbackStaff_OtherTriggersUseActor()
        {
            var customer = new Harness(Good());
            await customer.Run(AutoInvoiceTrigger.KhachTaiAnh, actor: 99);
            Assert.Equal(Fallback, customer.Issuance.CreateCalls.Single().ActorId);

            foreach (var trigger in new[] { AutoInvoiceTrigger.NhanVienTaiAnh, AutoInvoiceTrigger.XacNhanAnh, AutoInvoiceTrigger.SuaSoDaXacNhan, AutoInvoiceTrigger.ChotKy })
            {
                var staff = new Harness(Good());
                await staff.Run(trigger, actor: Actor);
                Assert.Equal(Actor, staff.Issuance.CreateCalls.Single().ActorId);
                Assert.DoesNotContain("FindFallbackActor", staff.Log);
            }
        }

        [Fact]
        public async Task CustomerUpload_WithoutFallbackStaff_StopsAndNotifiesStaff()
        {
            var h = new Harness(Good());
            h.Store.FallbackActorId = null;

            var result = await h.Run(AutoInvoiceTrigger.KhachTaiAnh, actor: 99);

            Assert.Equal(AutoInvoiceOutcome.DungCoThongBao, result.Data!.Outcome);
            Assert.Equal(new[] { AutoInvoiceReason.ChiNhanhKhongCoNhanVien }, result.Data.LyDos);
            Assert.Empty(h.Confirms);
            Assert.Empty(h.Issuance.CreateCalls);
            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Phong", notice.Kind);
            Assert.Contains("Chi nhánh chưa có nhân viên được phân công", notice.NoiDung);
        }

        [Theory]
        [InlineData(EmployeeActionCodes.MeterReview)]
        [InlineData(EmployeeActionCodes.InvoiceDraft)]
        public async Task MissingAnyPermission_StopsBeforeAnyStep(string missing)
        {
            var h = new Harness(Good());
            h.Access.Permissions.Remove((Actor, Chi, missing));

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DungCoThongBao, result.Data!.Outcome);
            Assert.Equal(new[] { AutoInvoiceReason.NguoiThucHienThieuQuyen }, result.Data.LyDos);
            Assert.Empty(h.Confirms);
            Assert.Empty(h.Workflow.ApprovedBatchRequests);
            Assert.Empty(h.Issuance.CreateCalls);
            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Phong", notice.Kind);
            Assert.Contains("Meter.Review hoặc Invoice.Draft", notice.NoiDung);
            Assert.DoesNotContain("Invoice.Submit", notice.NoiDung);
        }

        [Theory]
        [InlineData(AutoInvoiceTrigger.NhanVienTaiAnh, Actor)]
        [InlineData(AutoInvoiceTrigger.KhachTaiAnh, Fallback)]
        public async Task NguoiThucHienKhongCanInvoiceSubmit(AutoInvoiceTrigger trigger, int nguoiThucHien)
        {
            var h = new Harness(Good());
            h.Access.Permissions.Remove((nguoiThucHien, Chi, EmployeeActionCodes.InvoiceSubmit));

            var result = await h.Run(trigger, actor: trigger == AutoInvoiceTrigger.KhachTaiAnh ? 99 : Actor);

            Assert.Equal(AutoInvoiceOutcome.DaTaoNhap, result.Data!.Outcome);
            Assert.Equal(nguoiThucHien, Assert.Single(h.Issuance.CreateCalls).ActorId);
            Assert.Equal("Phong", Assert.Single(h.ThongBao.Sent).Kind);
        }

        // ---------- Dừng sớm ----------

        [Fact]
        public async Task SilentReasons_DoNotNotifyAnyone()
        {
            var contexts = new[]
            {
                Ctx(periodId: null, status: null),                                  // ThieuChiSo
                Ctx(coHoaDon: true),                                               // DaCoHoaDon
                Ctx(contracts: Array.Empty<AutoInvoiceContractRef>()),             // KhongCoHopDong
                Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 120m) })                   // thiếu đồng hồ nước
            };

            foreach (var ctx in contexts)
            {
                var h = new Harness(ctx);
                var result = await h.Run();

                Assert.True(result.Success);
                Assert.Equal(AutoInvoiceOutcome.DungImLang, result.Data!.Outcome);
                Assert.Empty(h.ThongBao.Sent);
                Assert.Empty(h.Issuance.CreateCalls);
            }
        }

        [Fact]
        public async Task RoomNotFound_IsSilent()
        {
            var h = new Harness(null);

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DungImLang, result.Data!.Outcome);
            Assert.Empty(h.ThongBao.Sent);
        }

        [Fact]
        public async Task InvalidArguments_AreSilent_AndDoNotTouchStore()
        {
            var h = new Harness(Good());

            var r1 = await h.Service.RunForRoomAsync(0, AutoInvoiceTrigger.ChotKy, Phong, 10, 2026);
            var r2 = await h.Service.RunForRoomAsync(Actor, AutoInvoiceTrigger.ChotKy, 0, 10, 2026);
            var r3 = await h.Service.RunForRoomAsync(Actor, AutoInvoiceTrigger.ChotKy, Phong, 13, 2026);
            var r4 = await h.Service.RunForRoomAsync(Actor, AutoInvoiceTrigger.ChotKy, Phong, 10, 1999);

            Assert.All(new[] { r1, r2, r3, r4 }, r => Assert.Equal(AutoInvoiceOutcome.DungImLang, r.Data!.Outcome));
            Assert.Equal(0, h.Store.GetContextCalls);
        }

        [Fact]
        public async Task LowConfidence_NotifiesStaffAndCustomer()
        {
            var images = new[] { Img(1, LoaiDongHo.Dien, ai: 120m, tinCay: 0.85), Img(2, LoaiDongHo.Nuoc, ai: 60m) };
            var h = new Harness(Good(images: images));

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DungCoThongBao, result.Data!.Outcome);
            Assert.Equal(new[] { AutoInvoiceReason.DoTinCayThap }, result.Data.LyDos);
            Assert.Empty(h.Confirms);
            Assert.Empty(h.Issuance.CreateCalls);

            var staff = Assert.Single(h.ThongBao.Sent, n => n.Kind == "Phong");
            Assert.Equal(Phong, staff.TargetId);
            Assert.Equal("/QuanLyNhaTro/ChotDienNuoc?chiNhanhId=1&thang=10&nam=2026&phongTroId=101", staff.Link);
            Assert.Contains("Phòng 101 kỳ 10/2026", staff.TieuDe);
            Assert.Contains("0,85 < 0,90", staff.NoiDung);

            var customer = Assert.Single(h.ThongBao.Sent, n => n.Kind == "Khach");
            Assert.Equal(7, customer.TargetId);
            Assert.Equal("Cần chụp lại ảnh đồng hồ", customer.TieuDe);
            Assert.Equal("DienNuoc", customer.LinhVuc);
            Assert.Equal("/KhachThue/ChiSoDienNuoc", customer.Link);
            Assert.Contains("điện", customer.NoiDung);
        }

        [Fact]
        public async Task Unreadable_NotifiesCustomerForEachMeterWithReason()
        {
            var images = new[]
            {
                Img(1, LoaiDongHo.Dien, TrangThaiXuLyAnhChiSo.KhongDocDuoc),
                Img(2, LoaiDongHo.Nuoc, TrangThaiXuLyAnhChiSo.CanChupLai)
            };
            var h = new Harness(Good(images: images));

            await h.Run();

            var customers = h.ThongBao.Sent.Where(n => n.Kind == "Khach").ToList();
            Assert.Equal(2, customers.Count);
            Assert.Contains(customers, n => n.NoiDung.Contains("điện"));
            Assert.Contains(customers, n => n.NoiDung.Contains("nước"));
        }

        [Fact]
        public async Task ChiSoGiam_NotifiesCustomerExactlyOnce_WithValuesAndPhone()
        {
            var images = new[] { Img(1, LoaiDongHo.Dien, ai: 95m), Img(2, LoaiDongHo.Nuoc, ai: 60m) };
            var h = new Harness(Good(images: images));

            var result = await h.Run();

            Assert.Equal(new[] { AutoInvoiceReason.ChiSoGiam }, result.Data!.LyDos);
            var customer = Assert.Single(h.ThongBao.Sent, n => n.Kind == "Khach");
            Assert.Equal("Chỉ số đồng hồ thấp hơn kỳ trước", customer.TieuDe);
            Assert.Contains("95", customer.NoiDung);
            Assert.Contains("100", customer.NoiDung);
            Assert.Contains("0900000000", customer.NoiDung);
            Assert.Equal("/KhachThue/ChiSoDienNuoc", customer.Link);
            Assert.Single(h.ThongBao.Sent, n => n.Kind == "Phong");
        }

        [Fact]
        public async Task ChiSoGiam_WithoutBranchPhone_OmitsContactClause()
        {
            var images = new[] { Img(1, LoaiDongHo.Dien, ai: 95m), Img(2, LoaiDongHo.Nuoc, ai: 60m) };
            var h = new Harness(KhongSoDienThoai(Good(images: images)));

            await h.Run();

            var customer = Assert.Single(h.ThongBao.Sent, n => n.Kind == "Khach");
            Assert.DoesNotContain("liên hệ", customer.NoiDung);
            Assert.EndsWith(".", customer.NoiDung);
        }

        private static AutoInvoiceRoomContext KhongSoDienThoai(AutoInvoiceRoomContext ctx) => new()
        {
            PhongTroId = ctx.PhongTroId, SoPhong = ctx.SoPhong, ChiNhanhId = ctx.ChiNhanhId, SoDienThoaiChiNhanh = "",
            Thang = ctx.Thang, Nam = ctx.Nam, DichVuDienNuocCuaPhongId = ctx.DichVuDienNuocCuaPhongId,
            TrangThaiGhiNhan = ctx.TrangThaiGhiNhan, ChiSoDienCu = ctx.ChiSoDienCu, ChiSoNuocCu = ctx.ChiSoNuocCu,
            DonGiaDien = ctx.DonGiaDien, DonGiaNuoc = ctx.DonGiaNuoc, LichSu = ctx.LichSu,
            Images = ctx.Images, HopDongHieuLuc = ctx.HopDongHieuLuc
        };

        [Fact]
        public async Task NghiSaiChuSo_NotifiesCustomerAndStaff_WithExactText()
        {
            // Chỉ số điện cũ 100, đọc được 1000: nghi đọc thừa một chữ số.
            var images = new[] { Img(1, LoaiDongHo.Dien, ai: 1000m), Img(2, LoaiDongHo.Nuoc, ai: 60m) };
            var h = new Harness(Good(images: images));

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DungCoThongBao, result.Data!.Outcome);
            Assert.Equal(new[] { AutoInvoiceReason.NghiSaiChuSo }, result.Data.LyDos);
            Assert.Empty(h.Issuance.CreateCalls);
            var customer = Assert.Single(h.ThongBao.Sent, n => n.Kind == "Khach");
            Assert.Equal(7, customer.TargetId);
            Assert.Equal("Chỉ số đồng hồ có thể chưa đúng", customer.TieuDe);
            Assert.Equal("Chỉ số điện kỳ 10/2026 của phòng 101 đọc được là 1.000, chênh lệch nhiều so với các tháng trước. "
                + "Vui lòng chụp lại rõ mặt đồng hồ hoặc liên hệ 0900000000.", customer.NoiDung);
            Assert.Equal("DienNuoc", customer.LinhVuc);
            Assert.Equal("/KhachThue/ChiSoDienNuoc", customer.Link);
            var staff = Assert.Single(h.ThongBao.Sent, n => n.Kind == "Phong");
            Assert.Contains("đọc thừa một chữ số", staff.NoiDung);
        }

        [Fact]
        public async Task TieuThuThap_NotifiesCustomer_WithoutBranchPhone_EndsWithPeriod()
        {
            // Lịch sử hợp đồng: điện 40, nước 10 mỗi kỳ; kỳ này điện chỉ 5 kWh (< 40 × 0,3), nước 10 bình thường.
            var lichSu = new MeterConsumptionHistory(
                new[] { new MeterConsumption(40m, 10m), new MeterConsumption(40m, 10m), new MeterConsumption(40m, 10m) }, 31, 31, 10);
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 105m), Img(2, LoaiDongHo.Nuoc, ai: 60m) }, lichSu: lichSu);
            var h = new Harness(KhongSoDienThoai(ctx));

            var result = await h.Run();

            Assert.Equal(new[] { AutoInvoiceReason.TieuThuThapBatThuong }, result.Data!.LyDos);
            var customer = Assert.Single(h.ThongBao.Sent, n => n.Kind == "Khach");
            Assert.Equal("Chỉ số đồng hồ có thể chưa đúng", customer.TieuDe);
            Assert.Equal("Chỉ số điện kỳ 10/2026 của phòng 101 đọc được là 105, chênh lệch nhiều so với các tháng trước. "
                + "Vui lòng chụp lại rõ mặt đồng hồ.", customer.NoiDung);
            var staff = Assert.Single(h.ThongBao.Sent, n => n.Kind == "Phong");
            Assert.Contains("trung vị 3 kỳ của hợp đồng 40 kWh", staff.NoiDung);
        }

        [Fact]
        public async Task TieuThuThap_OfficialConfirmedImage_NotifiesStaffOnly()
        {
            // Nhân viên đã xác nhận ảnh chính thức với số thấp: khách không bị yêu cầu chụp lại.
            var lichSu = new MeterConsumptionHistory(
                new[] { new MeterConsumption(40m, 10m), new MeterConsumption(40m, 10m), new MeterConsumption(40m, 10m) }, 31, 31, 10);
            var ctx = Ctx(new[]
            {
                Img(1, LoaiDongHo.Dien, ai: 105m, xacNhan: 105m, official: true),
                Img(2, LoaiDongHo.Nuoc, ai: 60m, xacNhan: 60m, official: true)
            }, lichSu: lichSu);
            var h = new Harness(ctx);

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DungCoThongBao, result.Data!.Outcome);
            Assert.Equal(new[] { AutoInvoiceReason.TieuThuThapBatThuong }, result.Data.LyDos);
            Assert.Empty(h.Issuance.CreateCalls);
            Assert.Equal("Phong", Assert.Single(h.ThongBao.Sent).Kind);
        }

        [Fact]
        public async Task NghiSaiChuSo_PeriodAlreadyApproved_NotifiesStaffOnly()
        {
            var h = new Harness(Ctx(status: TrangThaiGhiNhan.DaDuyet, dienMoi: 1000, nuocMoi: 60));

            var result = await h.Run();

            Assert.Equal(new[] { AutoInvoiceReason.NghiSaiChuSo }, result.Data!.LyDos);
            Assert.Empty(h.Issuance.CreateCalls);
            Assert.Equal("Phong", Assert.Single(h.ThongBao.Sent).Kind);
        }

        [Fact]
        public async Task TieuThuThap_OcrValue_NotifiesCustomer()
        {
            var lichSu = new MeterConsumptionHistory(
                new[] { new MeterConsumption(40m, 10m), new MeterConsumption(40m, 10m), new MeterConsumption(40m, 10m) }, 31, 31, 10);
            var h = new Harness(Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 105m), Img(2, LoaiDongHo.Nuoc, ai: 60m) }, lichSu: lichSu));

            await h.Run();

            Assert.Single(h.ThongBao.Sent, n => n.Kind == "Khach");
            Assert.Single(h.ThongBao.Sent, n => n.Kind == "Phong");
        }

        [Fact]
        public async Task AbnormalConsumption_NotifiesStaffOnly()
        {
            var images = new[] { Img(1, LoaiDongHo.Dien, ai: 700m), Img(2, LoaiDongHo.Nuoc, ai: 60m) };
            var h = new Harness(Good(images: images));

            var result = await h.Run();

            Assert.Equal(new[] { AutoInvoiceReason.TieuThuBatThuong }, result.Data!.LyDos);
            Assert.Equal("Phong", Assert.Single(h.ThongBao.Sent).Kind);
        }

        [Fact]
        public async Task PreviousPeriodNotClosed_NotifiesStaffOnly()
        {
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 120m), Img(2, LoaiDongHo.Nuoc, ai: 60m) }, kyTruocChuaChot: true);
            var h = new Harness(ctx);

            var result = await h.Run();

            Assert.Equal(new[] { AutoInvoiceReason.KyTruocChuaChot }, result.Data!.LyDos);
            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Phong", notice.Kind);
            Assert.Contains("Kỳ 09/2026 trước chưa được chốt", notice.NoiDung);
        }

        [Fact]
        public async Task MultipleContracts_NotifiesStaffOnly_AndDoesNotRun()
        {
            var contracts = new[] { new AutoInvoiceContractRef(10, 7), new AutoInvoiceContractRef(11, 8) };
            var ctx = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 120m), Img(2, LoaiDongHo.Nuoc, ai: 60m) }, contracts: contracts);
            var h = new Harness(ctx);

            var result = await h.Run();

            Assert.Equal(new[] { AutoInvoiceReason.NhieuHopDongTrongKy }, result.Data!.LyDos);
            Assert.Empty(h.Issuance.CreateCalls);
            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Phong", notice.Kind);
            Assert.Contains("nhiều hợp đồng", notice.NoiDung);
        }

        // ---------- Thất bại và khôi phục ----------

        [Fact]
        public async Task ConfirmFails_ButReloadShowsOfficialImage_Continues()
        {
            var initial = Good();
            var recovered = Ctx(new[]
            {
                Img(1, LoaiDongHo.Dien, TrangThaiXuLyAnhChiSo.DaXacNhan, xacNhan: 120m, official: true),
                Img(2, LoaiDongHo.Nuoc, ai: 60m)
            });
            var h = new Harness(initial);
            h.Store.Contexts.Add(recovered);
            h.Workflow.OnConfirmImage = (req, actor) =>
            {
                h.Log.Add($"Confirm:{req.AnhChiSoDongHoId}");
                h.Confirms.Add(req);
                return req.AnhChiSoDongHoId == 1
                    ? ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh đã được xác nhận bởi người khác.")
                    : ServiceResult<MeterImageWorkflowResult>.Ok(new MeterImageWorkflowResult());
            };

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DaTaoNhap, result.Data!.Outcome);
            Assert.Equal(2, h.Confirms.Count);
            Assert.Single(h.Issuance.CreateCalls);
        }

        [Fact]
        public async Task ConfirmFails_AndNotRecoverable_StopsWithLoiKhiTao()
        {
            var h = new Harness(Good());
            h.Workflow.OnConfirmImage = (req, actor) =>
            {
                h.Log.Add($"Confirm:{req.AnhChiSoDongHoId}");
                return ServiceResult<MeterImageWorkflowResult>.Fail("Không thể xác nhận ảnh.");
            };

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DungCoThongBao, result.Data!.Outcome);
            Assert.Equal(new[] { AutoInvoiceReason.LoiKhiTao }, result.Data.LyDos);
            Assert.Empty(h.Workflow.ApprovedBatchRequests);
            Assert.Empty(h.Issuance.CreateCalls);
            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Phong", notice.Kind);
            Assert.Contains("Không thể xác nhận ảnh.", notice.NoiDung);
        }

        [Fact]
        public async Task ConfirmFails_ButInvoiceAlreadyExists_StopsSilently()
        {
            var withInvoice = Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 120m) }, coHoaDon: true);
            var h = new Harness(Good());
            h.Store.Contexts.Add(withInvoice);
            h.Workflow.OnConfirmImage = (req, actor) => ServiceResult<MeterImageWorkflowResult>.Fail("Kỳ đã chốt.");

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DungImLang, result.Data!.Outcome);
            Assert.Empty(h.ThongBao.Sent);
            Assert.Empty(h.Issuance.CreateCalls);
        }

        [Fact]
        public async Task ApproveFails_ButReloadShowsApproved_Continues()
        {
            var h = new Harness(Good());
            h.Store.Contexts.Add(Good());                 // sau xác nhận: chưa duyệt
            h.Store.Contexts.Add(Approved());             // sau khi Approve thất bại: đã duyệt
            h.Workflow.OnApproveBatch = (req, actor) => ServiceResult<MeterPeriodsApprovalResult>.Fail("Kỳ đã được duyệt.");

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DaTaoNhap, result.Data!.Outcome);
            Assert.Single(h.Issuance.CreateCalls);
        }

        [Fact]
        public async Task ApproveFails_AndNotRecoverable_StopsWithLoiKhiTao()
        {
            var h = new Harness(Good());
            h.Workflow.OnApproveBatch = (req, actor) => ServiceResult<MeterPeriodsApprovalResult>.Fail("Không thể chốt kỳ.");

            var result = await h.Run();

            Assert.Equal(new[] { AutoInvoiceReason.LoiKhiTao }, result.Data!.LyDos);
            Assert.Empty(h.Issuance.CreateCalls);
            Assert.Contains("Không thể chốt kỳ.", Assert.Single(h.ThongBao.Sent).NoiDung);
        }

        [Fact]
        public async Task CreateDraftsSkippedBecauseInvoiceExists_IsSilent()
        {
            var h = new Harness(Good());
            h.Issuance.OnCreateDrafts = (req, actor) => ServiceResult<InvoiceDraftBatchResult>.Ok(new InvoiceDraftBatchResult
            {
                Items = new[]
                {
                    new InvoiceDraftItemResult { PhongTroId = Phong, HopDongId = 10, Status = InvoiceDraftItemStatus.SkippedHasActiveInvoice, Message = "Đã phát sinh hóa đơn" }
                }
            });

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DungImLang, result.Data!.Outcome);
            Assert.Empty(h.ThongBao.Sent);
        }

        [Fact]
        public async Task CreateDraftsFails_NotifiesStaff()
        {
            var h = new Harness(Good());
            h.Issuance.OnCreateDrafts = (req, actor) => ServiceResult<InvoiceDraftBatchResult>.Fail("Không thể tạo hóa đơn nháp.");

            var result = await h.Run();

            Assert.Equal(new[] { AutoInvoiceReason.LoiKhiTao }, result.Data!.LyDos);
            Assert.Equal("Phong", Assert.Single(h.ThongBao.Sent).Kind);
        }

        [Fact]
        public async Task CreateDraftsItemInvalid_NotifiesStaffWithItemMessage()
        {
            var h = new Harness(Good());
            h.Issuance.OnCreateDrafts = (req, actor) => ServiceResult<InvoiceDraftBatchResult>.Ok(new InvoiceDraftBatchResult
            {
                Items = new[]
                {
                    new InvoiceDraftItemResult { PhongTroId = Phong, HopDongId = 10, Status = InvoiceDraftItemStatus.Invalid, Message = "Dữ liệu tính tiền phòng không hợp lệ." }
                }
            });

            var result = await h.Run();

            Assert.Equal(new[] { AutoInvoiceReason.LoiKhiTao }, result.Data!.LyDos);
            Assert.Contains("Dữ liệu tính tiền phòng không hợp lệ.", Assert.Single(h.ThongBao.Sent).NoiDung);
        }

        // ---------- Thông báo nhân viên ----------

        [Fact]
        public async Task StaffNotificationThrowing_StillReportsDaTaoNhap_WithWarningOnly()
        {
            var h = new Harness(Good());
            h.ThongBao.ThrowOnSend = true;

            var result = await h.Run();

            Assert.True(result.Success);
            Assert.Equal(AutoInvoiceOutcome.DaTaoNhap, result.Data!.Outcome);
            Assert.Equal(500, result.Data.HoaDonId);
            Assert.Contains(h.Logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(h.Logger.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task StaffNotificationFailing_StillReportsDaTaoNhap()
        {
            var h = new Harness(Good());
            h.ThongBao.FailOnSend = true;

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DaTaoNhap, result.Data!.Outcome);
            Assert.Contains(h.Logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("Không có người nhận thông báo."));
            Assert.DoesNotContain(h.Logger.Entries, e => e.Level == LogLevel.Error);
        }

        // ---------- Lỗi ngoài dự kiến ----------

        [Fact]
        public async Task StoreThrows_LogsOneError_NeverThrows()
        {
            var h = new Harness(Good());
            h.Store.ThrowOnGetContext = new InvalidOperationException("DB down");

            var result = await h.Run();

            Assert.True(result.Success);
            Assert.Equal(AutoInvoiceOutcome.DungCoThongBao, result.Data!.Outcome);
            Assert.Equal(1, h.Logger.Entries.Count(e => e.Level == LogLevel.Error));
        }

        [Fact]
        public async Task UnexpectedExceptionAfterContext_NotifiesStaffAndLogsOneError()
        {
            var h = new Harness(Good());
            h.Issuance.OnCreateDrafts = (req, actor) => throw new InvalidOperationException("boom");

            var result = await h.Run();

            Assert.True(result.Success);
            Assert.Equal(AutoInvoiceOutcome.DungCoThongBao, result.Data!.Outcome);
            Assert.Equal(1, h.Logger.Entries.Count(e => e.Level == LogLevel.Error));
            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Phong", notice.Kind);
            Assert.Contains("chưa tạo được hóa đơn tự động", notice.TieuDe);
        }

        [Fact]
        public async Task UsesConfiguredNumberOfPreviousPeriods()
        {
            var h = new Harness(Good());

            await h.Run();

            Assert.Equal(new AutoInvoiceOptions().SoKyLichSuToiDa, h.Store.LastSoKyLichSuToiDa);
        }


        // ---------- Đợt B: ThieuDonGia, đánh giá lại (L3) ----------

        [Fact]
        public async Task ThieuDonGia_NotifiesStaffWithGuidance_NotCustomer_AndCreatesNothing()
        {
            var ctx = Ctx(status: TrangThaiGhiNhan.DaDuyet, dienMoi: 120, nuocMoi: 60, donGiaDien: 0, donGiaNuoc: 20000);
            var h = new Harness(ctx);

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DungCoThongBao, result.Data!.Outcome);
            Assert.Equal(new[] { AutoInvoiceReason.ThieuDonGia }, result.Data.LyDos);
            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Phong", notice.Kind);
            Assert.Equal("/QuanLyNhaTro/ChotDienNuoc?chiNhanhId=1&thang=10&nam=2026&phongTroId=101", notice.Link);
            Assert.Contains("chưa có đơn giá điện cho kỳ 10/2026", notice.NoiDung);
            Assert.Contains("Đơn giá của kỳ được lưu lúc tạo kỳ; lập hóa đơn theo luồng tay và sửa dòng điện/nước ở bản nháp.", notice.NoiDung);
            Assert.DoesNotContain(h.ThongBao.Sent, n => n.Kind == "Khach");
            Assert.Empty(h.Workflow.ApprovedBatchRequests);
            Assert.Empty(h.Issuance.CreateCalls);
        }

        [Fact]
        public async Task OtherReasons_DoNotCarryThieuDonGiaGuidance()
        {
            var h = new Harness(Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 120m), Img(2, LoaiDongHo.Nuoc, ai: 60m) }, kyTruocChuaChot: true));

            await h.Run();

            Assert.DoesNotContain("Đơn giá của kỳ", Assert.Single(h.ThongBao.Sent).NoiDung);
        }

        [Fact]
        public async Task ReevaluatesBeforeCreating_StopsWhenRoomNoLongerEligible_WithNotice()
        {
            var h = new Harness(Good());
            h.Store.Contexts.Add(Good());
            h.Store.Contexts.Add(Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 120m), Img(2, LoaiDongHo.Nuoc, ai: 60m) }, kyTruocChuaChot: true));

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DungCoThongBao, result.Data!.Outcome);
            Assert.Equal(new[] { AutoInvoiceReason.KyTruocChuaChot }, result.Data.LyDos);
            Assert.Empty(h.Issuance.CreateCalls);
            Assert.Equal("Phong", Assert.Single(h.ThongBao.Sent).Kind);
        }

        [Fact]
        public async Task ReevaluatesBeforeCreating_StopsSilentlyWhenInvoiceAppeared()
        {
            var h = new Harness(Good());
            h.Store.Contexts.Add(Good());
            h.Store.Contexts.Add(Ctx(new[] { Img(1, LoaiDongHo.Dien, ai: 120m), Img(2, LoaiDongHo.Nuoc, ai: 60m) }, coHoaDon: true));

            var result = await h.Run();

            Assert.Equal(AutoInvoiceOutcome.DungImLang, result.Data!.Outcome);
            Assert.Empty(h.Issuance.CreateCalls);
            Assert.Empty(h.ThongBao.Sent);
        }
    }
}
