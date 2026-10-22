# Đặc tả: AI Assistant và Thông báo theo chi nhánh

- Phiên bản: 1.0, ngày 05/10/2026. Người dùng đã duyệt.
- Phạm vi: trợ lý AI trong cổng quản lý và cổng khách thuê; thông báo sự cố mới gửi theo chi nhánh; quyền đánh dấu đã đọc thông báo.
- Ngoài phạm vi: thêm câu hỏi AI mới (người dùng sẽ bổ sung sau); RAG, MCP, Text-to-SQL; AI cho khách vãng lai; phân quyền theo nghiệp vụ trong cùng chi nhánh; thông báo cho các sự kiện khác ngoài sự cố; màn hình xem nhật ký AI.
- **Không đổi schema.**

---

## 1. Mục tiêu

1. Mỗi vai trò dùng đúng bộ công cụ AI của mình. Nhân viên không nhận bộ của khách thuê, khách thuê không gọi được công cụ quản lý.
2. Nhân viên chỉ thấy dữ liệu của chi nhánh được phân công, giống như các màn hình đã giới hạn ở bước 1.
3. AI không bịa số liệu. Không có dữ liệu thì nói rõ là không có; câu hỏi ngoài khả năng thì nói rõ là chưa hỗ trợ.
4. Sự cố mới chỉ báo cho người có trách nhiệm với chi nhánh đó.

## 2. Hiện trạng (kiểm tra trong code ngày 05/10/2026)

| # | Vấn đề | Vị trí |
|---|---|---|
| H1 | Chỉ khi `userRole == "Admin"` mới dùng bộ công cụ quản lý. Nhân viên nhận bộ công cụ và lời nhắc hệ thống của khách thuê | `AiAssistantService.cs` dòng 263, 268 |
| H2 | `HandleFunctionCallAsync` chạy mọi hàm quản lý mà không kiểm tra vai trò | `AiAssistantService.cs` dòng 410 |
| H3 | Các truy vấn quản lý không lọc theo chi nhánh | `AiAssistantService.cs` dòng 43–234 |
| H4 | Widget của khách thuê gọi `/QuanLyNhaTro/AiAssistant/Chat`, endpoint chỉ dành cho Admin/NhanVien, nên AI của khách thuê không dùng được. Không có chỗ nào truyền `nguoiThueId` | `_AiChatWidget.cshtml` dòng 391, `AiAssistantController.cs` |
| H5 | "Hóa đơn chưa thanh toán" tính cả hóa đơn nháp, chờ duyệt và đã hủy; chỉ lấy phòng đang thuê. Màn Công nợ chỉ tính hóa đơn đã gửi | `AiAssistantService.cs` dòng 68 |
| H6 | Tra cứu theo số phòng lấy phòng đầu tiên trùng số, không phân biệt chi nhánh | `GetCongNoPhongAsync`, `GetChiSoDienNuocAsync` |
| H7 | Chỉ xử lý một lời gọi hàm và một vòng (`parts[0]`). Lượt 2 mà AI muốn gọi thêm hàm thì trả "không phản hồi" | `AiAssistantService.cs` dòng 299–379 |
| H8 | Tham số sai làm `int.Parse` ném lỗi, chuỗi lỗi C# được gửi cho AI | `AiAssistantService.cs` dòng 432 |
| H9 | Client tự gửi `history` với `role` tùy ý, không giới hạn độ dài | `ChatRequest` |
| H10 | Lịch sử chat lưu trong `sessionStorage` dưới một khóa chung `ai_chat_history`. Người đăng nhập sau trên cùng tab thấy lịch sử của người trước | `_AiChatWidget.cshtml` dòng 439–452 |
| H11 | Logic AI nằm ở Infrastructure, gọi thẳng `DbContext`, nên không viết được unit test cho phần phân quyền | `Infrastructure/ExternalServices/AiAssistants` |
| H12 | Sự cố mới gửi cho **mọi** nhân viên (`GuiChoQuyenAsync(NhanVien)`), SignalR phát cho cả nhóm "NhanVien" | `KhachThue/SuCoController.cs` dòng 122–123 |
| H13 | `DanhDauDaDocAsync(id)` không kiểm tra chủ sở hữu thông báo | `ThongBaoService.cs`, `Controllers/ThongBaoController.cs` dòng 86 |

Thông báo thanh toán và phát hành hóa đơn đã gửi đúng chi nhánh hoặc đúng khách (`NotifyReviewersAsync(chiNhanhId)`, `NotifyTenantAsync`), nên giữ nguyên.

## 3. Nguyên tắc bất biến

