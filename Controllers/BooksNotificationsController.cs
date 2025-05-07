using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PageWhispers.Data;
using PageWhispers.Model;
using System.Threading.Tasks;

namespace PageWhispers.Controllers
{
    // Only authenticated users can access this controller
    [Authorize]
    public class BooksNotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Constructor injection for database context
        public BooksNotificationsController(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // Displays a list of currently active notices
        public async Task<IActionResult> Index()
        {
            var activeNotices = await _context.TimedAnnouncements
                .Where(n => n.ExpiresAt >= DateTime.UtcNow)
                .ToListAsync();

            return View(activeNotices);
        }

        // Admins only - load the create form with default values
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new TimedAnnouncement
            {
                StartDate = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(5),
                CreatedAt = DateTime.UtcNow
            });
        }

        // Admins only - handle announcement creation form submission
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TimedAnnouncement input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            input.CreatedAt = DateTime.UtcNow;
            _context.TimedAnnouncements.Add(input);
            await _context.SaveChangesAsync();

            TempData["NoticeMessage"] = "New notice posted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // Admins only - load the edit form for a specific announcement
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var notice = await _context.TimedAnnouncements.FindAsync(id);
            if (notice == null)
            {
                return NotFound();
            }
            return View(notice);
        }

        // Admins only - process the edited notice
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TimedAnnouncement updated)
        {
            if (id != updated.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(updated);
            }

            try
            {
                var existing = await _context.TimedAnnouncements.FindAsync(id);
                if (existing == null)
                {
                    return NotFound();
                }

                // Update only necessary fields
                existing.Title = updated.Title;
                existing.Message = updated.Message;
                existing.StartDate = updated.StartDate;
                existing.ExpiresAt = updated.ExpiresAt;

                _context.TimedAnnouncements.Update(existing);
                await _context.SaveChangesAsync();

                TempData["NoticeMessage"] = "Notice updated successfully.";
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

        // Admins only - load delete confirmation view
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var notice = await _context.TimedAnnouncements.FindAsync(id);
            if (notice == null)
            {
                return NotFound();
            }
            return View(notice);
        }

        // Admins only - confirm and delete the announcement
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var notice = await _context.TimedAnnouncements.FindAsync(id);
            if (notice != null)
            {
                _context.TimedAnnouncements.Remove(notice);
                await _context.SaveChangesAsync();
            }

            TempData["NoticeMessage"] = "Notice removed from the board.";
            return RedirectToAction(nameof(Index));
        }
    }
}
