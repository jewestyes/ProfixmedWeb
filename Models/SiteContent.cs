namespace ProfixmedWeb.Models;

/// <summary>
/// Editable content of the home page, stored as JSON in App_Data and managed from the admin panel.
/// </summary>
public class SiteContent
{
	public List<SiteEvent> Events { get; set; } = new();
	public List<NewsItem> News { get; set; } = new();

	/// <summary>
	/// Content that was hardcoded in Index.cshtml before the admin panel appeared,
	/// so the site looks the same right after the first deploy.
	/// </summary>
	public static SiteContent CreateDefault() => new()
	{
		Events =
		{
			new SiteEvent
			{
				Date = new DateOnly(2026, 10, 17),
				Location = "г. Москва, ул. Шипиловская, д. 28А, Отель «Милан».",
				PdfUrl = "/files/Пригласительный 17-10-26 отель Милан.pdf",
				ImageUrl = "/files/Пригласительный 17-10-26 отель Милан.png",
			},
		},
		News =
		{
			new NewsItem
			{
				Title = "Вьетнам хочет закупать у России вакцину от ВПЧ",
				Date = new DateOnly(2025, 9, 16),
				Url = "https://ria.ru/20250916/vaktsina-2042198375.html",
			},
			new NewsItem
			{
				Title = "В Саратове создан датчик, выявляющий болезни сердца за три минуты",
				Date = new DateOnly(2025, 9, 16),
				Url = "https://ria.ru/20250916/nauka-2041507597.html",
			},
			new NewsItem
			{
				Title = "Сургутские ученые поняли, как снизить угрозу рака при ЭКО",
				Date = new DateOnly(2025, 9, 16),
				Url = "https://ri.ria.ru/20250915/nauka-2040438635.html",
			},
		},
	};
}

public class SiteEvent
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public DateOnly Date { get; set; }
	public string Location { get; set; } = "";
	public string? PdfUrl { get; set; }
	public string? ImageUrl { get; set; }

	/// <summary>True when ImageUrl was rendered from the PDF, so it is regenerated when the PDF is replaced.</summary>
	public bool ImageIsAuto { get; set; }

	public bool IsVisible { get; set; } = true;
}

public class NewsItem
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Title { get; set; } = "";
	public DateOnly Date { get; set; }
	public string Url { get; set; } = "";
	public bool IsVisible { get; set; } = true;
}

public static class RuDate
{
	private static readonly string[] MonthsGenitive =
	{
		"января", "февраля", "марта", "апреля", "мая", "июня",
		"июля", "августа", "сентября", "октября", "ноября", "декабря",
	};

	// Month names are spelled out by hand: the server may run without ICU, where ru-RU culture is unavailable.
	public static string Long(DateOnly date) => $"{date.Day} {MonthsGenitive[date.Month - 1]} {date.Year} г.";

	public static string Short(DateOnly date) => date.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
