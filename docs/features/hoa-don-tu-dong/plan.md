# Plan: Hóa đơn tự động

- Nguồn: [spec.md](spec.md) v7. Kiểm chứng và đánh giá: [review.md](review.md). Hướng mở rộng: [huong-mo-rong.md](huong-mo-rong.md).
- Nhánh `feature/hoa-don-tu-dong`, tách từ `master` = `dc391e3`. Spec `e444f37`, đợt A `39b3d81`, đợt B `0736f4d`; đợt C và các sửa sau review chưa commit (11/10/2026).
- Plan này gộp ba bản kế hoạch `/ship` đã qua cổng xác nhận (đợt A 10/10/2026 17:13, đợt B 10/10/2026 21:39, đợt C 11/10/2026 09:45 và 11:54, giờ Asia/Bangkok). Ký hiệu S (schema), T (tiền, hiển thị tiền), F (fixture test) và số câu hỏi giữ như lúc xác nhận để đối chiếu với review.

## 1. Mục tiêu và tiến độ

Có chỉ số điện nước đáng tin cậy thì hệ thống tự tạo hóa đơn, gửi khách bản tạm tính; hết thời gian xem xét của chi nhánh thì tự chốt và công bố. Khách phản ánh bên ngoài, nhân viên bấm "Điều chỉnh và gửi lại".

- [x] **Đợt A** (`39b3d81`): schema, chuỗi tự tạo và gửi hóa đơn tạm tính, khách xem bản tạm tính (spec 3, 4, 5.1, 5.2, 7.1 phần dữ liệu, 7.2, 8, 9, 10).
- [x] **Đợt B** (`0736f4d`): job tự chốt, điều chỉnh và gửi lại, chặn chốt/trả lại tay, `ThieuDonGia` (L1), email nền khi chốt nhiều phòng (L2), đánh giá lại phòng trước khi tạo hóa đơn (L3) (spec 5.3, 6, 7.3 phần server).
- [x] **Đợt C** (chưa commit): giao diện cổng khách, màn hóa đơn quản lý, màn chi nhánh, Việc cần làm (spec 7, 11); bổ sung sau review T25, T26 (công nợ ở Quản lý phòng trọ) và N1 (định dạng chỉ số).
- [x] Sửa dữ liệu seed test chống trùng mã chi nhánh (N6, chưa commit).
- [ ] Commit đợt C; dọn DB test (bị bộ phân loại auto mode chặn, người dùng tự làm); gộp `master` và cập nhật mục Hiện trạng trong `AGENTS.md`.

## 2. Quyết định đã xác nhận

### 2.1 Schema (đợt A, migration `20261010101505_AddAutoInvoiceReviewWindow`)

| # | Thay đổi |
| --- | --- |
| S1 | `ChiNhanh.SoGioPhanHoiHoaDon` (`int`, mặc định 24) |
| S2 | `HasDefaultValue(24)` + CHECK `CK_ChiNhanh_SoGioPhanHoiHoaDon`: `"SoGioPhanHoiHoaDon" BETWEEN 1 AND 168` |
| S3 | `HoaDon.HanPhanHoi` (`DateTime?`, UTC): khác null nghĩa là hóa đơn đi luồng tự động |
| S4 | Index `IX_hoa_don_TrangThaiPhatHanh_HanPhanHoi` trên (`TrangThaiPhatHanh`, `HanPhanHoi`), lọc `"HanPhanHoi" IS NOT NULL AND "IsDeleted" = false` |
| S5 | Migration chỉ gồm 2 cột, 1 CHECK, 1 index; `Down` gỡ đủ. Không bảng mới, không sửa migration cũ; chi nhánh cũ nhận 24 qua DEFAULT |

Đợt B và C không đổi schema (`has-pending-model-changes` PASS).

### 2.2 Tiền và hiển thị tiền

