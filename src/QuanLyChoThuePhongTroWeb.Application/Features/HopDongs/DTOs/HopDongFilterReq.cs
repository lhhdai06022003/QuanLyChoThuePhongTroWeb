using System;
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
    }
}
