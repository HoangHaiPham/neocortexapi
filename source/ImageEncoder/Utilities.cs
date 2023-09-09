using System.IO;
using System.Runtime.InteropServices;

namespace HtmImageEncoder
{
    internal static class Utilities
    {
        /// <summary>
        /// Some Windows may be configured to allow long path name.<br/> 
        /// Some external libraries depends on reading a file by file name may not work well with these system.<br/>
        /// This extension method is used to convert a relative path name to a format that would resolve the compatibility issue.
        /// </summary>
        /// <returns></returns>
        public static string GetCompatibleLongPath(this string path)
        {
            var result = path;

            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            if (isWindows)
            {
                result = string.Concat(@"\\?\", Path.GetFullPath(path));
            }

            return result;
        }
    }
}
