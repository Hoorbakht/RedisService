using Newtonsoft.Json;
using System.Reflection;
using StackExchange.Redis;
using System.Linq.Expressions;
using System.Xml.Serialization;
using Hoorbakht.RedisService.Contracts;

namespace Hoorbakht.RedisService;

public class RedisService<T> : IRedisService<T>, IDisposable where T : class
{
	#region [Field(s)]

	private readonly IDatabase _redisDatabase;
	private readonly int _cacheTimeInMinutes;
	private readonly string _systemName;
	private readonly List<IServer> _redisServers;

	#endregion

	#region [Constructor]

	public RedisService(RedisConfiguration redisConfiguration, string contractName)
	{
		var redisConnectionHelper = new RedisConnectionHelper(redisConfiguration);
		_redisDatabase = redisConnectionHelper.Connection.GetDatabase(redisConfiguration.Database);
		_redisServers = redisConnectionHelper.Servers;
		_cacheTimeInMinutes = redisConfiguration.CacheTimeInMinutes;
		_systemName = redisConfiguration.SystemName + ":" + contractName;
	}

	#endregion

	#region [Public Method(s)]

	#region [String Section]

	public async Task SetStringRangeAsync(Dictionary<RedisKey, T?> inputs)
	{
		foreach (var (key, value) in inputs) await SetStringAsync(key, value);
	}

	public async Task<T?> GetStringAsync(RedisKey key) =>
		_redisDatabase.KeyType(CompleteKey(key)) == RedisType.String
			? await GetPrivateStringAsync(CompleteKey(key))
			: default;

	public async Task<bool> SetStringAsync(RedisKey key, T? value, int? cacheDuration = null) =>
		!cacheDuration.HasValue
			? _cacheTimeInMinutes == -1
				? await _redisDatabase.StringSetAsync(CompleteKey(key),
					ToByteArray(value))
				: await _redisDatabase.StringSetAsync(CompleteKey(key),
					ToByteArray(value), TimeSpan.FromMinutes(_cacheTimeInMinutes))
			: await _redisDatabase.StringSetAsync(CompleteKey(key),
				ToByteArray(value), TimeSpan.FromMinutes(cacheDuration.Value));

	public List<T?> GetAllString(int page = 0, int pageSize = 20)
	{
		var result = new List<RedisKey>();
		foreach (var redisServer in _redisServers)
			result.AddRange(redisServer.Keys(_redisDatabase.Database, $"{_systemName}:*")
				.Where(x => _redisDatabase.KeyType(x) == RedisType.String));
		return result
			.Skip(page * pageSize)
			.Take(pageSize)
			.Select(x => GetPrivateStringAsync(x).Result)
			.Where(x => x != null)
			.ToList();
	}

	public List<RedisKey> GetAllKeysString()
	{
		var result = new List<RedisKey>();
		foreach (var redisServer in _redisServers)
			result.AddRange(redisServer.Keys(_redisDatabase.Database, $"{_systemName}:*")
				.Where(x => _redisDatabase.KeyType(x) == RedisType.String));
		return result;
	}

	public int CountAllString() =>
		_redisServers.Sum(redisServer => redisServer.Keys(_redisDatabase.Database, $"{_systemName}:*")
			.Count(x => _redisDatabase.KeyType(x) == RedisType.String));

	#endregion

	#region [Hash Section]

	public async Task SetHashRangeAsync(Dictionary<string, T> inputs)
	{
		var redisBatch = _redisDatabase.CreateBatch();

		var taskList = new List<Task>();

		foreach (var (key, value) in inputs)
		{
			taskList.Add(redisBatch.HashSetAsync(CompleteKey(key), ConvertToHashEntries(value)));

			if (_cacheTimeInMinutes != -1)
				taskList.Add(redisBatch.KeyExpireAsync(CompleteKey(key), TimeSpan.FromMinutes(_cacheTimeInMinutes)));
		}

		redisBatch.Execute();

		await Task.WhenAll(taskList);
	}

	public async Task SetHashRangeAsync(List<T> inputs, Expression<Func<T, object>> keySelector)
	{
		if (keySelector == null)
			throw new ArgumentNullException(nameof(keySelector));

		string propertyNameSelector;

		if (keySelector.Body is not MemberExpression memberExpression)
		{
			if (keySelector.Body is not ConstantExpression constantExpression)
				throw new Exception("key selector should be a member expression or a constant value expression");

			if (constantExpression.Type != typeof(string))
				throw new Exception("key selector should be a type of string");

			propertyNameSelector = constantExpression.Value?.ToString()!;
		}
		else
			propertyNameSelector = memberExpression.Member.Name;

		var redisBatch = _redisDatabase.CreateBatch();

		var taskList = new List<Task>();

		foreach (var item in inputs)
		{
			var key = item.GetType().GetProperty(propertyNameSelector ?? throw new InvalidOperationException())?.GetValue(item)?.ToString();

			taskList.Add(redisBatch.HashSetAsync(CompleteKey(key), ConvertToHashEntries(item)));

			if (_cacheTimeInMinutes != -1)
				taskList.Add(redisBatch.KeyExpireAsync(CompleteKey(key), TimeSpan.FromMinutes(_cacheTimeInMinutes)));
		}

		redisBatch.Execute();

		await Task.WhenAll(taskList);
	}

