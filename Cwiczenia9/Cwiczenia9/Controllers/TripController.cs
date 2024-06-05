using Cwiczenia9.Data;
using Cwiczenia9.DTO;
using Cwiczenia9.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cwiczenia9.Controllers;

[ApiController] 
[Route("api/[controller]")]
public class TripController : ControllerBase
{ 
   
    private readonly Cw9Context _context; 
     
    public TripController(Cw9Context context) 
    { 
        _context = context; 
    } 
    
    [HttpGet]
    public async Task<IActionResult> GetTrips([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _context.Trips.OrderByDescending(x => x.DateFrom).Include(x => x.IdCountries)
            .Skip((page - 1) * pageSize).Take(pageSize);

        var totalTrip = await _context.Trips.CountAsync();
        List<Trip> trips = await query.ToListAsync();

        var result = new
        {
            pageNum = page,
            pageSize = pageSize,
            allPages = (int)Math.Ceiling(totalTrip / (double)pageSize),
            trips = trips.Select((Trip t) => new
            {
                Name = t.Name,
                Description = t.Description,
                DateFrom = t.DateFrom,
                DateTo = t.DateTo,
                MaxPeople = t.MaxPeople,
                Countries = t.IdCountries.Select(x => new {x.Name}),
                Clients = _context.ClientTrips.Include(x => x.IdClientNavigation)
                    .Where(x => x.IdTrip == t.IdTrip).Select(x => new
                    {
                        FirstName = x.IdClientNavigation.FirstName, LastName = x.IdClientNavigation.LastName
                    })
            })
        };
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteClient(int id)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client == null)
        {
            return NotFound("Nie znaleziono kienta");
        }

        var exist = await _context.ClientTrips.AnyAsync(x => x.IdClient == id);
        if (exist)
        {
            return NotFound("Kient ma juz wycieczki");
        }   

        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();

        return NotFound("usunięto");
    }

    [HttpPost("{idTrip:int}/clients")]
    public async Task<IActionResult> AddClient(int idTrip, DataDTO client)
    {
        var trip = await _context.Trips.FindAsync(idTrip);
        if (trip == null)
        {
            return NotFound($"Wycieczka o id: {idTrip} nie istnieje");
        }

        var existClient = await _context.Clients.AnyAsync(x => x.Pesel == client.Pesel)!;
        if (existClient)
        {
            return NotFound("Klient juz istnieje");
        }

        var isClientAssigned =
            await _context.ClientTrips.AnyAsync(x => x.IdClientNavigation.Pesel == client.Pesel && x.IdTrip == idTrip);
        if (isClientAssigned)
        {
            return NotFound("Klient jest juz na wycieczce");
        }

        if (trip.DateFrom >= DateTime.Now)
        {
            return NotFound("wycieczka sie juz skonczyla");
        }

        var newClient = new Client
        {
            FirstName = client.FirstName,
            LastName = client.LastName,
            Email = client.Email,
            Telephone = client.Telephone,
            Pesel = client.Pesel
        };
        var clientTrip = new ClientTrip
        {
            IdClientNavigation = newClient,
            IdTripNavigation = trip,
            RegisteredAt = DateTime.Now,
            PaymentDate = client.PaymentDate
        };

        _context.Clients.Add(newClient);
        _context.ClientTrips.Add(clientTrip);
        await _context.SaveChangesAsync();
        
        return Ok("Klient został pomyslnie dodany");
    }
}