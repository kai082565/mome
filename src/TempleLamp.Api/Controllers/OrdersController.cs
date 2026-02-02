using Microsoft.AspNetCore.Mvc;
using TempleLamp.Api.DTOs.Requests;
using TempleLamp.Api.DTOs.Responses;
using TempleLamp.Api.Middleware;
using TempleLamp.Api.Services;

namespace TempleLamp.Api.Controllers;

/// <summary>
/// 訂單管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// 取得訂單詳細資訊
    /// </summary>
    /// <param name="id">訂單 ID</param>
    /// <returns>訂單資訊</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> GetById(int id)
    {
        var result = await _orderService.GetByIdAsync(id);
        return Ok(ApiResponse<OrderResponse>.Ok(result));
    }

    /// <summary>
    /// 建立訂單
    /// </summary>
    /// <param name="request">訂單資料</param>
    /// <returns>新建立的訂單</returns>
    /// <remarks>
    /// 建立訂單前，所有燈位必須先被當前工作站鎖定
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> Create([FromBody] CreateOrderRequest request)
    {
        var workstationId = HttpContext.GetWorkstationId();

        _logger.LogInformation("建立訂單: CustomerId={CustomerId}, SlotCount={SlotCount}, Workstation={Workstation}",
            request.CustomerId, request.LampSlotIds.Count, workstationId);

        var result = await _orderService.CreateAsync(request, workstationId);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.OrderId },
            ApiResponse<OrderResponse>.Ok(result));
    }

    /// <summary>
    /// 確認訂單（完成付款）
    /// </summary>
    /// <param name="id">訂單 ID</param>
    /// <param name="request">付款資料</param>
    /// <returns>更新後的訂單</returns>
    [HttpPost("{id:int}/confirm")]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> Confirm(
        int id,
        [FromBody] ConfirmOrderRequest request)
    {
        var workstationId = HttpContext.GetWorkstationId();

        _logger.LogInformation("確認訂單: OrderId={OrderId}, PaymentMethod={PaymentMethod}, Amount={Amount}, Workstation={Workstation}",
            id, request.PaymentMethod, request.AmountReceived, workstationId);

        var result = await _orderService.ConfirmAsync(id, request, workstationId);
        return Ok(ApiResponse<OrderResponse>.Ok(result));
    }

    /// <summary>
    /// 取消訂單
    /// </summary>
    /// <param name="id">訂單 ID</param>
    /// <param name="request">取消原因</param>
    /// <returns>更新後的訂單</returns>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> Cancel(
        int id,
        [FromBody] CancelOrderRequest request)
    {
        var workstationId = HttpContext.GetWorkstationId();

        _logger.LogWarning("取消訂單: OrderId={OrderId}, Reason={Reason}, Workstation={Workstation}",
            id, request.Reason, workstationId);

        var result = await _orderService.CancelAsync(id, request, workstationId);
        return Ok(ApiResponse<OrderResponse>.Ok(result));
    }

    /// <summary>
    /// 取得收據資訊
    /// </summary>
    /// <param name="id">訂單 ID</param>
    /// <returns>收據資訊</returns>
    [HttpGet("{id:int}/receipt")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReceiptResponse>>> GetReceipt(int id)
    {
        var result = await _orderService.GetReceiptAsync(id);
        return Ok(ApiResponse<ReceiptResponse>.Ok(result));
    }

    /// <summary>
    /// 列印收據
    /// </summary>
    /// <param name="id">訂單 ID</param>
    /// <param name="request">列印參數</param>
    /// <returns>列印結果</returns>
    [HttpPost("{id:int}/print")]
    [ProducesResponseType(typeof(ApiResponse<PrintResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PrintResultResponse>>> Print(
        int id,
        [FromBody] PrintReceiptRequest? request)
    {
        var workstationId = HttpContext.GetWorkstationId();
        request ??= new PrintReceiptRequest();

        _logger.LogInformation("列印收據: OrderId={OrderId}, Copies={Copies}, Workstation={Workstation}",
            id, request.Copies, workstationId);

        var result = await _orderService.PrintReceiptAsync(id, request, workstationId);
        return Ok(ApiResponse<PrintResultResponse>.Ok(result));
    }
}
