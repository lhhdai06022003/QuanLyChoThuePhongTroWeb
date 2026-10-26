# Module: Thanh toán trực tuyến hóa đơn

- **Nối tiếp:** [thanh toán hóa đơn](../thanh-toan-hoa-don/spec.md) (lượt + minh chứng + đối chiếu tay).
- **Nhánh Git:** `feature/thanh-toan-truc-tuyen`.
- **Phần dùng chung:** ghi sổ (`InvoiceTransferHandler`, `IncomingBankTransfer.Kenh`), khóa chống trùng theo tiền tố `MaGiaoDich` (`SEPAY-`, `VNPAY-`), thông báo sau ghi sổ (trong app, SignalR, Telegram, email biên lai). Thiết kế gốc ở [spec SePay](sepay/spec.md) §5 (ghi sổ) và §6 (thông báo); VNPay dùng lại theo [spec VNPay](vnpay/spec.md).

## Hai tính năng con

| Tính năng | Kênh | Hiện trạng |
| --- | --- | --- |
| [SePay](sepay/README.md) (đợt 1) | Chuyển khoản VietQR, webhook SePay `POST /api/webhooks/payment/sepay` | Code xong 5 phần 16–18/10/2026, đã có trong `master`; thử tay 20/10/2026 trên SePay Test mode; đồng bộ bù chưa xác nhận |
| [VNPay](vnpay/README.md) (đợt 2) | Thẻ ATM / Visa / MasterCard, IPN `GET /api/webhooks/payment/vnpay-ipn` | Phần 1 commit `74e5c81`; phần 2 reviewer CHOT, commit `0f326ad`; sandbox V01–V07 PASS 26/10/2026; việc còn mở sau sandbox xong ở `3de7181`; chưa push |

## Hồ sơ

- `sepay/`: `README.md`, `spec.md`, `plan.md`, `review.md` của đợt 1.
- `vnpay/`: `README.md`, `spec.md`, `plan.md`, `review.md` của đợt 2.
- [`huong-dan-thiet-lap.md`](huong-dan-thiet-lap.md): dùng chung cho cả hai. Mục 0–8 cho SePay (kèm Telegram, email, ngrok dùng chung), mục 9 cho VNPay sandbox.
- Ngày 26/10/2026 gộp thư mục `docs/features/thanh-toan-vnpay/` cũ vào `vnpay/`, chuyển hồ sơ đợt 1 vào `sepay/`. Báo cáo ngày và `.bangiao/` cũ còn ghi đường dẫn trước khi chuyển.
