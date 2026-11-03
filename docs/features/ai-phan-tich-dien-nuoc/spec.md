# Spec: AI đọc chỉ số điện nước, cảnh báo theo mức và xử lý đổi đồng hồ

- **Trạng thái:** v1, người dùng đã duyệt (02/11/2026), kèm các bổ sung ở mục 10. Đang triển khai qua dây chuyền `/ship`.
- **Nhánh:** `feature/ai-phan-tich-dien-nuoc`.
- **Thay thế:** `SPEC_METER_ANOMALY_4_TIERS.md` ở thư mục gốc (không xóa file đó; khi mâu thuẫn, tin spec này).
- **Bổ sung cho:** [duyệt hóa đơn tự động](../duyet-hoa-don-tu-dong/spec.md). Các ngưỡng ở mục 2.2 của spec đó giữ nguyên.
- **Schema:** không đổi. `has-pending-model-changes` phải báo không có thay đổi.

## 1. Mục tiêu

Nhân viên chụp ảnh đồng hồ, model ONNX điền sẵn số, hệ thống cảnh báo số đáng ngờ, nhân viên xác nhận, hóa đơn tính đúng tiền. Docker dùng để chạy demo trên máy.

### Quyết định đã chốt

| Chủ đề | Quyết định |
|---|---|
| Đọc ảnh | Chỉ dùng model ONNX. Gỡ Gemini khỏi đọc ảnh; trợ lý AI vẫn dùng `GeminiChatModel`. |
| Mức tự động | AI chỉ gợi ý số cho tới khi đo được độ chính xác. Nhân viên xác nhận xong thì hóa đơn nháp vẫn tự tạo. |
| Bộ quy tắc | Một bộ duy nhất, mở rộng `MeterAnomalyPolicy`, giữ ngưỡng trung vị. Gỡ `MeterAnomalyDetector`. |
| Độ chính xác số | Lưu tối đa 3 chữ số thập phân, so sánh trên giá trị chính xác, chỉ làm tròn khi hiển thị. |
| Đổi đồng hồ, quay vòng | Kỳ đó chỉ lưu số của đồng hồ đang lắp. Phần trước đó do nhân viên thêm thành dòng truy thu trên hóa đơn. |
| Kẹt số | Chỉ cảnh báo, không có thao tác riêng. Muốn thu thì thêm dòng ước tính trên hóa đơn. |
| Luồng chốt số | Giữ luồng tải ảnh → Cloudinary → OCR → xác nhận. Không thêm endpoint quét thử. |
| Docker | Chỉ demo cục bộ, cổng `5009`, không sửa `Program.cs`. |

### Ngoài phạm vi

- Huấn luyện lại model; kiểm thử tay trên ảnh thật do người dùng thực hiện.
- Bật tự xác nhận (chỉ thêm cờ, bật lại là quyết định riêng sau khi đo).
- Quản lý từng đồng hồ, số seri, báo cáo lịch sử thay đồng hồ.
- Hệ thống tự kiểm tra số tiền truy thu.
- Deploy thật: HTTPS, lưu khóa Data Protection, volume cho `sent_*.json`.

## 2. Model và kết quả đọc

### 2.1 `YoloMeterScannerService`

Giữ model, lớp nhận diện và cách quét hai lượt hiện có. Thay đổi:

1. **Độ tin cậy** lấy theo chữ số có độ tin cậy thấp nhất, thay cho trung bình.
2. **Bắt buộc thấy khung đồng hồ.** Không có khung lớp 0 hoặc 1 thì `DocThanhCong = false`, thông báo "Không nhận ra mặt đồng hồ trong ảnh".
3. **Không làm tròn về 2 chữ số.** Kết quả có hơn 3 chữ số thập phân được làm tròn về 3 để qua được `MeterOcrResult.Readable`. `ChuoiSoTho` giữ chuỗi đọc được.
4. **Phân biệt lỗi hệ thống.** Thêm `LoiHeThong` vào `MeterScanResultDto`, đặt `true` khi thiếu model hoặc model khởi tạo lỗi.

### 2.2 `OnnxMeterOcrService`

Bỏ tham số `GeminiMeterOcrService` và mọi nhánh dự phòng.

