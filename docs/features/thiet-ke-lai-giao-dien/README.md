# Thiết kế lại giao diện cho chủ trọ, nhân viên và khách thuê

Làm cho ba nhóm người dùng nhìn là biết việc cần làm: chủ trọ thấy tiền tháng này thu tới đâu, nhân viên thấy việc hôm nay kèm nút làm luôn, khách thuê mở ra thấy số tiền, hạn trả và nút thanh toán. Mọi màn trông như cùng một sản phẩm, ở chế độ sáng, tối và trên điện thoại.

- Nhánh: `feature/thiet-ke-lai-giao-dien`, tách ngày 11/11/2026 từ `feature/don-dep-truoc-demo` (`268d3b3`, hơn `master` một commit dọn dẹp).
- Phạm vi: cổng quản lý (`Areas/QuanLyNhaTro`, `Views/Shared`) và cổng khách thuê (`Areas/KhachThue`). **Không đụng cổng khách vãng lai** (`Web/Controllers`, `_PublicRoomLayout`, các file `guest-*.css`, `khach-vang-lai.css`).
- Không đổi nghiệp vụ, schema, controller, quyền. Giữ nền Tabler 1.4 và các `id`, `window.<man>Config`, tên cột DataTables mà JS đang dùng.

## Hướng chung (người dùng duyệt 11/11/2026)

1. Menu nhóm theo việc, nhóm luôn mở, không còn một danh sách 16 mục.
2. Một thanh thu tiền của tháng thay bốn ô màu đặc; màu chỉ dùng cho trạng thái.
3. Nhân viên thấy việc trước, số liệu sau; mỗi việc có nút làm luôn.
4. Thanh tab dưới trên điện thoại cho nhân viên và khách thuê.
5. Một khung trang chung cho mọi màn: tiêu đề, một nút chính, thanh lọc, bảng; dưới 768px bảng chuyển thành thẻ.
6. Đổi font Inter sang Be Vietnam Pro; cổng khách thuê dùng tên TroSync.

## Chia đợt

Mỗi đợt một spec ngắn, người dùng duyệt rồi mới làm. Đợt sau chỉ bắt đầu khi đợt trước đã kiểm trên trình duyệt.

| Đợt | Nội dung | Cách thực thi | Tài liệu | Trạng thái |
| --- | --- | --- | --- | --- |
| 1 | Nền chung: màu, font, menu, header, khung vỏ hai cổng, vị trí nút trợ lý AI | Claude lập kế hoạch; **một** agent Sonnet 5.5 (xhigh) thực thi tuần tự (các file dùng chung cho mọi màn, hai người viết sẽ đạp nhau); phiên chính chạy test tích hợp và kiểm trình duyệt | [spec](spec-dot-1-nen-chung.md), [plan](plan-dot-1-nen-chung.md) | Xong trên nhánh 11/11/2026 (`f4a20f6`…`cb9aaba`), chưa gộp `master`. `Web.IntegrationTests` 874/874; đã kiểm trình duyệt ba vai trò ở sáng, tối, 375px (ảnh `giao-dien-1-01…13`) |
| 2 | Ba màn chính: bảng điều khiển chủ trọ (thanh thu tiền), bảng điều khiển nhân viên, trang chủ khách thuê; thanh tab dưới cho nhân viên, chỉnh thanh tab khách thuê | `/doi-ngu`: Fable lập phương án và nghiệm thu, Opus trưởng nhóm, Sonnet thực thi (V4 tầng Application do Opus làm) | [spec](spec-dot-2-ba-man-chinh.md) | Nghiệm thu ĐẠT 12/11/2026 trên nhánh (`87d56b4`…`7ef9487`), chưa gộp `master`; kiểm trình duyệt ba vai trò (ảnh `giao-dien-2-01…06`) |
| 3 | Nhóm thu tiền: điện nước, hóa đơn, đối chiếu, lịch sử thanh toán, giao dịch SePay; bỏ màu teal riêng, bảng thành thẻ dưới 768px | `/doi-ngu` đội mới: Opus điều phối, co-van Fable soát phương án và nghiệm thu, Sonnet làm song song từng màn sau lớp dùng chung; Opus tự làm phần view hóa đơn | [spec](spec-dot-3-nhom-thu-tien.md) | Xong trên nhánh 12/11/2026 (`d436338`…), chưa gộp `master`; ảnh `giao-dien-3-01…10` |
| 4 | Phòng trọ (sơ đồ, bảng, năm modal), người thuê, hợp đồng (danh sách, modal bốn bước, chi tiết, thanh lý, gia hạn), đăng ký dịch vụ | Phiên Fable lập kế hoạch và nghiệm thu; một agent khác thực thi theo plan, báo cáo vào `.bangiao/giao-dien/bao-cao-dot-4.md` ([lời giao việc](giao-viec-dot-4-5.md)) | [spec](spec-dot-4-phong-nguoi-thue-hop-dong.md), [plan](plan-dot-4-phong-nguoi-thue-hop-dong.md) | Nghiệm thu ĐẠT 14/11/2026 trên nhánh (`9f37a95`…`adb6fcc`, một vòng sửa), chưa gộp `master`; `Web.IntegrationTests` 931/931; ảnh `giao-dien-4-01…15` |
| 5 | Thiết lập (chi nhánh, dịch vụ, điều khoản, tài khoản, phân công), vận hành (sự cố, thông báo, báo cáo, việc cần làm), đón khách (chạm nhẹ), bảy màn con của khách thuê; rà toàn module | Như đợt 4; chỉ bắt đầu khi đợt 4 nghiệm thu đạt | [spec](spec-dot-5-cac-man-con-lai.md), [plan](plan-dot-5-cac-man-con-lai.md) | Nghiệm thu ĐẠT 14/11/2026 trên nhánh (`65066e7`…`f1e2048`, một vòng sửa), chưa gộp `master`; `Web.IntegrationTests` 974/974; ảnh `giao-dien-5-01…27` |

Cách làm người dùng chốt ngày 11/11/2026: Claude lập kế hoạch, agent Sonnet 5.5 mức xhigh thực thi. Đợt 4 và 5 (13/11/2026): mỗi đợt một lượt chạy của agent thực thi, xong thì dừng chờ nghiệm thu; mỗi việc một commit cục bộ, không push.

Vì sao chia đợt (người dùng giao Claude chọn giữa "chia đợt làm dần" và "dùng subagent", 11/11/2026): hai cách không loại nhau. Subagent chỉ chạy tốt khi nền chung đã cố định; nếu cho nhiều subagent làm ngay, mỗi cái sẽ tự chọn màu, khoảng cách, kiểu tiêu đề và ta quay lại đúng vấn đề đang có. Nên đợt 1 làm tay để chốt nền, từ đợt 3 mới chia subagent theo màn.

Bản mẫu hướng thiết kế đã trình trong phiên 11/11/2026 (ba khung: chủ trọ trên máy tính, nhân viên và khách thuê trên điện thoại); ảnh hiện trạng trước khi làm ở `E:\UTE\TieuLuanChuyenNganh\hinhanhprojectdemo` (`van-hanh-*`, `fw-bold-*`, `hieu-nang-07`).
