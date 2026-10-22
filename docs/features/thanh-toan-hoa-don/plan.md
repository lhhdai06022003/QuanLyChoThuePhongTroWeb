# Thanh toán hóa đơn: kế hoạch triển khai (phương án C)

**Mục tiêu:** hoàn thành module 2 theo `spec.md` v2.0 trên schema hiện có (không migration).

**Kiến trúc:** lai hai bản thiết kế đã so sánh ngày 03/10/2026.

- Quy tắc trạng thái lượt và minh chứng nằm trong entity (`YeuCauThanhToanHoaDon`, `MinhChungThanhToanHoaDon`, `LichSuThanhToan`, `HoaDon.CapNhatTrangThaiThanhToan`). Điều kiện "lượt đang hoạt động" (§5.1) là một `Expression` duy nhất trong Domain, store và fake cùng dùng.
- Mọi luồng ghi đi qua `InvoicePaymentTransaction` (Application, scoped). Thứ tự: mở transaction → khóa dòng `hoa_don` → chuyển lượt quá hạn sang `HetHan` và lưu → chạy logic → lưu ledger, **đọc lại tổng từ DB** rồi tính lại trạng thái → commit → gửi SignalR sau commit. Lỗi nghiệp vụ trả về sau khi đã lưu `HetHan` thì vẫn commit phần hết hạn (§6.4); lỗi khác thì rollback.
- Phần thuần, test riêng được: `InvoicePaymentPolicy` (bảng §13.2, §10, §18), `PaymentNoteTags` (thẻ `[CHENH-LECH]`, `[TIEN-THUA]`, `[CHO-ADMIN]`, `[DA-HUY]`), `IPaymentCodeGenerator`, `ProofImageValidator` (Web và Application cùng gọi).
- Service:
  - `InvoicePaymentRequestService` (khách).
  - `InvoicePaymentConfirmationService` (đối chiếu, viết lại).
  - `InvoiceLedgerService` (thu tiền thủ công, hủy ghi nhận, hủy hóa đơn; chuyển ra khỏi `HoaDonService` và `LichSuThanhToanService`).
- `ServiceResult` thêm `ServiceErrorKind` (Validation/Forbidden/NotFound). Web map sang 400/403/404 bằng `StatusCode(403, json)`, vì cookie `AccessDeniedPath` biến `Forbid()` thành redirect.

**Quyết định đã chốt thêm (03/10/2026):**

- Thu thủ công bằng chuyển khoản: mã ngân hàng tùy chọn. Để trống thì `GhiChu` bắt buộc và hệ thống sinh mã `CK-{yyyyMMdd}-{8 ký tự}`. Tiền mặt sinh `TM-...`. Thay đổi này áp dụng cho §18 và §22.
- `PublicId` ảnh minh chứng là `payment-proofs/{MaYeuCau}_xxxx` (Cloudinary sinh hậu tố từ `FileName`), không đổi `IMeterImageStorageService`.
- Thông báo: lưu dòng `ThongBao` trong transaction và đẩy SignalR từng người dùng sau commit. Người nhận là Admin và nhân viên đang phân công chi nhánh của hóa đơn.
- Tenant service nhận thêm `actorUserId` (claim `NameIdentifier`) để ghi người thực hiện.
- Sửa thêm các màn đang lọc công nợ bằng `== ChuaThanhToan`, để hóa đơn trả một phần không biến mất.

---

## 1. Nền tảng (không đổi hành vi)

- [x] `ServiceResult`: `ServiceErrorKind`, `Forbidden()`, `NotFound()`; test trong `CommonTypesTests`.
- [x] `EmployeeActionCodes`: `Payment.Review`, `Payment.ConfigurePartial` (Admin-only); cập nhật `EmployeeAccessTests`.
- [x] `AppEnums`: `AppTrangThaiYeuCauThanhToan`, `AppTrangThaiMinhChungThanhToan`; `AppEnumMirrorTests`.
- [x] `InvoicePaymentOptions` (section `InvoicePayment`), bind ở Infrastructure, mẫu trong `appsettings.example.json`.
- [x] `UniqueConstraintViolationException`; `EfUnitOfWork` đổi lỗi 23505 sang exception này.
- [x] Web: `ServiceResultHttpExtensions`.

## 2. Domain

- [x] `HoaDon.CapNhatTrangThaiThanhToan`; `CauHinhThanhToanMotPhan` kiểm số nguyên và ghi lịch sử.
- [x] `YeuCauThanhToanHoaDon`: `DangHoatDong(now)`, `Tao`, `ChuyenTrangThai` (bảng chuyển §6.1), `GhiChuyenAdmin`, `NopMinhChung`, `TuChoiMinhChung`.
- [x] `MinhChungThanhToanHoaDon.XacNhan/TuChoi`; `LichSuThanhToan.HuyGhiNhan`.
- [x] Domain tests.

## 3. Module 1: làm tròn và hiển thị (§31.1)

