using Castle.MicroKernel.Lifestyle;

namespace Jarvis.Framework.Shared.IoC
{
    public class TransientNotTrackingLifestyle : AbstractLifestyleManager
    {
        public override void Dispose()
        {

        }
    }
}
