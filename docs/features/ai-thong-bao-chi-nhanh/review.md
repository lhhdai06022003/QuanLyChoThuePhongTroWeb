# Review: AI Assistant và Thông báo theo chi nhánh

- **Nhánh:** `feature/ai-thong-bao-chi-nhanh`
- **Căn cứ:** [spec.md](spec.md) 1.0 và [plan.md](plan.md).
- **Review code:** 05/10/2026, reviewer chỉ đọc code, thuộc dây chuyền `/ship`.
- **Thử tay:** người dùng, 06/10/2026.
- **Phán quyết:** **ĐẠT.**
  - Review code kết luận đạt có điều kiện: cần thử tay các phần chưa có test tự động.
  - Ngày 06/10/2026 người dùng báo phần chatbox và phần thông báo sự cố đều đạt.
  - Đã sửa M1, M2, L1–L6 trong nhánh này; L7 giữ nguyên theo quyết định của người dùng. Người dùng thử tay M2 và L2 đạt ngày 06/10/2026.

## 1. Schema và tiền

- **Schema không đổi.**
  - Diff không đụng `Migrations/`, `Persistence/Configurations/`, `ApplicationDbContext.cs` hay `Domain/`.
  - `dotnet ef migrations has-pending-model-changes` báo không có thay đổi model.
- **Tiền chỉ được đọc và hiển thị.** Code không ghi tiền, không làm tròn, không chuyển trạng thái hóa đơn hay thanh toán. Người dùng đã xác nhận 6 điểm tiền A1–A6 trong plan, trong đó A4 tính theo kỳ hóa đơn.
  - **A1/A2, công nợ:**
    - Chỉ lấy hóa đơn chưa xóa, đã gửi khách và chưa trả đủ.
    - Số đã thu là tổng các khoản thanh toán chưa xóa, giống màn Công nợ.
    - Số còn lại = tổng tiền − đã thu. Kiểu `decimal` từ đầu đến cuối.
  - **A3, doanh thu thực thu:** tính trong khoảng ngày theo giờ Việt Nam, chuyển sang UTC dạng nửa mở. Bỏ khoản thanh toán đã xóa và khoản thuộc hóa đơn đã xóa.
  - **A4, doanh thu theo chi nhánh:** theo tháng/năm của hóa đơn, gom theo chi nhánh. Không tính trùng.
  - **A5/A6, khách thuê:** công cụ luôn dùng id người thuê lấy từ phiên đăng nhập. Id do model gửi lên bị bỏ qua, có test chứng minh.

## 2. Đối chiếu với spec

| Spec | Kết quả |
|---|---|
| §4 Vai trò, §5 Công cụ | `AiToolCatalog` cấp Q1–Q9 cho Admin và nhân viên, K1–K3 cho khách thuê. Công cụ sai vai trò hoặc tên lạ bị từ chối trước khi truy vấn dữ liệu |
| §3 Fail closed | Không lấy được phạm vi thì từ chối. Nhân viên chưa phân công nhận câu cố định, không gọi model. Khách thuê thiếu id người thuê thì từ chối |
| §5.3 Phòng trùng số | Không có phòng khớp, hoặc phòng nằm ngoài phạm vi: trả "Không tìm thấy phòng". Nhiều phòng khớp: trả danh sách chi nhánh để AI hỏi lại |
| §6 Vòng lặp công cụ | Tối đa 3 vòng, mỗi vòng 5 lời gọi; lời gọi vượt mức nhận lỗi `VUOT_GIOI_HAN`. Lượt chốt không cho gọi công cụ |
| §6 Nhật ký | Mỗi lời gọi ghi một dòng log: id, vai trò, công cụ, kết quả, số dòng, thời gian xử lý. Không ghi câu hỏi, tham số hay dữ liệu |
| §7 Widget | Endpoint theo cổng, lịch sử theo tài khoản (`ai_chat_history_{userId}`), xóa lịch sử khi vào trang đăng nhập, giới hạn 1.000 ký tự, gửi 10 lượt gần nhất, có nút gợi ý |
| §8 Thông báo | Người nhận là Admin và nhân viên có phân công còn hiệu lực ở chi nhánh của phòng. Thông báo đẩy tới từng người; lỗi khi đẩy chỉ ghi cảnh báo. Đánh dấu đã đọc có kiểm chủ sở hữu |
| §9 Kiến trúc | Application không dùng EF, ASP.NET, `IQueryable`, `HttpClient`, `IConfiguration`. Web không tham chiếu Domain. Ba bộ test ranh giới đều đạt |
| F1 | Khách thuê chỉ báo sự cố được cho phòng thuộc hợp đồng của mình. Phần kiểm này nằm ở controller (plan cho phép) và chạy trước khi upload ảnh |
| F2 | Widget escape HTML trước khi dựng Markdown, tin nhắn người dùng hiển thị bằng `textContent` |
| Gemini | API key gửi ở header. Lượt model được gửi lại nguyên văn để giữ thoughtSignature. Response rỗng được xử lý thành lỗi. Lỗi HTTP không lộ nội dung kỹ thuật cho người dùng |

## 3. Vấn đề đã nêu

### Đã sửa

