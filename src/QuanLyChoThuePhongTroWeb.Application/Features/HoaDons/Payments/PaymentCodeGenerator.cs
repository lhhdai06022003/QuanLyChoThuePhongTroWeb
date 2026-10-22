using System;
using System.Globalization;
using System.Security.Cryptography;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments
{
    public interface IPaymentCodeGenerator
    {
        // TT{HoaDonId}{6 ký tự}; dùng làm nội dung chuyển khoản (spec §10.1).
        string NewRequestCode(int hoaDonId);

        // {prefix}-{yyyyMMdd}-{8 ký tự}, ví dụ TM-20261003-AB12CD34 cho tiền mặt (spec §18).
        string NewLedgerCode(string prefix, DateTime nowUtc);
    }

    public sealed class PaymentCodeGenerator : IPaymentCodeGenerator
    {
        // Bỏ O, I, 0, 1 để khách đọc và gõ lại không nhầm.
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public string NewRequestCode(int hoaDonId)
        {
            return $"TT{hoaDonId}{RandomChars(6)}";
        }

        public string NewLedgerCode(string prefix, DateTime nowUtc)
        {
            return $"{prefix}-{nowUtc.AddHours(7).ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{RandomChars(8)}";
        }

        private static string RandomChars(int length)
        {
            Span<char> chars = stackalloc char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }

            return new string(chars);
        }
    }
}
