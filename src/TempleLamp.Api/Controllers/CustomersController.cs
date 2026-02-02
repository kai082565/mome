using Microsoft.AspNetCore.Mvc;
using TempleLamp.Api.DTOs.Requests;
using TempleLamp.Api.DTOs.Responses;
using TempleLamp.Api.Services;

namespace TempleLamp.Api.Controllers;

/// <summary>
/// 客戶管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(ICustomerService customerService, ILogger<CustomersController> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    /// <summary>
    /// 依電話號碼搜尋客戶
    /// </summary>
    /// <param name="phone">電話號碼（模糊搜尋，至少 3 碼）</param>
    /// <returns>符合條件的客戶清單</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<CustomerSearchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CustomerSearchResponse>>> Search([FromQuery] string phone)
    {
        _logger.LogDebug("搜尋客戶: Phone={Phone}", phone);

        var result = await _customerService.SearchByPhoneAsync(phone);
        return Ok(ApiResponse<CustomerSearchResponse>.Ok(result));
    }

    /// <summary>
    /// 取得客戶詳細資訊
    /// </summary>
    /// <param name="id">客戶 ID</param>
    /// <returns>客戶資訊</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> GetById(int id)
    {
        var result = await _customerService.GetByIdAsync(id);
        return Ok(ApiResponse<CustomerResponse>.Ok(result));
    }

    /// <summary>
    /// 建立新客戶
    /// </summary>
    /// <param name="request">客戶資料</param>
    /// <returns>新建立的客戶資訊</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Create([FromBody] CreateCustomerRequest request)
    {
        _logger.LogInformation("建立客戶: Phone={Phone}", request.Phone);

        var result = await _customerService.CreateAsync(request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.CustomerId },
            ApiResponse<CustomerResponse>.Ok(result));
    }
}
