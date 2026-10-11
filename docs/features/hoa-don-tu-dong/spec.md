# Spec: Hóa đơn tự động từ chỉ số điện nước

- **Trạng thái:** v7, bản giao thực thi (10/10/2026). Người dùng đã duyệt nghiệp vụ và schema. Đã code đủ (đợt A–D và lượt dọn code, xem [plan.md](plan.md)), review ĐẠT ([review.md](review.md)); chưa gộp `master`.
- **Nhánh:** `feature/hoa-don-tu-dong` (tách từ `master` = `dc391e3`).
- **Phạm vi:** cổng quản lý + cổng khách thuê. Không đụng cổng khách vãng lai.
- **Ngoài phạm vi:** đối soát chuyển khoản tự động, Zalo/Web Push, IoT, hoàn tiền (xem [huong-mo-rong.md](huong-mo-rong.md)).

## 1. Mục tiêu

Khi kỳ điện nước của một phòng có chỉ số đáng tin cậy, hệ thống tự: chốt kỳ → tạo hóa đơn → gửi khách bản **tạm tính** → hết thời gian xem xét của chi nhánh thì tự chốt và công bố. Đây là quy trình mặc định cho mọi chi nhánh, không có công tắc bật/tắt.

Khách thuê **không thao tác gì với hóa đơn trong hệ thống** (chỉ xem). Thấy sai thì liên hệ nhân viên bên ngoài; nhân viên vào hệ thống bấm **"Điều chỉnh và gửi lại"**.

Cách lấy chỉ số giữ nguyên: khách thuê/nhân viên tải ảnh đồng hồ (OCR), nhân viên xác nhận ảnh, nhân viên nhập tay ở màn chốt điện nước.

## 2. Khái niệm

- **Hóa đơn tự động:** hóa đơn có `HanPhanHoi` khác null.
- **Tạm tính:** hóa đơn tự động ở `ChoDuyet`. Khách xem được, chưa thanh toán được, chưa tính công nợ.
- **Hạn tự chốt (`HanPhanHoi`):** thời điểm UTC = lúc gửi khách + `ChiNhanh.SoGioPhanHoiHoaDon` tại lúc gửi. Đổi số giờ của chi nhánh không làm dời hạn của hóa đơn đã gửi.

## 3. Schema (đã duyệt)

Một migration `AddAutoInvoiceReviewWindow`. Quy ước: tên bảng snake_case, tên cột PascalCase có ngoặc kép.

| Bảng.Cột | Kiểu | Ràng buộc |
|---|---|---|
| `chi_nhanh."SoGioPhanHoiHoaDon"` | `integer` | `NOT NULL DEFAULT 24`; `CK_ChiNhanh_SoGioPhanHoiHoaDon`: `"SoGioPhanHoiHoaDon" BETWEEN 1 AND 168` |
| `hoa_don."HanPhanHoi"` | `timestamp with time zone` | `NULL` |

Index lọc `IX_hoa_don_TrangThaiPhatHanh_HanPhanHoi` trên (`"TrangThaiPhatHanh"`, `"HanPhanHoi"`) `WHERE "HanPhanHoi" IS NOT NULL AND "IsDeleted" = false`.

- `HanPhanHoi` giữ nguyên sau khi chốt, công bố hoặc hủy (để biết hóa đơn từng đi luồng tự động). Không thêm CHECK giữa `HanPhanHoi` và trạng thái.
- Không thêm bảng. Dấu vết lưu ở `lich_su_trang_thai_hoa_don."LyDo"` (mục 9).
- Không đổi dữ liệu cũ, không sửa migration cũ. `Down` gỡ đủ. Kiểm bằng `has-pending-model-changes` và Infrastructure integration test trên DB `_test`.

## 4. Điều kiện tự chạy

### 4.1 Chỉ số đáng tin cậy

Một chỉ số (điện hoặc nước) của kỳ là đáng tin cậy khi:
1. **Nguồn:** ảnh OCR `DocDuoc` có `DoTinCay >= NguongDoTinCay` (mặc định 0,90); **hoặc** do nhân viên/Admin xác nhận ảnh (`ConfirmImageAsync`), sửa số (`CorrectConfirmedImageAsync`) hay nhập tay ở màn chốt.
2. **Không giảm:** chỉ số mới `>=` chỉ số cũ của kỳ.
3. **Không bất thường:** tiêu thụ (mới − cũ) `<= HeSoBatThuong` (mặc định 3) × trung bình tiêu thụ của tối đa `SoKyTrungBinh` (mặc định 3) kỳ `DaDuyet` gần nhất **trước kỳ đang xét** của phòng. Nếu chưa có kỳ nào hoặc trung bình bằng 0: tiêu thụ `<=` trần tuyệt đối (`TranDienKwh` = 500, `TranNuocM3` = 30).