| Kết quả quét | `MeterOcrResult` | Trạng thái ảnh |
|---|---|---|
| Đọc được | `Readable(giá trị, độ tin cậy)` | `DocDuoc` |
| Chụp nhầm loại đồng hồ | `Unreadable(thông báo)` | `KhongDocDuoc`, hiện đúng lý do |
| Mờ, không thấy số hoặc khung đồng hồ | `Unreadable(thông báo)` | `KhongDocDuoc` |
| `LoiHeThong = true` hoặc có ngoại lệ, kể cả ảnh không giải mã được | `Failed(thông báo)` | `Loi`, thử lại được |

Ảnh `KhongDocDuoc` và `Loi` vẫn xác nhận tay được kèm lý do bắt buộc theo quy tắc hiện có.

### 2.3 Gỡ Gemini khỏi đọc ảnh

Xóa `GeminiMeterOcrService.cs`, `GeminiMeterOcrServiceTests.cs` và dòng `AddHttpClient<GeminiMeterOcrService>`. Giữ `GeminiChatModel` và cấu hình `Gemini:*`.

### 2.4 Cờ `TuXacNhanAnhAi`

Thêm `AutoInvoiceOptions.TuXacNhanAnhAi` kiểu `bool`, mặc định `false`, và thêm vào `appsettings.example.json`. Không sửa `appsettings.json` của người dùng.

Trong `AutoInvoiceReadingPolicy.EvaluateMeter`, nhánh ảnh chưa xác nhận giữ nguyên các kiểm tra độ tin cậy và bất thường, để khách thuê vẫn nhận các thông báo hiện có. Sau đó:

- Có bất thường: xử lý như hiện tại.
- Không bất thường, cờ tắt: trả `ThieuChiSo` với mô tả "ảnh chờ nhân viên xác nhận"; không tạo hóa đơn và không gửi thông báo vì `ThieuChiSo` là lý do im lặng.
- Không bất thường, cờ bật: tự xác nhận như hiện tại.

Ảnh đã xác nhận và kỳ đã duyệt không bị ảnh hưởng. `MeterImagePortalService` đã gọi `RunForRoomAsync` sau khi xác nhận (`AutoInvoiceTrigger.XacNhanAnh`), nên hóa đơn nháp vẫn tự tạo.

### 2.5 Đo độ chính xác

Thêm test đo ở `Infrastructure.IntegrationTests`, không thêm script PowerShell:

- Đọc thư mục ảnh từ biến môi trường `QLCTPT_METER_EVAL_DIR`, kèm `labels.csv` gồm `tep,loai,gia_tri`. Thiếu biến thì test ở trạng thái **Skip**, ghi là CHƯA CHẠY, không được tính là PASS.
- Bộ ảnh do người dùng cung cấp, không đưa vào repo, không trùng ảnh huấn luyện; đề xuất ít nhất khoảng 100 ảnh mỗi loại, gồm cả ảnh mờ và lóa.
- Báo cáo riêng cho điện và nước: tỷ lệ đọc đúng toàn bộ số, và với mỗi ngưỡng: tỷ lệ ảnh đạt ngưỡng cùng tỷ lệ đọc đúng trong nhóm đó.
- Đề xuất điều kiện bật lại cờ: tỷ lệ đọc đúng trong nhóm đạt ngưỡng từ 99% trở lên. Test chỉ báo số liệu, không tự bật cờ.

## 3. Bộ quy tắc chung

### 3.1 Ranh giới

- `MeterAnomalyPolicy.Evaluate` vẫn trả `null` khi hợp lệ, và kết quả khác `null` vẫn nghĩa là bất thường cần ghi chú. Bước xác nhận, hóa đơn tự động và điều chỉnh hóa đơn tiếp tục dùng hàm này.
- `MeterAnomalyResult` thêm dữ liệu tùy chọn: `MucDo`, `ChiSoGoiY`, `SanLuongTheoGoiY`, `TranCongTo`.
- Các ca "chỉ nhắc" không đi qua `Evaluate`, để không làm dừng hóa đơn tự động và không bắt buộc ghi chú.
- Gỡ `Application/Services/Meters/`, `Application/Common/Models/Meters/` và `MeterAnomalyDetectorTests`.
- Test hiện có của `MeterAnomalyPolicyTests` phải tiếp tục PASS, trừ khi thêm dữ liệu mới vào kết quả.

### 3.2 Mức cảnh báo

