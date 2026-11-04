# AI hỗ trợ tìm phòng và tư vấn cọc

## Sử dụng

Vào `/phong`, bấm **Hỏi AI tìm phòng** ở góc dưới bên phải. Không cần đăng nhập để chat. Widget cũng xuất hiện trên các trang dùng layout công khai, có thể nhận biết phòng khách đang xem.

Ví dụ:

- “Tìm phòng Gò Vấp dưới 3 triệu, ở 2 người.”
- “Tìm phòng từ 20 m², giá tối đa 4 triệu.”
- “Phòng đầu tiên cần cọc bao nhiêu?” sau khi đã nhận thẻ gợi ý.
- “Đặt cọc giữ chỗ thực hiện thế nào?”
- “Tôi muốn hẹn xem phòng.”

Thẻ kết quả có giá thuê, mức cọc thực tế và các nút xem chi tiết/hẹn xem/đặt phòng. Phòng chưa có mức cọc sẽ báo chưa mở đặt cọc. Bấm hẹn xem/đặt phòng tiếp tục qua luồng đăng nhập hiện có khi cần; chat không tự tạo lịch hoặc giao dịch.

Khung chat mở nhỏ gọn và mở rộng khi có cuộc hội thoại. AI ưu tiên trả lời 1–2 câu, không lặp lại danh sách thông tin đã có trên thẻ. Với câu trả lời dài, giao diện hiển thị đoạn ngắn và nút “Đọc đầy đủ”/“Thu gọn”; nội dung đầy đủ vẫn được giữ nguyên. Giá thuê và cọc đặt cạnh nhau trên thẻ để dễ so sánh.

AI hỗ trợ giá, khu vực, diện tích, số người, mức cọc và quy trình. Tra cứu hồ sơ cá nhân, hợp đồng, hóa đơn và trạng thái yêu cầu riêng chưa thuộc chức năng AI công khai đợt này; các màn hình tài khoản hiện có vẫn sử dụng bình thường.

## Dữ liệu và cấu hình

- Tái sử dụng `Gemini:ApiKey` và `Gemini:Model` của adapter hiện có. Khóa chỉ đặt trong cấu hình cục bộ/biến môi trường máy chủ, không đưa vào JavaScript hoặc Git.
- Chỉ đọc qua `IPublicRoomService`, tôn trọng cấu hình `PublicRooms:RoomIds`. Không thêm bảng, migration, entity, DbContext hoặc bản ghi DB.
- Không cung cấp công cụ quản lý, dữ liệu khách thuê, số điện thoại chi nhánh hoặc phương thức ghi dữ liệu cho AI công khai.
- Các ID phòng của lượt trước chỉ là tham chiếu, server tra lại dữ liệu công khai; không nhận giá/cọc từ trình duyệt. Kiểm tra lại toàn bộ phòng đã đưa cho model trước khi trả câu trả lời. Có thay đổi thì thay câu trả lời bằng thông báo kiểm tra dữ liệu mới nhất.
- Lịch sử hội thoại chỉ nằm trong bộ nhớ tab, không lưu localStorage/sessionStorage/DB. “Chat mới” hoặc tải lại trang sẽ xóa lịch sử. Đóng/mở lại khung chat trong cùng trang giữ cuộc hội thoại.

## Giới hạn và lỗi

`POST /phong/tro-ly-ai` nhận token CSRF, body tối đa 32 KB, câu hỏi tối đa 1.000 ký tự và lịch sử 8 tin đã giới hạn độ dài. Client giới hạn cả kích thước UTF-8 của lịch sử tiếng Việt. Tối đa 3 vòng công cụ, 4 công cụ/vòng. Server ngắt sau 40 giây, trình duyệt sau 45 giây.

Giới hạn tần suất (`Web/Helpers/GuestAssistantRateLimiting.cs`, hằng số, không có khóa cấu hình), vượt thì trả 429 và không gọi Gemini:

- Mỗi khách 6 yêu cầu/phút: khách đăng nhập tính theo tài khoản, khách chưa đăng nhập tính theo IP kết nối.
- Cả hệ thống 30 yêu cầu/phút cho endpoint này. Yêu cầu đã bị chặn ở lớp theo khách không trừ vào trần chung.

IP lấy từ kết nối trực tiếp tới ứng dụng. Chạy bằng `docker-compose.yml` (map cổng thẳng) hoặc sau reverse proxy, ứng dụng có thể thấy chung một IP cho mọi khách chưa đăng nhập; khi đó họ dùng chung 6 yêu cầu/phút, còn khách đăng nhập không bị ảnh hưởng. Muốn tính đúng IP khi đặt sau reverse proxy (nginx, Cloudflare Tunnel...), người triển khai cấu hình `ForwardedHeaders` với `KnownProxies`/`KnownNetworks` là địa chỉ proxy đó. Không bật chế độ tin mọi `X-Forwarded-For`, vì khách có thể tự gửi header này để vượt giới hạn. Mã hiện chưa bật `UseForwardedHeaders`.

Nếu AI gián đoạn, giao diện giữ câu hỏi để thử lại và có đường dẫn tìm phòng/cẩm nang. Tin nhắn và tiêu đề phòng được chèn bằng văn bản, đường dẫn thẻ chỉ dựng từ ID số và tuyến nội bộ. Không chèn HTML hay đường dẫn do model tạo.

## Kiểm tra

Các test tự động dùng model giả; các Web test chỉ dùng database có hậu tố `_test`. Thử Gemini thật chỉ dùng câu hỏi và dữ liệu phòng công khai. Không tạo lịch, giữ chỗ hay thanh toán trên database thật để kiểm tra AI.
