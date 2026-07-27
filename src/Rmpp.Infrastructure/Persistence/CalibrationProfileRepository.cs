using Microsoft.Data.Sqlite;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Printing;

namespace Rmpp.Infrastructure.Persistence;

/// <summary>按稳定打印机与介质键保存本机校准参数，不修改模板几何。</summary>
public sealed class CalibrationProfileRepository(SqliteAppDatabase database)
{
    public async Task SaveAsync(CalibrationProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        CalibrationProfile validated = profile.Validate();
        await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO calibration_profiles(
                    printer_stable_id, media_key, offset_x_mm, offset_y_mm, scale_x, scale_y,
                    rotation_degrees, last_verified_at_utc, updated_at_utc)
                VALUES ($printer, $media, $offsetX, $offsetY, $scaleX, $scaleY, $rotation, $verified, $updated)
                ON CONFLICT(printer_stable_id, media_key) DO UPDATE SET
                    offset_x_mm = excluded.offset_x_mm,
                    offset_y_mm = excluded.offset_y_mm,
                    scale_x = excluded.scale_x,
                    scale_y = excluded.scale_y,
                    rotation_degrees = excluded.rotation_degrees,
                    last_verified_at_utc = excluded.last_verified_at_utc,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            command.Parameters.AddWithValue("$printer", validated.Key.PrinterStableId);
            command.Parameters.AddWithValue("$media", validated.Key.MediaKey);
            command.Parameters.AddWithValue("$offsetX", validated.OffsetMm.X);
            command.Parameters.AddWithValue("$offsetY", validated.OffsetMm.Y);
            command.Parameters.AddWithValue("$scaleX", validated.ScaleX);
            command.Parameters.AddWithValue("$scaleY", validated.ScaleY);
            command.Parameters.AddWithValue("$rotation", validated.RotationDegrees);
            command.Parameters.AddWithValue(
                "$verified",
                validated.LastVerifiedAt is { } verified
                    ? SqlitePersistenceHelpers.FormatTimestamp(verified)
                    : DBNull.Value);
            command.Parameters.AddWithValue("$updated", SqlitePersistenceHelpers.FormatTimestamp(DateTimeOffset.UtcNow));
            await command.ExecuteNonQueryAsync(token);
        }, cancellationToken);
    }

    public async Task<CalibrationProfile?> GetAsync(
        PrinterMediaKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT offset_x_mm, offset_y_mm, scale_x, scale_y, rotation_degrees, last_verified_at_utc
                FROM calibration_profiles
                WHERE printer_stable_id = $printer AND media_key = $media;
                """;
            command.Parameters.AddWithValue("$printer", key.PrinterStableId);
            command.Parameters.AddWithValue("$media", key.MediaKey);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                return null;
            }

            return new CalibrationProfile
            {
                Key = key,
                OffsetMm = new MmPoint(reader.GetDouble(0), reader.GetDouble(1)),
                ScaleX = reader.GetDouble(2),
                ScaleY = reader.GetDouble(3),
                RotationDegrees = reader.GetDouble(4),
                LastVerifiedAt = reader.IsDBNull(5)
                    ? null
                    : SqlitePersistenceHelpers.ParseTimestamp(reader.GetString(5)),
            }.Validate();
        }, cancellationToken);
    }
}