| # | Đợt | Nội dung |
| --- | --- | --- |
| T1 | A | `AutoInvoiceService` tự gọi `CreateDraftsAsync` (cách tính, làm tròn giữ nguyên) khi chỉ số đạt điều kiện |
| T2 | A | Ngưỡng: độ tin cậy OCR ≥ 0,90; tiêu thụ ≤ 3 × trung bình tối đa 3 kỳ `DaDuyet` trước; chưa có kỳ trước hoặc trung bình 0 thì trần 500 kWh / 30 m³ |
| T3 | A | `CreateDraftsAsync` điền `HoaDonId` sau `SaveChanges` (trước đây luôn 0) |
| T4 | A | `SubmitProvisionalAsync` + `HoaDon.GuiTamTinh`: `Nhap → ChoDuyet`, `HanPhanHoi = now + SoGioPhanHoiHoaDon`, lịch sử `[TU_DONG]` |
| T5 | A | Khách xem được hóa đơn `ChoDuyet && HanPhanHoi != null && !IsDeleted` (`HoaDonStore`); `KhachXemDuoc` không đổi nên không thanh toán được |
| T6 | A | PDF của khách trả 404 cho hóa đơn `ChoDuyet` |
| T7 | A | Email tạm tính: kỳ, tổng tiền, chi tiết, chỉ số, giờ tự chốt, SĐT chi nhánh; không PDF, không VietQR |
| T8 | A | Thông báo cổng cho khách có số tiền tạm tính |
| T9 | B | Lý do dừng `ThieuDonGia` (L1): có tiêu thụ mà đơn giá của kỳ bằng 0 thì không tạo hóa đơn, báo nhân viên phụ trách + Admin (kèm câu hướng dẫn lập hóa đơn tay), không báo khách |
| T10 | B | Job `AutoInvoiceFinalizeJob` mỗi `ChuKyJobPhut` phút: hóa đơn tạm tính hết hạn `ChoDuyet → DaChot → DaGui`, `HanThanhToan` theo `SoNgayHanThanhToan`, báo khách như công bố. Không kiểm quyền (ngoại lệ của "chỉ Admin chốt"); người ghi lịch sử là người của lần chuyển sang `ChoDuyet` gần nhất |
| T11 | B | Điều chỉnh hóa đơn tạm tính (spec 6.2): đổi chỉ số, tính lại dòng điện nước, hạn mới = bây giờ + số giờ hiện tại của chi nhánh; một transaction. Bỏ qua kiểm tiêu thụ bất thường |
| T12 | B | Điều chỉnh hóa đơn `DaGui` chưa thu đồng nào (spec 6.3): hủy hóa đơn cũ và lượt `ChoThanhToan`, tạo hóa đơn thay thế `-R{n}` tính lại MỌI dòng theo dữ liệu hiện tại, gửi tạm tính lại; một transaction |
| T13 | B | Tách lõi `CreateDraftsAsync` thành `CreateDraftsInCurrentTransactionAsync`, không đổi cách tính |
| T14 | B | `FinalizeAsync`, `RejectAsync` từ chối hóa đơn tạm tính: "Hóa đơn đang tự chốt theo hạn, dùng Điều chỉnh và gửi lại." |
| T15 | B | `GuiDuyet` (gửi duyệt tay) xóa `HanPhanHoi` |
| T16 | B | `AdjustReadingsInCurrentTransactionAsync`: đổi chỉ số kỳ `DaDuyet`, tính lại hóa đơn nháp liên kết |
| T17 | B | L3: đánh giá lại phòng ngay trước bước tạo hóa đơn; email dùng chỉ số mới nhất |
| T18 | B | L2: email tạm tính của lần chốt kỳ gom lại, gửi nền sau khi request trả về; lỗi chỉ Warning |
| T19 | B | Thông báo/email cho khách sau điều chỉnh (tổng tiền mới, giờ tự chốt mới) và sau tự chốt (như công bố) |
| T20 | C | Cổng khách: nhãn "Tạm tính, tự chốt lúc dd/MM/yyyy HH:mm"; chi tiết có câu chưa cần thanh toán, SĐT chi nhánh, bảng chỉ số; ẩn "Tải PDF" và khung thanh toán |
| T21 | C | DTO `HoaDonRes`, `HoaDonChiTietRes` thêm `LaHoaDonTuDong`, `LaTamTinh`, `HanTuChot` và chỉ số của kỳ; chỉ đọc |
| T22 | C | Màn hóa đơn quản lý: nhãn tạm tính, ẩn Chốt/Trả lại cho hóa đơn tạm tính, mục "Điều chỉnh và gửi lại" (không ô lý do) |
| T23 | C | Màn chi nhánh: Admin sửa "Số giờ xem xét hóa đơn" 1–168; service từ chối ngoài khoảng (L6); chỉ áp cho hóa đơn gửi sau khi đổi |
| T24 | C | Việc cần làm: `HD2` không đếm hóa đơn tạm tính; thêm `HD4` "Hóa đơn tạm tính chờ tự chốt" (Theo dõi, `Invoice.Read`) |
| T25 | C | Quản lý phòng trọ, modal "Thu tiền": chỉ lấy hóa đơn `DaGui` |
| T26 | C | Quản lý phòng trọ, cờ nợ của thẻ phòng: chỉ cộng hóa đơn `DaGui` (spec 11.9) |
| F1 | C | `FakeEmailService` thêm cổng chờ tùy chọn `ProvisionalGate` (mặc định null, giữ hành vi cũ) để chứng minh email gửi nền (M4) |

