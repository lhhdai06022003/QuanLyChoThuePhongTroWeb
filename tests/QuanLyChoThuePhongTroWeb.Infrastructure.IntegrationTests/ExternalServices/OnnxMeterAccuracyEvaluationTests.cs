using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants;
using Xunit;
using Xunit.Abstractions;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    /// <summary>
    /// Chỉ chạy khi có QLCTPT_METER_EVAL_DIR chứa labels.csv (tep,loai,gia_tri). Thiếu thì bị bỏ qua: CHƯA CHẠY.
    /// </summary>
    public sealed class MeterEvalFactAttribute : FactAttribute
    {
        public MeterEvalFactAttribute()
        {
            var d = Environment.GetEnvironmentVariable("QLCTPT_METER_EVAL_DIR");
            if (string.IsNullOrWhiteSpace(d) || !File.Exists(Path.Combine(d, "labels.csv")))
            {
                Skip = "Thiếu QLCTPT_METER_EVAL_DIR hoặc labels.csv: CHƯA CHẠY";
            }
        }
    }

    /// <summary>
    /// Đo độ chính xác model đọc chỉ số trên bộ ảnh có nhãn, để người dùng chọn ngưỡng tự xác nhận.
    /// Chỉ xuất số liệu, không assert ngưỡng và không tự bật cờ TuXacNhanAnhAi.
    /// </summary>
    public class OnnxMeterAccuracyEvaluationTests
    {
        private static readonly double[] Thresholds = { 0.5, 0.6, 0.7, 0.75, 0.8, 0.9, 0.95 };
        private readonly ITestOutputHelper _output;

        public OnnxMeterAccuracyEvaluationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private sealed record Label(string File, bool IsElectric, decimal Value);

        private sealed record Sample(bool Correct, double Confidence);

        [MeterEvalFact]
        public async Task EvaluateAccuracy_OnLabeledImages()
        {
            var dir = Environment.GetEnvironmentVariable("QLCTPT_METER_EVAL_DIR")!;
            var labels = ReadLabels(Path.Combine(dir, "labels.csv"));
            Assert.NotEmpty(labels);

            using var scanner = new YoloMeterScannerService(NullLogger<YoloMeterScannerService>.Instance);
            var electric = new List<Sample>();
            var water = new List<Sample>();

            foreach (var label in labels)
            {
                var path = Path.Combine(dir, label.File);
                Assert.True(File.Exists(path), $"Không thấy ảnh {label.File}");

                await using var stream = File.OpenRead(path);
                var result = await scanner.QuetAnhDongHoAsync(stream, label.IsElectric);

                var read = result.DocThanhCong && !result.ChupNhamLoaiDongHo && result.ChiSoDocDuoc.HasValue;
                var correct = read && result.ChiSoDocDuoc!.Value == label.Value;
                var sample = new Sample(correct, read ? result.DoTinCayThapNhat : 0);
                (label.IsElectric ? electric : water).Add(sample);
            }

            Report("ĐIỆN", electric);
            Report("NƯỚC", water);
            _output.WriteLine("Điều kiện đề xuất để bật TuXacNhanAnhAi: tỷ lệ đúng trong nhóm đạt ngưỡng >= 99%. Người dùng tự quyết, bài đo không bật cờ.");
        }

        private void Report(string name, IReadOnlyList<Sample> samples)
        {
            _output.WriteLine($"=== {name}: {samples.Count} ảnh ===");
            if (samples.Count == 0)
            {
                return;
            }

            _output.WriteLine($"Tỷ lệ đúng toàn số: {Percent(samples.Count(s => s.Correct), samples.Count)}");
            foreach (var t in Thresholds)
            {
                var passed = samples.Where(s => s.Confidence >= t).ToList();
                _output.WriteLine(
                    $"Ngưỡng {t.ToString("0.00", CultureInfo.InvariantCulture)}: đạt ngưỡng {Percent(passed.Count, samples.Count)}; " +
                    $"đúng trong nhóm đạt {(passed.Count == 0 ? "n/a" : Percent(passed.Count(s => s.Correct), passed.Count))}");
            }
        }

        private static string Percent(int part, int total) =>
            (total == 0 ? 0 : 100.0 * part / total).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        private static List<Label> ReadLabels(string path)
        {
            var result = new List<Label>();
            foreach (var line in File.ReadLines(path).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split(',');
                Assert.True(parts.Length >= 3, $"Dòng nhãn không hợp lệ: {line}");
                var loai = parts[1].Trim().ToLowerInvariant();
                Assert.True(loai is "dien" or "nuoc", $"Cột loai phải là dien hoặc nuoc: {line}");
                Assert.True(decimal.TryParse(parts[2].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value), $"gia_tri không hợp lệ: {line}");
                result.Add(new Label(parts[0].Trim(), loai == "dien", value));
            }

            return result;
        }
    }
}