	public async Task SetHashAsync(RedisKey key, T value, int? cacheDuration = null)
	{
		await _redisDatabase.HashSetAsync(CompleteKey(key), ConvertToHashEntries(value));

		if (cacheDuration.HasValue)
			await _redisDatabase.KeyExpireAsync(CompleteKey(key),
				TimeSpan.FromMinutes(cacheDuration.Value));

		else if (_cacheTimeInMinutes != -1)
			await _redisDatabase.KeyExpireAsync(CompleteKey(key), TimeSpan.FromMinutes(_cacheTimeInMinutes));
	}

	public async Task<T?> GetHashAsync(RedisKey key) =>
		_redisDatabase.KeyType(CompleteKey(key)) == RedisType.Hash
			? await GetPrivateHashAsync(CompleteKey(key))
			: default;

	public List<string> GetNearExpireHash(int days)
	{
		var result = new List<RedisKey>();
		foreach (var redisServer in _redisServers)
			result.AddRange(redisServer.Keys(_redisDatabase.Database, $"{_systemName}:*")
				.Where(x => _redisDatabase.KeyType(x) == RedisType.Hash &&
					    _redisDatabase.HashExists(x, "_Type") &&
					    !string.IsNullOrWhiteSpace(_redisDatabase.HashGet(x, "_Type")
						    .ToString()) &&
					    _redisDatabase.HashGet(x, "_Type").ToString() == typeof(T).Name));

		return result
			.Select(x => new Tuple<string, TimeSpan?>(x, _redisDatabase.KeyTimeToLive(x)))
			.Where(x => x.Item2.HasValue && x.Item2.Value.TotalDays < days)
			.Select(x => x.Item1)
			.ToList();
	}

	public async Task<bool> HashExistAsync(RedisKey key, RedisValue hashValue) =>
		await _redisDatabase.HashExistsAsync(CompleteKey(key), hashValue);

	public async Task<List<Tuple<string, dynamic>>> GetPartialHashAsync(RedisKey key, List<string> innerKeys)
	{
		var result = new List<Tuple<string, dynamic>>();
		foreach (var item in innerKeys)
			if (await HashExistAsync(key, item))
				result.Add(new Tuple<string, dynamic>(item,
					await _redisDatabase.HashGetAsync(CompleteKey(key), item)));
		return result;
	}

	public List<T?> GetAllHash(int page = 0, int pageSize = 20)
	{
		var result = new List<RedisKey>();
		foreach (var redisServer in _redisServers)
			result.AddRange(redisServer.Keys(_redisDatabase.Database, $"{_systemName}:*")
				.Where(x => _redisDatabase.KeyType(x) == RedisType.Hash &&
					    _redisDatabase.HashExists(x, "_Type") &&
					    !string.IsNullOrWhiteSpace(_redisDatabase.HashGet(x, "_Type")
						    .ToString()) &&
					    _redisDatabase.HashGet(x, "_Type").ToString() == typeof(T).Name));
		return result
			.Skip(page * pageSize)
			.Take(pageSize)
			.Select(x => GetPrivateHashAsync(x).Result)
			.Where(x => x != null)
			.ToList();
	}

	public int CountAllHash()
	{
		return _redisServers.Sum(redisServer => redisServer.Keys(_redisDatabase.Database, $"{_systemName}:*")
			.Count(x => _redisDatabase.KeyType(x) == RedisType.Hash &&
				    _redisDatabase.HashExists(x, "_Type") &&
				    !string.IsNullOrWhiteSpace(_redisDatabase.HashGet(x, "_Type").ToString()) &&
				    _redisDatabase.HashGet(x, "_Type").ToString() == typeof(T).Name));
	}

	public List<RedisKey> GetAllKeysHash(string prefix)
	{
		var result = new List<RedisKey>();
		foreach (var redisServer in _redisServers)
			result.AddRange(redisServer.Keys(_redisDatabase.Database, $"{_systemName}:{prefix}*")
				.Where(x => _redisDatabase.KeyType(x) == RedisType.Hash));
		return result;
	}

	#endregion

	#region [Common Section]

	public async Task<bool> CheckKeyExistAsync(RedisKey key) =>
		await _redisDatabase.KeyExistsAsync(key);

