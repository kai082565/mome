using System.Globalization;

namespace TempleLampSystem.Helpers;

/// <summary>
/// 農曆日期轉換工具，使用 .NET 內建的 ChineseLunisolarCalendar
/// </summary>
public static class LunarCalendarHelper
{
    private static readonly ChineseLunisolarCalendar _lunar = new();

    /// <summary>
    /// 取得當前農曆年份的農曆12月24日（送神日）對應的西曆日期。
    /// 若今天已過今年的農曆12/24，則回傳下一個農曆年的12/24。
    /// </summary>
    public static DateTime GetLunarYearEndDate(DateTime referenceDate)
    {
        int lunarYear = _lunar.GetYear(referenceDate);
        var endDate = LunarToGregorian(lunarYear, 12, 24);

        // 若今天已超過今年農曆12/24，使用下一年的
        if (referenceDate.Date >= endDate.Date)
        {
            endDate = LunarToGregorian(lunarYear + 1, 12, 24);
        }

        return endDate;
    }

    /// <summary>
    /// 將農曆日期轉為西曆日期
    /// </summary>
    private static DateTime LunarToGregorian(int lunarYear, int lunarMonth, int lunarDay)
    {
        int leapMonth = _lunar.GetLeapMonth(lunarYear);

        // 若有閏月且在目標月份之前或同月，內部月份編號要 +1
        int internalMonth = (leapMonth > 0 && lunarMonth >= leapMonth)
            ? lunarMonth + 1
            : lunarMonth;

        return _lunar.ToDateTime(lunarYear, internalMonth, lunarDay, 0, 0, 0, 0);
    }

}