| Mức | Ca | Lưu thế nào | Nơi kiểm tra |
|---|---|---|---|
| 🔴 Chặn | `ChiSoGiam` | Sửa số hoặc dùng thao tác ở mục 4; ghi chú không vượt qua được | Server |
| 🔴 Đỏ, cần ghi chú | `NghiSaiChuSo`; `TieuThuBatThuong` với M > 0 và tiêu thụ ≥ M × 3 | Sửa số hoặc ghi lý do | Server |
| 🔴 Đỏ, cần ghi chú | Chụp nhầm loại đồng hồ | Chụp lại, hoặc xác nhận tay kèm lý do | Server, qua trạng thái `KhongDocDuoc` |
| 🟡 Vàng, cần ghi chú | `TieuThuBatThuong` còn lại, `TieuThuThapBatThuong` | Ghi lý do | Server |
| 🟡 Vàng, chỉ nhắc | Độ tin cậy dưới `NguongDoTinCay` khi ô nhập đang đúng số AI; tiêu thụ bằng 0 mà `Evaluate` trả `null` | Xem lại rồi lưu | Giao diện |
| 🟢 Xanh | Không có ca nào | Lưu ngay | |

Khi tiêu thụ bằng 0 và lịch sử có M đủ lớn, `TieuThuThapBatThuong` đã xử lý với mô tả có thêm gợi ý kiểm tra kẹt số hoặc ảnh cũ. Hóa đơn tự động vẫn dừng ở mọi ca `Evaluate` trả khác `null`.

### 3.3 Trần cho phép

`TranChoPhep` dùng cho gợi ý ở mục 3.4:

- Đủ ≥ `SoKyToiThieu` kỳ: `max(M × HeSoTang, M + sàn tăng)`.
- Chưa đủ lịch sử: trần chung của `AutoInvoiceOptions` theo số ngày hợp đồng phủ trong tháng.

### 3.4 Thứ tự kiểm tra và gợi ý

**Khi mới < cũ** (luôn là `ChiSoGiam`):

1. **Quay vòng.** `TranCongTo` là luỹ thừa của 10 nhỏ nhất lớn hơn cũ, tối thiểu 10.000. Gợi ý quay vòng nếu cũ ≥ 90% trần và `(trần − cũ) + mới ≤ TranChoPhep`.
2. **Lệch dấu phẩy.** Thử `mới × k` với k ∈ {10, 100}, chọn k nhỏ nhất có `0 < ứng viên − cũ ≤ TranChoPhep`.
3. Không khớp: chỉ báo số giảm.

Quay vòng phải xét trước: cũ 9950, mới 100 là quay vòng 150 kWh, không phải sửa thành 10000.

**Khi mới ≥ cũ:**

1. **Lệch dấu phẩy.** Nếu `cũ ≥ 100 và mới ≥ cũ × 9`, hoặc tiêu thụ > `TranChoPhep`, thì thử `mới ÷ k` với k ∈ {10, 100, 1000}, chọn k nhỏ nhất có `0 < ứng viên − cũ ≤ TranChoPhep`. Có ứng viên thì trả `NghiSaiChuSo` kèm gợi ý.
2. Điều kiện `cũ ≥ 100 và mới ≥ cũ × 9` không có ứng viên: vẫn `NghiSaiChuSo`, không có gợi ý.
3. Các kiểm tra tiêu thụ giữ nguyên như hiện tại.

Sản lượng gợi ý phải lớn hơn 0. Ứng viên tính trên giá trị chính xác và làm tròn về tối đa 3 chữ số thập phân.

### 3.5 API kiểm tra

`GET /DienNuoc/KiemTraBatThuong` giữ tham số và các trường hiện có, thêm vào mỗi loại đồng hồ: `mucDo`, `chiSoGoiY`, `sanLuongTheoGoiY`, `coTheQuayVong`, `tranCongTo`. `MeterImageVm` thêm `doTinCayThap` do server tính theo `NguongDoTinCay`.

## 4. Đổi đồng hồ và quay vòng

### 4.1 Thao tác

`POST /DienNuoc/AnhChiSo/{id}/DoiMoc`, gọi qua `IMeterImagePortalService`, nghiệp vụ ở `MeterReadingWorkflowService`.

| Trường | Bắt buộc | Quy tắc |
|---|---|---|
| `Loai` | Có | `DoiDongHo` hoặc `QuayVong` |
| `GiaTriHienTai` | Có | ≥ 0, tối đa 3 chữ số thập phân |
| `SoDauDongHoMoi` | `DoiDongHo` | Mặc định 0; `0 ≤ số đầu ≤ GiaTriHienTai`, tối đa 3 chữ số thập phân. `QuayVong` luôn là 0 |
| `SoCuoiDongHoCu` | Không | Chỉ `DoiDongHo`; nếu có thì ≥ chỉ số cũ của kỳ |
| `LyDo` | Có | Không rỗng, tối đa 500 ký tự |

