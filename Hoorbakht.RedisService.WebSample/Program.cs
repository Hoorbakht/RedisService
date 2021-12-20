using Hoorbakht.RedisService;
using Hoorbakht.RedisService.Contracts;
using Hoorbakht.RedisService.WebSample.DataAccess;
using Hoorbakht.RedisService.WebSample.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

var configuration = new RedisConfiguration(-1, "Person", "localhost:6379", 1);

builder.Services.AddSingleton<IRedisService<Person>>(_ => new RedisService<Person>(configuration, "RedisPerson"));

builder.Services.AddDbContext<SampleContext>(options => options.UseInMemoryDatabase("Sample"));

var app = builder.Build();

await using var scope = app.Services.CreateAsyncScope();

await using var context = scope.ServiceProvider.GetRequiredService<SampleContext>();

await context.Database.EnsureCreatedAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

await app.RunAsync();