Điều kiện 3 áp dụng cho **mọi nguồn**, kể cả nhập tay: nhân viên nhập số bất thường thì kỳ vẫn được chốt như hiện nay nhưng không tự tạo hóa đơn (mục 4.3).

### 4.2 Điều kiện của phòng

1. Cả **điện và nước** đều có chỉ số đáng tin cậy (mỗi kỳ `DichVuDienNuocCuaPhong` luôn có cả hai).
2. Phòng có hợp đồng hiệu lực trong kỳ; kỳ trước `DaDuyet` hoặc chưa có kỳ trước (quy tắc sẵn có của `ApprovePeriodsAsync`).
3. Kỳ chưa có hóa đơn hoạt động (quy tắc sẵn có của `CreateDraftsAsync`, trả `SkippedHasActiveInvoice`).
4. Khách thuê đại diện của hợp đồng có ít nhất một kênh nhận: tài khoản cổng (`NguoiDung` liên kết `NguoiThueId`, đang hoạt động) hoặc email.
5. Tìm được người thực hiện (mục 8), và người đó có quyền ở từng bước (`Meter.Review`, `Invoice.Draft`, `Invoice.Submit` tại chi nhánh) như khi làm tay.

### 4.3 Không đạt

Không tạo hóa đơn, không làm hỏng thao tác vừa làm (tải ảnh, xác nhận, chốt). Gửi một thông báo cổng cho nhân viên phụ trách và Admin qua `IThongBaoService.GuiChoNguoiPhuTrachPhongAsync(phongTroId, …)`, và cho khách thuê qua `GuiChoKhachThueAsync(nguoiThueId, …)` nếu lý do là việc khách cần làm (chụp lại). Các thông báo khác của spec dùng cùng hai method này. Nội dung nêu lý do: độ tin cậy thấp, không đọc được, số giảm, tiêu thụ bất thường, kỳ trước chưa chốt, khách không có kênh nhận, chi nhánh không có nhân viên, người thực hiện thiếu quyền. Hai lý do sau **không** gửi thông báo vì là trạng thái bình thường: còn thiếu chỉ số điện/nước; kỳ đã có hóa đơn hoạt động.

Lý do đã lường trước ghi log mức Information/Warning, không dùng Error (`CustomWebApplicationFactory` FAIL khi có log Error ngoài dự kiến). Error chỉ cho exception ngoài dự kiến.

## 5. Chuỗi tự động

### 5.1 Điểm kích hoạt

Chạy cho đúng một phòng/kỳ, **sau khi** thao tác gốc đã commit thành công:

| Thao tác gốc | Service hiện có |
|---|---|
| Khách hoặc nhân viên tải ảnh, OCR chạy xong | `MeterReadingWorkflowService.UploadImageAsync` (gọi từ `MeterImagePortalService.UploadAsync`) |
| Nhân viên xác nhận ảnh | `ConfirmImageAsync` |
| Nhân viên sửa số đã xác nhận (kỳ chưa có hóa đơn) | `CorrectConfirmedImageAsync` |
| Nhân viên chốt kỳ ở màn điện nước | `DienNuocService.SaveChotDienNuocAsync` → `ApprovePeriodsAsync` (chạy cho từng phòng vừa chốt) |

Lỗi của chuỗi chỉ được ghi log và thông báo, không đổi kết quả trả về của thao tác gốc.

### 5.2 Các bước (từng phòng, độc lập với phòng khác)

1. Đồng hồ nào **chưa có chỉ số chính thức** mà có ảnh OCR đạt 4.1: tự xác nhận ảnh đó làm chỉ số chính thức, `NguoiXacNhanId` = người thực hiện, ghi chú `"Tự xác nhận, độ tin cậy {x}"`. Đồng hồ đã có chỉ số chính thức thì không tự thay bằng ảnh mới (nhân viên tự xác nhận nếu muốn).
2. Nếu kỳ chưa `DaDuyet`: chốt kỳ.
3. Tạo hóa đơn nháp (cùng cách tính của `CreateDraftsAsync`).
4. Chuyển `Nhap → ChoDuyet`, đặt `HanPhanHoi`.
5. Sau bước 4: gửi khách thông báo cổng + email (mục 7.2).

