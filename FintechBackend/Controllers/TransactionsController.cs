using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FintechBackend.Data;
using FintechBackend.Models;
using Microsoft.AspNetCore.Authorization;
using FintechBackend.Constants; // Required for Roles.Admin
using FintechBackend.Extensions; // Required for User.GetUserId()
using FintechBackend.DTOs;
using FluentValidation;
using System.ComponentModel.DataAnnotations;
using Ganss.Xss;

namespace FintechBackend.Controllers;

[ApiController]
[Route("api/[controller]")] // Means the route will be /api/transactions
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IValidator<CreateTransactionDto> _validator;
    private readonly IValidator<UpdateTransactionDto> _updateValidator;

    // Injecting the DbContext into the controller via the constructor
    public TransactionsController(AppDbContext context, IValidator<CreateTransactionDto> validator, IValidator<UpdateTransactionDto> updateValidator)
    {
        _context = context;
        _validator = validator;
        _updateValidator = updateValidator;
    }

// 1. READ ALL: GET api/transactions
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionResponseDto>>> GetTransactions()
    {
        var currentUserId = User.GetUserId();
        bool isAdmin = User.IsInRole(Roles.Admin);
        var query = _context.Transactions.AsQueryable();

        // If not admin, filter by user ID
        if (!isAdmin)
        {
            query = query.Where(t => t.UserId.ToString() == currentUserId);
        }

        // Map the secure data to the DTO, leaving the User object behind
        var userTransactions = await query
            .Select(t => new TransactionResponseDto
            {
                Id = t.Id,
                AccountHolder = t.AccountHolder,
                Amount = t.Amount,
                TransactionType = t.TransactionType,
                CreatedAt = t.CreatedAt,
                UserId = t.UserId
            })
            .ToListAsync();

        return userTransactions;
    }   

    // 2. READ 1 only: GET api/transactions/5
    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionResponseDto>> GetTransaction(int id)
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

        // Map to DTO
        var response = new TransactionResponseDto
        {
            Id = transaction.Id,
            AccountHolder = transaction.AccountHolder,
            Amount = transaction.Amount,
            TransactionType = transaction.TransactionType,
            CreatedAt = transaction.CreatedAt,
            UserId = transaction.UserId
        };

        return response;
    }

    // 3. CREATE: POST api/transactions
    [HttpPost]
    public async Task<ActionResult<Transaction>> CreateTransaction(CreateTransactionDto dto)
    {
        // 1. Explicitly validate the incoming payload.
        // If validation fails, this line throws FluentValidation.ValidationException immediately.
        await _validator.ValidateAndThrowAsync(dto);

        // Security Best Practice: Don't trust the UserId sent in the JSON payload!
        // Force the transaction to belong to the person currently logged in via their JWT.
        var currentUserId = User.GetUserId();
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized("User ID claim is missing from token or corrupted.");

        var sanitizer = new HtmlSanitizer();
        var safeAccountHolder = sanitizer.Sanitize(dto.AccountHolder);
        var transaction = new Transaction
        {
            AccountHolder = safeAccountHolder,
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
    public async Task<IActionResult> UpdateTransaction(int id, UpdateTransactionDto dto) // Name is now 'dto'
    {
        // 1. Use the correct validator for the update DTO
        await _updateValidator.ValidateAndThrowAsync(dto);

        // 2. Fetch the existing record to verify ownership BEFORE modifying
        var existingTransaction = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id);
        
        if (existingTransaction == null) return NotFound();

        // 3. IDOR Prevention: Ownership Check
        var currentUserId = User.GetUserId();
        bool isOwner = existingTransaction.UserId.ToString() == currentUserId;
        bool isAdmin = User.IsInRole(Roles.Admin);

        if (!isOwner && !isAdmin) return Forbid(); 

        // 4. Map the updated fields from the DTO to the tracked entity
        existingTransaction.AccountHolder = dto.AccountHolder;
        existingTransaction.Amount = dto.Amount;
        existingTransaction.TransactionType = dto.TransactionType;

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

    //    // 4. UPDATE: PUT api/transactions/5
    // [HttpPut("{id}")]
    // public async Task<IActionResult> UpdateTransaction(int id , Transaction, UpdateTransactionDto dto)
    // {
    //     await _validator.ValidateAndThrowAsync(dto);
    //     // if (id != transaction.Id)
    //     // {
    //     //     return BadRequest("ID mismatch between URL and payload.");
    //     // }

    //     // Fetch the existing record to verify ownership BEFORE modifying
    //     var existingTransaction = await _context.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        
    //     if (existingTransaction == null)
    //     {
    //         return NotFound();
    //     }

    //     // 4. IDOR Prevention: Ownership Check
    //     var currentUserId = User.GetUserId();
    //     bool isOwner = existingTransaction.UserId.ToString() == currentUserId;
    //     bool isAdmin = User.IsInRole(Roles.Admin);

    //     if (!isOwner && !isAdmin)
    //     {
    //         return Forbid(); 
    //     }

    //     // 4. Map the updated fields from the DTO to the tracked entity
    //     existingTransaction.AccountHolder = dto.AccountHolder;
    //     existingTransaction.Amount = dto.Amount;
    //     existingTransaction.TransactionType = dto.TransactionType;

    //     _context.Entry(transaction).State = EntityState.Modified;

    //     try
    //     {
    //         await _context.SaveChangesAsync();
    //     }
    //     catch (DbUpdateConcurrencyException)
    //     {
    //         if (!_context.Transactions.Any(e => e.Id == id))
    //         {
    //             return NotFound();
    //         }
    //         else
    //         {
    //             throw;
    //         }
    //     }

    //     return NoContent(); // HTTP 204: Updated successfully with no response body needed
    // }

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