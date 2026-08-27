using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using SIOMS.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIOMS.ViewComponents
{
    public class AlertBadgeViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public AlertBadgeViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var products = await _context.Products
                .Where(p => p.StockQuantity <= p.MinStockLimit || p.StockQuantity == 0)
                .OrderBy(p => p.Name)
                .Take(10)
                .ToListAsync();

            return View(products);
        }
    }
}