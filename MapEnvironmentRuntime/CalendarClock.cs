using System;
using System.Globalization;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    public static class CalendarClock
    {
        public static bool IsLeap(int year, Parameters p) => year % p.I("calendar.leapDivisor") == default(int)
            && (year % p.I("calendar.centuryDivisor") != default(int) || year % p.I("calendar.leapCenturyDivisor") == default(int));
        public static int DaysInMonth(int year, int month, Parameters p)
        {
            int days = p.I("calendar.monthDays." + month.ToString(CultureInfo.InvariantCulture));
            if (month == p.I("math.two") && IsLeap(year, p)) days += p.I("math.one");
            return days;
        }
        public static int DayOfYear(int year, int month, int day, Parameters p)
        {
            int result = day;
            for (int current = p.I("math.one"); current < month; current++) result += DaysInMonth(year, current, p);
            return result;
        }
        public static int DaysInYear(int year, Parameters p)
        {
            int result = default(int);
            for (int month = p.I("math.one"); month <= p.I("calendar.months"); month++) result += DaysInMonth(year, month, p);
            return result;
        }
        public static double AbsoluteSeconds(int year, int month, int day, float hour, Parameters p)
        {
            double years = year - p.I("math.one");
            double leapDays = Math.Floor(years / p.N("calendar.leapDivisor"))
                - Math.Floor(years / p.N("calendar.centuryDivisor")) + Math.Floor(years / p.N("calendar.leapCenturyDivisor"));
            double days = years * DaysInYear(p.I("math.one"), p) + leapDays
                + DayOfYear(year, month, day, p) - p.N("math.one");
            return (days * p.N("clock.hoursPerDay") + hour) * p.N("calendar.minutesPerHour") * p.N("clock.secondsPerMinute");
        }
    }
}
