using MySqlConnector;

namespace BattleShip.LobbyServer.Repositories
{
    public class GameRecordRepository
    {
        private readonly string _connectionString;

        public GameRecordRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task SaveAsync(string sessionId, string winnerId, string loserId, int totalTurns)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            INSERT INTO game_records (session_id, winner_id, loser_id, total_turns)
            VALUES (@sessionId, @winnerId, @loserId, @totalTurns)";

            cmd.Parameters.AddWithValue("@sessionId", sessionId);
            cmd.Parameters.AddWithValue("@winnerId", winnerId);
            cmd.Parameters.AddWithValue("@loserId", loserId);
            cmd.Parameters.AddWithValue("@totalTurns", totalTurns);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