### 2.3 Câu hỏi đã chốt

| Đợt | # | Quyết định |
| --- | --- | --- |
| A | 1 | Đồng hồ có nhiều ảnh chưa chính thức: chỉ xét ảnh mới nhất chưa bị thay |
| A | 2 | Một đồng hồ cần báo, đồng hồ kia chưa có ảnh: báo ngay |
| A | 3 | Không có hợp đồng hiệu lực: dừng im lặng. Từ 2 hợp đồng trong kỳ: không tự chạy, báo nhân viên lập tay (`NhieuHopDongTrongKy`) |
| A | 4 | **Người dùng đổi:** báo khách cho `DoTinCayThap`, `KhongDocDuoc` và cả `ChiSoGiam` (kèm SĐT chi nhánh) |
| A | 5 | Email không có link tuyệt đối (repo không có địa chỉ gốc), ghi "Xem chi tiết tại cổng khách thuê, mục Hóa đơn" |
| A | 6 | Thử lại OCR không kích hoạt chuỗi |
| A | 7 | Chấp nhận khoảng hở giữa các đợt; không gộp `master` trước khi xong đợt C |
| B | 1 | Có 2 endpoint JSON `GET`/`POST /HoaDon/DieuChinh/{id}` ngay ở đợt B |
| B | 2 | Job gặp hóa đơn vừa được điều chỉnh: chỉ log. Lỗi khác: báo nhân viên một lần cho mỗi cặp (hóa đơn, lý do), nhớ trong bộ nhớ |
| B | 3 | Không kiểm `ThieuDonGia` ở điều chỉnh và job |
| B | 4 | **Người dùng thêm:** thông báo `ThieuDonGia` có câu "Đơn giá của kỳ được lưu lúc tạo kỳ; lập hóa đơn theo luồng tay và sửa dòng điện/nước ở bản nháp." |
| C | 1 | Không thêm bộ lọc "Tạm tính"; `HD2`, `HD4` cùng mở bộ lọc Chờ duyệt |
| C | 2 | **Người dùng đổi:** QR chuyển khoản trong modal chi tiết của nhân viên chỉ hiện khi hóa đơn `DaChot` hoặc `DaGui`, áp cho mọi hóa đơn |
| C | 3 | Không thêm nhãn "Tự động" cho hóa đơn đã chốt/gửi |
| C | 4 | Thử tay trên preview: người dùng chọn trỏ preview vào DB test bằng biến môi trường, được tạo dữ liệu mẫu |

Quyết định cho lưu ý của review đợt A: L1 dừng chuỗi `ThieuDonGia`; L2 gửi email sau vòng lặp, chạy nền, không thêm bảng hàng đợi.

## 3. Thiết kế

### 3.1 Chuỗi tự tạo hóa đơn tạm tính (`AutoInvoiceService.RunForRoomAsync`)

