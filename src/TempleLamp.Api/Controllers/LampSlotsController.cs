using Microsoft.AspNetCore.Mvc;
using TempleLamp.Api.DTOs.Requests;
using TempleLamp.Api.DTOs.Responses;
using TempleLamp.Api.Middleware;
using TempleLamp.Api.Services;

namespace TempleLamp.Api.Controllers;

/// <summary>
/// 燈位管理 API
/// </summary>
[ApiController]
[Route("api/lamp-slots")]
[Produces("application/json")]
public class LampSlotsController : ControllerBase
{
    private readonly ILampSlotService _lampSlotService;
    private readonly ILogger<LampSlotsController> _logger;

    public LampSlotsController(ILampSlotService lampSlotService, ILogger<LampSlotsController> logger)
    {
        _lampSlotService = lampSlotService;
        _logger = logger;
    }

    /// <summary>
    /// 查詢燈位
    /// </summary>
    /// <param name="lampTypeId">燈種 ID（選填）</param>
    /// <param name="zone">區域（選填）</param>
    /// <param name="availableOnly">僅顯示可用燈位（預設 true）</param>
    /// <param name="year">年度（選填，預設當年）</param>
    /// <returns>燈位清單</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<LampSlotResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<LampSlotResponse>>>> Query(
        [FromQuery] int? lampTypeId,
        [FromQuery] string? zone,
        [FromQuery] bool availableOnly = true,
        [FromQuery] int? year = null)
    {
        var request = new LampSlotQueryRequest
        {
            LampTypeId = lampTypeId,
            Zone = zone,
            AvailableOnly = availableOnly,
            Year = year
        };

        var result = await _lampSlotService.QueryAsync(request);
        return Ok(ApiResponse<IEnumerable<LampSlotResponse>>.Ok(result));
    }

    /// <summary>
    /// 取得燈種清單
    /// </summary>
    /// <returns>燈種清單（含可用燈位數）</returns>
    [HttpGet("types")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<LampTypeResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<LampTypeResponse>>>> GetTypes()
    {
        var result = await _lampSlotService.GetLampTypesAsync();
        return Ok(ApiResponse<IEnumerable<LampTypeResponse>>.Ok(result));
    }

    /// <summary>
    /// 取得單一燈位資訊
    /// </summary>
    /// <param name="id">燈位 ID</param>
    /// <returns>燈位資訊</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<LampSlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<LampSlotResponse>>> GetById(int id)
    {
        var result = await _lampSlotService.GetByIdAsync(id);
        return Ok(ApiResponse<LampSlotResponse>.Ok(result));
    }

    /// <summary>
    /// 鎖定燈位
    /// </summary>
    /// <param name="id">燈位 ID</param>
    /// <param name="request">鎖定參數</param>
    /// <returns>鎖定結果</returns>
    [HttpPost("{id:int}/lock")]
    [ProducesResponseType(typeof(ApiResponse<LockLampSlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<LockLampSlotResponse>>> Lock(
        int id,
        [FromBody] LockLampSlotRequest? request)
    {
        var workstationId = HttpContext.GetWorkstationId();
        request ??= new LockLampSlotRequest();

        _logger.LogInformation("鎖定燈位: SlotId={SlotId}, Workstation={Workstation}, Duration={Duration}s",
            id, workstationId, request.LockDurationSeconds);

        var result = await _lampSlotService.LockAsync(id, workstationId, request);
        return Ok(ApiResponse<LockLampSlotResponse>.Ok(result));
    }

    /// <summary>
    /// 釋放燈位鎖定
    /// </summary>
    /// <param name="id">燈位 ID</param>
    /// <returns>釋放結果</returns>
    [HttpPost("{id:int}/release")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> Release(int id)
    {
        var workstationId = HttpContext.GetWorkstationId();

        _logger.LogInformation("釋放燈位: SlotId={SlotId}, Workstation={Workstation}", id, workstationId);

        var released = await _lampSlotService.ReleaseAsync(id, workstationId);

        if (released)
        {
            return Ok(ApiResponse.Ok());
        }

        return Ok(ApiResponse.Fail("RELEASE_FAILED", "燈位釋放失敗或已不在鎖定狀態"));
    }
}