Các bước được dùng transaction của service sẵn có. Dừng giữa chừng thì dữ liệu luôn ở một trạng thái hợp lệ của luồng tay, và nhân viên chi nhánh nhận thông báo "chưa tạo được hóa đơn tạm tính":
- Dừng sau bước 1 hoặc 2 (ảnh đã xác nhận, kỳ đã chốt): lần kích hoạt sau chạy tiếp từ bước còn thiếu.
- Dừng sau bước 3 (có hóa đơn `Nhap` không có hạn): **không** tự chạy tiếp, vì không phân biệt được với nháp tạo tay (điều kiện 4.2.3). Nhân viên xử lý theo luồng tay (việc `HD1`).

Kích hoạt lặp (hai thao tác gần nhau) không tạo hóa đơn thứ hai: `CreateDraftsAsync` bỏ qua kỳ đã có hóa đơn hoạt động, và unique index (`HopDongId`, `Thang`, `Nam`) lọc `"IsDeleted" = false` chặn ở DB.

### 5.3 Tự chốt khi hết hạn

`AutoInvoiceFinalizeJob` chạy mỗi `ChuKyJobPhut` phút (mặc định 5). Nghiệp vụ nằm ở Application; Infrastructure chỉ lập lịch, đăng ký theo `BackgroundJobs:Enabled` như các job hiện có.

Mỗi lượt lấy hóa đơn `ChoDuyet`, `IsDeleted = false`, `HanPhanHoi <= now` (theo index ở mục 3). Với từng hóa đơn, trong một transaction và khóa dòng (khóa kỳ trước, hóa đơn sau, như `FinalizeAsync`):
1. Kiểm lại: vẫn `ChoDuyet`, `HanPhanHoi <= now`, kỳ chỉ số vẫn `DaDuyet`, tổng tiền khớp tổng chi tiết, không có hóa đơn hoạt động khác cùng hợp đồng/kỳ (cùng các kiểm tra của `FinalizeAsync`). Không đạt: bỏ qua, ghi Warning, báo nhân viên chi nhánh.
2. `ChoDuyet → DaChot` → `DaChot → DaGui` (đặt `NgayGui`, `HanThanhToan` theo `SoNgayHanThanhToan` như `PublishAsync`).

Sau commit: thông báo cổng + email kèm PDF như công bố hiện tại. Lỗi một hóa đơn không ảnh hưởng hóa đơn khác; email lỗi không hoàn tác chốt. Nhân viên đang điều chỉnh cùng lúc: một bên chờ khóa, bên sau kiểm lại trạng thái và dừng nếu không còn hợp lệ.

Job không kiểm quyền nhân viên (`Invoice.Finalize` vẫn chỉ Admin với thao tác tay). Người thực hiện ghi vào lịch sử = người của lần chuyển sang `ChoDuyet` gần nhất (bước 5.2.4 hoặc người bấm điều chỉnh ở mục 6).

## 6. Nhân viên "Điều chỉnh và gửi lại"

Một thao tác trên màn hóa đơn quản lý, dùng cho hóa đơn tự động ở `ChoDuyet`, hoặc `DaGui` chưa có khoản thu. Nhập chỉ số điện và nước mới (điền sẵn số hiện tại) **và/hoặc sửa các khoản phí** của hóa đơn (tên, đơn giá, số lượng, thêm, xóa dòng), như màn "Chỉnh sửa Chi tiết Hóa đơn" của luồng tay. **Không có ô lý do.**

> **Cập nhật 11/10/2026 theo yêu cầu người dùng** (kế hoạch: [plan.md](plan.md) mục 2.4, T29): bản v7 chỉ cho nhập chỉ số. Danh sách dòng gửi lên là danh sách cuối cùng của hóa đơn; đổi chỉ số thì dòng điện/nước của đồng hồ đó được tính lại cùng công thức lúc tạo hóa đơn rồi vẫn sửa tay được. Cần thêm quyền `Invoice.Draft`.

