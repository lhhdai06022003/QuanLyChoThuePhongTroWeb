using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public sealed record ContractAlertSettings
    {
        public bool Enabled { get; init; } = true;
        public List<int> AlertDays { get; init; } = new() { 30, 15 };
        public string? AdminEmail { get; init; }
    }
}
