// SPDX-License-Identifier: Apache-2.0
namespace PicoGK;

/// <summary>
/// Exception raised when the native PicoGK.Core runtime reports an API error.
/// </summary>
public class PicoGKException : Exception
{
    public PicoGKException(string strMessage) : base(strMessage) { }
    public PicoGKException(string strMessage, Exception xInner) : base(strMessage, xInner) { }
}