	public async Task<bool> SetExpirationAsync(RedisKey key) =>
		await _redisDatabase.KeyExpireAsync(CompleteKey(key), TimeSpan.FromSeconds(5));

	public async Task<bool> DeleteAsync(RedisKey key) =>
		await _redisDatabase.KeyDeleteAsync(CompleteKey(key));

	public async Task<bool> DeleteWithoutPrefixAsync(RedisKey key) =>
		await _redisDatabase.KeyDeleteAsync(key);

	public void Dispose() =>
		_redisDatabase.Multiplexer.Dispose();

	#endregion

	#region [Geo Section]

	public async Task<long> SetGeoAsync(RedisKey key, GeoEntry[] entry) =>
		await _redisDatabase.GeoAddAsync(CompleteKey(key), entry);

	public async Task<List<GeoRadiusResult>> GetRadiusByMemberAsync(RedisKey key, RedisValue center, double radiusInKm) =>
		(await _redisDatabase.GeoRadiusAsync(CompleteKey(key), center, radiusInKm,
			GeoUnit.Kilometers, -1, Order.Ascending, GeoRadiusOptions.WithDistance)).ToList();

	public async Task<List<GeoRadiusResult>> GetRadiusByLocationAsync(RedisKey key, double lat, double @long, double radiusInKm) =>
		(await _redisDatabase.GeoRadiusAsync(CompleteKey(key), lat, @long, radiusInKm,
			GeoUnit.Kilometers, -1, Order.Ascending, GeoRadiusOptions.WithDistance)).ToList();

	#endregion

	#endregion

	#region [Private Method(s)]

	private async Task<T?> GetPrivateHashAsync(RedisKey key) =>
		ConvertToT(await _redisDatabase.HashGetAllAsync(key));

	private async Task<T?> GetPrivateStringAsync(RedisKey key) =>
		FromByteArray(await _redisDatabase.StringGetAsync(key));

	private RedisKey CompleteKey(string? key) =>
		$"{_systemName}:{key}";

	private static HashEntry[] ConvertToHashEntries(T t)
	{
		var data = typeof(T).GetProperties()
			.Where(x => x.GetValue(t) != null)
			.Select(x => new HashEntry(x.Name, x.GetValue(t, null) == null
				? string.Empty
				: x.PropertyType.Namespace == "System.Collections.Generic"
				  || x.PropertyType.IsArray
				  || x.PropertyType.IsDefined(typeof(CacheableContract))
					? JsonConvert.SerializeObject(x.GetValue(t, null))
					: x.GetValue(t, null)?.ToString())).ToList();
		data.Add(new HashEntry("_Type", typeof(T).Name));
		return data.ToArray();
	}

	private static T? ConvertToT(HashEntry[] hash)
	{
		if (hash.All(x => x.Name != "_Type")) return null;
		if (hash.Single(x => x.Name == "_Type").Value != typeof(T).Name) return null;
		var result = Activator.CreateInstance<T>();

		foreach (var item in typeof(T).GetProperties())
		{
			var value = hash.SingleOrDefault(x => x.Name == item.Name).Value;
			if (string.IsNullOrWhiteSpace(value)) continue;

			if (!item.CanWrite) continue;

			if (item.PropertyType == typeof(string))
				item.SetValue(result, value.ToString());
			else if (item.PropertyType.Namespace == "System.Collections.Generic"
				 || item.PropertyType.IsArray
				 || item.PropertyType.IsDefined(typeof(CacheableContract)))
			{
				item.SetValue(result, item.PropertyType.Name.StartsWith("Null")
					? null
					: JsonConvert.DeserializeObject(value, item.PropertyType));
			}
			else if (item.PropertyType.IsEnum)
				item.SetValue(result, Enum.Parse(item.PropertyType, value.ToString()));
			else
			{
				item.SetValue(result,
					(item.PropertyType.Name.StartsWith("Null")
						? Nullable.GetUnderlyingType(item.PropertyType)
						: item.PropertyType)?
					.InvokeMember("Parse",
						BindingFlags.Static |
						BindingFlags.InvokeMethod |
						BindingFlags.Public,
						null,
						null,
						new object[] { value.ToString() })
				);
			}
		}
		return result;
	}

	private static byte[]? ToByteArray(T? obj)
	{
		if (obj == null)
			return null;
		using var ms = new MemoryStream();
		new XmlSerializer(typeof(T)).Serialize(ms, obj);
		return ms.ToArray();
	}

	private static T? FromByteArray(byte[]? data)
	{
		if (data == null)
			return default;
		using var ms = new MemoryStream(data);
		return (T)new XmlSerializer(typeof(T)).Deserialize(ms)!;
	}

	#endregion
}