1. **Server quyết định phạm vi dữ liệu.** Vai trò, chi nhánh và `NguoiThueId` lấy từ phiên đăng nhập, không lấy từ câu hỏi hay từ AI.
2. **Fail closed.** Công cụ không thuộc vai trò, thiếu claim, hoặc nhân viên chưa được phân công thì trả kết quả rỗng hoặc từ chối, không bao giờ trả dữ liệu toàn hệ thống.
3. **Code tính số, AI diễn đạt.** Tổng tiền, số lượng và số còn nợ do code tính sẵn trong kết quả công cụ.
4. **Dữ liệu ngoài phạm vi coi như không tồn tại.** Nhân viên hỏi phòng thuộc chi nhánh khác thì nhận "không tìm thấy phòng", không nhận "không có quyền", để không lộ phòng đó có tồn tại.
5. AI chỉ đọc dữ liệu, không ghi gì vào hệ thống.

## 4. Vai trò và phạm vi

| Vai trò | Bộ công cụ | Phạm vi dữ liệu |
|---|---|---|
| Admin | Quản lý (§5.1) | Toàn hệ thống |
| Nhân viên | Quản lý (§5.1), **kể cả doanh thu** | Chỉ các chi nhánh đang được phân công. Chưa được phân công: AI trả lời "Bạn chưa được phân công chi nhánh nào", không gọi công cụ |
| Khách thuê | Khách thuê (§5.2) | Chỉ dữ liệu của `NguoiThueId` trong phiên |

Endpoint:
- Cổng quản lý: `POST /QuanLyNhaTro/AiAssistant/Chat`, chỉ Admin và NhanVien.
- Cổng khách thuê: `POST /KhachThue/AiAssistant/Chat` (mới), chỉ KhachThue và phải có claim `NguoiThueId` hợp lệ.

## 5. Công cụ AI

**Ngày tháng mặc định:** khi người dùng không nói thời gian, lấy thời điểm hiện tại theo giờ Việt Nam (Asia/Bangkok, UTC+7). Ngoại lệ duy nhất là "hóa đơn chưa thanh toán": không nói tháng thì lấy tất cả các kỳ.

### 5.1 Bộ công cụ quản lý

| # | Công cụ | Mặc định khi thiếu tham số | Quy tắc |
|---|---|---|---|
| Q1 | Phòng đang thuê chưa chốt điện nước | Tháng và năm hiện tại | Lọc theo chi nhánh |
| Q2 | Hóa đơn chưa thanh toán | Mọi kỳ | Chỉ tính hóa đơn **đã gửi khách**, chưa trả đủ và chưa xóa; không phụ thuộc phòng còn thuê hay không; số còn lại tính giống màn Công nợ. Kết quả kèm số hóa đơn và tổng số còn nợ. Danh sách gửi cho AI tối đa 50 hóa đơn; số hóa đơn và tổng còn nợ vẫn tính trên toàn bộ, kèm ghi chú số hóa đơn chưa hiện |
| Q3 | Doanh thu thực thu trong khoảng ngày | Từ đầu tháng hiện tại đến hôm nay | Tổng các khoản thanh toán chưa bị hủy, lọc theo chi nhánh của phòng |
| Q4 | Phòng trống, có thể lọc theo giá tối đa | Không lọc giá | Lọc theo chi nhánh |
| Q5 | Hợp đồng sắp hết hạn trong N ngày | N = 30 | Kèm tên chi nhánh |
| Q6 | Tra cứu khách thuê theo tên hoặc số phòng | Bắt buộc có từ khóa | Kèm tên chi nhánh; trạng thái hợp đồng hiển thị tiếng Việt; tối đa 50 kết quả, kèm tổng số và ghi chú khi bị rút gọn |
| Q7 | Công nợ của một phòng | Bắt buộc có số phòng | Áp dụng §5.3 cho phòng trùng số; công nợ tính theo Q2 |
| Q8 | Doanh thu theo từng chi nhánh | Tháng và năm hiện tại | Nhân viên chỉ thấy các chi nhánh được phân công. Liệt kê cả chi nhánh trong phạm vi không có giao dịch (doanh thu 0) |
| Q9 | Chỉ số điện nước của một phòng | Tháng và năm hiện tại | Áp dụng §5.3 |

### 5.2 Bộ công cụ khách thuê

| # | Công cụ | Mặc định | Quy tắc |
|---|---|---|---|
| K1 | Hợp đồng của tôi | | Hợp đồng đang hoạt động của `NguoiThueId` trong phiên |
| K2 | Chỉ số điện nước của tôi | Tháng và năm hiện tại | Phòng của hợp đồng đang hoạt động, chỉ các kỳ từ tháng bắt đầu hợp đồng trở đi |
| K3 | Hóa đơn chưa thanh toán của tôi | Mọi kỳ | Giữ quy tắc hiện tại: hóa đơn đã gửi, chưa trả đủ |

