using Microsoft.AspNetCore.Mvc;
using ProfixmedWeb.Models;
using ProfixmedWeb.Services;
using System.Diagnostics;

namespace ProfixmedWeb.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ContentStore _store;

    public HomeController(ILogger<HomeController> logger, ContentStore store)
    {
        _logger = logger;
        _store = store;
    }

    public IActionResult Index()
    {
        return View(_store.Current);
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

