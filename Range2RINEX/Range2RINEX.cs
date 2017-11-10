using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

/*
 * Parse Range-message from Novatel OEMV.
 * Status:
 *  GPS handled correct, except L2C and L5
 *  GLONASS handled correct
 *  
 * To do:
 *      Handle GPS L2C
 *      Handle GPS L5
 *      Handle SBAS
 *      Parse RangeCMP
 *      Parse binary format
 * 
 */
namespace Range2RINEX
{
    partial class Range2RINEX
    {
        static void Main(string[] args)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            
            // Gobble up stdin
            List<Epoch> epochs = new List<Epoch>();
            string line;

            while ((line = Console.ReadLine()) != null)
                RangeParser.Parse(line, epochs);

            // Output RINEX
            ExportRinex(epochs);
        }
    }
}
