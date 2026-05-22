using System;
using System.Linq;
using System.Threading.Tasks;
using HotelReservationSystem.Data;
using HotelReservationSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HotelReservationSystem.Controllers
{
    [Authorize]
    public class ReservationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ReservationsController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Reservations/Calendar
        public IActionResult Calendar() => View();

        // GET: Reservations/GetReservations (JSON pour FullCalendar)
        [HttpGet]
        public async Task<IActionResult> GetReservations()
        {
            var events = await _context.Reservations
                .Include(r => r.Room)
                .Include(r => r.Client)
                .Include(r => r.User)
                .Where(r => !r.IsCancelled)
                .Select(r => new
                {
                    title = r.Room!.RoomNumber + " - " + (r.Client != null ? r.Client.FullName : (r.User != null ? r.User.Email : "")),
                    start = r.CheckIn.ToString("yyyy-MM-dd"),
                    end = r.CheckOut.ToString("yyyy-MM-dd")
                })
                .ToListAsync();

            return Json(events);
        }

        // GET: Reservations/GetAvailableRooms?checkIn=...&checkOut=...
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableRooms(DateTime checkIn, DateTime checkOut, int? excludeReservationId = null)
        {
            if (checkIn >= checkOut)
                return BadRequest("CheckOut must be after CheckIn");

            // Chambres déjà réservées sur la période (hors réservations annulées)
            var reservedIds = await _context.Reservations
                .Where(r => !r.IsCancelled && r.CheckIn < checkOut && r.CheckOut > checkIn)
                .Where(r => !excludeReservationId.HasValue || r.Id != excludeReservationId.Value)
                .Select(r => r.RoomId)
                .Distinct()
                .ToListAsync();

            // On exclut aussi les chambres en maintenance
            var available = await _context.Rooms
                .Where(room => room.Status == RoomStatus.Available && !reservedIds.Contains(room.Id))
                .Select(r => new { r.Id, r.RoomNumber, r.Type, r.Price })
                .ToListAsync();

            return Json(available);
        }

        // GET: Reservations
        public async Task<IActionResult> Index()
        {
            var query = _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.Room)
                .Include(r => r.User)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
            {
                var user = await _userManager.GetUserAsync(User);
                query = user != null
                    ? query.Where(r => r.UserId == user.Id)
                    : query.Where(r => false);
            }

            var reservations = await query.OrderByDescending(r => r.CheckIn).ToListAsync();
            return View(reservations);
        }

        // GET: Reservations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var reservation = await _context.Reservations
                .Include(r => r.Room)
                .Include(r => r.Client)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();
            if (!await UserCanAccess(reservation)) return Forbid();

            return View(reservation);
        }

        // GET: Reservations/Create
        public async Task<IActionResult> Create(DateTime? checkIn, DateTime? checkOut)
        {
            var reservation = new Reservation();
            if (checkIn.HasValue) reservation.CheckIn = checkIn.Value;
            if (checkOut.HasValue) reservation.CheckOut = checkOut.Value;

            await LoadDropdowns(reservation);
            return View(reservation);
        }

        // POST: Reservations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reservation reservation)
        {
            if (reservation.CheckIn >= reservation.CheckOut)
                ModelState.AddModelError("", "La date de départ doit être après la date d'arrivée.");

            if (reservation.RoomId == 0)
                ModelState.AddModelError("RoomId", "Veuillez choisir une chambre.");

            // La chambre ne doit pas être en maintenance
            var room = await _context.Rooms.FindAsync(reservation.RoomId);
            if (room == null)
                ModelState.AddModelError("RoomId", "Chambre introuvable.");
            else if (room.Status == RoomStatus.Maintenance)
                ModelState.AddModelError("RoomId", "Cette chambre est en maintenance et ne peut pas être réservée.");

            // Vérifier la disponibilité (les réservations annulées ne comptent pas)
            bool roomTaken = await _context.Reservations.AnyAsync(r =>
                !r.IsCancelled &&
                r.RoomId == reservation.RoomId &&
                reservation.CheckIn < r.CheckOut &&
                reservation.CheckOut > r.CheckIn);

            if (roomTaken)
                ModelState.AddModelError("", "Cette chambre est déjà réservée pour ces dates.");

            if (ModelState.IsValid)
            {
                if (!User.IsInRole("Admin"))
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user != null)
                    {
                        reservation.UserId = user.Id;
                        // Lier la réservation à la fiche Client de l'utilisateur
                        // (créée automatiquement lors de l'inscription, retrouvée par email)
                        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Email == user.Email);
                        reservation.ClientId = client?.Id;
                    }
                    else
                    {
                        reservation.ClientId = null;
                    }
                }

                // une nouvelle réservation n'est jamais annulée / notée
                reservation.IsCancelled = false;
                reservation.Rating = null;
                reservation.Comment = null;

                _context.Add(reservation);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Réservation créée avec succès.";
                return RedirectToAction(nameof(Index));
            }

            await LoadDropdowns(reservation);
            return View(reservation);
        }

        // GET: Reservations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null) return NotFound();
            if (!await UserCanAccess(reservation)) return Forbid();

            await LoadDropdowns(reservation);
            return View(reservation);
        }

        // POST: Reservations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Reservation reservation)
        {
            if (id != reservation.Id) return NotFound();

            var existing = await _context.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (existing == null) return NotFound();
            if (!await UserCanAccess(existing)) return Forbid();

            if (reservation.CheckIn >= reservation.CheckOut)
                ModelState.AddModelError("", "La date de départ doit être après la date d'arrivée.");

            if (reservation.RoomId == 0)
                ModelState.AddModelError("RoomId", "Veuillez choisir une chambre.");

            bool roomTaken = await _context.Reservations.AnyAsync(r =>
                r.Id != id &&
                !r.IsCancelled &&
                r.RoomId == reservation.RoomId &&
                reservation.CheckIn < r.CheckOut &&
                reservation.CheckOut > r.CheckIn);

            if (roomTaken)
                ModelState.AddModelError("", "Cette chambre est déjà réservée pour ces dates.");

            if (ModelState.IsValid)
            {
                try
                {
                    // On préserve les champs gérés ailleurs (annulation, note, propriétaire)
                    reservation.UserId = existing.UserId;
                    reservation.IsCancelled = existing.IsCancelled;
                    reservation.Rating = existing.Rating;
                    reservation.Comment = existing.Comment;
                    if (!User.IsInRole("Admin"))
                        reservation.ClientId = existing.ClientId;

                    _context.Update(reservation);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReservationExists(reservation.Id)) return NotFound();
                    throw;
                }
                TempData["Success"] = "Réservation modifiée.";
                return RedirectToAction(nameof(Index));
            }

            await LoadDropdowns(reservation);
            return View(reservation);
        }

        // GET: Reservations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var reservation = await _context.Reservations
                .Include(r => r.Room)
                .Include(r => r.Client)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();
            if (!await UserCanAccess(reservation)) return Forbid();

            return View(reservation);
        }

        // POST: Reservations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation != null)
            {
                if (!await UserCanAccess(reservation)) return Forbid();
                _context.Reservations.Remove(reservation);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Reservations/Cancel/5 — annulation (min. 24h avant l'arrivée)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null) return NotFound();
            if (!await UserCanAccess(reservation)) return Forbid();

            if (reservation.IsCancelled)
            {
                TempData["Error"] = "Cette réservation est déjà annulée.";
                return RedirectToAction(nameof(Index));
            }

            // Règle : annulation possible au moins 24h avant la date d'arrivée
            if (reservation.CheckIn < DateTime.Now.AddHours(24))
            {
                TempData["Error"] = "Une réservation ne peut être annulée qu'au moins 24h avant la date d'arrivée.";
                return RedirectToAction(nameof(Index));
            }

            reservation.IsCancelled = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Réservation annulée avec succès.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Reservations/Rate/5 — noter et commenter une réservation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rate(int id, int rating, string? comment)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null) return NotFound();
            if (!await UserCanAccess(reservation)) return Forbid();

            if (rating < 1 || rating > 5)
            {
                TempData["Error"] = "La note doit être comprise entre 1 et 5.";
                return RedirectToAction(nameof(Details), new { id });
            }

            reservation.Rating = rating;
            reservation.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            await _context.SaveChangesAsync();
            TempData["Success"] = "Merci ! Votre avis a bien été enregistré.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ---- Helpers ----

        private bool ReservationExists(int id) => _context.Reservations.Any(e => e.Id == id);

        private async Task<bool> UserCanAccess(Reservation reservation)
        {
            if (User.IsInRole("Admin")) return true;
            var user = await _userManager.GetUserAsync(User);
            return user != null && reservation.UserId == user.Id;
        }

        private async Task LoadDropdowns(Reservation? reservation = null)
        {
            // Seules les chambres disponibles (pas en maintenance) sont proposées
            var rooms = await _context.Rooms
                .Where(r => r.Status == RoomStatus.Available || (reservation != null && r.Id == reservation.RoomId))
                .OrderBy(r => r.RoomNumber)
                .ToListAsync();
            ViewBag.RoomId = new SelectList(rooms, "Id", "RoomNumber", reservation?.RoomId);

            if (User.IsInRole("Admin"))
            {
                var clients = await _context.Clients.OrderBy(c => c.FullName).ToListAsync();
                ViewBag.ClientId = new SelectList(clients, "Id", "FullName", reservation?.ClientId);
            }
            else
            {
                ViewBag.ClientId = new SelectList(Enumerable.Empty<Client>(), "Id", "FullName");
            }
        }
    }
}
