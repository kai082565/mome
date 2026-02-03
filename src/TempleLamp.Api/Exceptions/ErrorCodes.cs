namespace TempleLamp.Api.Exceptions;

/// <summary>
/// 系統錯誤碼定義
/// </summary>
public static class ErrorCodes
{
    // 通用錯誤 (1xxx)
    public const string SYSTEM_ERROR = "ERR_1000";
    public const string VALIDATION_ERROR = "ERR_1001";
    public const string NOT_FOUND = "ERR_1002";
    public const string INVALID_REQUEST = "ERR_1003";
    public const string WORKSTATION_REQUIRED = "ERR_1004";

    // 客戶相關 (2xxx)
    public const string CUSTOMER_NOT_FOUND = "ERR_2001";
    public const string CUSTOMER_PHONE_INVALID = "ERR_2002";

    // 燈位相關 (3xxx)
    public const string SLOT_NOT_FOUND = "ERR_3001";
    public const string SLOT_LOCKED = "ERR_3002";
    public const string SLOT_NOT_AVAILABLE = "ERR_3003";
    public const string SLOT_LOCK_EXPIRED = "ERR_3004";
    public const string SLOT_LOCK_FAILED = "ERR_3005";
    public const string SLOT_NOT_LOCKED_BY_YOU = "ERR_3006";
    public const string SLOT_RELEASE_FAILED = "ERR_3007";

    // 訂單相關 (4xxx)
    public const string ORDER_NOT_FOUND = "ERR_4001";
    public const string ORDER_ALREADY_CONFIRMED = "ERR_4002";
    public const string ORDER_ALREADY_CANCELLED = "ERR_4003";
    public const string ORDER_INVALID_STATUS = "ERR_4004";
    public const string ORDER_CREATE_FAILED = "ERR_4005";
    public const string ORDER_PAYMENT_FAILED = "ERR_4006";

    // 付款相關 (5xxx)
    public const string PAYMENT_AMOUNT_MISMATCH = "ERR_5001";
    public const string PAYMENT_METHOD_INVALID = "ERR_5002";
}
