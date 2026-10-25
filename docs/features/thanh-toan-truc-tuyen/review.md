# Review và kiểm chứng: Thanh toán trực tuyến — đợt 1 (VietQR + SePay)

- **Nhánh:** `feature/thanh-toan-truc-tuyen`. **Spec:** [spec.md](spec.md). **Kế hoạch:** [plan.md](plan.md).
- **Cập nhật lần cuối:** 25/10/2026 (dọn mã cũ và ba khuyến nghị nhỏ của phần 4, mục 3.1).
- **Kết luận:** đợt 1 code xong cả 5 phần, test tự động PASS (Infrastructure cần một lần chạy lại vì một test đếm cả bảng FAIL lẻ), **không đổi schema**; người dùng đã thử tay trên SePay Test mode (báo "tương đối ổn", 20/10/2026). **Chưa kiểm chứng:** đồng bộ bù, giao dịch thật, và nhiều chi tiết giao diện trên server thật.

## 1. Nguồn bằng chứng và giới hạn

| Nguồn | Có gì | Giới hạn |
| --- | --- | --- |
| `git log`, commit message | Phạm vi và quyết định từng phần | Commit message chỉ tóm tắt |
| `.bangiao/danh-gia.md`, `ket-qua-test.md` | Review và test của **phần 4** (18/10/2026) | `.bangiao/` không nằm trong Git, mỗi lượt `/ship` ghi đè lượt trước |
| Phần 1–3 | Phán quyết CHOT của reviewer và việc người dùng xác nhận được ghi lại trong báo cáo phiên | **Bản review và kết quả test gốc của phần 1–3 không còn**; số test từng phần không được lưu |
| Báo cáo ngày `.agents/bao-cao-hang-ngay/2026-10-20/` | Việc làm và kiểm chứng ngày 20/10/2026 | Chỉ ghi phần đã làm hôm đó |

## 2. Kết quả theo phần

| Phần | Commit | Phán quyết reviewer | Ghi chú |
| --- | --- | --- | --- |
| 1 | `6b690e4`, `33e210e`, `0e6924c` | CHOT | Người dùng thử tay trên Test mode và đạt (có dòng thanh toán trong lịch sử) |
| 2 | `2111a43` | CHOT | Telegram và email thật thử tay ngày 20/10/2026 |
| 3 | `6057625` | CHOT | `POST` tạo lượt/nộp minh chứng trả 405 thay vì 404 (trùng số đoạn route với `GET` panel); reviewer chấp nhận, spec §11.2 sửa thành "404 hoặc 405, không tạo lượt" |
| 4 | `d093a96` | CAN SUA, đã sửa rồi chốt | Chi tiết ở mục 3 |
| 5 | chưa commit | không có (chỉ tài liệu) | `huong-dan-thiet-lap.md`, cập nhật README module, danh mục, `AGENTS.md` |

## 3. Phần 4: bốn điểm bắt buộc và cách xử lý

Reviewer ra phán quyết CAN SUA vì tài liệu và comment khẳng định đồng bộ bù "không thể ghi đôi", trong khi vẫn còn khả năng khi webhook thiếu `referenceCode`. Bốn điểm bắt buộc đã sửa trước khi commit:

1. Spec §5.2: viết lại thành "Rủi ro ghi đôi còn lại" (hai đường dùng hai khóa khác nhau `SEPAY-ID-{id}` và `SEPAY-{mã}`; chỉ xảy ra khi bật `SyncRecordEnabled`).
2. Comment `DedupKey` trong `IncomingBankTransfer.cs`: sửa cho đúng.
3. Spec §11.3 mục 6: viết lại theo PA2 thành ba bước (a) cờ tắt kiểm "Đã có", (b) kiểm `referenceCode` không rỗng và không có dòng `SEPAY-ID-`, (c) đạt cả hai mới bật cờ.
4. Hai ghi chú trong `SePaySyncService.cs` và hai assert trong `SePaySyncServiceTests.cs`: thêm câu "Kiểm lịch sử thanh toán của hóa đơn trước khi Thu thủ công."

Sau khi sửa, toàn bộ test được chạy lại và PASS (theo báo cáo phiên; số liệu ở mục 4).

### Khuyến nghị của reviewer (không chặn)

