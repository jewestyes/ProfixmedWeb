using Microsoft.AspNetCore.Mvc;
using ProfixmedWeb.Models;

namespace ProfixmedWeb.Views.ViewsComponents;

public class EventModalViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(EventModalViewModel model)
    {
        return View(model);
    }
}

