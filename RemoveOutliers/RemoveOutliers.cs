using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;

namespace RemoveOutliers
{
    class RemoveOutliers
    {
        static void Usage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("RemoveOutliers [-v] [-s <sigma>] [-fsep <char>] [-fld <f>] [<file>]");
            Console.WriteLine(" -v\t\tVerbose. Print information on detected outliers to STDERR");
            Console.WriteLine(" -s <sigma>\tDeviations larger than sigma multiples of Median Absolute Deviation will be treated as outliers. Default 7");
            Console.WriteLine(" -fsep <char>\tField-separator. For data in .CSV-format, specify separation character. Default ';'");
            Console.WriteLine(" -fld <f>\tFor data in .CSV-format, specify which column to process. Default 2. Other columns left unchanged.");
            Console.WriteLine(" <filename>\tFile to read data from. If not specified, read from STDIN.");
        }
        /*
         * TO-DO
         *  Autodetect if inputdata is CSV. If so, use sane defaults; fsep=;, fld=2
         *  If input data is not CSV, work normally
         *  
         *  Options: sigma, fld, fsep
         */
        static void Main(string[] args)
        {
            float sigma = 7;
            char fsep = ';';
            int valueField = 0;
            bool verbose = false;
            bool inputIsCsv = false;

            StreamReader inputStream = null;
            for(int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-s":
                        if (++i >= args.Length || ! float.TryParse(args[i], out sigma))
                        {
                            Usage();
                            return;
                        }
                        break;
                    case "-fsep":
                        if (++i >= args.Length)
                        {
                            Usage();
                            return;
                        }

                        fsep = args[i].First<char>();
                        inputIsCsv = true;
                        break;
                    case "-fld":
                        if (++i >= args.Length || !int.TryParse(args[i], out valueField))
                        {
                            Usage();
                            return;
                        }
                        inputIsCsv = true;
                        break;
                    case "-v":
                        verbose = true;
                        break;
                    default:
                        try
                        {
                            inputStream = new StreamReader(args[i]);
                        }
                        catch
                        {
                            Console.Error.WriteLine("Error opening file {0}", args[i]);
                            return;
                        }
                        break;
                }
            }
            
            
            int outlierCount = 0;

            // Set cultureinfo to InvariantCulture, use dot as decimal separator in output and input
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            List<String[]> rawInput = new List<String[]>();
            List<Double> input = new List<double>();
            List<Double> differences = new List<double>();

            // No input-file given, read data from STDIN
            if(inputStream == null)
                inputStream = new StreamReader(Console.OpenStandardInput());

            string line;
            while (!inputStream.EndOfStream)
            {
                double value;
                line = inputStream.ReadLine();
                // split on fsep char
                // store array "rawinput"
                // parse value-field
                // Store parsed value in input(?)
                // Use index to modify value-field in rawInput
                // output rawInput with changes, separated by fsep
                if (inputIsCsv)
                {
                    String[] fields = line.Split(fsep);
                    if (valueField >= fields.Length || !Double.TryParse(fields[valueField], out value))
                    {
                        if (verbose)
                            Console.Error.WriteLine("Skipping malformed line: \"{0}\"", line.Trim());

                        continue;
                    }
                    
                    input.Add(value);
                    rawInput.Add(fields);
                }
                else
                {
                    if (!Double.TryParse(line, out value))
                    {
                        if (verbose)
                            Console.Error.WriteLine("Skipping malformed line: \"{0}\"", line.Trim());

                        continue;
                    }

                    input.Add(value);
                }
            }

            // Take differences
            for(int i = 1; i < input.Count; i++)
            {
                differences.Add(input[i] - input[i-1]);
            }

            // Get median absolute deviation
            double mad = Math.Abs(differences.OrderBy(x => Math.Abs(x)).Skip(differences.Count() / 2).First());

            // Output first phase-point. Identify outliers, remove, convert back to phase and print
            double lastVal = input[0];
            Console.WriteLine(lastVal);
            for (int i = 0; i < differences.Count; i++)
            {
                if (i < differences.Count - 1)
                {
                    if (verbose)
                        if (Math.Abs(differences[i + 1]) > mad * sigma)
                        {
                            Console.Error.WriteLine("Removed outlier at {0}", i + 1);
                            outlierCount++;
                        }

                    if (differences[i + 1] < -mad * sigma)
                        differences[i + 1] = -mad;
                    else if (differences[i + 1] > mad * sigma)
                        differences[i + 1] = mad;
                }

                lastVal += differences[i];
                Console.WriteLine(lastVal);
            }


            //    // Look for outliers bigger than MAD*Sigma, replace with difference of MAD, preserving sign
            //    for (int i = 0; i < differences.Count; i++)
            //{
            //    if (verbose)
            //        if (Math.Abs(differences[i]) > mad * sigma)
            //        {
            //            Console.Error.WriteLine("Removed outlier at {0}", i);
            //            outlierCount++;
            //        }

            //    if(differences[i] < -mad * sigma)
            //        differences[i] = -mad;
            //    else if (differences[i] > mad * sigma)
            //        differences[i] = mad;
            //}

            //// Convert back to phase, output result
            //double lastVal = 0;
            //differences.Insert(0, input[0]);
            //for (int i = 0; i < differences.Count; i++)
            //{
            //    lastVal += differences[i];

            //    if (inputIsCsv)
            //    {
            //        for (int ix = 0; ix < rawInput[i].Length; ix++)
            //        {
            //            if (ix == valueField)
            //                Console.Write("{0}{1}", lastVal, fsep);
            //            else
            //                Console.Write("{0}{1}", rawInput[i][ix], fsep);
            //        }

            //        Console.WriteLine();
            //    }
            //    else
            //    {
            //        Console.WriteLine(lastVal);
            //    }
            //}

            if (verbose)
                Console.Error.WriteLine("Removed {0} outliers.", outlierCount);
        }
    }
}
