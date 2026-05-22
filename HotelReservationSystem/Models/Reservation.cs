using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HotelReservationSystem.Models
{
    public class Reservation
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Date d'arrivée")]
        public DateTime CheckIn { get; set; }

        [Required]
        [Display(Name = "Date de départ")]
        public DateTime CheckOut { get; set; }

        [Required(ErrorMessage = "Veuillez sélectionner une chambre")]
        [Display(Name = "Chambre")]
        public int RoomId { get; set; }

        // Optionnel : réservation liée à un Client (saisi par l'admin)
        [Display(Name = "Client")]
        public int? ClientId { get; set; }

        // Optionnel : réservation liée à un utilisateur Identity (client connecté)
        public string? UserId { get; set; }

        // Annulation (la réservation est conservée dans l'historique)
        [Display(Name = "Annulée")]
        public bool IsCancelled { get; set; }

        // Note laissée par le client (1 à 5)
        [Range(1, 5, ErrorMessage = "La note doit être comprise entre 1 et 5.")]
        [Display(Name = "Note")]
        public int? Rating { get; set; }

        // Commentaire laissé par le client
        [StringLength(500, ErrorMessage = "Le commentaire ne peut pas dépasser 500 caractères.")]
        [Display(Name = "Commentaire")]
        public string? Comment { get; set; }

        [ForeignKey("RoomId")]
        [ValidateNever]
        public Room? Room { get; set; }

        [ForeignKey("ClientId")]
        [ValidateNever]
        public Client? Client { get; set; }

        [ValidateNever]
        public IdentityUser? User { get; set; }
    }
}
