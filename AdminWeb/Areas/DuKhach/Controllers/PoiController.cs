using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdminWeb.Areas.DuKhach.ViewModels;

namespace AdminWeb.Areas.DuKhach.Controllers
{
    [Area("DuKhach")]
    public class PoiController : Controller
    {
        private readonly AppDbContext _context;

        public PoiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("/DuKhach/Poi/Details/{id:int}")]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var poi = await _context.Pois
                .AsNoTracking()
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == id && p.Status == "Approved", cancellationToken);

            if (poi == null)
            {
                return NotFound();
            }

            var model = new PoiDetailViewModel
            {
                Poi = poi
            };

            var viTranslation = poi.Translations.FirstOrDefault(t => t.LanguageCode == "vi") ?? poi.Translations.FirstOrDefault();
            ViewData["Title"] = viTranslation?.Name ?? $"POI #{poi.Id}";

            return View(model);
        }
    }
}