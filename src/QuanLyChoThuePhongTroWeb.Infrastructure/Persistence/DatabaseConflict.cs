using Npgsql;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

internal static class DatabaseConflict
{
    // Npgsql can wrap serialization failures inside DbUpdateException and InvalidOperationException.
    public static bool IsExpected(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation or
                PostgresErrorCodes.DeadlockDetected)
                return true;
        }
        return false;
    }
}
