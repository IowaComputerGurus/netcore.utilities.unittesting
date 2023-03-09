using System;

namespace ICG.NetCore.Utilities.UnitTesting;

/// <summary>
///     A process that generates sample data for testing purposes
/// </summary>
public interface ISampleDataGenerator
{
    /// <summary>
    ///     Creates a random string with a combination of upper case, lower case, or numbers, including spaces
    /// </summary>
    /// <param name="length">Optional length, defaults to 10</param>
    /// <returns>The random string of requested length</returns>
    string RandomAlphaNumericString(int length = 10);

    /// <summary>
    ///     Creates a random string with a combination of upper case and lower case letters with spaces.
    /// </summary>
    /// <param name="length">Optional length, defaults to 10</param>
    /// <returns>The random string of requested length</returns>
    string RandomAlphaString(int length = 10);

    /// <summary>
    ///     Creates a random date value
    /// </summary>
    /// <param name="minDate">Optional minimum date, if omitted defaults to 1/1/1900</param>
    /// <param name="maxDate">Optional maximum date, if omitted defaults to today</param>
    /// <returns>A random date value</returns>
    DateTime RandomDate(DateTime? minDate = null, DateTime? maxDate = null);

    /// <summary>
    ///     Creates a random integer value
    /// </summary>
    /// <param name="min">Optional minimum integer, if omitted defaults to 0</param>
    /// <param name="max">Optional maximum integer, if omitted defaults to <see cref="int.MaxValue" /></param>
    /// <returns>The random int value</returns>
    int RandomInt(int min = 0, int max = int.MaxValue);
}

/// <summary>
///     A process that generates sample data for testing purposes
/// </summary>
public class SampleDataGenerator : ISampleDataGenerator
{
    private readonly Random _random = new();

    /// <summary>
    ///     Creates a random string with a combination of upper case, lower case, or numbers, including spaces
    /// </summary>
    /// <param name="length">Optional length, defaults to 10</param>
    /// <returns>The random string of requested length</returns>
    public string RandomAlphaNumericString(int length = 10)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ";
        var stringChars = new char[length];

        for (var i = 0; i < stringChars.Length; i++) stringChars[i] = chars[_random.Next(chars.Length)];

        return new string(stringChars);
    }

    /// <summary>
    ///     Creates a random string with a combination of upper case and lower case letters with spaces.
    /// </summary>
    /// <param name="length">Optional length, defaults to 10</param>
    /// <returns>The random string of requested length</returns>
    public string RandomAlphaString(int length = 10)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ";
        var stringChars = new char[length];

        for (var i = 0; i < stringChars.Length; i++) stringChars[i] = chars[_random.Next(chars.Length)];

        return new string(stringChars);
    }

    /// <summary>
    ///     Creates a random date value
    /// </summary>
    /// <param name="minDate">Optional minimum date, if omitted defaults to 1/1/1900</param>
    /// <param name="maxDate">Optional maximum date, if omitted defaults to today</param>
    /// <returns>A random date value</returns>
    public DateTime RandomDate(DateTime? minDate = null, DateTime? maxDate = null)
    {
        var start = new DateTime(1900, 1, 1);
        if (minDate.HasValue) start = minDate.Value;

        var end = DateTime.Today;
        if (maxDate.HasValue) end = maxDate.Value;

        var range = (end - start).Days;
        return start.AddDays(_random.Next(range));
    }

    /// <summary>
    ///     Creates a random integer value
    /// </summary>
    /// <param name="min">Optional minimum integer, if omitted defaults to 0</param>
    /// <param name="max">Optional maximum integer, if omitted defaults to <see cref="int.MaxValue" /></param>
    /// <returns>The random int value</returns>
    public int RandomInt(int min = 0, int max = int.MaxValue)
    {
        return _random.Next(min, max);
    }
}