namespace Hoorbakht.RedisService.Contracts;

public class RedisConfiguration
{
	#region [Constructor]

	public RedisConfiguration(int cacheTimeInMinutes, string systemName, string connectionString, short database = 0)
	{
		CacheTimeInMinutes = cacheTimeInMinutes;
		SystemName = systemName;
		ConnectionString = connectionString;
		Database = database;
	}

	#endregion

	#region [Properties]

	internal int CacheTimeInMinutes { get; set; }

	internal string SystemName { get; set; }

	internal string ConnectionString { get; set; }

	internal short Database { get; set; }

	#endregion
}