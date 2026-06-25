# TÀI LIỆU TỔNG QUAN HỆ THỐNG & ĐẶC TẢ USE CASE TỔNG THỂ

Tài liệu này cung cấp cái nhìn toàn diện về hệ thống **Quản lý cho thuê phòng trọ (Rental Management System)** và sơ đồ Use Case tích hợp toàn bộ các phân hệ chức năng.

---

## 1. Đánh giá Tổng quan về Hệ thống

Hệ thống được phát triển theo kiến trúc **ASP.NET Core MVC** chia vùng (Areas), kết nối cơ sở dữ liệu **PostgreSQL** thông qua **Entity Framework Core**. Hệ thống bao gồm hai phân hệ tương tác độc lập nhưng đồng bộ về dữ liệu:

1.  **Phân hệ Quản trị (Admin Portal - Area `QuanLyNhaTro`)**: Dành cho **Chủ nhà** và **Nhân viên**. Tập trung vào các tác vụ cấu hình hệ thống, quản lý vận hành (phòng, sơ đồ, khách thuê, hợp đồng) và quản trị tài chính (chốt số điện nước, phát sinh hóa đơn, sinh mã VietQR, ghi nhận thu tiền, quản lý công nợ).
2.  **Phân hệ Khách thuê (Tenant Portal - Area `KhachThue`)**: Giao diện gọn nhẹ dành riêng cho **Khách thuê** để tra cứu hợp đồng, theo dõi lịch sử tiêu thụ điện nước, bảng kê chi tiết hóa đơn hàng tháng và quét mã QR chuyển khoản thanh toán nhanh.
3.  **Hệ thống chạy ngầm tự động (Background Jobs)**: Các tác vụ nền chạy độc lập theo lịch biểu để tối ưu hóa nhân lực:
    *   *InvoiceReminderService*: Quét nợ hóa đơn và gửi email nhắc nợ tự động kèm file PDF hóa đơn.
    *   *ContractAutoCloseJob*: Tự động đóng hợp đồng hết hạn và giải phóng phòng về trạng thái trống.

---

## 2. Sơ đồ Use Case Tổng thể hệ thống (Mermaid)

```mermaid
graph LR
    subgraph Actors [Tác nhân hệ thống]
        Admin(((Chủ nhà / Nhân viên)))
        Renter(((Khách thuê)))
        System[Hệ thống chạy ngầm]
    end

    subgraph RentalSystem [Hệ thống Quản lý Phòng trọ]
        subgraph AdminPortal [Phân hệ Quản trị - Admin Portal]
            UC1(UC-01: Đăng nhập / Đăng xuất)
            UC2(UC-02: Quản lý Tài khoản & Phân quyền)
            UC3(UC-03: Xem Dashboard thống kê)
            UC4(UC-04: Quản lý Chi nhánh)
            UC5(UC-05: Quản lý Phòng trọ)
            UC5_2(UC-05.2: Quản lý Dịch vụ & Đơn giá)
            UC6(UC-06: Quản lý Khách thuê)
            UC7(UC-07: Quản lý Hợp đồng)
            UC8(UC-08: Quản lý Điện nước)
            UC9(UC-09: Quản lý Hóa đơn & Thu tiền)
            UC10(UC-10: Quản lý Điều khoản mẫu)
            UC11(UC-11: Trợ lý AI Vận hành)
        end

        subgraph TenantPortal [Phân hệ Khách thuê - Tenant Portal]
            UC12(UC-12: Xem Hồ sơ & Đổi mật khẩu)
            UC13(UC-13: Tra cứu Hợp đồng)
            UC14(UC-14: Tra cứu Hóa đơn & VietQR)
        end

        subgraph BgJobs [Dịch vụ chạy ngầm tự động]
            UC15(UC-15: Tự động gửi email nhắc nợ)
            UC16(UC-16: Tự động đóng hợp đồng hết hạn)
        end
    end

    %% Connections
    Admin --> UC1
    Admin --> UC2
    Admin --> UC3
    Admin --> UC4
    Admin --> UC5
    Admin --> UC5_2
    Admin --> UC6
    Admin --> UC7
    Admin --> UC8
    Admin --> UC9
    Admin --> UC10
    Admin --> UC11

    Renter --> UC12
    Renter --> UC13
    Renter --> UC14
    Renter --> UC11

    System --> UC15
    System --> UC16

    %% Relations
    UC15 -.->|<<include>>| UC9
    UC16 -.->|<<include>>| UC7
    UC15 --> Renter
```
