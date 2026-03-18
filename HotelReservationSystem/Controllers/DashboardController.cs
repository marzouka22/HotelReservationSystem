using Microsoft.AspNetCore.Mvc;
using HotelReservationSystem.Data;
using System.Linq;

public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        ViewBag.Rooms = _context.Rooms.Count();
        ViewBag.Clients = _context.Clients.Count();
        ViewBag.Reservations = _context.Reservations.Count();

        return View();
    }
}