Điều kiện chung:

- Quyền `EmployeeActionCodes.MeterReview` tại chi nhánh của phòng. Khách thuê không gọi được.
- Trạng thái ảnh và khóa kỳ giống `ConfirmImageAsync`: không làm trên ảnh đã xác nhận, đã thay thế, cần chụp lại, đang xử lý; kỳ không có kỳ sau đã khóa hoặc hóa đơn đã phát hành.
- `QuayVong` yêu cầu `GiaTriHienTai < chỉ số cũ` và chỉ số cũ ≥ 90% `TranCongTo`.
- `DoiDongHo` không yêu cầu số giảm, vì đồng hồ đã qua sử dụng có thể có số lớn hơn số cũ.

Khi thành công, trong một transaction:

1. Domain thêm `DatLaiMocDongHo(loai, moc, moi)`: đặt `Cũ = mốc`, `Mới = GiaTriHienTai` của đúng loại đồng hồ và giữ nguyên loại còn lại.
2. Xác nhận ảnh làm chỉ số chính thức, bỏ chọn ảnh chính thức cũ cùng loại.
3. Ghi `GhiChuXacNhan`, tối đa 1000 ký tự, do server dựng theo mẫu:

   `[DOI_DONG_HO] Chỉ số cũ 4.520; số đầu 0; số cuối đồng hồ cũ 4.565,25; phần đồng hồ cũ 45,25 kWh; lý do: ...`

   `[QUAY_VONG] Chỉ số cũ 9.950; trần 10.000; phần trước mốc 50 kWh; lý do: ...`

   Không có số cuối thì ghi "không có số cuối đồng hồ cũ".
4. Duyệt lại nếu kỳ đã duyệt, tính lại hóa đơn nháp, cập nhật kỳ sau còn nháp.
5. Sau commit, chạy hóa đơn tự động như khi xác nhận ảnh (`AutoInvoiceTrigger.XacNhanAnh`).

Làm lại hoặc hoàn tác: thực hiện lại trên một ảnh chưa xác nhận. Đổi nhầm thì đặt số đầu bằng `Mới` của kỳ trước, kỳ sẽ trở về trạng thái không đổi mốc.

### 4.2 Nhận biết kỳ đã đổi mốc

Một loại đồng hồ của kỳ được coi là **đã đổi mốc** khi có kỳ trước chưa xóa gần nhất và `Cũ` của kỳ khác `Mới` của kỳ trước. Kỳ đầu tiên không có kỳ trước thì không tính.

Đã rà trong code ngày 02/11/2026: `CreateDraftPeriodAsync` và `CascadeSubsequentPeriodsAsync` là nơi đặt `Cũ` theo `Mới` của kỳ trước; không có giao diện sửa trực tiếp `Cũ`. Dữ liệu cũ vẫn có thể lệch, nên cần rà bằng truy vấn chỉ đọc trước khi gộp. Lệch dữ liệu cũ chỉ gây nhắc thừa hoặc thông báo ở mục 4.3, không làm sai tiền.

### 4.3 Sửa kỳ trước

`CascadeSubsequentPeriodsAsync` nhận `Mới` cũ của kỳ vừa sửa. Nếu `Cũ` của kỳ sau khác giá trị đó thì kỳ sau đã đổi mốc: không ghi đè, dừng transaction và báo "Kỳ MM/YYYY đã đổi đồng hồ hoặc quay vòng, cần xử lý kỳ đó trước". Khi sửa một kỳ, giá trị so sánh cho kỳ tiếp theo vẫn là `Mới` trước khi sửa.

### 4.4 Dòng truy thu

- Nhân viên tự thêm dòng khi sửa hóa đơn. Tên cố định: **"Truy thu điện đồng hồ cũ"** hoặc **"Truy thu nước đồng hồ cũ"**; quay vòng và ước tính kẹt số đặt tên tương tự, bắt đầu bằng "Truy thu". `InvoiceMeterLine.Classify` xếp các tên này vào loại `Khac`, nên `RecalculateDraftInvoices` không ghi đè.
- Giao diện có nút thêm dòng, điền sẵn tên và đơn giá của kỳ. Số lượng do nhân viên nhập, căn cứ vào ghi chú xác nhận.
- Hệ thống chỉ nhắc, không kiểm tra số tiền.
- **Giới hạn đã biết:** `CreateDraftsInCurrentTransactionAsync` tạo hóa đơn từ kỳ và dịch vụ, không chép dòng thêm tay. Hóa đơn bị hủy rồi lập lại bằng luồng tay phải thêm lại dòng truy thu; nhắc ở mục 4.5 vẫn hiện vì được tính từ kỳ. Bản thay thế `-R{n}` khi điều chỉnh hóa đơn đã gửi phải giữ dòng này theo danh sách gửi lên, và phải có test.

