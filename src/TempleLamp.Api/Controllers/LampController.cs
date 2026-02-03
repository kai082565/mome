using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TempleLamp.Api.Data;
using TempleLamp.Api.Models;

namespace TempleLamp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LampController : ControllerBase
{
    private readonly AppDbContext _context;

    public LampController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 取得所有燈
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var lamps = await _context.Lamps.ToListAsync();
        return Ok(lamps);
    }

    /// <summary>
    /// 取得單一燈
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var lamp = await _context.Lamps.FindAsync(id);
        if (lamp == null)
        {
            return NotFound(new { message = $"找不到 ID 為 {id} 的燈" });
        }
        return Ok(lamp);
    }

    /// <summary>
    /// 新增燈
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLampRequest request)
    {
        var lamp = new Lamp
        {
            Name = request.Name,
            CreatedAt = DateTime.UtcNow
        };

        _context.Lamps.Add(lamp);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = lamp.Id }, lamp);
    }

    /// <summary>
    /// 更新燈
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateLampRequest request)
    {
        var lamp = await _context.Lamps.FindAsync(id);
        if (lamp == null)
        {
            return NotFound(new { message = $"找不到 ID 為 {id} 的燈" });
        }

        lamp.Name = request.Name;
        await _context.SaveChangesAsync();

        return Ok(lamp);
    }

    /// <summary>
    /// 刪除燈
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var lamp = await _context.Lamps.FindAsync(id);
        if (lamp == null)
        {
            return NotFound(new { message = $"找不到 ID 為 {id} 的燈" });
        }

        _context.Lamps.Remove(lamp);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class CreateLampRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateLampRequest
{
    public string Name { get; set; } = string.Empty;
}
