using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Chống quên phân quyền chi nhánh: method của các service phía quản lý phải nhận actorId làm tham số đầu.
    // Thêm method không cần phân quyền thì phải ghi vào danh sách ngoại lệ dưới đây kèm lý do.
    public class BranchScopeArchitectureTests
    {
        private static readonly Type[] ScopedServices =
        {
            typeof(IPhongTroService),
            typeof(IHopDongService),
            typeof(IThanhVienHopDongService),
            typeof(INguoiThueService),
            typeof(IDichVuService),
            typeof(IDashboardService),
            typeof(IAiAssistantService),
            typeof(IYeuCauSuCoService)
        };

        private static readonly HashSet<string> Exceptions = new()
        {
            // Cổng khách thuê: đã giới hạn theo NguoiThueId của tài khoản đăng nhập.
            $"{nameof(INguoiThueService)}.{nameof(INguoiThueService.GetByIdAsync)}",
            $"{nameof(IHopDongService)}.{nameof(IHopDongService.GetHopDongsByNguoiThueIdAsync)}",
            $"{nameof(IHopDongService)}.{nameof(IHopDongService.GetChiTietHopDongKhachThueAsync)}",
            // Cổng khách thuê: giới hạn theo NguoiThueId
            $"{nameof(IAiAssistantService)}.{nameof(IAiAssistantService.ChatKhachThueAsync)}",
            // Cổng khách thuê: xem và gửi sự cố của chính mình (NguoiThueId lấy từ claim, phòng lấy từ hợp đồng của khách).
            $"{nameof(IYeuCauSuCoService)}.{nameof(IYeuCauSuCoService.GetByNguoiThueAsync)}",
            $"{nameof(IYeuCauSuCoService)}.{nameof(IYeuCauSuCoService.CreateAsync)}",
            // Danh mục dịch vụ dùng chung: ai cũng được xem.
            $"{nameof(IDichVuService)}.{nameof(IDichVuService.GetDanhSachDichVuAsync)}",
            $"{nameof(IDichVuService)}.{nameof(IDichVuService.GetDichVuByIdAsync)}"
        };

        [Fact]
        public void ScopedServiceMethods_TakeActorIdFirst()
        {
            var missing = ScopedServices
                .SelectMany(t => t.GetMethods().Select(m => (Type: t, Method: m)))
                .Where(x => !Exceptions.Contains($"{x.Type.Name}.{x.Method.Name}"))
                .Where(x =>
                {
                    var first = x.Method.GetParameters().FirstOrDefault();
                    return first == null || first.Name != "actorId" || first.ParameterType != typeof(int);
                })
                .Select(x => $"{x.Type.Name}.{x.Method.Name}")
                .ToList();

            Assert.True(missing.Count == 0, "Thiếu actorId: " + string.Join(", ", missing));
        }

        [Fact]
        public void ExceptionList_OnlyNamesExistingMethods()
        {
            var existing = ScopedServices
                .SelectMany(t => t.GetMethods().Select(m => $"{t.Name}.{m.Name}"))
                .ToHashSet();

            Assert.All(Exceptions, e => Assert.Contains(e, existing));
        }
    }
}
