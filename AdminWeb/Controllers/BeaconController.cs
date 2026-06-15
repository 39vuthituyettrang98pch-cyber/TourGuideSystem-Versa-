using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public class BeaconController : Controller
{
    private readonly AppDbContext _context;

    public BeaconController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Lấy danh sách thiết bị Beacon từ bảng Beacons
        var beacons = await _context.Beacons
            .Include(b => b.Poi)
            .ThenInclude(p => p!.Translations)
            .ToListAsync();
        return View(beacons);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Pois = await _context.Pois.Include(p => p.Translations).ToListAsync();
        return View(new Beacon());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Beacon beacon)
    {
        if (ModelState.IsValid)
        {
            _context.Beacons.Add(beacon);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Pois = await _context.Pois.Include(p => p.Translations).ToListAsync();
        return View(beacon);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var beacon = await _context.Beacons.FindAsync(id);
        if (beacon == null) return NotFound();
        
        ViewBag.Pois = await _context.Pois.Include(p => p.Translations).ToListAsync();
        return View(beacon);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Beacon beacon)
    {
        if (id != beacon.Id) return NotFound();

        if (ModelState.IsValid)
        {
            _context.Update(beacon);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Pois = await _context.Pois.Include(p => p.Translations).ToListAsync();
        return View(beacon);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var beacon = await _context.Beacons.FindAsync(id);
        if (beacon != null)
        {
            _context.Beacons.Remove(beacon);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
