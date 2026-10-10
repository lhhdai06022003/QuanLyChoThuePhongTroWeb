# Review: Hóa đơn tự động

- Nhánh `feature/hoa-don-tu-dong`, tách từ `master` = `dc391e3`; spec commit ở `e444f37`. Code đợt A commit trên nhánh này ngày 10/10/2026.
- Nguồn đối chiếu: [spec.md](spec.md) v7, [plan-dot-a.md](plan-dot-a.md).
- File này gộp các lần đánh giá theo đợt. Mỗi đợt gồm: coder, tester, reviewer `/ship`, và phần sửa/kiểm chứng của phiên chính.

## Đợt A: chuỗi tự động và dữ liệu (spec mục 3, 4, 5.1, 5.2, 7.1 phần dữ liệu, 7.2 tạm tính, 8, 9, 10)

### Phán quyết: ĐẠT (sau khi sửa lỗi bắt buộc)

Reviewer `/ship` cho phán quyết **CẦN SỬA** vì một lỗi chức năng (mục "Lỗi đã sửa"), và ghi rõ "sửa xong và chạy lại test liên quan thì đợt A có thể CHOT". Phiên chính đã sửa lỗi đó và chạy lại Application + Web test: PASS. Schema và tiền không có điểm nào phải chặn.

### Kiểm chứng

| Lệnh hoặc thao tác | Kết quả | Ghi chú |
| --- | --- | --- |
| `dotnet ef migrations has-pending-model-changes` | PASS | Trước khi sửa và sau migration; reviewer chạy lại |
| `dotnet build QuanLyChoThuePhongTroWeb.sln` | PASS | 0 lỗi; chạy lại sau khi sửa link |
| `dotnet test` Domain.UnitTests | PASS 129/129 | Coder, tester, reviewer. Không chạy lại sau khi sửa link (không đụng Domain) |
| `dotnet test` Application.UnitTests | PASS 991/991 | Chạy lại sau khi sửa link |
| `dotnet test` Infrastructure.IntegrationTests (DB `quanlyphongtro_test`) | PASS 372/372 | Tester, reviewer, và chạy lại sau khi sửa link (một mình, trước Web không chồng nhau) |
| `dotnet test` Web.IntegrationTests (cùng DB, chạy tuần tự) | PASS 266/266 | Chạy lại sau khi sửa link. Lượt đầu của reviewer FAIL 250/514 (tổng gần gấp đôi), đúng hiện tượng FAIL giả khi hai lượt test chồng nhau trên cùng DB ghi trong `AGENTS.md`; chạy lại một mình PASS |
| `git diff --check` | PASS | Chỉ cảnh báo LF/CRLF |
| Thử tay trên trình duyệt | CHƯA CHẠY | Chưa có giao diện đợt C; luồng HTTP phủ bằng Web test (`AutoInvoiceTriggerTests`, `TenantInvoicePortalTests`) |
| Gửi email qua SMTP thật | CHƯA CHẠY | Web test dùng `FakeEmailService` |

### Cổng xác nhận

Người dùng xác nhận ngày 10/10/2026 17:13 (ghi trong [plan-dot-a.md](plan-dot-a.md)):
- Schema S1–S5: cột `chi_nhanh."SoGioPhanHoiHoaDon"` (default 24, CHECK 1–168), cột `hoa_don."HanPhanHoi"`, index lọc `IX_hoa_don_TrangThaiPhatHanh_HanPhanHoi`, migration `20261010101505_AddAutoInvoiceReviewWindow`.
- Tiền T1–T8.
- Câu hỏi mở 1, 2, 3, 5, 6, 7 theo đề xuất; câu 4 đổi theo người dùng: lý do "số giảm" (`ChiSoGiam`) cũng báo khách.

Reviewer đối chiếu: không có thay đổi schema hay tiền ngoài S1–S5/T1–T8; migration chỉ gồm 2 cột, 1 CHECK, 1 index lọc, `Down` gỡ đủ; không sửa migration cũ hay `ApplicationDbContext`.

### Đã đạt (reviewer)

1. **Tiền:** hóa đơn tạm tính dùng đúng `CreateDraftsAsync` của luồng tay. Test `AutoInvoice_HasSameMoneyAsManualFlowForIdenticalRoom` so từng dòng chi tiết và tổng giữa luồng tự động và luồng tay (cùng 3.270.000). Sửa `HoaDonId` trả về sau `SaveChanges` (lỗi có sẵn: trước đây luôn bằng 0 với EF thật) không đổi hành vi khác.
2. **Phân quyền:** người thực hiện phải có `Meter.Review`, `Invoice.Draft`, `Invoice.Submit` tại chi nhánh, thiếu thì dừng và báo; từng service con tự kiểm lại. Khách không bao giờ là người thực hiện; nhân viên dự phòng không bị chọn nhầm chi nhánh khác. Ranh giới tầng và `BranchScopeArchitectureTests` đạt.
3. **Log:** lý do đã lường trước chỉ ghi Information/Warning; lỗi SMTP/SignalR không hoàn tác dữ liệu và không đổi kết quả thao tác gốc.
4. **Phía khách:** thấy hóa đơn tạm tính ở danh sách và chi tiết; PDF 404; mọi endpoint thanh toán 404; không tính vào tổng nợ Dashboard khách, danh sách nợ và nhắc nợ. `KhachXemDuoc` không bị sửa.
5. **DbContext dùng chung:** `ChangeTracker.Clear()` chỉ chạy sau khi thao tác gốc đã commit; không mất hay ghi nhầm dữ liệu.
6. **Chạy lặp/song song:** chỉ ra một hóa đơn (unique index + khóa dòng).
7. **Phạm vi:** đúng đợt A, không có job tự chốt, điều chỉnh hay giao diện.

