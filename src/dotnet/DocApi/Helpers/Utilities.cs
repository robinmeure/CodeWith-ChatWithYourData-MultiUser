using System.Text;

namespace WebApi.Helpers
{
    public static class Utitlities
    {
        internal static string SanitizeFileName(string fileName)
        {
            var sanitizedFileName = new StringBuilder();
            foreach (var c in fileName)
            {
                if (c <= sbyte.MaxValue)
                {
                    sanitizedFileName.Append(c);
                }
                else
                {
                    sanitizedFileName.Append('_'); // Replace non-ASCII characters with an underscore
                }
            }
            return sanitizedFileName.ToString();
        }
    }
}
