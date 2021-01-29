using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;
//using System.Text;
//using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
//using System.Xml;

namespace InterLayerLib
{
    static public class ToolKit
    {
        static public CancellationTokenSource tokenSource = new CancellationTokenSource();

        /// <summary>
        /// format and trace message        
        /// <param name="message">message to be traced out</param>
        /// <param name="memberName">caller member name</param>
        /// <param name="sourceFilePath">current source file</param>
        /// <param name="sourceLineNumber">current source line number</param>
        /// </summary>
        static public void Trace(string message,
    [CallerMemberName] string memberName = "",
    [CallerFilePath] string sourceFilePath = "",
    [CallerLineNumber] int sourceLineNumber = 0)
        {
            string s;
#if (MODULE_TEST)
            s = sourceFilePath + ", line: " + sourceLineNumber + Environment.NewLine;
#else
            s = DateTime.Now.ToString("HH:mm:ss.ff ") + sourceFilePath + ", line: " + sourceLineNumber + ", thread: " + Thread.CurrentThread.Name + "/" + Thread.CurrentThread.ManagedThreadId + Environment.NewLine;
#endif
            s += memberName + "() " + message;
            //LogWriter.Instance.WriteToLog(s);
            Debug.WriteLine(s);
        }

        static public void ThrowCancel()
        {
            tokenSource.Token.ThrowIfCancellationRequested();
        }

