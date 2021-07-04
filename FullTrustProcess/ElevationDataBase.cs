using ShareClassLibrary;
using System;
using System.Collections.Generic;

namespace FullTrustProcess
{
    public class ElevationDataBase
    {
        public IEnumerable<string> Source { get; }

        public string Destination { get; }

        public ElevationDataBase(IEnumerable<string> Source, string Destination)
        {
            this.Source = Source;
            this.Destination = Destination;
        }
    }
}