### 5.3 Phòng trùng số giữa các chi nhánh

Công cụ Q7 và Q9 nhận thêm tên chi nhánh (không bắt buộc).
- Có đúng một phòng khớp trong phạm vi của người hỏi: trả kết quả.
- Có nhiều phòng khớp: trả danh sách chi nhánh để AI hỏi lại người dùng.
- Không có phòng nào khớp: "không tìm thấy phòng".

## 6. Cách AI xử lý câu hỏi

| Tình huống | Hành vi |
|---|---|
| Câu hỏi nhiều ý | AI gọi được nhiều công cụ trong một lượt, tối đa **3 vòng** và **5 lời gọi mỗi vòng**. Vượt giới hạn thì trả lời bằng dữ liệu đã có và nói rõ còn thiếu phần nào |
| Câu hỏi ngoài các công cụ ở §5 | Trả lời "Tôi chưa hỗ trợ câu hỏi này" kèm 2–3 câu hỏi gợi ý theo vai trò. Chào hỏi xã giao vẫn trả lời bình thường |
| Tham số sai kiểu hoặc ngoài khoảng (tháng 13, ngày sai định dạng, N âm) | Công cụ trả lỗi có cấu trúc, mô tả bằng tiếng Việt. AI hỏi lại người dùng, không tự đoán |
| AI gọi công cụ không thuộc vai trò | Server từ chối và trả lỗi "công cụ không khả dụng" cho AI; không chạy truy vấn |
| Không có dữ liệu | Kết quả có `soLuong = 0`; AI nói rõ là không có |
| Gemini lỗi, hết hạn mức, hoặc thiếu API key | Báo lỗi ngắn gọn bằng tiếng Việt, không hiện nội dung lỗi kỹ thuật |
| Câu trả lời có số liệu | **Không** thêm ghi chú về nguồn dữ liệu. Người dùng muốn thì tự hỏi |

**Nhật ký:** mỗi lời gọi công cụ ghi một dòng log ở server gồm: id người hỏi, vai trò, tên công cụ, số kết quả, thành công hay lỗi và thời gian xử lý. Không ghi nội dung câu hỏi, tham số tự do (tên, từ khóa) hay dữ liệu trả về.

## 7. Widget chat

1. **Nút gợi ý câu hỏi:** hiện khi khung chat trống, bấm vào là gửi ngay.
   - Quản lý: "Hóa đơn nào chưa thanh toán?", "Phòng nào còn trống?", "Hợp đồng nào hết hạn trong 30 ngày tới?", "Phòng nào chưa chốt điện nước tháng này?", "Doanh thu tháng này?"
   - Khách thuê: "Hợp đồng của tôi", "Tôi còn nợ hóa đơn nào?", "Chỉ số điện nước tháng này"
2. **Endpoint theo cổng:** widget gọi endpoint của cổng đang mở (§4).
3. **Lịch sử theo tài khoản:** khóa lưu lịch sử gắn với id người dùng; lịch sử bị xóa khi đăng xuất.
4. **Giới hạn đầu vào:** câu hỏi tối đa 1.000 ký tự, widget chặn trước khi gửi và server kiểm tra lại. Chỉ gửi 10 lượt hội thoại gần nhất. Server chỉ nhận role `user`/`model`; các lượt sai định dạng bị bỏ qua. Mỗi lượt cũ trong lịch sử bị cắt còn tối đa 1.000 ký tự (lượt `user`) hoặc 4.000 ký tự (lượt `model`).

## 8. Thông báo

| Sự kiện hoặc thao tác | Người nhận hoặc quy tắc |
|---|---|
| Khách thuê báo sự cố mới | Chỉ báo được cho phòng của hợp đồng **đang hoạt động** của mình. Người nhận: mọi Admin đang hoạt động, cộng nhân viên đang hoạt động có phân công còn hiệu lực ở chi nhánh của phòng. Không có nhân viên nào thì chỉ Admin nhận. Gửi realtime tới từng người nhận, không phát cho cả nhóm vai trò |
| Lỗi khi gửi thông báo sự cố | Không làm hỏng việc tạo sự cố; ghi log cảnh báo |
| Đánh dấu một thông báo đã đọc | Chỉ khi thông báo thuộc người đang đăng nhập; không thuộc thì trả "Không tìm thấy thông báo" |
| Nhân viên bị thu hồi chi nhánh | Thông báo cũ vẫn giữ trong danh sách. Màn hình đích tự chặn theo phân quyền chi nhánh của bước 1 |
| Thanh toán, phát hành hóa đơn | Giữ nguyên |