### Lỗi đã sửa

| # | Lỗi | Sửa | Kiểm chứng |
| --- | --- | --- | --- |
| A1 | Thông báo "chưa tạo được hóa đơn tạm tính" gửi nhân viên/Admin có link `/QuanLyNhaTro/DienNuoc`. `DienNuocController` là `[ApiController]`, trang thật ở `QuanLyNhaTro/ChotDienNuoc`, nên link mở ra 404. Lỗi có từ kế hoạch, coder chép theo | `AutoInvoiceService.cs`: link đổi thành `/QuanLyNhaTro/ChotDienNuoc?thang={Thang}&nam={Nam}` (cùng dạng với `ViecCanLamService`). `AutoInvoiceServiceTests.cs`: sửa assertion đang khẳng định link sai | Thêm vào `AutoInvoiceTriggerTests.TenantUpload_WithLowConfidence_CreatesNoInvoice_AndNoEmail`: đọc link trong thông báo thật của nhân viên, đăng nhập bằng nhân viên đó, GET link phải trả 200. Application 991/991, Web 266/266 PASS |

### Lưu ý mang sang đợt B/C

| # | Nội dung | Mức | Xử lý |
| --- | --- | --- | --- |
| L1 | Chi nhánh chưa khai báo đơn giá điện nước: hóa đơn tạm tính chỉ có tiền phòng mà vẫn gửi khách, không cảnh báo (luồng tay cũng tính như vậy, nhưng có người nhìn bản nháp) | Trung bình ở đợt A; **chặn trước khi bật job tự chốt ở đợt B** (sẽ thành thu thiếu tiền thật) | Đề xuất lý do dừng mới `ThieuDonGia` (tiêu thụ > 0 mà đơn giá của kỳ bằng 0). Đổi điều kiện T2 nên phải vào CẦN NGƯỜI DÙNG XÁC NHẬN của kế hoạch đợt B |
| L2 | Chốt nhiều phòng: `SaveChotDienNuocAsync` chạy chuỗi lần lượt từng phòng kèm gửi SMTP đồng bộ trong HTTP request; 30–50 phòng có thể mất 1–2 phút, dễ quá timeout. Dữ liệu không hỏng | Trung bình | Cân nhắc ở đợt B: hàng đợi nền hoặc gửi email sau vòng lặp. Đổi thiết kế, cần người dùng duyệt |
| L3 | Nhánh khôi phục khi chạy song song đi tiếp mà không chạy lại `EvaluateRoom` với chỉ số người khác vừa xác nhận; email lấy chỉ số từ kết quả đánh giá cũ | Thấp (cửa sổ rất hẹp) | Gọi lại `EvaluateRoom` trước bước tạo hóa đơn |
| L4 | Khoảng hở đã chấp nhận (câu 7): khách thấy nhãn cũ "Chưa thanh toán" và có thể thấy nút thanh toán (server 404); Admin vẫn chốt/trả lại tay được hóa đơn tạm tính; thông báo ghi "tự chốt lúc…" nhưng chưa có job | Đã chấp nhận | Đợt B (job, chặn chốt/trả lại tay), đợt C (giao diện). Không gộp `master` trước khi xong đợt C |
| L5 | `FakeMeterOcrService` của Web test mặc định độ tin cậy 0,90, vừa đạt ngưỡng: test cũ tải đủ hai ảnh có thể sinh hóa đơn tạm tính ngầm | Thấp | Nhớ khi viết test đợt B/C; test mới đặt giá trị riêng và `Reset()` |
| L6 | `HasDefaultValue(24)` trên `int`: gán 0 qua entity bị EF bỏ qua, DB tự đặt 24, CHECK không bắt được | Thấp | Đợt C validate 1–168 ở service khi sửa màn chi nhánh |
| L7 | Comment của `KhachXemDuoc` (`InvoicePaymentSnapshots.cs` khoảng dòng 22) lỗi thời | Thấp | Sửa ở đợt sau (kế hoạch đợt A không cho đụng file này) |
| L8 | Email dùng `ServerCertificateValidationCallback` trả `true` (bỏ kiểm chứng chứng chỉ SMTP), chép theo mẫu có sẵn trong `MailKitEmailService` | Bảo mật, lỗi cũ của file | Sửa chung cho cả file trong một đợt riêng |

### Quyết định của người dùng cho đợt B (10/10/2026)

- **L1:** dừng chuỗi với lý do mới `ThieuDonGia`. Khi kỳ có tiêu thụ điện (hoặc nước) > 0 mà đơn giá điện (hoặc nước) đã chụp vào kỳ bằng 0, không tạo hóa đơn tạm tính. Gửi thông báo cho nhân viên phụ trách phòng và Admin, nêu chi nhánh và loại đơn giá còn thiếu, kèm link trang chốt điện nước. Không báo khách. Đây là thay đổi điều kiện tiền T2, nên kế hoạch đợt B vẫn phải ghi vào mục CẦN NGƯỜI DÙNG XÁC NHẬN để đối chiếu.
- **L2:** chọn hướng (a). Trong `SaveChotDienNuocAsync`, gom email hóa đơn tạm tính lại và gửi sau vòng lặp, chạy nền, để request trả về ngay. Không thêm bảng hàng đợi, không đổi schema. Lỗi gửi email chỉ ghi log Warning, không hoàn tác dữ liệu.