Chặn chung cho 6.2 và 6.3 (giữ nguyên dữ liệu, báo lý do): kỳ sau đã chốt hoặc đã có hóa đơn phát hành (`HasSubsequentPeriodAsync`); chỉ số mới nhỏ hơn chỉ số cũ của kỳ.

### 6.1 Quyền

Admin, hoặc nhân viên có `Meter.Review`, `Invoice.Draft` và `Invoice.Submit` tại chi nhánh của phòng (thêm `Invoice.Cancel` cho trường hợp 6.3). Kiểm qua `IEmployeeAccessService`, fail closed. Method nhận `actorId` đầu tiên (theo `BranchScopeArchitectureTests`).

### 6.2 Hóa đơn đang tạm tính (`ChoDuyet`)

Trong **một** transaction: `ChoDuyet → Nhap` → cập nhật chỉ số của kỳ → hóa đơn nháp tự tính lại (`RecalculateDraftInvoices` chỉ chạy ở `Nhap`) → `Nhap → ChoDuyet` với `HanPhanHoi` mới = bây giờ + số giờ hiện tại của chi nhánh. Thất bại ở bất kỳ bước nào: rollback, hóa đơn giữ trạng thái và hạn cũ. Đây là thao tác đổi tiền nên bắt buộc nguyên tử; `CorrectConfirmedImageAsync` và `ApprovePeriodsAsync` hiện tự mở transaction, nên phần lõi của chúng phải được tách ra để chạy trong transaction chung (hành vi công khai của hai method giữ nguyên).

Lưu ý code hiện có:
- `CorrectConfirmedImageAsync` từ chối khi kỳ có hóa đơn khác `Nhap` (`HasLockedInvoiceAsync`), nên phải về `Nhap` trước trong cùng transaction.
- Có ảnh chính thức thì sửa số ảnh; không có (kỳ nhập tay) thì cập nhật như nhập tay ở `ApprovePeriodsAsync`.
- Hai chỗ đó bắt lý do (`LyDo`, `LyDoDienThuCong`, `LyDoNuocThuCong`): luồng này tự điền `"Điều chỉnh hóa đơn theo yêu cầu khách thuê"`. Khi dùng ở màn khác, hai chức năng vẫn bắt lý do như cũ.
- Bỏ qua điều kiện 4.1 mục 3 (bất thường): nhân viên đã chịu trách nhiệm số mới.
- Chỉ số mới trùng chỉ số hiện tại ở cả hai đồng hồ và các dòng khoản phí không đổi: báo "Không có thay đổi", không gửi lại.
- Có sửa dòng: thay toàn bộ dòng của hóa đơn trong cùng transaction (6.3: bản thay thế `-R{n}` nhận đúng danh sách dòng đã chốt). Dòng cũ không đổi đơn giá và số lượng giữ thành tiền đã lưu; tổng tiền phải lớn hơn 0.

### 6.3 Hóa đơn đã công bố (`DaGui`)

Chỉ khi hủy được theo đúng điều kiện của `InvoiceLedgerService.CancelInvoiceAsync` hiện có: chưa thu đồng nào (`DaThu = 0`, `TrangThaiHoaDon = ChuaThanhToan`) và không có yêu cầu thanh toán `DangDoiChieu`. Yêu cầu thanh toán đang `ChoThanhToan` bị hủy theo như luồng hủy hiện có. Không đạt: nút khóa, hiện lý do ("Hóa đơn đã có khoản thu" hoặc "Khách đã nộp minh chứng, cần đối chiếu trước").

Trong một transaction: hủy hóa đơn cũ (`DaHuy`, `IsDeleted = true`, lý do tự điền như 6.2) → cập nhật chỉ số → tạo hóa đơn thay thế (mã `-R1`, `-R2`… như luồng hủy/tạo lại hiện có) → `Nhap → ChoDuyet` với `HanPhanHoi` mới. Bắt buộc nguyên tử như 6.2; `CancelInvoiceAsync` và `CreateDraftsAsync` cũng tự mở transaction nên phần lõi phải tách tương tự. Hóa đơn cũ vẫn hiện cho khách là "Đã hủy" (quy tắc sẵn có `DaHuy && NgayGui != null`).

### 6.4 Sau khi điều chỉnh

