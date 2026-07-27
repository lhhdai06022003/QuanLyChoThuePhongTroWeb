using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Hubs;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ThongBaos
{
    public class ThongBaoService : IThongBaoService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ThongBaoHub> _hubContext;

        public ThongBaoService(ApplicationDbContext context, IHubContext<ThongBaoHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<List<ThongBao>> LayDanhSachTheoNguoiDungAsync(int nguoiDungId)
        {
            return await _context.ThongBaos
                .Where(x => x.NguoiDungId == nguoiDungId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .ToListAsync();
        }

        public async Task<bool> GuiChoNguoiDungAsync(int nguoiDungId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null)
        {
            var thongBao = new ThongBao
            {
                NguoiDungId = nguoiDungId,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                LinhVuc = linhVuc,
                LinkDieuHuong = linkDieuHuong,
                CreatedAt = DateTime.UtcNow
            };

            _context.ThongBaos.Add(thongBao);
            var saved = await _context.SaveChangesAsync() > 0;

            if (saved)
            {
                await _hubContext.Clients.User(nguoiDungId.ToString()).SendAsync("NhanThongBao", new
                {
                    id = thongBao.Id,
                    tieuDe = thongBao.TieuDe,
                    noiDung = thongBao.NoiDung,
                    linhVuc = thongBao.LinhVuc,
                    linkDieuHuong = thongBao.LinkDieuHuong,
                    createdAt = thongBao.CreatedAt.AddHours(7).ToString("dd/MM/yyyy HH:mm")
                });
            }

            return saved;
        }

        public async Task<bool> GuiChoKhachThueAsync(int nguoiThueId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null)
        {
            var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.NguoiThueId == nguoiThueId && !u.IsDeleted && u.IsActive);
            if (user == null) return false;

            return await GuiChoNguoiDungAsync(user.NguoiDungId, tieuDe, noiDung, linhVuc, linkDieuHuong);
        }

        public async Task<bool> GuiChoQuyenAsync(Role role, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null)
        {
            var targetUsers = await _context.NguoiDungs
                .Where(u => u.Role == role && !u.IsDeleted && u.IsActive)
                .ToListAsync();

            if (!targetUsers.Any()) return false;

            var thongBaos = targetUsers.Select(user => new ThongBao
            {
                NguoiDungId = user.NguoiDungId,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                LinhVuc = linhVuc,
                LinkDieuHuong = linkDieuHuong,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.ThongBaos.AddRange(thongBaos);
            var saved = await _context.SaveChangesAsync() > 0;

            if (saved)
            {
                await _hubContext.Clients.Group(role.ToString()).SendAsync("NhanThongBao", new
                {
                    tieuDe = tieuDe,
                    noiDung = noiDung,
                    linhVuc = linhVuc,
                    linkDieuHuong = linkDieuHuong,
                    createdAt = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm")
                });
            }

            return saved;
        }

        public async Task<bool> DanhDauDaDocAsync(int thongBaoId)
        {
            var tb = await _context.ThongBaos.FindAsync(thongBaoId);
            if (tb == null || tb.IsRead) return false;

            tb.IsRead = true;
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<int> LaySoLuongChuaDocAsync(int nguoiDungId)
        {
            return await _context.ThongBaos.CountAsync(x => x.NguoiDungId == nguoiDungId && !x.IsRead);
        }

        public async Task<bool> DanhDauTatCaDaDocAsync(int nguoiDungId)
        {
            var unreadThongBaos = await _context.ThongBaos
                .Where(x => x.NguoiDungId == nguoiDungId && !x.IsRead)
                .ToListAsync();

            if (!unreadThongBaos.Any()) return false;

            foreach (var tb in unreadThongBaos)
            {
                tb.IsRead = true;
            }

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> XoaThongBaoAsync(int thongBaoId, int nguoiDungId)
        {
            var tb = await _context.ThongBaos.FirstOrDefaultAsync(x => x.Id == thongBaoId && x.NguoiDungId == nguoiDungId);
            if (tb == null) return false;

            _context.ThongBaos.Remove(tb);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> XoaTatCaThongBaoAsync(int nguoiDungId)
        {
            var thongBaos = await _context.ThongBaos
                .Where(x => x.NguoiDungId == nguoiDungId)
                .ToListAsync();

            if (!thongBaos.Any()) return false;

            _context.ThongBaos.RemoveRange(thongBaos);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests.DataTableResponse<ThongBao>> LayDanhSachPhanTrangAsync(QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests.DataTableRequest request, int nguoiDungId, bool? chuaDoc)
        {
            int recordsTotal = await _context.ThongBaos.CountAsync(x => x.NguoiDungId == nguoiDungId);
            
            var query = _context.ThongBaos.Where(x => x.NguoiDungId == nguoiDungId);

            if (chuaDoc.HasValue)
            {
                query = query.Where(x => x.IsRead == !chuaDoc.Value);
            }

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                query = query.Where(x => x.TieuDe.ToLower().Contains(request.SearchValue.ToLower()) 
                                      || x.NoiDung.ToLower().Contains(request.SearchValue.ToLower()));
            }

            int recordsFiltered = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip(request.Start)
                .Take(request.Length)
                .ToListAsync();

            return new QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests.DataTableResponse<ThongBao>
            {
                draw = request.Draw,
                recordsTotal = recordsTotal,
                recordsFiltered = recordsFiltered,
                data = data
            };
        }
    }
}
