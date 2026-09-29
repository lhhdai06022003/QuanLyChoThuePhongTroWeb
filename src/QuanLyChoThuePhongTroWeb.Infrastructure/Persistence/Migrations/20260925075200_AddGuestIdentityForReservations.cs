using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260925075200_AddGuestIdentityForReservations")]
public sealed class AddGuestIdentityForReservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CCCD",
            table: "khach_vang_lai",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CCCD", table: "khach_vang_lai");
    }
}