- **M1: độ dài từng phần tử lịch sử chat.**
  - Lỗi: `BuildTurns` chuyển nguyên từng tin nhắn cũ cho Gemini, không giới hạn độ dài. Người dùng có thể làm tốn hạn mức Gemini.
  - Sửa ngày 06/10/2026: cắt lượt `user` còn 1.000 ký tự và lượt `model` còn 4.000 ký tự (`AiAssistantService.BuildTurns`).
  - Thêm test `History_TruncatesOverlongItems_ByRole` và bổ sung quy tắc này vào spec §7.4.

- **Sửa ngày 06/10/2026 theo yêu cầu người dùng.** Các phương án cho L1–L4 và L7 do người dùng chọn, ghi ở `plan.md` §10.

| Mã | Mức | Vấn đề | Cách sửa |
|---|---|---|---|
| M2 | Trung bình, có từ trước | Lỗi XSS trong danh sách thông báo: `wwwroot/js/thongbao.js` chèn tiêu đề, nội dung và link vào HTML mà không escape. Toast SweetAlert dùng `title`, mà `title` được hiểu là HTML | Escape tiêu đề, nội dung, thời gian. Toast dùng `titleText`. Bỏ `onclick` nội tuyến, chuyển sang `data-*` cộng xử lý sự kiện. Chỉ nhận link nội bộ dạng `/...` |
| L1 | Thấp | Q2 và Q6 không giới hạn số dòng | Gửi cho AI tối đa 50 dòng. Số dòng và tổng còn nợ vẫn tính trên toàn bộ; có `ghiChu` khi danh sách bị rút gọn |
| L2 | Thấp | F1 nhận cả phòng của hợp đồng đã kết thúc hoặc đã hủy | Danh sách phòng và bước kiểm khi tạo sự cố chỉ lấy hợp đồng đang hoạt động |
| L3 | Thấp | K2 có thể trả chỉ số kỳ của người thuê trước | Chỉ trả kỳ từ tháng bắt đầu hợp đồng (giờ Việt Nam) |
| L4 | Thấp | Q8 bỏ chi nhánh doanh thu 0 | Liệt kê cả chi nhánh chưa xóa trong phạm vi, với doanh thu 0 |
| L5 | Thấp | HttpClient Gemini timeout 100 giây | Timeout 30 giây. Hết thời gian thì người dùng nhận câu báo lỗi chung |
| L6 | Thấp | Tên công cụ do model đặt ghi thẳng vào log | Chỉ ghi khi là định danh `[A-Za-z0-9_]` dài tối đa 64 ký tự, còn lại ghi "(không hợp lệ)" |

### Giữ nguyên

- **L7:** Q2 vẫn tính hóa đơn còn nợ của hợp đồng đã xóa mềm.
  - Hợp đồng đã kết thúc vẫn xóa được dù còn hóa đơn chưa trả, nên khoản nợ đó vẫn có thật.
  - Màn Công nợ cũng tính như vậy nên hai nơi vẫn khớp nhau.
  - Người dùng chọn giữ nguyên ngày 06/10/2026.

## 4. Kiểm chứng

| Lệnh hoặc thao tác | Ngày | Kết quả |
|---|---|---|
| `dotnet test QuanLyChoThuePhongTroWeb.sln` trên DB `quanlyphongtro_test` | 05/10 | Domain 122, Application 768, Infrastructure 299, Web 233; 0 rớt, 0 bỏ qua |
| `dotnet ef migrations has-pending-model-changes` | 05/10 | Không có thay đổi model |
| `git diff --check` | 05/10, 06/10 | Không lỗi |
| `dotnet test QuanLyChoThuePhongTroWeb.sln` trên DB `quanlyphongtro_test`, sau khi sửa M1 | 06/10 | Domain 122, Application 769, Infrastructure 299, Web 233; 0 rớt, 0 bỏ qua |
| Thử tay chatbox: widget, F2, phân quyền theo vai trò, công cụ, Gemini thật, đổi tài khoản | 06/10 | Người dùng báo đạt |
| Thử tay thông báo sự cố: Admin và nhân viên chi nhánh A nhận, nhân viên chi nhánh B không nhận | 06/10 | Người dùng báo đạt |
| `dotnet test QuanLyChoThuePhongTroWeb.sln` trên DB `quanlyphongtro_test`, sau khi sửa M2 và L1–L6 | 06/10 | Domain 122, Application 776, Infrastructure 300, Web 234; 0 rớt, 0 bỏ qua |
| `dotnet ef migrations has-pending-model-changes`, sau khi sửa M2 và L1–L6 | 06/10 | Không có thay đổi model |
| `escapeHtml` và `safeLink` của `thongbao.js` chạy bằng Node | 06/10 | Payload `<img onerror>` bị escape; `//host`, `javascript:` và URL tuyệt đối bị đổi thành `#` |
| Thử tay M2 (tiêu đề sự cố chứa HTML ở toast và danh sách thông báo, bấm mở trang Sự cố) và L2 | 06/10 | Người dùng báo đạt |

Còn hạn chế:
- Widget và `thongbao.js` chưa có test JS tự động.
- Test `Anonymous_CannotCallEitherEndpoint` chỉ kiểm tra mã trả về khác 200.
