# RedisService
Redis Service Nuget to Connect Redis with Stackexchange.Redis

[Get Hoorbakht.RedisService on nuget](https://www.nuget.org/packages/Hoorbakht.RedisService/)


## Usage for ASP.NET Core

In this example, consider an app with a `Person` entity. 
We'll use Hoorbakht.RedisService to Store Person Entity in Redis Instance .

### 1. Add required services

Inject the `IRedisService<Person>` service. So in `Program.cs` add:
```C#
// Create Configuration to Connect Redis Server
// First Argument is Cache Time in Minutes
// Second Argument is SubSystem Name
// Third Argument is Redis ConnectionString
// Fourth Argument is Database Number
var configuration = new RedisConfiguration(-1, "Person", "localhost:6379", 1);

// First Arguemnt is Configuration
// Second Argument is Name Contract Name
builder.Services.AddSingleton<IRedisService<Person>>(_ => new RedisService<Person>(configuration, "RedisPerson"));
```

### 2. Get IRedisService<Person> in Controller and Use Methods

```C#
var cachedData = await _redisService.GetHashAsync(PersonId.ToString());

if (cachedData != null)
{
  ViewData["Person"] = cachedData;
  return;
}

var person = await _sampleContext.Persons!.SingleOrDefaultAsync(x => x.Id == PersonId);

if (person != null)
{
  ViewData["Person"] = person;
  await _redisService.SetHashAsync(person.Id.ToString(), person);
}
```
