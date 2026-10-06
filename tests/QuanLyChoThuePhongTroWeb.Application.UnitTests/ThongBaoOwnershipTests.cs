using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class ThongBaoOwnershipTests
    {
        private readonly FakeThongBaoStore _store = new();
        private readonly RecordingUnitOfWork _unitOfWork = new();
        private readonly RecordingNotifier _notifier = new();
        private readonly ThongBaoService _service;

        public ThongBaoOwnershipTests()
        {
            _service = new ThongBaoService(_store, _unitOfWork, _notifier);
        }

        [Fact]
        public async Task DanhDauDaDoc_NonexistentId_IsNotFound_WithoutSaving()
        {
            var result = await _service.DanhDauDaDocAsync(999, 1);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
            Assert.Equal(0, _unitOfWork.SaveChangesCalls);
        }

        [Fact]
        public async Task DanhDauDaDoc_OtherUsersNotification_DoesNotSaveChanges()
        {
            _store.ThongBaos.Add(new ThongBao { Id = 5, NguoiDungId = 1, TieuDe = "t", NoiDung = "n" });

            await _service.DanhDauDaDocAsync(5, 2);

            Assert.Equal(0, _unitOfWork.SaveChangesCalls);
        }

        [Fact]
        public async Task DanhDauDaDoc_NotificationWithoutOwner_CannotBeMarkedByAnyone()
        {
            var tb = new ThongBao { Id = 6, NguoiDungId = null, TieuDe = "t", NoiDung = "n" };
            _store.ThongBaos.Add(tb);

            var result = await _service.DanhDauDaDocAsync(6, 1);

            Assert.False(result.Success);
            Assert.False(tb.IsRead);
        }

        [Fact]
        public async Task DanhDauDaDoc_AlreadyRead_Succeeds_WithoutSaving()
        {
            _store.ThongBaos.Add(new ThongBao { Id = 7, NguoiDungId = 1, TieuDe = "t", NoiDung = "n", IsRead = true });

            var result = await _service.DanhDauDaDocAsync(7, 1);

            Assert.True(result.Success);
            Assert.Equal(0, _unitOfWork.SaveChangesCalls);
        }

        [Fact]
        public async Task GuiChoNguoiPhuTrachPhong_SingleRecipient_ContentMatchesAndSavedOnce()
        {
            _store.RoomResponsibleUserIds.Add(9);

            var result = await _service.GuiChoNguoiPhuTrachPhongAsync(7, "Sự cố mới", "Phòng 101 hỏng vòi", "SuCo", "/QuanLyNhaTro/YeuCauSuCo");

            Assert.True(result.Success);
            var tb = Assert.Single(_store.ThongBaos);
            Assert.Equal(9, tb.NguoiDungId);
            Assert.Equal("Phòng 101 hỏng vòi", tb.NoiDung);
            Assert.False(tb.IsRead);
            Assert.Equal(1, _unitOfWork.SaveChangesCalls);
        }
    }
}
