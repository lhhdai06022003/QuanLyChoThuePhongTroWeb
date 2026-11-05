using System;
using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs
{
    public class HopDongFilterReq : DataTableRequest
    {
        public int? ChiNhanhId { get; set; }
        public int? NguoiThueId { get; set; }
        public int? TrangThai { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        // "QuaHan" | "SapHetHan" | "GiaHan" | null.
        public string? BoLoc { get; set; }
        // Đầu ngày hôm nay (giờ VN) tính bằng UTC; do service luôn ghi đè, giống AllowedBranchIds.
        public DateTime? MocDauNgayHomNayUtc { get; set; }

        // Do service gán theo phạm vi người dùng (null = Admin, không giới hạn); giá trị client gửi lên luôn bị ghi đè.
        public IReadOnlyCollection<int>? AllowedBranchIds { get; set; }
    }
}
