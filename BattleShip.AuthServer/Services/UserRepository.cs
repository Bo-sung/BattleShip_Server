using MySqlConnector;

namespace BattleShip.AuthServer.Services
{
    public class UserRepository
    {
        private readonly string _connectionString;

        public UserRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<User?> FindByUsernameAsync(string username)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, username, password_hash FROM users WHERE username = @username";
            cmd.Parameters.AddWithValue("@username", username);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
            };
        }

        public async Task<bool> ExistsAsync(string username)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM users WHERE username = @username";
            cmd.Parameters.AddWithValue("@username", username);

            long count = (long)(await cmd.ExecuteScalarAsync())!;
            return count > 0;
        }

        public async Task CreateAsync(string username, string passwordHash)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO users (username, password_hash) VALUES (@username, @passwordHash)";
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@passwordHash", passwordHash);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
    }
}