- **Điểm kích hoạt:** khách hoặc nhân viên tải ảnh đồng hồ, xác nhận ảnh, sửa số đã xác nhận (`MeterImagePortalService`), nhân viên chốt kỳ (`DienNuocService.SaveChotDienNuocAsync`, từng phòng theo Id tăng dần). Thao tác gốc thất bại thì không chạy; kết quả thao tác gốc không đổi; chuỗi không ném.
- **Chỉ số đáng tin cậy** (`AutoInvoiceReadingPolicy.EvaluateMeter`): kỳ đã `DaDuyet`, ảnh chính thức, hoặc ảnh mới nhất `DocDuoc` với độ tin cậy ≥ ngưỡng. Lý do dừng theo đồng hồ: `ThieuChiSo` (im lặng), `KhongDocDuoc`, `DoTinCayThap`, `ChiSoGiam`, `TieuThuBatThuong` (áp cho mọi nguồn).
- **Điều kiện phòng** (`EvaluateRoom`, dừng ở bước đầu có kết quả): có kỳ → chưa có hóa đơn hoạt động (`DaCoHoaDon`, im lặng) → đúng 1 hợp đồng hiệu lực → chỉ số hai đồng hồ → đơn giá kỳ khác 0 khi có tiêu thụ (`ThieuDonGia`) → kỳ trước đã chốt (`KyTruocChuaChot`) → khách có tài khoản hoặc email (`KhachKhongCoKenhNhan`).
- **Người thực hiện** (spec 8): nhân viên/Admin làm thao tác gốc; khách tải ảnh thì lấy nhân viên được phân công sớm nhất của chi nhánh (`ChiNhanhKhongCoNhanVien` nếu không có). Phải có `Meter.Review`, `Invoice.Draft`, `Invoice.Submit` (`NguoiThucHienThieuQuyen`); service con tự kiểm lại.
- **Các bước:** tự xác nhận ảnh (ghi chú "Tự xác nhận, độ tin cậy 0,93") → duyệt kỳ → đánh giá lại phòng (L3) → `CreateDraftsAsync` → `SubmitProvisionalAsync` → báo khách (cổng + email). Thất bại ở bước 1, 2 có nhánh khôi phục khi chạy song song; dừng sau bước 3 để lại hóa đơn `Nhap` cho luồng tay.
- **Thông báo khi dừng:** nhân viên phụ trách + Admin nhận "chưa tạo được hóa đơn tạm tính" với link `/QuanLyNhaTro/ChotDienNuoc?thang=..&nam=..`; khách chỉ nhận cho `DoTinCayThap`, `KhongDocDuoc`, `ChiSoGiam`.
- **DbContext dùng chung:** `DiscardPendingChanges()` ở đầu và sau mỗi bước để `ThongBaoService` không lưu nhầm thay đổi dở.
- **Email nền (L2):** khi chốt kỳ, `RunForRoomAsync(..., hoanGuiEmail: true)` trả email chờ; `DienNuocService` gom và gọi `IProvisionalInvoiceEmailDispatcher.Enqueue` một lần. Dispatcher singleton ở Infrastructure gửi bằng `Task.Run` trong DI scope riêng, chỉ dùng `IEmailService`, lỗi chỉ Warning.

### 3.2 Job tự chốt (`AutoInvoiceFinalizeUseCase`, `AutoInvoiceFinalizeJob`)

- Job chỉ lập lịch, đăng ký trong khối `BackgroundJobs:Enabled`; nghiệp vụ ở use case.
- Lấy hóa đơn `ChoDuyet`, `HanPhanHoi <= now`, chưa xóa, theo hạn tăng dần; mỗi hóa đơn một transaction: khóa kỳ rồi hóa đơn, kiểm lại (vẫn tạm tính và đến hạn; kỳ `DaDuyet`; tổng tiền khớp chi tiết; không có hóa đơn hoạt động khác; xác định được người gửi tạm tính), rồi `ChotHoaDon` + `GuiHoaDon` + commit.
- Sau commit báo khách như công bố (cổng, email PDF + VietQR). Lỗi một hóa đơn không dừng vòng; exception ngoài dự kiến mới ghi Error.