Gửi khách thông báo cổng + email "hóa đơn đã được điều chỉnh", kèm số tiền mới và hạn tự chốt mới.

## 7. Giao diện và thông báo

### 7.1 Cổng khách thuê

Quy tắc "khách xem được hóa đơn" hiện là `(DaGui && !IsDeleted) || (DaHuy && NgayGui != null)`. Thêm vế `(ChoDuyet && HanPhanHoi != null && !IsDeleted)` vào **đúng hai chỗ**: `HoaDonStore.GetInvoicesByContractIdsAsync` (danh sách) và `HoaDonStore.CheckHoaDonOwnershipAsync` (chi tiết, dùng bởi `InvoiceViewService.GetTenantDetailAsync`).

**Không sửa** `InvoicePaymentSnapshots.KhachXemDuoc`: hàm này chỉ dùng cho luồng thanh toán (`InvoicePaymentRequestService`), nên hóa đơn tạm tính tiếp tục không vào được mọi màn thanh toán.

- Danh sách và chi tiết: hóa đơn tạm tính hiện nhãn **"Tạm tính, tự chốt lúc dd/MM/yyyy HH:mm"** (giờ Việt Nam), chi tiết tiền, chỉ số cũ/mới, số điện thoại chi nhánh (`ChiNhanh.SoDienThoai`) với dòng "Nếu có sai sót, vui lòng liên hệ".
- Không có nút thao tác. Không thanh toán được.
- Không tải PDF: `InvoiceViewService.ExportTenantPdfAsync` đi qua `GetTenantDetailAsync`, nên phải thêm kiểm tra riêng trả null (Web trả 404) khi hóa đơn đang tạm tính.
- Tổng nợ ở Dashboard khách (`KhachThue/DashboardController`) đã chỉ cộng hóa đơn `DaGui`: giữ nguyên, thêm test để không bị tính nhầm.

### 7.2 Email và thông báo cho khách

| Lúc | Nội dung |
|---|---|
| Gửi tạm tính (5.2) | Kỳ, tổng tiền, chi tiết, chỉ số, giờ tự chốt, số điện thoại chi nhánh, link cổng. Không đính PDF |
| Sau điều chỉnh (6.4) | Như trên, tiêu đề "đã được điều chỉnh" |
| Tự chốt (5.3) | Như công bố hiện tại (PDF, VietQR, hạn thanh toán) |

Không có email thì chỉ gửi thông báo cổng; không có tài khoản cổng thì chỉ gửi email. Lỗi gửi được ghi log, không hoàn tác dữ liệu.

### 7.3 Cổng quản lý

- **Màn hóa đơn:** cột/nhãn "Tạm tính, tự chốt lúc…" cho hóa đơn tự động; nút "Điều chỉnh và gửi lại" (mục 6).
- **Hóa đơn tự động ở `ChoDuyet`:** ẩn nút Chốt và Trả lại; server từ chối `FinalizeAsync`, `RejectAsync` với thông báo "Hóa đơn đang tự chốt theo hạn, dùng Điều chỉnh và gửi lại". Nút Hủy giữ quy tắc hiện có (chỉ `DaChot`/`DaGui`), nên hóa đơn tạm tính không hủy được, chỉ điều chỉnh được.
- **Luồng tay** (Phát sinh → Gửi duyệt → Admin chốt → Công bố) giữ nguyên; `SubmitAsync` tay luôn đặt `HanPhanHoi = null`.
- **Màn chi nhánh** (`ChiNhanhsController`, chỉ Admin; `ChiNhanhReq`): thêm trường "Số giờ xem xét hóa đơn", số nguyên 1–168, mặc định 24.
- **Việc cần làm** (`ViecCanLamService`):
  - `HD2` "Hóa đơn chờ Admin chốt": loại hóa đơn có `HanPhanHoi != null`.
  - Thêm `HD4` "Hóa đơn tạm tính chờ tự chốt": hóa đơn tự động ở `ChoDuyet`, mức Theo dõi, quyền `Invoice.Read`, cùng cách lọc chi nhánh như các việc khác.
  - Thêm `HD5` "Chưa tạo được hóa đơn tạm tính T{m}/{y}" (cập nhật 11/10/2026, [plan.md](plan.md) T28): phòng có kỳ đã chốt, chưa có hóa đơn nào của kỳ đó, mà chuỗi tự tạo (mục 4) sẽ dừng có thông báo (ví dụ tiêu thụ bất thường, thiếu đơn giá). Mức Cần làm, quyền `Invoice.Draft`, link `ChotDienNuoc?thang&nam`. Tính từ dữ liệu hiện tại, nên tự hết khi có hóa đơn hoặc hết bất thường.
  - `CS2` "Ảnh chỉ số chờ xác nhận" chỉ đếm ảnh của kỳ chưa chốt và chưa bị khóa bởi kỳ sau (cập nhật 11/10/2026, T27).