Ba mục đầu đã làm ngày 25/10/2026 (mục 3.1); mục cuối vẫn để sau.

- `SePaySyncService.cs`: một trang mà mọi phần tử sai định dạng vẫn kết thúc "hoàn tất", dù `CanhBao` vẫn báo số giao dịch sai định dạng. **Đã sửa.**
- Chế độ chỉ đối chiếu chưa có link tới hóa đơn cho dòng "Chưa ghi sổ". **Đã sửa** (thêm cho cả dòng "Thiếu mã tham chiếu").
- Nút `btn-sm` trong modal lịch sử dưới 44px trên điện thoại. **Đã sửa.**
- Chốt chặn "Nghi trùng, kiểm tay": người dùng chọn **để sau** (xem [plan.md](plan.md) mục 5).

### 3.1 Dọn mã và sửa khuyến nghị (25/10/2026, cùng nhánh, chưa commit)

Người dùng yêu cầu làm trên chính nhánh `feature/thanh-toan-truc-tuyen`. Không đổi schema.

- **Dọn mã khách tự tạo lượt/nộp minh chứng** (spec §8): `InvoicePaymentRequestService` và `IInvoicePaymentRequestService` chỉ còn `GetProofImageAsync`. Gỡ thêm những gì chỉ phục vụ các method đã gỡ: `PaymentPanelDto`, `SubmitPaymentProofRequest`, `PaymentCodeGenerator.NewRequestCode`, `InvoicePaymentPolicy.ResolveRequestAmount`, và bốn method của `IInvoicePaymentStore` (`ExistsMaYeuCauAsync`, `ExistsPendingProofMaGiaoDichAsync`, `AddRequest`, `GetTargetByYeuCauAsync`) cùng bản cài ở Infrastructure và fake. Giữ Domain (`YeuCauThanhToanHoaDon.Tao`, `NopMinhChung`) và entity vì còn lượt cũ trong dữ liệu và test dựng dữ liệu bằng chúng. Giữ các tùy chọn `InvoicePaymentOptions` (vẫn có chỗ dùng khác).
- **Test:** gỡ các test của method đã gỡ (tạo/hủy lượt, nộp minh chứng, khung thanh toán cũ, QR theo lượt, `ResolveRequestAmount`, sinh mã lượt, hai test đồng thời tạo lượt); `InvoicePaymentRequestServiceTests` còn 3 ca đọc ảnh minh chứng (thêm ca không tồn tại và kho ảnh lỗi). Hai test Infrastructure còn cần dữ liệu cũ được viết lại để vẫn kiểm cùng hành vi qua đường còn lại: ghi `HetHan` dù thao tác thất bại (nay qua thu thủ công trên hóa đơn đã thu đủ) và đối chiếu trọn luồng từ minh chứng cũ.
- **Phân trang đồng bộ bù:** trang không có phần tử hợp lệ nhưng có phần tử sai định dạng và API báo còn trang thì đọc tiếp; trang không có gì cả mà báo còn trang vẫn coi là hết (test cũ giữ nguyên) và vẫn chặn ở 100 trang. 2 test mới.
- **Link hóa đơn:** `TransferCodeParser.ExtractInvoiceId` (chỉ nhận đúng `TTPTHD{1–9 chữ số}`); dòng `Chưa ghi sổ` và `Thiếu mã tham chiếu` gắn `HoaDonId`/`MaHoaDon` khi hóa đơn có thật, giao diện đã có sẵn cột link. Dòng `Đã có` không có link. Test: 14 ca parser, 6 ca đồng bộ.
- **Nút trên điện thoại:** `hoadon.css`, `#chiTietModal .btn-list .btn` cao tối thiểu 44px (trước đó quy tắc điện thoại còn thu nhỏ nút xuống padding 0.25rem, chữ 12px).

| Lệnh | Kết quả | Ghi chú |
| --- | --- | --- |
| `dotnet build QuanLyChoThuePhongTroWeb.sln` | PASS | 0 lỗi |
| `dotnet test QuanLyChoThuePhongTroWeb.sln` (một lượt, DB `quanlyphongtro_test`) | PASS | Domain 135/135; Application 1774/1774; Infrastructure 613/613; Web 579/579 |
| `dotnet ef migrations has-pending-model-changes` | PASS | "No changes have been made to the model since the last migration." |
| `git diff --check` | PASS | chỉ có cảnh báo LF/CRLF |
| Đo nút trên app thật (`web-test-db`, hóa đơn có lịch sử thanh toán, viewport giả lập 375px) | PASS | cả hai nút "Biên lai" và "Gửi lại biên lai" cao 44px, chữ 13px; ảnh chụp màn hình lúc giả lập không dùng được nên chỉ có số đo |

