// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MixedRealityExtension.Util
{
    /// <summary>
    /// MRE Runtime Utilities
    /// </summary>
    public static class UtilMethods
    {
        /// <summary>
        /// Converts an enum from one type to another based on matching strings for the possible values of the enums.
        /// </summary>
        /// <typeparam name="ReturnT">The enum type to be converted to.</typeparam>
        /// <typeparam name="SourceT">The enum type being converted from.</typeparam>
        /// <param name="source">The value of the enum being converted from.</param>
        /// <returns>The value of the enum that has been converted to.</returns>
        /// <example>
        /// enum Gender
        /// {
        ///     Male,
        ///     Female
        /// }
        /// 
        /// enum Sex
        /// {
        ///     Male,
        ///     Female
        /// }
        /// 
        /// var gen = Gender.Male;
        /// 
        /// // Result will be the string matching of Gender.Male.ToString() and Sex.Male.ToString()
        /// Sex sex = UnityHelpers.ConvertEnum(gen);
        /// </example>
        internal static ReturnT ConvertEnum<ReturnT, SourceT>(SourceT source)
        {
            if (typeof(ReturnT).IsEnum && typeof(SourceT).IsEnum)
            {
                return (ReturnT)Enum.Parse(typeof(ReturnT), source.ToString(), true);
            }

            if (!typeof(SourceT).IsEnum)
            {
                throw new InvalidOperationException(
                    string.Format("ConvertEnum: Cannot convert a non enum of type {0} to another enum", typeof(SourceT).ToString()));
            }
            else
            {
                throw new InvalidOperationException(
                    string.Format("ConvertEnum: Cannot convert an enum to a non enum of type {0}.", typeof(ReturnT).ToString()));
            }
        }

        internal static IEnumerable<FlagT> GetFlagEnumerable<FlagT>() where FlagT : struct, IComparable
        {
            if (!typeof(FlagT).IsEnum)
            {
                throw new InvalidOperationException(
                    $"Trying to get an enumerable for an enum flag type that is not an enum.  Type given is {typeof(FlagT).ToString()}");
            }

            if (typeof(FlagT).GetCustomAttributes(typeof(FlagsAttribute), false).Length == 0)
            {
                throw new InvalidOperationException(
                    $"Trying to get an enumerable for an enum flag type that is does not have the [Flags] attribute.  Type given is {typeof(FlagT).ToString()}");
            }

            return Enum.GetValues(typeof(FlagT)).Cast<FlagT>();
        }

        /// <summary>
        /// Generates a GUID from the provided string. Note the result is not a valid GUID (not compliant with RFC 4122), only shaped like a GUID (a reasonably unique 16-byte value).
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static Guid StringToGuid(string str)
        {
            var stringbytes = Encoding.ASCII.GetBytes(str);
            var hashedBytes = new System.Security.Cryptography.SHA1CryptoServiceProvider().ComputeHash(stringbytes);
            Array.Resize(ref hashedBytes, 16);
            return new Guid(hashedBytes);
        }

        /// <summary>
        /// Breaks the given url into a filename and everything else
        /// </summary>
        /// <param name="url">The base absolute URL</param>
        /// <param name="rootUrl">Everything preceding the final slash in the URL</param>
        /// <param name="filename">Everything after the final slash</param>
        internal static void GetUrlParts(string url, out string rootUrl, out string filename)
        {
            var uri = new Uri(url);

            rootUrl = uri.GetLeftPart(UriPartial.Authority);
            for (int i = 0; i < uri.Segments.Length - 1; i++)
            {
                rootUrl += uri.Segments[i];
            }

            filename = uri.Segments[uri.Segments.Length - 1];
        }
    }
}
