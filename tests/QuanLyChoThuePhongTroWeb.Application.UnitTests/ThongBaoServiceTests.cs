using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class ThongBaoServiceTests
    {
        private sealed class GroupAwareNotifier : IThongBaoNotifier
        {
            public List<int> UserIds { get; } = new();
            public List<string> Groups { get; } = new();
            public bool Throw { get; set; }

            public Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default)
            {
                if (Throw) throw new InvalidOperationException("SignalR down");
                UserIds.Add(nguoiDungId);
                return Task.CompletedTask;
            }

            public Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default)
            {
                Groups.Add(roleName);
                return Task.CompletedTask;
            }
        }

        private readonly FakeThongBaoStore _store = new();
        private readonly RecordingUnitOfWork _unitOfWork = new();
        private readonly GroupAwareNotifier _notifier = new();
        private readonly CapturingLogger<ThongBaoService> _logger = new();
        private readonly ThongBaoService _service;

        public ThongBaoServiceTests()
        {
            _service = new ThongBaoService(_store, _unitOfWork, _notifier, _logger);
        }

        [Fact]
        public async Task GuiChoNguoiPhuTrachPhong_DistinctRecipients_SavesAndPushesPerUser_NotGroup()
        {
            _store.RoomResponsibleUserIds.AddRange(new[] { 1, 2, 2, 3 });

            var result = await _service.GuiChoNguoiPhuTrachPhongAsync(7, "Sự cố mới", "nội dung", "SuCo", "/link");

            Assert.True(result.Success);
            Assert.Equal(3, _store.ThongBaos.Count);
            Assert.Equal(new int?[] { 1, 2, 3 }, _store.ThongBaos.Select(t => t.NguoiDungId));
            Assert.All(_store.ThongBaos, t => Assert.Equal("Sự cố mới", t.TieuDe));
            Assert.Equal(new[] { 1, 2, 3 }, _notifier.UserIds);
            Assert.Empty(_notifier.Groups);
        }

        [Fact]
        public async Task GuiChoNguoiPhuTrachPhong_NotifierThrows_StillSucceeds_AndLogsWarning()
        {
            _store.RoomResponsibleUserIds.AddRange(new[] { 1, 2 });
            _notifier.Throw = true;

            var result = await _service.GuiChoNguoiPhuTrachPhongAsync(7, "t", "n");

            Assert.True(result.Success);
            Assert.Equal(2, _store.ThongBaos.Count);
            Assert.Equal(2, _logger.Entries.Count(e => e.Level == LogLevel.Warning));
            Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task GuiChoNguoiPhuTrachPhong_NoRecipients_FailsAndSavesNothing()
        {
            var result = await _service.GuiChoNguoiPhuTrachPhongAsync(7, "t", "n");

            Assert.False(result.Success);
            Assert.Equal("Không có người nhận thông báo.", result.Message);
            Assert.Empty(_store.ThongBaos);
            Assert.Equal(0, _unitOfWork.SaveChangesCalls);
        }

        [Fact]
        public async Task DanhDauDaDoc_OtherUsersNotification_IsNotFound_AndStaysUnread()
        {
            var tb = new ThongBao { Id = 10, NguoiDungId = 1, TieuDe = "t", NoiDung = "n" };
            _store.ThongBaos.Add(tb);

            var result = await _service.DanhDauDaDocAsync(10, 2);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
            Assert.Equal("Không tìm thấy thông báo.", result.Message);
            Assert.False(tb.IsRead);
        }

        [Fact]
        public async Task DanhDauDaDoc_OwnNotification_MarksRead()
        {
            var tb = new ThongBao { Id = 10, NguoiDungId = 1, TieuDe = "t", NoiDung = "n" };
            _store.ThongBaos.Add(tb);

            var result = await _service.DanhDauDaDocAsync(10, 1);

            Assert.True(result.Success);
            Assert.True(tb.IsRead);
        }
    }
}
