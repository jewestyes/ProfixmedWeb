using System.ComponentModel.DataAnnotations;

namespace ProfixmedWeb.Models;

public class AdminLoginForm
{
	[Required(ErrorMessage = "Введите логин")]
	public string? Username { get; set; }

	[Required(ErrorMessage = "Введите пароль")]
	[DataType(DataType.Password)]
	public string? Password { get; set; }

	public string? ReturnUrl { get; set; }
}

public class AdminEventForm
{
	public Guid? Id { get; set; }

	[Required(ErrorMessage = "Укажите дату")]
	public DateOnly? Date { get; set; }

	[Required(ErrorMessage = "Укажите адрес")]
	[StringLength(500, ErrorMessage = "Адрес слишком длинный")]
	public string? Location { get; set; }

	public bool IsVisible { get; set; } = true;

	/// <summary>Invitation PDF. Required for a new event, optional (replace) when editing.</summary>
	public IFormFile? Pdf { get; set; }

	/// <summary>Custom card picture; without it the first page of the PDF is used.</summary>
	public IFormFile? Image { get; set; }

	/// <summary>Drop the custom picture and go back to the one rendered from the PDF.</summary>
	public bool ResetImage { get; set; }

	public string? CurrentPdfUrl { get; set; }
	public string? CurrentImageUrl { get; set; }
	public bool CurrentImageIsAuto { get; set; }
}

public class AdminNewsForm
{
	public Guid? Id { get; set; }

	[Required(ErrorMessage = "Укажите заголовок")]
	[StringLength(300, ErrorMessage = "Заголовок слишком длинный")]
	public string? Title { get; set; }

	[Required(ErrorMessage = "Укажите дату")]
	public DateOnly? Date { get; set; }

	[Required(ErrorMessage = "Укажите ссылку")]
	[Url(ErrorMessage = "Ссылка должна начинаться с http:// или https://")]
	public string? Url { get; set; }

	public bool IsVisible { get; set; } = true;
}
