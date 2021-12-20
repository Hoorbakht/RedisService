using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hoorbakht.RedisService.WebSample.Models;

public class Person
{
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	public string? Name { get; set; }

	public string? Family { get; set; }

	public string FullName => Name + " " + Family;
}