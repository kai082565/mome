using System.Globalization;

namespace TempleLampSystem.Helpers;

/// <summary>
/// 農曆日期轉換工具，使用 .NET 內建的 ChineseLunisolarCalendar
/// </summary>
public static class LunarCalendarHelper
{
    private static readonly ChineseLunisolarCalendar _lunar = new();

    /// <summary>
    /// 將西曆日期轉換為農曆月日字串，例如 "1月15日"
    /// </summary>
    public static string ToLunarDateString(DateTime date)
    {
        var (month, day, isLeapMonth) = GetLunarMonthDay(date);

        var monthStr = isLeapMonth ? $"閏{month}" : $"{month}";

        return $"{monthStr}月{day}日";
    }

    /// <summary>
    /// 取得農曆的年份（天干地支 + 生肖）
    /// </summary>
    public static string GetLunarYearName(DateTime date)
    {
        int lunarYear = _lunar.GetYear(date);

        string[] tianGan = { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸" };
        string[] diZhi = { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };
        string[] shengXiao = { "鼠", "牛", "虎", "兔", "龍", "蛇", "馬", "羊", "猴", "雞", "狗", "豬" };

        int ganIndex = (lunarYear - 4) % 10;
        int zhiIndex = (lunarYear - 4) % 12;

        return $"{tianGan[ganIndex]}{diZhi[zhiIndex]}年（{shengXiao[zhiIndex]}年）";
    }

    /// <summary>
    /// 產生收據用的農曆期間字串，例如 "農曆1月15日起至1月14日止為期一年"
    /// </summary>
    public static string GetLunarPeriodString(DateTime startDate, DateTime endDate)
    {
        var startLunar = ToLunarDateString(startDate);
        var endLunar = ToLunarDateString(endDate);
        return $"農曆{startLunar}起至{endLunar}止為期一年";
    }

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

    /// <summary>
    /// 取得農曆年/月/日的個別數值，供感謝狀個別欄位使用
    /// </summary>
    public static (int year, int month, int day, bool isLeapMonth) GetLunarDate(DateTime date)
    {
        int lunarYear = _lunar.GetYear(date);
        var (month, day, isLeapMonth) = GetLunarMonthDay(date);
        return (lunarYear, month, day, isLeapMonth);
    }

    /// <summary>
    /// 取得農曆月份和日期數值
    /// </summary>
    private static (int month, int day, bool isLeapMonth) GetLunarMonthDay(DateTime date)
    {
        int lunarYear = _lunar.GetYear(date);
        int lunarMonth = _lunar.GetMonth(date);
        int lunarDay = _lunar.GetDayOfMonth(date);

        // 取得該年的閏月位置（0 表示無閏月）
        int leapMonth = _lunar.GetLeapMonth(lunarYear);

        bool isLeapMonth = false;
        int actualMonth;

        if (leapMonth == 0)
        {
            // 無閏月，月份直接對應
            actualMonth = lunarMonth;
        }
        else if (lunarMonth < leapMonth)
        {
            // 在閏月之前，月份不受影響
            actualMonth = lunarMonth;
        }
        else if (lunarMonth == leapMonth)
        {
            // 就是閏月本身
            isLeapMonth = true;
            actualMonth = lunarMonth - 1;
        }
        else
        {
            // 在閏月之後，實際月份要減 1
            actualMonth = lunarMonth - 1;
        }

        return (actualMonth, lunarDay, isLeapMonth);
    }
}
