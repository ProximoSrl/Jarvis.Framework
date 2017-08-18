using Castle.Core.Logging;
using Metrics;
using Metrics.Core;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Messaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Support
{
    public class DatabaseHealthCheck : HealthCheck
    {
        private readonly MongoClient client;
        private readonly String _dbDescription;

        public DatabaseHealthCheck(String dbDescription, String serverUrl)
           : base("MongoDatabaseCheck: " + dbDescription)
        {
            var url = new MongoUrl(serverUrl);
            client = new MongoClient(url);
            _dbDescription = dbDescription;
            HealthChecks.RegisterHealthCheck(this);
        }

        protected override HealthCheckResult Check()
        {
            var state = this.client.Cluster.Description.State;
            if (state == MongoDB.Driver.Core.Clusters.ClusterState.Connected)
                return HealthCheckResult.Healthy();

            return HealthCheckResult.Unhealthy("Unable to connect to Mongo Db Instance {0}!", _dbDescription);
        }
    }

    public class MsmqHealthCheck : HealthCheck
    {
        private readonly String _queueName;
        private readonly Int32 _messageLimit;
        private readonly ILogger _logger;

        public MsmqHealthCheck(String queueName, Int32 messageLimit, ILogger logger)
           : base("MsmqHealthCheck: " + queueName)
        {
            _queueName = queueName;
            _messageLimit = messageLimit;
            _logger = logger;
            HealthChecks.RegisterHealthCheck(this);
        }

        protected override HealthCheckResult Check()
        {
            PeekQueueToActivate();
            var count = GetCount();
            if (count > _messageLimit)
            {
                return HealthCheckResult.Unhealthy("Queue {0} has {1} messages waiting in queue, exceeding maximum allowable limit of {2}",
                    _queueName, count, _messageLimit);
            }

            return HealthCheckResult.Healthy();
        }

        private void PeekQueueToActivate()
        {
            try
            {
                using (var queue = new MessageQueue(_queueName))
                {
                    queue.Peek(TimeSpan.FromSeconds(1));
                }
            }
            catch (Exception)
            {
                //ignore exception, just peek the queue to force activation
            }

        }

        /// <summary>
        /// Taken from Ayende service bus https://github.com/hibernating-rhinos/rhino-esb
        /// Gets the count.
        /// http://blog.codebeside.org/archive/2008/08/27/counting-the-number-of-messages-in-a-message-queue-in.aspx
        /// </summary>
        /// <returns></returns>
        public int GetCount()
        {
            if (!MessageQueue.Exists(_queueName))
            {
                throw new ApplicationException(String.Format("Queue {0} does not exists!", _queueName));
            }

            var props = new NativeMethods.MQMGMTPROPS { cProp = 1 };
            try
            {
                props.aPropID = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(props.aPropID, NativeMethods.PROPID_MGMT_QUEUE_MESSAGE_COUNT);

                props.aPropVar = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.MQPROPVariant)));
                Marshal.StructureToPtr(new NativeMethods.MQPROPVariant { vt = NativeMethods.VT_NULL }, props.aPropVar, false);

                props.status = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(props.status, 0);

                int result = NativeMethods.MQMgmtGetInfo(null, "queue=DIRECT=OS:" + _queueName, ref props);
                if (result != 0)
                    throw new Win32Exception(result);

                if (Marshal.ReadInt32(props.status) != 0)
                {
                    return 0;
                }

                var propVar = (NativeMethods.MQPROPVariant)Marshal.PtrToStructure(props.aPropVar, typeof(NativeMethods.MQPROPVariant));
                if (propVar.vt != NativeMethods.VT_UI4)
                {
                    return 0;
                }
                else
                {
                    return Convert.ToInt32(propVar.ulVal);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(props.aPropID);
                Marshal.FreeHGlobal(props.aPropVar);
                Marshal.FreeHGlobal(props.status);
            }
        }

        public static class NativeMethods
        {
            public const int MQ_MOVE_ACCESS = 4;
            public const int MQ_DENY_NONE = 0;

            [DllImport("mqrt.dll")]
            internal static extern int MQMgmtGetInfo([MarshalAs(UnmanagedType.BStr)]string computerName, [MarshalAs(UnmanagedType.BStr)]string objectName, ref MQMGMTPROPS mgmtProps);

            public const byte VT_NULL = 1;
            public const byte VT_UI4 = 19;
            public const int PROPID_MGMT_QUEUE_MESSAGE_COUNT = 7;

            //size must be 16
            [StructLayout(LayoutKind.Sequential)]
            internal struct MQPROPVariant
            {
                public byte vt;       //0
                public byte spacer;   //1
                public short spacer2; //2
                public int spacer3;   //4
                public uint ulVal;    //8
                public int spacer4;   //12
            }

            //size must be 16 in x86 and 28 in x64
            [StructLayout(LayoutKind.Sequential)]
            internal struct MQMGMTPROPS
            {
                public uint cProp;
                public IntPtr aPropID;
                public IntPtr aPropVar;
                public IntPtr status;

            }
        }
    }
}
