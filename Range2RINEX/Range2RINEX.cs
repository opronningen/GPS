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
 *      OK Handle GPS L2C
 *      OK Handle GPS L5
 *      OK Handle SBAS
 *      Parse RangeCMP
 *      Parse binary format
 *      Parse Receiver Status-field
 * 
 */
namespace Range2RINEX
{
    partial class Range2RINEX
    {
        static void Main(string[] args)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            RangeParser rp = new RangeParser();
            RINEX211Log rl = new RINEX211Log();

            // Gobble up stdin
            string line;
            while ((line = Console.ReadLine()) != null)
                rl.Add(rp.Parse(line));

            rl.WriteLog();

            // Output RINEX
            //ExportRinex(epochs);
        }
    }
}