### 3.3 Điều chỉnh và gửi lại (`InvoiceAdjustmentService`)

- **Lý do khóa** (theo thứ tự): không phải hóa đơn tự động; trạng thái khác `ChoDuyet`/`DaGui`; thiếu quyền (`Meter.Review`, `Invoice.Submit`, thêm `Invoice.Cancel` khi `DaGui`); không gắn kỳ; kỳ sau đã chốt; `DaGui` đã có khoản thu; khách đã nộp minh chứng đang đối chiếu.
- **Khóa:** kỳ → hợp đồng → hóa đơn (job: kỳ → hóa đơn), cùng chiều với luồng thanh toán nên không có vòng khóa.
- **6.2 (`ChoDuyet`):** `MoLaiDeDieuChinh` → lưu → `AdjustReadingsInCurrentTransactionAsync` → kiểm tổng tiền → `GuiTamTinh` với hạn mới.
- **6.3 (`DaGui`):** hết hạn và hủy lượt `ChoThanhToan`, `HuyHoaDon` → lưu → đổi chỉ số → `CreateDraftsInCurrentTransactionAsync` (phải ra đúng 1 hóa đơn của hợp đồng cũ, nếu không báo rõ lý do) → gửi tạm tính bản `-R{n}`.
- Mọi thất bại rollback và `DiscardPendingChanges()` trước khi trả; sau commit báo khách (cổng + email "đã điều chỉnh").
- **Web:** `GET /HoaDon/DieuChinh/{id}` (kiểm `Invoice.Read` trước) và `POST` có antiforgery; cả hai trả `hanTuChot` giờ Việt Nam `dd/MM/yyyy HH:mm`.

### 3.4 Giao diện (đợt C)

- **Quy tắc tạm tính duy nhất:** `ChoDuyet && HanPhanHoi != null && !IsDeleted`, tính ở `InvoiceDetailProjection.LaTamTinh`; giờ VN đổi ở projection, JS không cộng múi giờ.
- **Cổng khách** (`KhachThue/HoaDon`): nhãn tạm tính ở danh sách và chi tiết; khung thông báo có SĐT chi nhánh (`tel:`); bảng chỉ số cũ/mới/tiêu thụ; không "Tải PDF", không khung thanh toán. JSON khách không thêm `hanThanhToan`, `lichSuTrangThais`.
- **Màn hóa đơn quản lý:** nhãn ở cột Phát hành và modal chi tiết; menu ẩn Chốt/Trả lại khi tạm tính, có "Điều chỉnh và gửi lại" cho hóa đơn tự động ở Chờ duyệt hoặc Đã gửi; modal `hoadon-dieuchinh.js` (cảnh báo client, hộp xác nhận, chống bấm đúp, escape mọi chuỗi server, `Swal.fire` dạng `text`). QR chỉ khi `DaChot`/`DaGui` (câu C2).
- **Màn chi nhánh:** ô số giờ 1–168; tạo mới không gửi thì 24, cập nhật không gửi thì giữ giá trị cũ; controller chặn giá trị không bind được.
- **Việc cần làm:** `HD2` chỉ hóa đơn chờ duyệt tay; `HD4` hóa đơn tạm tính, theo phạm vi chi nhánh.
- **Quản lý phòng trọ:** "Thu tiền" và cờ nợ chỉ dựa trên hóa đơn `DaGui` (T25, T26).
- **Định dạng chỉ số (N1):** `Common/Formatting/ChiSo.DinhDang` ("100", "1.234,5", không phụ thuộc culture) cho diễn giải dòng điện/nước và thông báo lỗi chỉ số; áp cho cả luồng tay; dòng cũ trong DB giữ nguyên.

## 4. File chính

