using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ICG.NetCore.Utilities.UnitTesting;

/// <summary>
///     Abstract class for creating unit tests containing methods that help with unit testing
/// </summary>
public abstract class AbstractModelTest
{
    /// <summary>
    ///     Asserts that an object has a <see cref="DisplayAttribute" /> for a particular property with a specific value.  Can
    ///     be used as part of a theory for detailed testing.
    /// </summary>
    /// <param name="modelType">The model type to test</param>
    /// <param name="property">The name of the property expected to contain the <see cref="DisplayAttribute" /></param>
    /// <param name="displayName">The expected name for the property defined in the <see cref="DisplayAttribute" /></param>
    /// <example>
    ///     [Theory]
    ///     [InlineData("PropertyName", "Property Name")]
    ///     public void DisplayPropertiesShouldHaveDisplayNamesDefined(string property, string expectedText)
    ///     {
    ///     //Act/Asset
    ///     AssertDisplayAttribute(typeof(ModelToTest), property, expectedText);
    ///     }
    /// </example>
    public void AssertDisplayAttribute(Type modelType, string property, string displayName)
    {
        var displayAttribute = modelType.GetTypeInfo().GetProperty(property).GetCustomAttribute<DisplayAttribute>();

        Assert.NotNull(displayAttribute);
        Assert.Equal(displayName, displayAttribute.Name);
    }

    /// <summary>
    ///     Asserts that an object has a <see cref="RequiredAttribute" /> for a particular property.  Can
    ///     be used as part of a theory for detailed testing.
    /// </summary>
    /// <param name="modelType">The model type to test</param>
    /// <param name="property">The name of the property expected to contain the <see cref="RequiredAttribute" /></param>
    /// <param name="errorMessage">
    ///     The expected value for the Error Message property of the <see cref="RequiredAttribute" />
    ///     this is optional
    /// </param>
    /// <example>
    ///     [Theory]
    ///     [InlineData("PropertyName")]
    ///     public void RequiredPropertiesShouldHaveRequiredAttribute(string property)
    ///     {
    ///     //Act/Asset
    ///     AssertRequiredAttribute(typeof(ModelToTest), property);
    ///     }
    /// </example>
    public void AssertRequiredAttribute(Type modelType, string property, string errorMessage = "")
    {
        var requiredAttribute = modelType.GetTypeInfo().GetProperty(property).GetCustomAttribute<RequiredAttribute>();

        Assert.NotNull(requiredAttribute);
        if (string.IsNullOrEmpty(errorMessage))
            return;
        Assert.Equal(errorMessage, requiredAttribute.ErrorMessage);
    }

    /// <summary>
    ///     Asserts that an object has a <see cref="MaxLengthAttribute" /> for a particular property with a defined value
    /// </summary>
    /// <param name="modelType">The model type to test</param>
    /// <param name="property">The property that should have the <see cref="MaxLengthAttribute" /></param>
    /// <param name="maxLength">The expected value for the MaxLength property of the <see cref="MaxLengthAttribute" /></param>
    /// <param name="errorMessage">
    ///     The expected value for the Error Message property of the <see cref="MaxLengthAttribute" />
    ///     this is optional.
    /// </param>
    public void AssertMaxLengthAttribute(Type modelType, string property, int maxLength, string errorMessage = "")
    {
        var maxLengthAttribute =
            modelType.GetTypeInfo().GetProperty(property).GetCustomAttribute<MaxLengthAttribute>();

        Assert.NotNull(maxLengthAttribute);
        Assert.Equal(maxLength, maxLengthAttribute.Length);
        if (string.IsNullOrEmpty(errorMessage))
            return;
        Assert.Equal(errorMessage, maxLengthAttribute.ErrorMessage);
    }

    /// <summary>
    ///     Creates a string with the specified length, helpful for validating minimum and maximum values for objects
    /// </summary>
    /// <param name="desiredLength">The desired length of the resultant string</param>
    /// <returns>A string that is <see cref="desiredLength" /> characters in length</returns>
    public string CreateString(int desiredLength)
    {
        return string.Join(string.Empty, Enumerable.Repeat("a", desiredLength));
    }
}