using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PageWhisphers.Data;
using PageWhisphers.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace PageWhisphers.Controllers.Communication
{
    [Authorize]
    public class AnnouncementsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnnouncementsController(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // GET: Announcements
        public async Task<IActionResult> Index()
        {
            var announcements = await _context.TimedAnnouncements
                .Where(a => a.ExpiresAt >= DateTime.UtcNow)
                .ToListAsync();
            return View(announcements);
        }

        // GET: Announcements/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new TimedAnnouncement
            {
                StartDate = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            });
        }

        // POST: Announcements/Create
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TimedAnnouncement model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.CreatedAt = DateTime.UtcNow;
            _context.TimedAnnouncements.Add(model);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Announcement created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Announcements/Edit/{id}
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var announcement = await _context.TimedAnnouncements.FindAsync(id);
            if (announcement == null)
            {
                return NotFound();
            }
            return View(announcement);
        }

        // POST: Announcements/Edit/{id}
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TimedAnnouncement model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var announcement = await _context.TimedAnnouncements.FindAsync(id);
                if (announcement == null)
                {
                    return NotFound();
                }

                announcement.Title = model.Title;
                announcement.Message = model.Message;
                announcement.StartDate = model.StartDate;
                announcement.ExpiresAt = model.ExpiresAt;
                _context.Update(announcement);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Announcement updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.TimedAnnouncements.AnyAsync(e => e.Id == id))
                {
                    return NotFound();
                }
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Announcements/Delete/{id}
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var announcement = await _context.TimedAnnouncements.FindAsync(id);
            if (announcement == null)
            {
                return NotFound();
            }
            return View(announcement);
        }

        // POST: Announcements/DeleteConfirmed/{id}
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var announcement = await _context.TimedAnnouncements.FindAsync(id);
            if (announcement != null)
            {
                _context.TimedAnnouncements.Remove(announcement);
                await _context.SaveChangesAsync();
            }
            TempData["SuccessMessage"] = "Announcement deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}