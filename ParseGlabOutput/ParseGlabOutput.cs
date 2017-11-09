using System;
using System.Collections.Generic;
using System.Linq;

namespace ParseGlabOutput
{
    class Epoch
    {
        public DateTime ts;
        public double ns;
    }

    class ParseGlabOutput
    {
        static void Main(string[] args)
        {
            bool forward = true;        // Detect if data is forward or forward/backward. Output only backward

            Epoch epoch;
            List<Epoch> epochs = new List<Epoch>();

            string line = "";
            while((line = Console.ReadLine()) != null)
            {
                if (!line.StartsWith("FILTER"))
                    continue;

                epoch = new Epoch();

                string[] fields = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                DateTime ts = new DateTime(int.Parse(fields[1]), 1, 1);
                ts = ts.AddDays((double)int.Parse(fields[2]));
                ts = ts.AddSeconds(double.Parse(fields[3]));

                epoch.ts = ts;
                epoch.ns = double.Parse(fields[7]) * (1.0e9 / 299792458.0);

                if (epochs.Count > 0)
                {
                    // Detect if we are now reversed. If so, keep only last epoch
                    if (forward && epoch.ts < epochs.Last().ts)
                    {
                        forward = false;
                        Epoch tmp = epochs.Last();
                        epochs.Clear();
                        epochs.Add(tmp);
                    }
                }

                epochs.Add(epoch);
            }

            // TO-DO interpolate missing epochs?

            if (!forward)
                epochs.Reverse();

            epochs.ForEach(e => Console.WriteLine(e.ns));
        }
    }
}
