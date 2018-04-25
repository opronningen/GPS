using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace ConcatCorr
{
    class ConcatCorr
    {
        // Concatenate corrections (sp3 or clk) in a way compatible with multiday procesing in glab
        // Feed it an unordered list of corrections EITHER CLK or SP3 - not mixed. Prints to STDOUT
        static void Main(string[] args)
        {
            bool isSP3 = false;

            // Check that all inputfiles can be read
            foreach(string fname in args)
            {
                if (!File.Exists(fname))
                {
                    Console.WriteLine("Error: file {0} not found.", fname);
                    return;
                }

                if (fname.Contains("sp3") || fname.Contains("SP3"))
                    isSP3 = true;
            }

            // Sort filenames oldest to newest
            var fnames = args.ToArray();
            Array.Sort(fnames);

            bool firstHeaderDone = false;
            bool isHeader = false;
            bool isLastFile = false;

            Regex sp3HeaderStart = new Regex(@"^\#cP");
            Regex sp3HeaderEnd = new Regex(@"^\*  ");
            Regex clkHeaderStart = new Regex("RINEX VERSION");
            Regex cklHeaderEnd = new Regex("END OF HEADER");

            foreach (string f in fnames)
            {
                isHeader = true;

                if (fnames.Last() == f)
                    isLastFile = true;

                StreamReader sr = new StreamReader(f);

                if (isSP3)
                {
                    int lineno = 0;

                    while (!sr.EndOfStream)
                    {
                        string s = sr.ReadLine();

                        //if (sp3HeaderStart.Match(s).Success) { isHeader = true; }
                        if (sp3HeaderEnd.Match(s).Success) { isHeader = false; firstHeaderDone = true; }

                        // Change first line of first header
                        if (lineno++ == 0 && !firstHeaderDone)
                        {
                            s = s.Replace(" 96", String.Format("{0:D3}", fnames.Count() * 96));
                        }

                        if (isHeader && firstHeaderDone)
                            continue;

                        // Output EOF only after last inputfile..
                        if (Regex.Match(s, @"EOF").Success && !isLastFile)
                            continue;

                        Console.WriteLine(s);
                    }
                }
                else
                {
                    while (!sr.EndOfStream)
                    {
                        string s = sr.ReadLine();

                        if (clkHeaderStart.Match(s).Success) { isHeader = true; }
                        if (cklHeaderEnd.Match(s).Success)
                        {
                            isHeader = false;
                            if (!firstHeaderDone)
                                firstHeaderDone = true;
                             else
                                continue;
                        }

                        if (isHeader && firstHeaderDone)
                            continue;

                        Console.WriteLine(s);
                    }
                }
            }
        }
    }
}