- [x] `InvoiceMoney.RoundVnd/IsWholeVnd`; áp dụng ở `HoaDonCalculatorService`, `HoaDonService` (sửa dòng nháp, đơn giá nguyên), `InvoiceIssuanceService`, `YeuCauSuCoService` (chi phí sửa chữa nguyên).
- [x] DTO hóa đơn có `DaThu`, `ConLai`, `TrangThaiThanhToanValue`, cấu hình trả một phần; nhãn `ThanhToanMotPhan` đúng ở mọi projection; QR trong PDF theo số còn lại.
- [x] Cập nhật test calculator và các test có tổng lẻ.

## 4. Kernel và store

- [x] `InvoicePaymentPolicy`, `PaymentNoteTags`, `IPaymentCodeGenerator`, `ProofImageValidator` kèm unit test.
- [x] `IInvoicePaymentStore` viết lại cùng `InvoicePaymentStore`: khóa dòng, lượt quá hạn/đang hoạt động, target trước khóa, kiểm tồn tại mã, read model (khung thanh toán, hàng đợi, chi tiết, tra cứu), người nhận thông báo.
- [x] `InvoicePaymentTransaction` và `InvoicePaymentScope`; fake store/UoW; test transaction.

## 5. Service

- [x] `InvoicePaymentRequestService` (§10–§12, QR, ảnh).
- [x] `InvoicePaymentConfirmationService` viết lại (§13–§17, §19, §21).
- [x] `InvoiceLedgerService` (§18, §20, §31.2); bỏ `ThuTienAsync`/`DeleteHoaDonAsync` khỏi `HoaDonService` và `HuyGiaoDichAsync` khỏi `LichSuThanhToanService`.
- [x] Unit test theo danh sách §32.

## 6. Web khách thuê

- [x] Endpoint `ThanhToan/*` trong `KhachThue/HoaDonController`; bỏ `GetVietQR` cũ.
- [x] Khung "Thanh toán" trong modal hóa đơn theo bảng §27.1; badge trả một phần.

## 7. Web quản lý

- [x] `DoiChieuThanhToanController` cùng view, modal đối chiếu và mục menu.
- [x] `HoaDonController`: thu tiền theo DTO mới, cấu hình trả một phần, QR theo số còn lại, hủy hóa đơn qua `InvoiceLedgerService`.
- [x] `LichSuThanhToan`: "Hủy ghi nhận thanh toán" có lý do, bộ lọc "Có tiền thừa", nhãn thẻ.
- [x] Sửa lời gọi thu tiền ở `PhongTros/QuanLyPhongTro.cshtml`.

## 8. Màn công nợ (ngoài spec, đã duyệt)

- [x] Đổi lọc `== ChuaThanhToan` sang `!= DaThanhToan` và hiển thị số còn lại: `PhongTroStore`, `DashboardStore`/`DashboardService`, `HoaDonStore.GetUnpaidInvoicesAsync`, `AiAssistantService`, Dashboard khách.

## 9. Kiểm thử tích hợp và nghiệm thu

- [x] Infrastructure: store queries, đồng thời (hai xác nhận, hai lần tạo lượt, trùng mã giao dịch), lỗi unique thành lỗi nghiệp vụ, `HetHan` được lưu khi lỗi nghiệp vụ.
- [x] Web: luồng khách trọn vẹn, chặn khách khác, 403 khác chi nhánh, 403 cấu hình một phần với nhân viên, tra cứu `HetHan`/`DaHuy`, thiếu antiforgery.
- [x] `dotnet build`, toàn bộ test trên `quanlyphongtro_test`, `has-pending-model-changes`, không có thay đổi trong `Migrations/`, `git diff --check`.
- [x] Review chất lượng (3 agent, 03/10/2026): sửa thẻ hệ thống tự gõ, ghi chú thô hiện cho khách, `HuyHoaDon` dùng `TimeProvider`, lỗi nghiệp vụ khi trạng thái lệch ledger; bổ sung test (`724977e`).
- [x] Cập nhật `spec.md` lên 2.1 (§2, §12, §18, §20, §22, §23.3, §26, §32, §36) theo các quyết định trên.
- [x] Xem giao diện trên app (04/10/2026, người dùng kiểm và phản hồi): sửa màn Đối chiếu (ảnh minh chứng, cuộn bảng, cột mã bị đè), Lịch sử thanh toán (mã HĐ xuống dòng), nhãn "Thanh toán một phần" (`white-space: nowrap`).
- [x] Thêm nút "Thu phần còn lại" sau khi xác nhận khoản chuyển thiếu (mở form thu tiền thủ công của hóa đơn).
- [x] `LichSuThanhToan` (danh sách và thống kê) lọc theo chi nhánh được phân công của nhân viên; Admin xem tất cả; test `LichSuThanhToanBranchScopeTests`.
- [x] Bỏ `IHoaDonStore.GetCancellationBlockersAsync`, record `InvoiceCancellationBlockers` và test của hàm này (không còn nơi gọi).
- [ ] Push nhánh và mở PR.
