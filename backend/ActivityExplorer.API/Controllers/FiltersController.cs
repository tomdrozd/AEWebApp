using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ActivityExplorer.Core.Models;
using ActivityExplorer.Data;

namespace ActivityExplorer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FiltersController : ControllerBase
    {
        private readonly ActivityContext _context;

        public FiltersController(ActivityContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetFilters()
        {
            var filters = await _context.SavedFilters
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
            return Ok(filters);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetFilter(int id)
        {
            var filter = await _context.SavedFilters.FindAsync(id);
            if (filter == null)
                return NotFound();
            return Ok(filter);
        }

        [HttpPost]
        public async Task<IActionResult> CreateFilter([FromBody] SavedFilter filter)
        {
            filter.CreatedAt = DateTime.UtcNow;
            _context.SavedFilters.Add(filter);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetFilter), new { id = filter.Id }, filter);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFilter(int id)
        {
            var filter = await _context.SavedFilters.FindAsync(id);
            if (filter == null)
                return NotFound();
            _context.SavedFilters.Remove(filter);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
