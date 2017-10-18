using System;
using System.Text.RegularExpressions;

namespace Wox.Plugin.MyFeedReader
{
    public class PluginHelper
    {
        public static string StripHtml(string input)
        {
            return Regex.Replace(input, "<.*?>", String.Empty);
        }
    }
}
