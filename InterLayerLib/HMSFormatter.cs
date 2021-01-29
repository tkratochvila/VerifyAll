using System;
using System.Collections.Generic;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

namespace InterLayerLib
{   
    /// \brief formatter for timespan
    /// <summary>
    /// formatter for plural/singular forms of
    /// seconds/hours/days
    /// a dictionary lookup for the correct format string based on TotalSeconds and a CustomFormatter to format the supplied Timespan accordingly.
    /// </summary>
    // formatter for forms of
    // seconds/hours/day
    public class HMSFormatter : ICustomFormatter, IFormatProvider
    {
        // list of Formats, with a P customformat for pluralization
        static Dictionary<string, string> timeformats = new Dictionary<string, string> {
            {"S", "{0:P:Seconds:Second}"},
            {"M", "{0:P:Minutes:Minute}"},
            {"H","{0:P:Hours:Hour}"},
            {"D", "{0:P:Days:Day}"}
        };

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            return String.Format(new PluralFormatter(), timeformats[format], arg);
        }

        public object GetFormat(Type formatType)
        {
            return formatType == typeof(ICustomFormatter) ? this : null;
        }
    }

    // formats a numeric value based on a format P:Plural:Singular
    public class PluralFormatter : ICustomFormatter, IFormatProvider
    {

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg != null)
            {
                var parts = format.Split(':'); // ["P", "Plural", "Singular"]

                if (parts[0] == "P") // correct format?
                {
                    // which index postion to use
                    int partIndex = (arg.ToString() == "1") ? 2 : 1;
                    // pick string (safe guard for array bounds) and format
                    return String.Format("{0} {1}", arg, (parts.Length > partIndex ? parts[partIndex] : ""));
                }
            }
            return String.Format(format, arg);
        }

        public object GetFormat(Type formatType)
        {
            return formatType == typeof(ICustomFormatter) ? this : null;
        }
    }
}
