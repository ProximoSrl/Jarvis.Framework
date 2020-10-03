using Castle.MicroKernel.Lifestyle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.IoC
{
    public class TransientNotTrackingLifestyle : AbstractLifestyleManager
    {
        public override void Dispose()
        {
            
        }
    }
}