| Tầng | File |
| --- | --- |
| Domain | `Entities/ChiNhanh.cs` (S1), `Entities/HoaDon.cs` (`HanPhanHoi`, `GuiTamTinh`, `MoLaiDeDieuChinh`, `GuiDuyet` xóa hạn, `KhoiTaoNhap` có lý do) |
| Application | `Common/Configurations/AutoInvoiceOptions.cs`; `Common/Formatting/ChiSo.cs`; `Features/HoaDons/AutoInvoices/` (`AutoInvoiceReadingPolicy`, `AutoInvoiceHistoryTags`, `AutoInvoiceFinalizeNotifyGuard`); `Features/HoaDons/Services/` (`AutoInvoiceService`, `InvoiceAdjustmentService`, `InvoiceIssuanceService`, `InvoiceViewService`, `HoaDonCalculatorService`); `Features/HoaDons/UseCases/AutoInvoiceFinalizeUseCase.cs`; `Features/HoaDons/Persistence/IAutoInvoiceStore.cs`; DTO (`AutoInvoiceDtos`, `InvoiceAdjustmentDtos`, `HoaDonDTOs`, `ProvisionalInvoiceEmailData`, `PendingProvisionalEmail`, `MeterAdjustmentDtos`); `Features/DienNuocs/Services/` (`MeterImagePortalService`, `DienNuocService`, `MeterReadingWorkflowService`); `Features/ChiNhanhs/` (DTO, service); `Features/ViecCanLam/`; `Abstractions/Services/IEmailService.cs`, `IProvisionalInvoiceEmailDispatcher.cs`; `DependencyInjection.cs` |
| Infrastructure | Migration `20261010101505_AddAutoInvoiceReviewWindow` + snapshot; `Configurations/ChiNhanhConfiguration.cs`, `HoaDonConfiguration.cs`; `Persistence/Features/` (`AutoInvoiceStore`, `HoaDonStore`, `InvoiceDetailProjection`, `InvoiceViewStore`, `ViecCanLamStore`, `PhongTroStore`); `ExternalServices/Emails/` (`MailKitEmailService`, `BackgroundProvisionalInvoiceEmailDispatcher`); `BackgroundJobs/AutoInvoiceFinalizeJob.cs`; `DependencyInjection.cs` |
| Web | `Areas/QuanLyNhaTro/Controllers/HoaDonController.cs` (endpoint điều chỉnh), `ChiNhanhsController.cs`; `Areas/QuanLyNhaTro/Views/HoaDon/Index.cshtml`, `ChiNhanhs/QuanLyChiNhanh.cshtml`; `Areas/KhachThue/Controllers/HoaDonController.cs`, `Views/HoaDon/Index.cshtml`; `wwwroot/js/hoadon-dieuchinh.js`; `appsettings.example.json` (khối `AutoInvoiceOptions`) |

Không đổi `Program.cs`, `.csproj`, `appsettings.json`, `ApplicationDbContext`, test kiến trúc theo hướng nới (chỉ thêm `IAutoInvoiceService`, `IInvoiceAdjustmentService` vào danh sách bắt buộc kiểm phạm vi chi nhánh).

## 5. Trường hợp biên

- **Thời gian:** mọi mốc lấy từ `TimeProvider`, lưu UTC; chỉ đổi giờ VN ở nội dung thông báo, email, JSON, projection. Số giờ chi nhánh đọc lúc gửi; đổi số giờ không dời hạn đã đặt.
- **Xóa mềm:** hóa đơn `IsDeleted` không là hóa đơn hoạt động, không tạm tính, không vào job hay điều chỉnh; ảnh, kỳ, phòng, hợp đồng, người dùng xóa mềm bị loại; phân công đã thu hồi không được chọn làm người thực hiện; tiền đã thu chỉ tính `LichSuThanhToan` chưa xóa.
- **Phân quyền:** fail closed qua `IEmployeeAccessService`, kiểm trước khi trả dữ liệu; khách không bao giờ là người thực hiện; job không kiểm quyền (spec 5.3); hóa đơn `ChoDuyet` tay vẫn chốt/trả lại như cũ.
- **Chạy lặp/song song:** không tạo hóa đơn thứ hai (kiểm hóa đơn hoạt động, unique index (`HopDongId`, `Thang`, `Nam`), khóa dòng); job và điều chỉnh cùng hóa đơn chạy tuần tự nhờ khóa kỳ trước.
- **Tiền:** decimal xuyên suốt; tạm tính không vào thanh toán, công nợ, nhắc nợ, Dashboard khách, Quản lý phòng trọ.
- **Log:** lý do lường trước ghi Information/Warning; Error chỉ cho exception ngoài dự kiến (`CustomWebApplicationFactory` FAIL khi có Error lạ).
- **Không tự đè chỉ số chính thức** bằng ảnh mới.