Chưa kiểm: link hóa đơn và đọc tiếp phân trang chỉ có test đơn vị (fake client), chưa chạy với API SePay thật (đồng bộ bù vẫn chưa thử tay, mục 6); màn Giao dịch SePay chưa mở trên app thật.

### Các điểm reviewer đã xác minh ở phần 4

- Không đổi schema: `git diff master` rỗng trên `Persistence/Configurations/`, `ApplicationDbContext`, `Migrations/`.
- `dotnet ef migrations has-pending-model-changes`: **PASS** ("No changes have been made to the model since the last migration"), do reviewer chạy ngày 18/10/2026 ở trạng thái trước commit `d093a96`.
- Unique index `IX_lich_su_thanh_toan_MaGiaoDich` có thật (cấu hình ở `LichSuThanhToanConfiguration.cs`, migration `20260921155855_AddBillingOcrAndInvoicePayments`, đã có trên `master`).
- Ranh giới kiến trúc: Application không dùng ASP.NET/EF, Web không tham chiếu Domain.
- Token SePay không lọt log; có test bắt log của client.
- Cờ `SyncRecordEnabled` tắt thì đồng bộ không ghi sổ và không thông báo; có test qua HTTP và qua ghi sổ thật, kèm ca đối chứng.

## 4. Kết quả test

| Lệnh | Kết quả | Thời điểm và giới hạn |
| --- | --- | --- |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests` | PASS 135/135 | 18/10/2026, tester phần 4 |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests` | PASS 1770/1770 | như trên |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests` | PASS 604/604 | như trên, DB `_test` |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests` | PASS 523/523 | như trên, DB `_test` |
| Test kiến trúc (`ArchitectureBoundaryTests`, `BranchScopeArchitectureTests`, `InfrastructureBoundaryTests`) | PASS | như trên |
| `dotnet test QuanLyChoThuePhongTroWeb.sln` (một lượt, DB `quanlyphongtro_test`) | Domain PASS 135/135; Application PASS 1770/1770; Infrastructure 603/604 (1 FAIL, xem bên dưới); Web PASS 523/523 | 20/10/2026, mã hiện tại của nhánh, gồm các sửa responsive chưa commit |
| `dotnet test ... Infrastructure.IntegrationTests` (chạy lại một mình, cả project) | PASS 604/604 | 20/10/2026 |
| Web.IntegrationTests lọc `TenantOnlinePayment` / `TenantOnlinePayment`, `Invoice`, `HoaDon`, `LichSu` | PASS 40/40 / PASS 169/169 | 20/10/2026, chạy lẻ trước lượt đầy đủ |
| `dotnet ef migrations has-pending-model-changes` (Infrastructure vừa là project vừa là startup, chuỗi kết nối từ `QLCTPT_DESIGNTIME_CONNECTION_STRING`, không in chuỗi) | PASS: "No changes have been made to the model since the last migration." (exit 0) | 20/10/2026, trên mã hiện tại của nhánh; không kiểm DB thật, chỉ so model với snapshot |
| `git diff master` trên `Persistence/Migrations`, `Persistence/Configurations`, `ApplicationDbContext.cs`, `Domain` | rỗng (không đổi) | 20/10/2026 |
| `git diff --check` | PASS (chỉ có cảnh báo LF/CRLF) | 18/10 và 20/10/2026 |

Số liệu ngày 18/10/2026 (135/1770/604/523) là của tester phần 4; ngày 20/10/2026 bốn bộ chạy lại đủ trên mã hiện tại với cùng tổng số test, ngoài một lần FAIL ở Infrastructure:

