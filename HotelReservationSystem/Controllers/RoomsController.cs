using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HotelReservationSystem.Data;
using HotelReservationSystem.Models;

namespace HotelReservationSystem.Controllers
{
    [Authorize]
    public class RoomsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoomsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Rooms (accessible à tous : admin et clients)
        [AllowAnonymous]
        public async Task<IActionResult> Index(
            string? type,
            decimal? minPrice,
            decimal? maxPrice,
            bool onlyAvailable = false,
            DateTime? checkIn = null,
            DateTime? checkOut = null)
        {
            var query = _context.Rooms.AsQueryable();

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(r => r.Type == type);

            if (minPrice.HasValue)
                query = query.Where(r => r.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(r => r.Price <= maxPrice.Value);

            if (onlyAvailable)
                query = query.Where(r => r.Status == RoomStatus.Available);

            // Si des dates sont fournies, exclure les chambres déjà réservées sur la période
            // (les réservations annulées ne bloquent pas la chambre)
            if (checkIn.HasValue && checkOut.HasValue && checkIn.Value < checkOut.Value)
            {
                var reservedIds = await _context.Reservations
                    .Where(r => !r.IsCancelled && r.CheckIn < checkOut.Value && r.CheckOut > checkIn.Value)
                    .Select(r => r.RoomId)
                    .Distinct()
                    .ToListAsync();

                query = query.Where(r => !reservedIds.Contains(r.Id));
            }

            var rooms = await query.OrderBy(r => r.RoomNumber).ToListAsync();

            ViewBag.Types = await _context.Rooms
                .Where(r => r.Type != null && r.Type != "")
                .Select(r => r.Type)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
            ViewBag.CurrentType = type;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.OnlyAvailable = onlyAvailable;
            ViewBag.CheckIn = checkIn;
            ViewBag.CheckOut = checkOut;

            return View(rooms);
        }

        // GET: Rooms/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var room = await _context.Rooms.FirstOrDefaultAsync(m => m.Id == id);
            if (room == null) return NotFound();

            // Tableau de bord (admin) : agenda des réservations + statistiques par chambre
            if (User.IsInRole("Admin"))
            {
                var reservations = await _context.Reservations
                    .Include(r => r.Client)
                    .Include(r => r.User)
                    .Where(r => r.RoomId == room.Id && !r.IsCancelled)
                    .OrderBy(r => r.CheckIn)
                    .ToListAsync();

                ViewBag.Reservations = reservations;
                ViewBag.NbReservations = reservations.Count;

                // Chiffre d'affaires = somme des (nuits × prix de la chambre)
                ViewBag.Revenue = reservations
                    .Sum(r => (decimal)(r.CheckOut - r.CheckIn).Days * room.Price);

                // Note moyenne des réservations notées
                var ratings = reservations.Where(r => r.Rating.HasValue).Select(r => r.Rating!.Value).ToList();
                ViewBag.AvgRating = ratings.Count > 0 ? (double?)ratings.Average() : null;
                ViewBag.NbRatings = ratings.Count;
            }

            return View(room);
        }

        // GET: Rooms/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create() => View();

        // POST: Rooms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Id,RoomNumber,Type,Price,Capacity,Status")] Room room)
        {
            if (ModelState.IsValid)
            {
                _context.Add(room);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // GET: Rooms/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();
            return View(room);
        }

        // POST: Rooms/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,RoomNumber,Type,Price,Capacity,Status")] Room room)
        {
            if (id != room.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(room);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoomExists(room.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // GET: Rooms/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var room = await _context.Rooms.FirstOrDefaultAsync(m => m.Id == id);
            if (room == null) return NotFound();
            return View(room);
        }

        // POST: Rooms/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room != null)
            {
                _context.Rooms.Remove(room);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RoomExists(int id) => _context.Rooms.Any(e => e.Id == id);
    }
}