### 4.5 Nhắc nhở

Hiện cảnh báo ở màn hình sửa hóa đơn nháp, chi tiết hóa đơn phía quản lý và bước Admin chốt, nội dung lấy từ ghi chú xác nhận của ảnh chính thức:

> Kỳ này có đổi đồng hồ điện: *[ghi chú]*. Kiểm tra đã thêm dòng truy thu nếu cần thu.

Nhắc không chặn gửi duyệt, chốt hay công bố. Khách thuê không thấy nhắc này; khách thấy chỉ số `0 → 85,5` và dòng truy thu trên hóa đơn.

### 4.6 Giới hạn đã chấp nhận

- Lịch sử tiêu thụ của kỳ đổi mốc chỉ tính đồng hồ đang lắp. Trung vị ít bị ảnh hưởng, nhưng kỳ đó có thể bị báo tiêu thụ thấp và dừng hóa đơn tự động; khi đó nhân viên lập tay.
- Chỉ số cũ thật chỉ còn trong ghi chú, không tra cứu theo dữ liệu có cấu trúc được.

## 5. Giao diện

Theme Tabler, class và icon `ti`; chữ hiển thị tiếng Việt.

### 5.1 Modal ảnh chỉ số (`_AnhChiSoModal.cshtml`)

- Thay vùng `#canhBaoBatThuong` bằng hộp cảnh báo đổi màu theo mức ở mục 3.2.
- Có `chiSoGoiY`: nút "Sửa nhanh thành X" chỉ điền số vào ô nhập và kiểm tra lại, không tự lưu.
- `ChiSoGiam`: khóa nút xác nhận, hiện nút "Đổi đồng hồ"; `coTheQuayVong` thì hiện thêm và làm nổi bật "Quay vòng".
- Nút "Đổi đồng hồ" cũng có dạng phụ trong khu xác nhận, cho ca đồng hồ có số lớn hơn.
- Hộp thoại nhập theo mục 4.1 và hiện trước tổng kết: chỉ số sẽ lưu, phần đồng hồ cũ cần truy thu nếu có số cuối.
- Mức cần ghi chú: bắt buộc ghi chú như hiện tại, server vẫn kiểm tra lại.

### 5.2 Bảng chốt số (`DienNuoc/Index.cshtml`)

Dùng chung kết quả API. Số giảm thì chặn lưu và hướng dẫn mở modal ảnh, vì thao tác đổi mốc bắt buộc có ảnh.

### 5.3 Nén ảnh

Nén trên trình duyệt ở modal nhân viên và trang gửi ảnh của khách: chiều lớn nhất 1600 px, JPEG chất lượng 0,85, chỉ áp dụng khi ảnh vượt 1600 px hoặc lớn hơn 2 MB. Không nén được thì gửi ảnh gốc. Giới hạn 5 MB ở server giữ nguyên.

## 6. Docker

- `.dockerignore`: loại `appsettings*.json`, giữ `appsettings.example.json`.
- `docker-compose.yml`: dùng `env_file: .env`, không ghi cứng mật khẩu; map `5009:8080`, mở tại `http://localhost:5009`.
- Thêm `.env.example` chỉ có tên biến. `.env` đã bị git bỏ qua; người dùng tự điền giá trị thật.
- Giữ `ASPNETCORE_ENVIRONMENT=Development`. Không sửa `Program.cs`.
- Không chạy cùng lúc với `dotnet run` profile `http` vì trùng cổng 5009.
- Chấp nhận khi tạo lại container: phải đăng nhập lại, `sent_*.json` có thể mất nên nhắc nhở có thể gửi lặp.
- Model `meter_scanner.onnx` (44,7 MB) được commit vào repo và nằm trong image.

## 7. Dọn dẹp

- Bỏ BOM vừa thêm vào dòng đầu `Infrastructure/DependencyInjection.cs` và dòng trống đã xóa ngoài phạm vi trong `Web.csproj`.
- `OnnxMeterScannerServiceTests`: bỏ `InspectModelMetadata` vì không có assert, cập nhật theo constructor mới.
- Cập nhật `docs/features/README.md`, và mục Hiện trạng của `AGENTS.md` sau khi code xong.