## 6. Test

| Project | Test chính |
| --- | --- |
| Domain.UnitTests | `InvoiceWorkflowTests` (`GuiTamTinh`, `MoLaiDeDieuChinh`, `GuiDuyet`, `KhoiTaoNhap`) |
| Application.UnitTests | `AutoInvoiceOptionsTests`, `AutoInvoiceReadingPolicyTests`, `AutoInvoiceServiceTests`, `MeterImagePortalAutoInvoiceTriggerTests`, `SaveChotDienNuocWorkflowTests`, `InvoiceDraftWorkflowTests`, `InvoiceViewServiceTests`, `InvoiceAdjustmentServiceTests`, `AutoInvoiceFinalizeUseCaseTests`, `ChiNhanhServiceTests`, `ViecCanLamServiceTests`, `HoaDonCalculatorServiceTests` (N1), `BranchScopeArchitectureTests` |
| Infrastructure.IntegrationTests | `AutoInvoiceSchemaTests`, `AutoInvoiceStoreTests`, `TenantInvoiceVisibilityTests`, `AutoInvoiceChainTests`, `InvoiceAdjustmentTests`, `InvoiceAdjustmentEdgeCaseTests`, `AutoInvoiceFinalizeTests`, `MeterAdjustmentCoreTests`, `BackgroundProvisionalInvoiceEmailDispatcherTests`, `ProvisionalInvoiceProjectionTests`, `ViecCanLamStoreTests`, `PhongTroDebtScopeTests`, test job nền (smoke, composition) |
| Web.IntegrationTests | `AutoInvoiceTriggerTests`, `AutoInvoiceAdjustmentEndpointTests`, `AutoInvoiceUiEndpointTests`, `AutoInvoiceUiStaticGuardTests`, `TenantInvoicePortalTests`, `StartupTests` |

Quy ước: test Web tải ảnh phải đặt giá trị OCR giả rõ ràng và `Reset()` trong `finally` (L5); mã chi nhánh trong dữ liệu seed lấy từ `TestCodes.MaChiNhanh(prefix)` (N6).

Lệnh (tuần tự, không chạy song song hai lượt `dotnet test`):

```powershell
dotnet build QuanLyChoThuePhongTroWeb.sln
dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
node .claude/skills/chay-test-tich-hop/scripts/kiem-tra-moi-truong.js
dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests
$p = "src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj"
dotnet ef migrations has-pending-model-changes --project $p --startup-project $p
git diff --check
```

## 7. Cấu hình

`AutoInvoiceOptions` (thiếu khối thì dùng mặc định, `Validate()` khi khởi động): `NguongDoTinCay` 0,90; `HeSoBatThuong` 3; `SoKyTrungBinh` 3; `TranDienKwh` 500; `TranNuocM3` 30; `ChuKyJobPhut` 5. Số giờ xem xét theo chi nhánh (`SoGioPhanHoiHoaDon`, 1–168, mặc định 24).

## 8. Ngoài phạm vi

- Cập nhật lại đơn giá cho kỳ đang dở khi chi nhánh khai giá sau (M1).
- Gắn email nền với vòng đời host (M2, đã chấp nhận mất email khi tắt máy).
- Kiểm chứng chỉ SMTP (L8).
- Giao diện cổng khách ở mobile: bảng tràn ngang, nút đổi theme ẩn (N2, N3); dòng "Hạn tự chốt hiện tại" ở modal điều chỉnh của hóa đơn Đã gửi (N4).
- Đối soát chuyển khoản tự động, Zalo, công tơ IoT: [huong-mo-rong.md](huong-mo-rong.md).
