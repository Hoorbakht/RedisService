using Hoorbakht.RedisService.WebSample.DataAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoorbakht.RedisService.WebSample.Pages.Person
{
	public class DetailModel : PageModel
	{
		private readonly IRedisService<Models.Person> _redisService;

		private readonly SampleContext _sampleContext;

		public DetailModel(IRedisService<Models.Person> redisService, SampleContext sampleContext)
		{
			_redisService = redisService;
			_sampleContext = sampleContext;
		}

		[BindProperty(SupportsGet = true)]
		public int PersonId { get; set; }

		public async Task OnGet()
		{
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
		}
	}
}
