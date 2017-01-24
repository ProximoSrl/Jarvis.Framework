using System;

namespace Jarvis.Framework.Shared.IdentitySupport
{

    public class ReservationSlot
    {
        public ReservationSlot(long startIndex, long endIndex)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public Int64 StartIndex { get; private set; }

        public Int64 EndIndex { get; private set; }
    }
}