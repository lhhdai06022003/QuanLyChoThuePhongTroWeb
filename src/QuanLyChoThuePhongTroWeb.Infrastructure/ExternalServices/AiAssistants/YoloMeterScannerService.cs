using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

[assembly: InternalsVisibleTo("QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests")]

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants
{
    public sealed class YoloMeterScannerService : IMeterScannerService, IDisposable
    {
        private const int InputSize = 640;
        private const float ConfidenceThreshold = 0.25f;
        private const float NmsIoUThreshold = 0.45f;
        private const double HighConfidencePassThreshold = 0.80;
        private const long MaxPixels = 40_000_000;

        private readonly ILogger<YoloMeterScannerService> _logger;
        private readonly InferenceSession? _session;
        private readonly string _inputName = "images";
        private readonly object _sessionLock = new();

        public YoloMeterScannerService(ILogger<YoloMeterScannerService> logger, string? customModelPath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var modelPath = ResolveModelPath(customModelPath);

            if (!File.Exists(modelPath))
            {
                _logger.LogWarning("Không tìm thấy file model AI tại: {ModelPath}. Dịch vụ quét AI tạm thời bị vô hiệu hóa.", modelPath);
                return;
            }

            try
            {
                var options = CreateSessionOptions();
                _session = new InferenceSession(modelPath, options);
                _inputName = _session.InputMetadata.Keys.FirstOrDefault() ?? "images";
                WarmUpModel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khởi tạo InferenceSession cho model ONNX tại {ModelPath}.", modelPath);
            }
        }

        public async Task<MeterScanResultDto> QuetAnhDongHoAsync(
            Stream imageStream,
            bool yeuCauDongHoDien,
            CancellationToken cancellationToken = default)
        {
            if (_session is null)
            {
                return new MeterScanResultDto
                {
                    DocThanhCong = false,
                    ThongBaoLoi = "Model AI nhận diện đồng hồ chưa sẵn sàng trên hệ thống.",
                    LoiHeThong = true
                };
            }

            if (imageStream is null || !imageStream.CanRead)
            {
                return new MeterScanResultDto
                {
                    DocThanhCong = false,
                    ThongBaoLoi = "Luồng dữ liệu ảnh không hợp lệ."
                };
            }

            if (!imageStream.CanSeek)
            {
                using var buffered = new MemoryStream();
                await imageStream.CopyToAsync(buffered, cancellationToken);
                buffered.Position = 0;
                return await QuetAnhDongHoAsync(buffered, yeuCauDongHoDien, cancellationToken);
            }

            // Giới hạn dung lượng chỉ tính trên tệp nén: kiểm số điểm ảnh trước khi giải mã vào bộ nhớ.
            imageStream.Position = 0;
            var imageInfo = await Image.IdentifyAsync(imageStream, cancellationToken);
            if ((long)imageInfo.Width * imageInfo.Height > MaxPixels)
            {
                return new MeterScanResultDto
                {
                    DocThanhCong = false,
                    ThongBaoLoi = "Ảnh có kích thước quá lớn. Vui lòng chụp lại hoặc giảm kích thước ảnh."
                };
            }

            imageStream.Position = 0;
            using var originalImage = await Image.LoadAsync<Rgb24>(imageStream, cancellationToken);
            originalImage.Mutate(x => x.AutoOrient());

            // Lần quét 1: Ảnh gốc chuẩn hóa Letterbox 640x640
            var pass1Result = RunSinglePass(originalImage, yeuCauDongHoDien, isEnhancedPass: false);

            if (pass1Result.DocThanhCong &&
                !pass1Result.ChupNhamLoaiDongHo &&
                pass1Result.DoTinCayThapNhat >= HighConfidencePassThreshold)
            {
                return pass1Result;
            }

            // Lần quét 2 (Dual-Pass Fallback): Tăng tương phản + làm nét khi ảnh mờ/lóa kính đồng hồ
            using var enhancedImage = originalImage.Clone(ctx => ctx
                .Contrast(1.35f)
                .GaussianSharpen(1.2f));

            var pass2Result = RunSinglePass(enhancedImage, yeuCauDongHoDien, isEnhancedPass: true);

            // Chọn kết quả tốt hơn giữa 2 lần quét
            if (!pass1Result.DocThanhCong && pass2Result.DocThanhCong)
            {
                return pass2Result;
            }

            if (pass2Result.DocThanhCong && pass2Result.DoTinCayThapNhat > pass1Result.DoTinCayThapNhat)
            {
                return pass2Result;
            }

            return pass1Result;
        }

        private MeterScanResultDto RunSinglePass(Image<Rgb24> sourceImage, bool yeuCauDongHoDien, bool isEnhancedPass)
        {
            var tensor = PreprocessLetterbox(sourceImage);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, tensor)
            };

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;
            lock (_sessionLock)
            {
                results = _session!.Run(inputs);
            }

            using (results)
            {
                var outputTensor = results.First().AsTensor<float>();
                var rawDetections = ParseYoloOutput(outputTensor);

                // 1. Tách nhóm NMS độc lập để decimal_zone (Class 12) không xóa mất chữ số (Class 2..11)
                var meterBoxes = ApplyNms(rawDetections.Where(d => d.ClassId is 0 or 1).ToList());
                var digitBoxes = ApplyNms(rawDetections.Where(d => d.ClassId is >= 2 and <= 11).ToList());
                var decimalZones = ApplyNms(rawDetections.Where(d => d.ClassId == 12).ToList());

                // 2. Kiểm tra loại đồng hồ (Class 0: Điện, Class 1: Nước)
                bool? phatHienLaDongHoDien = null;
                bool chupNhamLoai = false;

                var bestMeterBox = meterBoxes.OrderByDescending(b => b.Confidence).FirstOrDefault();
                if (bestMeterBox is not null)
                {
                    phatHienLaDongHoDien = bestMeterBox.ClassId == 0;
                    chupNhamLoai = phatHienLaDongHoDien.Value != yeuCauDongHoDien;
                }

                if (chupNhamLoai)
                {
                    return new MeterScanResultDto
                    {
                        DocThanhCong = false,
                        PhatHienLaDongHoDien = phatHienLaDongHoDien,
                        ChupNhamLoaiDongHo = true,
                        DaDungQuetKepTangCuong = isEnhancedPass,
                        ThongBaoLoi = yeuCauDongHoDien
                            ? "Ảnh tải lên là Đồng hồ Nước nhưng bạn đang chốt số Điện."
                            : "Ảnh tải lên là Đồng hồ Điện nhưng bạn đang chốt số Nước."
                    };
                }

                if (bestMeterBox is null)
                {
                    return new MeterScanResultDto
                    {
                        DocThanhCong = false,
                        PhatHienLaDongHoDien = null,
                        ChupNhamLoaiDongHo = false,
                        DaDungQuetKepTangCuong = isEnhancedPass,
                        ThongBaoLoi = "Không nhận ra mặt đồng hồ trong ảnh"
                    };
                }

                if (digitBoxes.Count == 0)
                {
                    return new MeterScanResultDto
                    {
                        DocThanhCong = false,
                        PhatHienLaDongHoDien = phatHienLaDongHoDien,
                        ChupNhamLoaiDongHo = false,
                        DaDungQuetKepTangCuong = isEnhancedPass,
                        ThongBaoLoi = "Không nhận diện được dãy chữ số trên mặt đồng hồ."
                    };
                }

                // 3. Sắp xếp chữ số từ trái sang phải theo tọa độ ngang CenterX
                var sortedDigits = digitBoxes.OrderBy(b => b.CenterX).ToList();
                var primaryDecimalZone = decimalZones.OrderByDescending(z => z.Confidence).FirstOrDefault();

                var integerPart = new List<char>();
                var fractionalPart = new List<char>();

                foreach (var digitBox in sortedDigits)
                {
                    int digitValue = digitBox.ClassId - 2; // Class 2..11 -> Chữ số 0..9
                    char digitChar = (char)('0' + digitValue);

                    bool isInDecimalZone = primaryDecimalZone is not null &&
                                           digitBox.CenterX >= primaryDecimalZone.XMin &&
                                           digitBox.CenterX <= primaryDecimalZone.XMax;

                    if (isInDecimalZone)
                    {
                        fractionalPart.Add(digitChar);
                    }
                    else
                    {
                        integerPart.Add(digitChar);
                    }
                }

                string rawNumberString = integerPart.Count > 0
                    ? new string(integerPart.ToArray())
                    : "0";

                if (fractionalPart.Count > 0)
                {
                    rawNumberString += "." + new string(fractionalPart.ToArray());
                }

                if (!decimal.TryParse(rawNumberString, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedValue))
                {
                    return new MeterScanResultDto
                    {
                        DocThanhCong = false,
                        ChuoiSoTho = rawNumberString,
                        PhatHienLaDongHoDien = phatHienLaDongHoDien,
                        DaDungQuetKepTangCuong = isEnhancedPass,
                        ThongBaoLoi = $"Không thể chuyển đổi chuỗi số '{rawNumberString}' thành số hợp lệ."
                    };
                }

                double minConf = sortedDigits.Min(d => (double)d.Confidence);
                decimal roundedValue = Math.Round(parsedValue, 3, MidpointRounding.AwayFromZero);

                return new MeterScanResultDto
                {
                    DocThanhCong = true,
                    ChiSoDocDuoc = roundedValue,
                    ChuoiSoTho = rawNumberString,
                    DoTinCayThapNhat = Math.Round(minConf, 4),
                    PhatHienLaDongHoDien = phatHienLaDongHoDien,
                    ChupNhamLoaiDongHo = false,
                    DaDungQuetKepTangCuong = isEnhancedPass
                };
            }
        }

        internal static DenseTensor<float> PreprocessLetterbox(Image<Rgb24> src)
        {
            float scale = Math.Min((float)InputSize / src.Width, (float)InputSize / src.Height);
            int newWidth = (int)Math.Round(src.Width * scale);
            int newHeight = (int)Math.Round(src.Height * scale);
            int padX = (InputSize - newWidth) / 2;
            int padY = (InputSize - newHeight) / 2;

            using var canvas = new Image<Rgb24>(InputSize, InputSize, new Rgb24(114, 114, 114));
            using var resized = src.Clone(ctx => ctx.Resize(newWidth, newHeight));
            canvas.Mutate(ctx => ctx.DrawImage(resized, new Point(padX, padY), 1f));

            var tensor = new DenseTensor<float>(new[] { 1, 3, InputSize, InputSize });

            // Ghi thẳng vào bộ đệm [1, 3, H, W] (R, G, B liền nhau theo kênh): indexer nhiều chiều cấp phát mảng chỉ số mỗi lần gọi.
            canvas.ProcessPixelRows(accessor =>
            {
                const int plane = InputSize * InputSize;
                var buffer = tensor.Buffer.Span;
                for (int y = 0; y < InputSize; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    int offset = y * InputSize;
                    for (int x = 0; x < InputSize; x++)
                    {
                        var pixel = row[x];
                        buffer[offset + x] = pixel.R / 255f;
                        buffer[plane + offset + x] = pixel.G / 255f;
                        buffer[2 * plane + offset + x] = pixel.B / 255f;
                    }
                }
            });

            return tensor;
        }

        internal static List<BoundingBox> ParseYoloOutput(Tensor<float> output)
        {
            // YOLOv8 output tensor shape: [1, 17, 8400] (4 box coords + 13 classes)
            int dimensions = output.Dimensions[1]; // 17
            int numAnchors = output.Dimensions[2]; // 8400
            int numClasses = dimensions - 4;       // 13

            // Đọc thẳng bộ đệm: phần tử [0, r, i] nằm ở r * numAnchors + i.
            var data = (output as DenseTensor<float> ?? output.ToDenseTensor()).Buffer.Span;
            var detections = new List<BoundingBox>();

            for (int i = 0; i < numAnchors; i++)
            {
                float maxScore = 0f;
                int bestClassId = -1;

                for (int c = 0; c < numClasses; c++)
                {
                    float score = data[(4 + c) * numAnchors + i];
                    if (score > maxScore)
                    {
                        maxScore = score;
                        bestClassId = c;
                    }
                }

                if (maxScore < ConfidenceThreshold || bestClassId < 0)
                {
                    continue;
                }

                float cx = data[i];
                float cy = data[numAnchors + i];
                float w = data[2 * numAnchors + i];
                float h = data[3 * numAnchors + i];

                detections.Add(new BoundingBox(
                    ClassId: bestClassId,
                    Confidence: maxScore,
                    CenterX: cx,
                    CenterY: cy,
                    Width: w,
                    Height: h));
            }

            return detections;
        }

        private static List<BoundingBox> ApplyNms(List<BoundingBox> boxes)
        {
            var sorted = boxes.OrderByDescending(b => b.Confidence).ToList();
            var selected = new List<BoundingBox>();

            while (sorted.Count > 0)
            {
                var current = sorted[0];
                selected.Add(current);
                sorted.RemoveAt(0);

                sorted.RemoveAll(other => CalculateIoU(current, other) > NmsIoUThreshold);
            }

            return selected;
        }

        private static float CalculateIoU(BoundingBox a, BoundingBox b)
        {
            float xA = Math.Max(a.XMin, b.XMin);
            float yA = Math.Max(a.YMin, b.YMin);
            float xB = Math.Min(a.XMax, b.XMax);
            float yB = Math.Min(a.YMax, b.YMax);

            float interWidth = Math.Max(0f, xB - xA);
            float interHeight = Math.Max(0f, yB - yA);
            float interArea = interWidth * interHeight;

            if (interArea <= 0f)
            {
                return 0f;
            }

            float unionArea = a.Area + b.Area - interArea;
            return unionArea > 0f ? interArea / unionArea : 0f;
        }

        private SessionOptions CreateSessionOptions()
        {
            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    options.AppendExecutionProvider_DML(0);
                    _logger.LogInformation("ONNX Runtime: Đã kích hoạt tăng tốc phần cứng GPU (Windows DirectML).");
                    return options;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể bật DirectML GPU, tự động chuyển sang chế độ CPU.");
                }
            }

            _logger.LogInformation("ONNX Runtime: Đang chạy ở chế độ CPU tiêu chuẩn.");
            return options;
        }

        private void WarmUpModel()
        {
            if (_session is null) return;

            var dummyTensor = new DenseTensor<float>(new[] { 1, 3, InputSize, InputSize });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, dummyTensor)
            };

            lock (_sessionLock)
            {
                using var _ = _session.Run(inputs);
            }

            _logger.LogInformation("ONNX Runtime: Hoàn tất WarmUp model meter_scanner.onnx.");
        }

        private static string ResolveModelPath(string? customModelPath)
        {
            if (!string.IsNullOrWhiteSpace(customModelPath) && File.Exists(customModelPath))
            {
                return customModelPath;
            }

            // 1. Thử các đường dẫn trực tiếp (khi chạy trong Web hoặc Docker container)
            var directCandidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "AIModels", "meter_scanner.onnx"),
                Path.Combine(AppContext.BaseDirectory, "wwwroot", "AIModels", "meter_scanner.onnx"),
                Path.Combine(Directory.GetCurrentDirectory(), "src", "QuanLyChoThuePhongTroWeb.Web", "wwwroot", "AIModels", "meter_scanner.onnx"),
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "models", "meter_scanner.onnx"),
                Path.Combine(AppContext.BaseDirectory, "wwwroot", "models", "meter_scanner.onnx")
            };

            foreach (var path in directCandidates)
            {
                if (File.Exists(path)) return path;
            }

            // 2. Tìm ngược lên từ BaseDirectory hoặc CurrentDirectory đến thư mục gốc chứa src/
            foreach (var startDir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var current = new DirectoryInfo(startDir);
                while (current != null && current.Exists)
                {
                    var checkPath = Path.Combine(current.FullName, "src", "QuanLyChoThuePhongTroWeb.Web", "wwwroot", "AIModels", "meter_scanner.onnx");
                    if (File.Exists(checkPath)) return checkPath;

                    var checkPathAlt = Path.Combine(current.FullName, "wwwroot", "AIModels", "meter_scanner.onnx");
                    if (File.Exists(checkPathAlt)) return checkPathAlt;

                    current = current.Parent;
                }
            }

            return directCandidates[0];
        }

        public void Dispose()
        {
            _session?.Dispose();
        }

        internal sealed record BoundingBox(
            int ClassId,
            float Confidence,
            float CenterX,
            float CenterY,
            float Width,
            float Height)
        {
            public float XMin => CenterX - (Width / 2f);
            public float YMin => CenterY - (Height / 2f);
            public float XMax => CenterX + (Width / 2f);
            public float YMax => CenterY + (Height / 2f);
            public float Area => Width * Height;
        }
    }
}
