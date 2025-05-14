using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(Roles = "Admin")]
public class UserController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
