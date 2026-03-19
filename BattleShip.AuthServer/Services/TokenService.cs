using StackExchange.Redis;

namespace BattleShip.AuthServer.Services
{
    public class TokenService
    {
        private readonly IDatabase _redis;
        private const int TokenTtlSeconds = 30;

        public TokenService(IDatabase redis)
        {
            _redis = redis;
        }

        public async Task<string> IssueAsync(int userId, string username)
        {
            string token = Guid.NewGuid().ToString("N");  // 32자 UUID (하이픈 없음)
            string key = $"auth:token:{token}";
            string value = $"{userId}:{username}";        // 단순 문자열로 저장

            await _redis.StringSetAsync(key, value, TimeSpan.FromSeconds(TokenTtlSeconds));

            return token;
        }
    }
}