## 9. Kiến trúc (đã chốt: tách đầy đủ)

- **Application** `Features/AiAssistants`:
  - Service điều phối vòng hội thoại và chọn bộ công cụ theo vai trò.
  - Danh mục công cụ theo vai trò và bộ kiểm tra tham số.
  - Store interface cho các truy vấn, nhận phạm vi chi nhánh hoặc `NguoiThueId`.
  - Interface trừu tượng cho mô hình AI, không phụ thuộc Gemini.
- **Infrastructure**: store EF cho các truy vấn; client Gemini chỉ lo HTTP và chuyển định dạng.
- **Web**: hai controller (quản lý, khách thuê) lấy danh tính từ claim rồi gọi service. Web không tham chiếu Domain.
- Thông báo: thêm hàm gửi theo chi nhánh của phòng vào `ThongBaoService`; `DanhDauDaDocAsync` nhận thêm id người dùng.

Chi tiết lớp và file nằm trong `plan.md`.

## 10. Kiểm thử

| Tầng | Nội dung |
|---|---|
| Application.UnitTests | Mỗi vai trò nhận đúng bộ công cụ; khách thuê gọi công cụ quản lý bị từ chối; nhân viên chưa được phân công không gọi được công cụ; ngày mặc định theo giờ Việt Nam; kiểm tra tham số; giới hạn 3 vòng và 5 lời gọi; lọc `history`; người nhận thông báo sự cố; đánh dấu đọc thông báo của người khác bị từ chối |
| Infrastructure.IntegrationTests | Store lọc đúng chi nhánh cho Q1–Q9; Q2 bỏ hóa đơn nháp và đã hủy; phòng trùng số; client Gemini vẫn giữ API key ở header (giữ các test hiện có) |
| Web.IntegrationTests | Khách thuê chat qua endpoint mới; khách thuê gọi endpoint quản lý bị chặn; nhân viên dùng bộ công cụ quản lý; sự cố mới không tới nhân viên chi nhánh khác; đánh dấu đọc thông báo của người khác thất bại |
| ArchitectureBoundaryTests | Giữ nguyên ranh giới tầng |

Các test gọi Gemini dùng HTTP handler giả, không gọi API thật.

## 11. Điều kiện nghiệm thu

1. Nhân viên chi nhánh A hỏi "phòng nào còn trống" chỉ thấy phòng chi nhánh A.
2. Nhân viên hỏi doanh thu chỉ thấy số của chi nhánh mình; Admin thấy mọi chi nhánh.
3. Khách thuê hỏi "tôi còn nợ hóa đơn nào" nhận đúng hóa đơn của mình.
4. Hóa đơn nháp hoặc đã hủy không xuất hiện trong câu trả lời về công nợ.
5. Câu hỏi ngoài phạm vi nhận câu "chưa hỗ trợ" và gợi ý; câu hỏi không có dữ liệu nhận câu "không có", không có số bịa.
6. Khách báo sự cố ở phòng chi nhánh A: Admin và nhân viên chi nhánh A nhận thông báo, nhân viên chi nhánh B không nhận.
7. Đăng xuất rồi đăng nhập tài khoản khác trên cùng tab không thấy lịch sử chat cũ.
8. `dotnet test` toàn solution PASS trên DB `_test`; không có migration mới.

## 12. Giới hạn chấp nhận

- AI vẫn có thể hiểu sai câu hỏi hoặc diễn đạt sai. Hệ thống chỉ bảo đảm dữ liệu đưa cho AI đúng phạm vi và đúng số.
- Phụ thuộc hạn mức và độ sẵn sàng của Gemini.
- Lịch sử chat chỉ lưu trong tab trình duyệt, không lưu server.
- Nhân viên trong cùng chi nhánh có quyền AI như nhau.

## 13. Quyết định đã chốt (05/10/2026)

1. Nhân viên được hỏi doanh thu, giới hạn ở chi nhánh được phân công.
2. Tách lớp đầy đủ (Application, Infrastructure, Web) thay vì vá trong Infrastructure.
3. Admin nhận thông báo sự cố của mọi chi nhánh.
4. Giữ tool calling; nâng cấp theo 5 điểm: vòng lặp nhiều công cụ, kiểm tra tham số, code tính tổng, xử lý câu hỏi ngoài phạm vi, nhật ký.
5. Thêm nút gợi ý câu hỏi trong widget.
6. Không nói thời gian thì mặc định thời điểm hiện tại (trừ Q2 và K3: lấy mọi kỳ).
7. Không thêm ghi chú nguồn dữ liệu vào câu trả lời.
8. Đợt này chỉ đổi thông báo sự cố; câu hỏi AI mới sẽ bổ sung sau.
