// SPDX-License-Identifier: Apache-2.0
namespace PicoGK;

/// <summary>
/// Exception raised when the native PicoGK.Core runtime reports an API error.
/// </summary>
public class PicoGKException : Exception
{
    /// <summary>Creates an exception with the native runtime error message.</summary>
    public PicoGKException(string strMessage)
        : base(strMessage)
    {
    }

    /// <summary>Creates an exception with the native runtime error message and originating exception.</summary>
    public PicoGKException(
        string strMessage,
        Exception oInnerException)
        : base(strMessage, oInnerException)
    {
    }
}