## 8. Kiểm thử

| Tầng | Nội dung |
|---|---|
| `Domain.UnitTests` | `DatLaiMocDongHo`: đúng loại đồng hồ, giữ loại còn lại, từ chối số âm và mới nhỏ hơn mốc |
| `Application.UnitTests` | Mức cảnh báo mục 3.2; gợi ý: 1500 → 16082 gợi ý 1608,2; 520 → 53,4426 gợi ý 534,426; 9950 → 100 là quay vòng; 150 → 1500 không gợi ý sản lượng 0 |
| `Application.UnitTests` | `TuXacNhanAnhAi` tắt và bật; thao tác đổi mốc: quyền, kỳ khóa, trạng thái ảnh, điều kiện quay vòng, ghi chú; cascade dừng ở kỳ đã đổi mốc; `Classify` xếp "Truy thu…" vào `Khac` |
| `Infrastructure.IntegrationTests` | `OnnxMeterOcrService` theo bảng mục 2.2; nhận biết kỳ đổi mốc; dòng truy thu còn nguyên khi tính lại nháp và khi tạo `-R{n}`; test đo model (Skip nếu thiếu bộ ảnh) |
| `Web.IntegrationTests` | API đổi mốc: chặn khác chi nhánh, chặn khách thuê, antiforgery; `KiemTraBatThuong` trả trường mới; xác nhận thiếu ghi chú ở mức cần ghi chú bị từ chối |
| Toàn bộ | `dotnet test` PASS, gồm test kiến trúc; `has-pending-model-changes` không có thay đổi |
| Docker | Build image, mở `http://localhost:5009`, quét một ảnh trên Linux. Thiếu Docker hoặc `.env` thì ghi CHƯA CHẠY |
| Thử tay trên giao diện | Ảnh sai loại; số lệch dấu phẩy và sửa nhanh; số giảm và đổi đồng hồ; hóa đơn có dòng truy thu và nhắc nhở |

## 9. Tiêu chí hoàn thành

1. Không còn lời gọi Gemini khi đọc ảnh; trợ lý AI vẫn hoạt động.
2. Với cờ mặc định, không có ảnh nào được tự xác nhận; xác nhận xong thì hóa đơn nháp được tạo.
3. Giao diện và server cho cùng kết luận trên một chỉ số.
4. Chỉ số giảm không lưu được theo luồng thường, và lưu được qua thao tác đổi mốc với quyền phù hợp.
5. Ví dụ đổi đồng hồ: kỳ lưu `0 → 85,5`, hóa đơn tính 85,5 kWh; thêm dòng truy thu 45,25 kWh thì tổng tiền đúng 130,75 kWh, và dòng này còn nguyên sau khi tính lại nháp.
6. Không đổi schema, không thêm script PowerShell, không sửa `Program.cs` và `appsettings.json`.
7. Secret không nằm trong image Docker.

## 10. Bổ sung đã chốt khi lập kế hoạch (02/11/2026)

1. **Làm lại đổi mốc:** "chỉ số cũ thật" trong ghi chú và kiểm tra là `Mới` của kỳ trước chưa xóa gần nhất; không có kỳ trước thì dùng `Cũ` của kỳ. Ở ca làm lại, hộp thoại có thể tạm lệch; số chính thức là ghi chú server trả về.
2. **Mất ghi chú:** không đổi Domain. Khi sửa số đã xác nhận hoặc duyệt kỳ nhập tay làm mất ghi chú, nhắc vẫn hiện: "Kỳ này có đổi đồng hồ hoặc quay vòng [điện/nước] (không còn ghi chú xác nhận). Kiểm tra đã thêm dòng truy thu nếu cần thu."
3. **Đơn giá lẻ đồng:** nút truy thu điền nguyên đơn giá kỳ, không tự làm tròn. Server từ chối thì nhân viên tự sửa.
4. **Kỳ đầu tiên đổi đồng hồ:** có ghi chú, không có nhắc, theo mục 4.2.
5. **Mục 4.3:** cascade chỉ dừng với loại đồng hồ có `Mới` thay đổi. Sửa điện kỳ trước không bị dừng vì kỳ sau đổi đồng hồ nước.
6. **Thông báo cho khách:** giữ `CanBaoKhach`, nên ca đổi sang `NghiSaiChuSo` theo mục 3.4 sẽ báo khách.
