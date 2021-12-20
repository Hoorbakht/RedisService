using Hoorbakht.RedisService.WebSample.Models;
using Microsoft.EntityFrameworkCore;

namespace Hoorbakht.RedisService.WebSample.DataAccess
{
	public class SampleContext : DbContext
	{
		public SampleContext(DbContextOptions options) : base(options) { }

		public DbSet<Person>? Persons { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Person>().HasData(new List<Person>
			{
				new()
				{
					Id = 1,
					Name = "Mahyar",
					Family = "Hoorbakht"
				}
			});
		}
	}
}