## 8. Người thực hiện

Không có tài khoản hệ thống. `NguoiThucHienId` của bước tự động:
1. Thao tác gốc do nhân viên/Admin làm: chính người đó.
2. Thao tác gốc do khách thuê làm (tải ảnh): nhân viên có phân công chi nhánh còn hiệu lực (`NhanVienChiNhanh.IsActive`, chưa thu hồi; tài khoản đang hoạt động, chưa xóa) với `NgayPhanCong` sớm nhất, cùng ngày thì `NhanVienChiNhanhId` nhỏ nhất.
3. Không có ai: không tự chạy (mục 4.3, báo Admin).

Thao tác "Điều chỉnh và gửi lại" ghi đúng người bấm.

## 9. Dấu vết trong lịch sử

Mỗi chuyển trạng thái ghi một dòng `LichSuTrangThaiHoaDon` như hiện tại. `LyDo` của luồng mới mở đầu bằng nhãn, đặt trong một lớp hằng ở Application (theo cách `PaymentNoteTags`):

| Nhãn | Dùng cho |
|---|---|
| `[TU_DONG]` | 5.2.4 gửi tạm tính, 5.3 tự chốt và tự công bố |
| `[DIEU_CHINH]` | Mọi chuyển trạng thái trong mục 6 |

## 10. Cấu hình

`AutoInvoiceOptions` (Application `Common/Configurations`, bind và `Validate()` ở `AddInfrastructure`, thêm mục mẫu vào `appsettings.example.json`):

| Khóa | Mặc định | Ràng buộc |
|---|---|---|
| `NguongDoTinCay` | 0.90 | 0 < x ≤ 1 |
| `HeSoBatThuong` | 3 | > 0 |
| `SoKyTrungBinh` | 3 | ≥ 1 |
| `TranDienKwh` | 500 | > 0 |
| `TranNuocM3` | 30 | > 0 |
| `ChuKyJobPhut` | 5 | ≥ 1 |

Số giờ xem xét nằm ở `chi_nhanh`, không ở cấu hình.

## 11. Tiêu chí chấp nhận

1. Khách tải ảnh điện rồi ảnh nước, cả hai OCR ≥ 0,90, số hợp lý: một hóa đơn `ChoDuyet` có `HanPhanHoi` = lúc gửi + số giờ chi nhánh; khách nhận thông báo cổng + email; ảnh `DaXacNhan` với `NguoiXacNhanId` là nhân viên theo mục 8.
2. Một ảnh OCR 0,89: không có hóa đơn; nhân viên chi nhánh, Admin và khách nhận thông báo lý do. Nhân viên xác nhận ảnh đó: hóa đơn được tạo như tiêu chí 1, người thực hiện là nhân viên đó.
3. Tiêu thụ vượt 3 lần trung bình (hoặc vượt trần khi chưa có kỳ trước): không có hóa đơn, có thông báo "tiêu thụ bất thường".
4. Nhân viên chốt kỳ nhập tay ở màn điện nước với số hợp lý: hóa đơn tạm tính được tạo cho các phòng vừa chốt.
5. Quá `HanPhanHoi`: trong một chu kỳ job, hóa đơn thành `DaGui`, có `HanThanhToan`, khách nhận email PDF như công bố hiện tại; lịch sử thêm hai dòng `[TU_DONG]` (chốt, công bố) với người thực hiện theo mục 5.3.
6. Admin đổi số giờ của chi nhánh: hạn của hóa đơn đã gửi không đổi; hóa đơn gửi sau dùng số giờ mới. Nhập 0 hoặc 169: bị từ chối.
7. Điều chỉnh khi `ChoDuyet`: tiền tính lại, hạn mới, khách nhận thông báo; không có ô lý do. Lỗi giữa chừng: hóa đơn giữ trạng thái và hạn cũ.
8. Điều chỉnh khi `DaGui` chưa có khoản thu: hóa đơn cũ `DaHuy`, có hóa đơn `-R1` tạm tính. Đã có khoản thu hoặc kỳ sau đã chốt: bị chặn, có lý do.
9. Hóa đơn tạm tính: khách thấy nhãn và số điện thoại chi nhánh, không có nút; gọi API tạo yêu cầu thanh toán hoặc tải PDF bị từ chối; không tính vào công nợ, Dashboard (quản lý và khách), nhắc nợ.
10. Admin không chốt/trả lại tay được hóa đơn tạm tính; luồng tay cũ cho hóa đơn không tự động vẫn chạy như trước.
11. Nhân viên chi nhánh khác không điều chỉnh được và không thấy hóa đơn tạm tính của chi nhánh đó (HTML, JSON, Việc cần làm).
12. Hai lần kích hoạt gần nhau cho cùng phòng/kỳ: đúng một hóa đơn.

