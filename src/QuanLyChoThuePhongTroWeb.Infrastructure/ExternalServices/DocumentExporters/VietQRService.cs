using System;
using System.Collections.Generic;
using System.Text;
using QRCoder;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.DocumentExporters
{
    public class VietQRService : IVietQRService
    {
        private static readonly Dictionary<string, string> BankBinMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "MB", "970422" },
            { "MBBank", "970422" },
            { "VCB", "970436" },
            { "Vietcombank", "970436" },
            { "TCB", "970407" },
            { "Techcombank", "970407" },
            { "BIDV", "970418" },
            { "Vietinbank", "970415" },
            { "CTG", "970415" },
            { "Agribank", "970405" },
            { "VBA", "970405" },
            { "ACB", "970416" },
            { "VPB", "970443" },
            { "VPBank", "970443" },
            { "STB", "970403" },
            { "Sacombank", "970403" },
            { "TPB", "970423" },
            { "TPBank", "970423" },
            { "SHB", "970448" },
            { "VIB", "970441" },
            { "MSB", "970426" }
        };

        public string GenerateVietQRString(string bankIdOrBin, string accountNumber, decimal amount, string memo)
        {
            string bin = BankBinMap.TryGetValue(bankIdOrBin, out var foundBin) ? foundBin : bankIdOrBin;

            // Field 00: Payload Format Indicator (000201)
            string f00 = "000201";

            // Field 01: Point of Initiation Method (010212) -> 12 means dynamic (amount included)
            string f01 = "010212";

            // Field 38: Merchant Account Information (Domestic Payment Network - NAPAS)
            string subGUID = "0014A0000000720011";
            string subSubBin = $"0006{bin}";
            string subSubAcc = $"01{accountNumber.Length:D2}{accountNumber}";
            string subBeneficiaryVal = subSubBin + subSubAcc;
            string subBeneficiary = $"01{subBeneficiaryVal.Length:D2}{subBeneficiaryVal}";
            string subService = "0208QRIBFTTA";

            string f38Val = subGUID + subBeneficiary + subService;
            string f38 = $"38{f38Val.Length:D2}{f38Val}";

            // Field 53: Transaction Currency (5303704)
            string f53 = "5303704";

            // Field 54: Transaction Amount
            string amountStr = amount.ToString("0");
            string f54 = $"54{amountStr.Length:D2}{amountStr}";

            // Field 58: Country Code (5802VN)
            string f58 = "5802VN";

            // Field 62: Additional Data Field Template
            string unsignedMemo = RemoveSign4VietnameseString(memo);
            if (unsignedMemo.Length > 25) unsignedMemo = unsignedMemo.Substring(0, 25);
            string subMemo = $"08{unsignedMemo.Length:D2}{unsignedMemo}";
            string f62 = $"62{subMemo.Length:D2}{subMemo}";

            // Combine all fields to calculate CRC
            string rawData = f00 + f01 + f38 + f53 + f54 + f58 + f62 + "6304";
            ushort crc = CalculateCRC16(rawData);
            string f63 = $"6304{crc:X4}";

            return rawData + f63;
        }

        public byte[] GenerateQRCodePNGBytes(string content)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(20);
        }

        private static ushort CalculateCRC16(string input)
        {
            ushort crc = 0xFFFF;
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            foreach (byte b in bytes)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    }
                    else
                    {
                        crc <<= 1;
                    }
                }
            }
            return crc;
        }

        private static string RemoveSign4VietnameseString(string str)
        {
            string[] arr1 = new string[] { "á", "à", "ả", "ã", "ạ", "â", "ấ", "ầ", "ẩ", "ẫ", "ậ", "ă", "ắ", "ằ", "ẳ", "ẵ", "ặ",
                "đ",
                "é", "è", "ẻ", "ẽ", "ẹ", "ê", "ế", "ề", "ể", "ễ", "ệ",
                "í", "ì", "ỉ", "ĩ", "ị",
                "ó", "ò", "ỏ", "õ", "ọ", "ô", "ố", "ồ", "ổ", "ỗ", "ộ", "ơ", "ớ", "ờ", "ở", "ỡ", "ợ",
                "ú", "ù", "ủ", "ũ", "ụ", "ư", "ứ", "ừ", "ử", "ữ", "ự",
                "ý", "ỳ", "ỷ", "ỹ", "ỵ",
                "Á", "À", "Ả", "Ã", "Ạ", "Â", "Ấ", "Ầ", "Ẩ", "Ẫ", "Ậ", "Ă", "Ắ", "Ằ", "Ẳ", "Ẵ", "Ặ",
                "Đ",
                "É", "È", "E", "Ẽ", "Ẹ", "Ê", "Ế", "Ề", "Ể", "Ễ", "Ệ",
                "Í", "Ì", "Ỉ", "Ĩ", "Ị",
                "Ó", "Ò", "Ỏ", "Õ", "Ọ", "Ô", "Ố", "Ồ", "Ổ", "Ỗ", "Ộ", "Ơ", "Ớ", "Ờ", "Ở", "Ỡ", "Ợ",
                "Ú", "Ù", "Ủ", "Ũ", "Ụ", "Ư", "Ứ", "Ừ", "Ử", "Ữ", "Ự",
                "Ý", "Ỳ", "Ỷ", "Ỹ", "Ỵ" };
            string[] arr2 = new string[] { "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a",
                "d",
                "e", "e", "e", "e", "e", "e", "e", "e", "e", "e", "e",
                "i", "i", "i", "i", "i",
                "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o",
                "u", "u", "u", "u", "u", "u", "u", "u", "u", "u", "u",
                "y", "y", "y", "y", "y",
                "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A",
                "D",
                "E", "E", "E", "E", "E", "E", "E", "E", "E", "E", "E",
                "I", "I", "I", "I", "I",
                "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O",
                "U", "U", "U", "U", "U", "U", "U", "U", "U", "U", "U",
                "Y", "Y", "Y", "Y", "Y" };
            for (int i = 0; i < arr1.Length; i++)
            {
                str = str.Replace(arr1[i], arr2[i]);
            }
            return str;
        }
    }
}