        static public string Reverse(string s)
        {
            if (s == null)
                return null;
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        /// <summary>
        /// Function converts a string with number in bytes (for example: "1073741824") to human readable string in bytes.
        /// </summary>
        /// <param name="byteCount">number in bytes</param>
        /// <returns>human readable string in bytes</returns>
        static public String BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        static public String StringBytesToString(String byteCount)
        {
            long value = 0;
            try
            {
                value = Convert.ToInt64(byteCount);
            }
            catch
            {
                return "n/a";
            }
            return BytesToString(value);
        }

        /// <summary>
        /// converts time interval from .NET structure to human readable string
        /// </summary>
        /// <param name="ts">input time interval</param>
        /// <returns></returns>
        static public string GetReadableTimespan(TimeSpan ts)
        {
            // formats and its cutoffs based on total seconds
            var cutoff = new SortedList<long, string> {
            {60, "{3:S}" },
            {60*60, "{2:M}, {3:S}"},
            {24*60*60, "{1:H}, {2:M}"},
            {Int64.MaxValue , "{0:D}, {1:H}"}
            };

            // find nearest best match
            var find = cutoff.Keys.ToList()
                          .BinarySearch((long)ts.TotalSeconds);
            // negative values indicate a nearest match
            var near = find < 0 ? Math.Abs(find) - 1 : find;
            // use custom formatter to get the string
            return String.Format(
                new HMSFormatter(),
                cutoff[cutoff.Keys[near]],
                ts.Days,
                ts.Hours,
                ts.Minutes,
                ts.Seconds);
        }

        static public string getBetween(string source, string before, string after, int startFrom)
        {
            int indexBefore = source.IndexOf(before, startFrom) + before.Length;
            return source.Substring(indexBefore, source.IndexOf(after, indexBefore) - indexBefore);
        }

        static public bool IsCancellationRequested()
        {
            return tokenSource.IsCancellationRequested;
        }

        static public void Cancel()
        {
            tokenSource.Cancel();
        }

        static public void rebuildToken()
        {
            tokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Checks, if the filename has the extension ".c" or ".cpp".
        /// </summary>
        /// <param name="filename">Filename or the whole path to a file</param>
        /// <returns>boolean value</returns>
        public static bool isCFilename(string filename)
        {
            if (!String.IsNullOrEmpty(filename))
            {
                String extension = Path.GetExtension(filename);
                if (extension.Equals(".c") || extension.Equals(".cpp"))
                    return true;
            }
            return false;

        }

        static public string quote(string s)
        {
            return "\"" + s + "\"";
        }

        public static void RunProcessAsync(string fileName, string args)
        {
            using (var process = new Process
            {
                StartInfo =
                {
                    FileName = fileName, Arguments = args,
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                },
                EnableRaisingEvents = true
            })
            {
                RunProcessAsync(process).ConfigureAwait(false);
            }
        }
        private static Task<int> RunProcessAsync(Process process)
        {
            var tcs = new TaskCompletionSource<int>();

            process.Exited += (s, ea) => tcs.SetResult(process.ExitCode);
            process.OutputDataReceived += (s, ea) => Console.WriteLine(ea.Data);
            process.ErrorDataReceived += (s, ea) => Console.WriteLine("ERR: " + ea.Data);

            bool started = process.Start();
            if (!started)
            {
                //you may allow for the process to be re-used (started = false) 
                //but I'm not sure about the guarantees of the Exited event in such a case
                throw new InvalidOperationException("Could not start process: " + process);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }

        public static void Resize<T>(this List<T> list, int size, T element = default(T))
        {
            int count = list.Count;

            if (size < count)
            {
                list.RemoveRange(size, count - size);
            }
            else if (size > count)
            {
                if (size > list.Capacity)   // Optimization
                    list.Capacity = size;

                list.AddRange(Enumerable.Repeat(element, size - count));
            }
        }

        /// <summary>
        /// Encode text to XML format
        /// </summary>
        /// <param name="text">plain text</param>
        /// <returns>XML text</returns>
        public static string XMLEncode(string text)
        {
            return System.Net.WebUtility.HtmlEncode(text);
        }

        /// <summary>
        /// Decode given XML text to plain text
        /// </summary>
        /// <param name="text">XML text</param>
        /// <returns>plain text</returns>
        public static string XMLDecode(string text)
        {
            return System.Net.WebUtility.HtmlDecode(text);
        }

        /// <summary>
        /// The function determines whether the given Expression is a number or not.
        /// </summary>
        public static bool IsNumeric(object Expression)
        {
            if (Expression == null || Expression is DateTime)
                return false;

            if (Expression is Int16 || Expression is Int32 || Expression is Int64 || Expression is Decimal || Expression is Single || Expression is Double || Expression is Boolean)
                return true;

            try
            {
                if (Expression is string)
                    Double.Parse(Expression as string);
                else
                    Double.Parse(Expression.ToString());
                return true;
            }
            catch { } // just dismiss errors but return false
            return false;
        }

        /// <summary>
        /// replaces the first substring in a string only
        /// </summary>
        /// <param name="Source">initial string</param>
        /// <param name="Find">substring to be replaced</param>
        /// <param name="Replace">string replacing Find within a Source string</param>
        /// <returns></returns>
        public static string ReplaceFirst(string Source, string Find, string Replace)
        {
            int pos = Source.IndexOf(Find);
            if (pos < 0)
                return Source;
            return Source.Substring(0, pos) + Replace + Source.Substring(pos + Find.Length);
        }

        /// <summary>
        /// Replaces the last substring in a string only
        /// </summary>
        /// <param name="Source">initial string</param>
        /// <param name="Find">substring to be replaced</param>
        /// <param name="Replace">string replacing Find within a Source string</param>
        /// <returns></returns>
        public static string ReplaceLast(string Source, string Find, string Replace)
        {
            int place = Source.LastIndexOf(Find);
            if (place == -1)
                return Source;
            string result = Source.Remove(place, Find.Length).Insert(place, Replace);
            return result;
        }

        public static List<string> split_by(string input, string by)
        {
            List<string> ret_val = new List<string>();
            int from = 0;
            int to = input.IndexOf(by);
            while (to != -1)
            {
                ret_val.Add(input.Substring(from, to - from));
                from = to + by.Count();
                to = input.IndexOf(by, from);
            }
            ret_val.Add(input.Substring(from));
            return ret_val;
        }

        /// <summary>
        /// Depth-first recursive delete, with handling for descendant 
        /// directories open in Windows Explorer.
        /// </summary>
        public static void DeleteDirectory(string path)
        {
            foreach (string directory in Directory.GetDirectories(path))
                DeleteDirectory(directory);

            try
            {
                // Make sure that all files have read-write access
                foreach (var file in Directory.GetFiles(path))
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
        }

        public static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable
        {
            return listToClone.Select(item => (T)item.Clone()).ToList();
        }

    }
}