## 12. Kiểm thử

| Project | Nội dung |
|---|---|
| `Domain.UnitTests` | Chuyển trạng thái mới của `HoaDon` (gửi tạm tính có hạn, về `Nhap` khi điều chỉnh, gửi duyệt tay xóa hạn) |
| `Application.UnitTests` (`FixedTimeProvider`, fake trong `Fakes/`) | Chính sách 4.1 sát ngưỡng (0,89/0,90; đúng 3 lần và hơn; 500/501 kWh; 30/31 m³; trung bình 0), 4.2 từng điều kiện, chọn người thực hiện mục 8, chuỗi 5.2 rollback, job 5.3 sát hạn và lỗi một hóa đơn, điều chỉnh 6.2/6.3 và các trường hợp chặn, quyền 6.1, `HD2`/`HD4` |
| `Infrastructure.IntegrationTests` (DB `_test`) | Migration và mapping hai cột, CHECK 1–168, index lọc, truy vấn hóa đơn đến hạn, hai truy vấn khách xem được (danh sách, sở hữu) |
| `Web.IntegrationTests` (`[Collection(WebTestCollection.Name)]`) | Khách thấy tạm tính, không thanh toán/tải PDF được; nhân viên ngoài chi nhánh bị từ chối; Admin bị chặn chốt tay hóa đơn tạm tính; màn chi nhánh validate số giờ; job không chạy trong factory |

Chạy `dotnet test` cho Infrastructure/Web chỉ khi có `QLCTPT_TEST_CONNECTION_STRING` trỏ DB `_test`; thiếu thì ghi CHƯA CHẠY. Không chạy hai lượt `dotnet test` song song.

## 13. Chia đợt cho `/ship`

| Đợt | Nội dung | Mục spec |
|---|---|---|
| A | Schema + Domain + `AutoInvoiceOptions` + chính sách 4.1/4.2 + người thực hiện + chuỗi 5.2 + điểm kích hoạt 5.1 + thông báo 4.3/7.2 (tạm tính) + quy tắc khách xem được 7.1 (phần dữ liệu) | 3, 4, 5.1, 5.2, 7.1, 7.2, 8, 9, 10 |
| B | Job tự chốt 5.3 + điều chỉnh 6 + chặn chốt/trả lại tay + `SubmitAsync` xóa hạn | 5.3, 6, 7.3 (phần server) |
| C | Giao diện: cổng khách 7.1, màn hóa đơn và màn chi nhánh 7.3, Việc cần làm `HD2`/`HD4` | 7.1, 7.3 |

Mỗi đợt build xanh, test của đợt PASS, review riêng trước khi sang đợt sau.

## 14. Rủi ro

- OCR đọc nhầm mà vẫn qua ngưỡng: hóa đơn sai được gửi khách. Giảm bằng 4.1 mục 3, thời gian xem xét và mục 6.
- Nhân viên không kịp điều chỉnh trước hạn: vẫn sửa được ở `DaGui` nếu chưa có khoản thu; `HD4` cho thấy trước.
- Không lưu lý do điều chỉnh: lịch sử chỉ có ai và lúc nào. Người dùng đã chấp nhận.
- Sau khi xong phải cập nhật `AGENTS.md` mục Hiện trạng: hóa đơn tự động, ngoại lệ của quy tắc "chỉ Admin chốt".
