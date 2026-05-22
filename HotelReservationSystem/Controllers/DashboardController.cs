using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelReservationSystem.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HotelReservationSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            var totalRooms = await _context.Rooms.CountAsync();
            var totalClients = await _context.Clients.CountAsync();
            var totalReservations = await _context.Reservations.CountAsync();

            // Chambres occupées aujourd'hui (réservation en cours)
            var occupiedRoomIds = await _context.Reservations
                .Where(r => r.CheckIn <= today && r.CheckOut > today)
                .Select(r => r.RoomId)
                .Distinct()
                .ToListAsync();
            var roomsOccupied = occupiedRoomIds.Count;
            var roomsFree = totalRooms - roomsOccupied;

            var activeReservations = await _context.Reservations
                .CountAsync(r => r.CheckIn <= today && r.CheckOut > today);

            var upcomingReservations = await _context.Reservations
                .CountAsync(r => r.CheckIn > today);

            // Revenu estimé (réservations en cours et à venir : nuits * prix chambre)
            var revenueData = await _context.Reservations
                .Include(r => r.Room)
                .Where(r => r.CheckOut > today)
                .Select(r => new { Nights = EF.Functions.DateDiffDay(r.CheckIn, r.CheckOut), Price = r.Room!.Price })
                .ToListAsync();
            var estimatedRevenue = revenueData.Sum(x => x.Nights * x.Price);

            var occupancyRate = totalRooms == 0 ? 0 : (int)Math.Round(100.0 * roomsOccupied / totalRooms);

            ViewBag.Rooms = totalRooms;
            ViewBag.Clients = totalClients;
            ViewBag.Reservations = totalReservations;
            ViewBag.RoomsOccupied = roomsOccupied;
            ViewBag.RoomsFree = roomsFree;
            ViewBag.ActiveReservations = activeReservations;
            ViewBag.UpcomingReservations = upcomingReservations;
            ViewBag.EstimatedRevenue = estimatedRevenue;
            ViewBag.OccupancyRate = occupancyRate;

            return View();
        }
    }
}
