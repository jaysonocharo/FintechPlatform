using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FintechBackend.Data;
using FintechBackend.Models;
using Microsoft.AspNetCore.Authorization;
using FintechBackend.Constants; // Required for Roles.Admin
using FintechBackend.Extensions; // Required for User.GetUserId()
using FintechBackend.DTOs;

namespace FintechBackend.Controllers;

[ApiController]
[Route("api/[controller]")] // Means the route will be /api/transactions
[Authorize]
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
        var currentUserId = User.GetUserId();
        bool isAdmin = User.IsInRole(Roles.Admin);

        if (isAdmin)
        {
            // Admins can see all transactions in the system
            return await _context.Transactions.ToListAsync();
        }

        // 2. IDOR Prevention: Filter the list so users ONLY see their own transactions
        var userTransactions = await _context.Transactions
            .Where(t => t.UserId.ToString() == currentUserId)
            .ToListAsync();

        return userTransactions;
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
        // 3. IDOR Prevention: Ownership Check
        var currentUserId = User.GetUserId();

        // Guid.TryParse safely handles null or malformed strings
        bool isGuidValid = Guid.TryParse(currentUserId, out Guid parsedUserId);

        bool isOwner = isGuidValid && (transaction.UserId == parsedUserId);
        bool isAdmin = User.IsInRole(Roles.Admin);

        if (!isOwner && !isAdmin)
        {
            return Forbid(); // HTTP 403: Authenticated, but not authorized to view this specific resource
        }

        return transaction;
    }

    // 3. CREATE: POST api/transactions
    [HttpPost]
    public async Task<ActionResult<Transaction>> CreateTransaction(CreateTransactionDto dto)
    {
        // Security Best Practice: Don't trust the UserId sent in the JSON payload!
        // Force the transaction to belong to the person currently logged in via their JWT.
        var currentUserId = User.GetUserId();
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized("User ID claim is missing from token or corrupted.");
        
        var transaction = new Transaction
        {
            AccountHolder = dto.AccountHolder,
            Amount = dto.Amount,
            TransactionType = dto.TransactionType,
            UserId = Guid.Parse(currentUserId),
            CreatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var response = new TransactionResponseDto
        {
            Id = transaction.Id,
            AccountHolder = transaction.AccountHolder,
            Amount = transaction.Amount,
            TransactionType = transaction.TransactionType,
            CreatedAt = transaction.CreatedAt,
            UserId = transaction.UserId
        };

        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, response);
    }

    // 4. UPDATE: PUT api/transactions/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTransaction(int id, Transaction transaction)
    {
        if (id != transaction.Id)
        {
            return BadRequest("ID mismatch between URL and payload.");
        }

        // Fetch the existing record to verify ownership BEFORE modifying
        var existingTransaction = await _context.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        
        if (existingTransaction == null)
        {
            return NotFound();
        }

        // 4. IDOR Prevention: Ownership Check
        var currentUserId = User.GetUserId();
        bool isOwner = existingTransaction.UserId.ToString() == currentUserId;
        bool isAdmin = User.IsInRole(Roles.Admin);

        if (!isOwner && !isAdmin)
        {
            return Forbid(); 
        }

        // Ensure the payload doesn't try to change the owner of the transaction
        transaction.UserId = existingTransaction.UserId;

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

        // 5. IDOR Prevention: Ownership Check
        var currentUserId = User.GetUserId();
        bool isOwner = transaction.UserId.ToString() == currentUserId;
        bool isAdmin = User.IsInRole(Roles.Admin);

        if (!isOwner && !isAdmin)
        {
            return Forbid(); 
        }

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();

        return NoContent(); // HTTP 204: Deleted successfully
    }
}