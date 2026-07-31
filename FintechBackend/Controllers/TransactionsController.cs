using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FintechBackend.Data;
using FintechBackend.Models;

namespace FintechBackend.Controllers;

[ApiController]
[Route("api/[controller]")] // Means the route will be /api/transactions
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _context;

    // Injecting the DbContext into the controller via the constructor
    public TransactionsController(AppDbContext context)
    {
        _context = context;
    }

    // 1. READ ALL: GET api/transactions
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions()
    {
        return await _context.Transactions.ToListAsync();
    }

    // 2. READ 1 only: GET api/transactions/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Transaction>> GetTransaction(int id)
    {
        var transaction = await _context.Transactions.FindAsync(id);

        if (transaction == null)
        {
            return NotFound();
        }

        return transaction;
    }

    // 3. CREATE: POST api/transactions
    [HttpPost]
    public async Task<ActionResult<Transaction>> CreateTransaction(Transaction transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);
    }

    // 4. UPDATE: PUT api/transactions/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTransaction(int id, Transaction transaction)
    {
        if (id != transaction.Id)
        {
            return BadRequest("ID mismatch between URL and payload.");
        }

        _context.Entry(transaction).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Transactions.Any(e => e.Id == id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent(); // HTTP 204: Updated successfully with no response body needed
    }

    // 5. DELETE: DELETE api/transactions/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(int id)
    {
        var transaction = await _context.Transactions.FindAsync(id);
        if (transaction == null)
        {
            return NotFound();
        }

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();

        return NoContent(); // HTTP 204: Deleted successfully
    }
}