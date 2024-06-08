using System;

namespace Intertech.CallCenterPlusNuget.Validator
{
    /// <summary>
    /// Null attribute
    /// </summary>
    public sealed class ValidatedNotNullAttribute : Attribute { }
    /// <summary>
    /// Validates objects
    /// </summary>
    public static class Guard
    {
        /// <summary>
        /// Throws exception if request object is null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void NotNull<T>([ValidatedNotNullAttribute] this T value, string name) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
