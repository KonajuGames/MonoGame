// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Diagnostics;

namespace MonoGame.Utilities
{
    /// <summary>
    /// Class for logging debug output in a platform neutral manner.
    /// </summary>
    public class Log
    {
        /// <summary>
        /// Writes a single message to the debug log.
        /// </summary>
        /// <param name="message">The message to write to the debug log.</param>
        [Conditional("DEBUG")]
        static public void Write(string message)
        {
#if ANDROID
            Android.Util.Log.Debug("MonoGame", message);
#else
            Debug.WriteLine(message);
#endif
        }

        /// <summary>
        /// Writes a formatted message to the debug log.
        /// </summary>
        /// <param name="format">The format string for the message.</param>
        /// <param name="args">The parameters for the format string.</param>
        [Conditional("DEBUG")]
        static public void Write(string format, params object[] args)
        {
#if ANDROID
            Android.Util.Log.Debug("MonoGame", format, args);
#else
            Debug.WriteLine(format, args);
#endif
        }
    }
}
