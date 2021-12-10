using StackExchange.Redis;
using System.Linq.Expressions;

namespace Hoorbakht.RedisService;

public interface IRedisService<T> where T : class
{
	#region [String Section]

	public Task SetStringRange(Dictionary<RedisKey, T?> inputs);

	public Task<T?> GetString(RedisKey key);

	public Task<bool> SetString(RedisKey key, T? value, int? cacheDuration = null);

	public List<T?> GetAllString(int page = 0, int pageSize = 20);

	public List<RedisKey> GetAllKeysString();

	public int CountAllString();

	#endregion

	#region [Hash Section]

	public Task SetHashRangeAsync(Dictionary<string?, T> inputs);

	public Task SetHashRangeAsync(List<T> inputs, Expression<Func<T, object>> keySelector);

	public Task SetHashAsync(RedisKey key, T value, int? cacheDuration = null);

	public Task<T?> GetHashAsync(RedisKey key);

	public List<string> GetNearExpireHash(int days);

	public Task<bool> HashExistAsync(RedisKey key, RedisValue hashValue);

	public Task<List<Tuple<string, dynamic>>> GetPartialHashAsync(RedisKey key, List<string> innerKeys);

	public List<T?> GetAllHash(int page = 0, int pageSize = 20);

	public int CountAllHash();

	public List<RedisKey> GetAllKeysHash(string prefix);

	#endregion

	#region [Common Section]

	public Task<bool> SetExpiration(RedisKey key) ;

	public Task<bool> Delete(RedisKey key);

	#endregion
}