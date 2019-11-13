using PerfGraphVSIX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfGraphVSIX
{
    public interface ITakeSample
    {
        Task DoSampleAsync(MeasurementHolder measurementHolder, string descriptionOverride = "");
    }
}