- **Test FAIL:** `InvoicePublicationConcurrencyTests.Publish_Repeat_AfterDaGui_NoChanges` trong lượt toàn solution (19 giây): `Assert.Equal` số dòng `ThongBaos` của **cả bảng** giữa hai thời điểm, Expected 31133097, Actual 31150615 (lệch 17518). Test này và file của nó không được sửa trên nhánh (`git diff master` rỗng); nó đã có từ trước (commit `07f37a3`).
- **Chạy lại:** lớp `InvoicePublicationConcurrencyTests` một mình PASS 6/6; cả project Infrastructure một mình PASS 604/604.
- **Nhận định (chưa xác nhận):** test đếm toàn bảng nên fail khi có thao tác khác ghi vào bảng `thong_bao` của DB test đúng lúc đó (test Infrastructure chạy song song giữa các lớp). Chưa tìm ra nguồn ghi 17.518 dòng, và giá trị đếm khoảng 31 triệu cho thấy bảng `thong_bao` của DB test rất lớn; chưa điều tra. Lỗi không liên quan đến thay đổi của nhánh này theo những gì đã kiểm, nhưng chưa chứng minh được bằng cách khác ngoài việc test đó không đổi.

## 5. Đối chiếu spec với code (20/10/2026)

- 25 thành phần liệt kê ở spec §9 (kiểu, controller, sự kiện SignalR) đều tồn tại trong `src/`.
- Tám khóa cấu hình ở spec §10 (`TimestampToleranceSeconds`, `WebhookApiKey`, `SyncRecordEnabled`, `ApiToken`, `BranchChatIds`, `DefaultChatId`, `PublicBaseUrl`, `BankName`) đều có trong code và trong `appsettings.example.json`.
- Mới đối chiếu tên và sự tồn tại; **chưa đối chiếu hành vi từng dòng** của spec với code.

## 6. Thử tay (spec §11.3)

Theo [huong-dan-thiet-lap.md](huong-dan-thiet-lap.md) §6; người dùng báo "tương đối ổn" ngày 20/10/2026, **chỉ trên SePay Test mode**, không ghi chi tiết từng kịch bản:

- Đã thử: 6.1–6.5 (TC01, TC02, E01–E03, TC05), Telegram và email biên lai thật, 6.7 (TC04, deeplink trên điện thoại), 6.8–6.9 (nút Biên lai/Gửi lại biên lai, in A5).
- Chưa xác nhận: 6.6 đồng bộ bù (chưa kiểm `referenceCode` = `reference_number`, múi giờ và host sandbox của API v2); cờ `SePay:SyncRecordEnabled` vẫn tắt.
- Chưa ghi: danh sách app ngân hàng không hỗ trợ deeplink (spec §15 yêu cầu).
- Chưa thử với giao dịch thật.

## 7. Rủi ro và lỗi còn mở

1. **Ghi đôi khi bật `SyncRecordEnabled`** (spec §5.2): webhook thiếu `referenceCode` ghi `SEPAY-ID-{id}`, API trả `reference_number` cho cùng giao dịch. Cờ tắt thì không xảy ra. Giảm bằng chốt chặn "Nghi trùng, kiểm tay" (để sau).
2. **Tên trường, múi giờ và host sandbox của API v2 SePay là giả định**; test dùng JSON giả theo giả định đó nên chưa chứng minh khớp API thật.
3. **Thông báo Telegram và email nằm trong hàng đợi bộ nhớ**, khởi động lại có thể mất (bù bằng "Gửi lại biên lai", spec §14).
4. **Giao diện chưa xem trên server thật ở mọi màn:** các sửa responsive ngày 20/10/2026 chỉ đo trên trang thử tĩnh nạp đúng `site.css` và `hoadon.css`; màn quản lý (khối QR) chưa xem trực quan. Nguyên nhân lỗi cuộn ngang đã xác định: `site.css` ép `.table-responsive { overflow: visible !important }`; đã sửa trong `hoadon.css` cho `#chiTietModal`.

## 8. Điều kiện nghiệm thu (spec §15)

Xem trạng thái các checkbox ở [spec.md](spec.md) §15. Mục "§11.1, §11.2 PASS; `has-pending-model-changes` không có thay đổi" đã tick ngày 20/10/2026: bốn bộ test PASS trên mã hiện tại (Infrastructure cần chạy lại một lần vì một test đếm cả bảng FAIL lẻ, xem mục 4) và `has-pending-model-changes` PASS. Mục TC01–TC05 và đồng bộ bù chưa tick vì đồng bộ bù chưa xác nhận.
