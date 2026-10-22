using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

// Chỉ dùng cho bản xem trước trên database cũ; không truy vấn các cột/bảng của schema master.
public sealed class LegacyPublicRoomStore(ApplicationDbContext context, IConfiguration configuration)
    : IPublicRoomStore
{
    private const string AvailableRoomSql = """
        SELECT p."PhongTroId", p."SoPhong", c."TenChiNhanh", c."DiaChi",
               p."GiaThue"::numeric, p."DienTich", p."SoNguoiToiDa",
               p."TangLau", p."MoTa", c."SoDienThoai"
        FROM phong_tro AS p
        JOIN chi_nhanh AS c ON c."ChiNhanhId" = p."ChiNhanhId"
        WHERE NOT p."IsDeleted" AND NOT c."IsDeleted" AND p."TrangThai" = 0
          AND p."PhongTroId" = ANY(@allowedIds)
        """;

    public Task<IReadOnlyList<PublicRoomDto>> ListAvailableAsync(
        CancellationToken cancellationToken = default) => ReadAsync(null, cancellationToken);

    public async Task<PublicRoomDto?> GetAvailableAsync(int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        return (await ReadAsync(id, cancellationToken)).FirstOrDefault();
    }

    private async Task<IReadOnlyList<PublicRoomDto>> ReadAsync(int? roomId,
        CancellationToken cancellationToken)
    {
        var allowedIds = (configuration.GetSection("PublicRooms:RoomIds").Get<int[]>() ?? [])
            .Where(id => id > 0).Distinct().ToArray();
        if (allowedIds.Length == 0 || (roomId.HasValue && !allowedIds.Contains(roomId.Value)))
            return [];

        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            AvailableRoomSql + (roomId.HasValue ? " AND p.\"PhongTroId\" = @roomId" :
                " ORDER BY p.\"ChiNhanhId\", p.\"SoPhong\""), connection);
        command.Parameters.AddWithValue("allowedIds", NpgsqlDbType.Array | NpgsqlDbType.Integer, allowedIds);
        if (roomId.HasValue)
            command.Parameters.AddWithValue("roomId", roomId.Value);

        var rooms = new List<PublicRoomDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rooms.Add(new PublicRoomDto(
                reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetDecimal(4), reader.GetDouble(5),
                reader.GetInt32(6), reader.GetInt32(7), reader.GetString(8),
                reader.IsDBNull(9) ? string.Empty : reader.GetString(9)));
        }

        return rooms;
    }
}
