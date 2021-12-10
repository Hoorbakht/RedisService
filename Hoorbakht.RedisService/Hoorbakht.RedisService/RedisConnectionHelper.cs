using StackExchange.Redis;
using Hoorbakht.RedisService.Contracts;

namespace Hoorbakht.RedisService;

internal class RedisConnectionHelper
{
	#region [Field(s)]

	private readonly Lazy<ConnectionMultiplexer> _lazyConnection;

	#endregion

	#region [Constructor]

	internal RedisConnectionHelper(RedisConfiguration redisConfiguration)
	{
		_lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
			ConnectionMultiplexer.Connect(redisConfiguration.ConnectionString));
		Servers = Connection.GetEndPoints().Select(x => Connection.GetServer(x)).Where(x => !x.IsReplica).ToList();
	}

	#endregion

	#region [Internal Property]

	internal readonly List<IServer> Servers;

	internal ConnectionMultiplexer Connection => _lazyConnection.Value;

	#endregion
}