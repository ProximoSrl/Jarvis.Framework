using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Domain.Serialization;
using Jarvis.Framework.Shared.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Commands
{
    /// <summary>
    /// To transfer a generic command this is the message that offline
    /// service send to main jarvis system to sync a command but it can 
    /// be used to send any kind of command for direct execution to the
    /// corresponding service.
    /// </summary>
    public class ExecuteCommandDto
    {
        public static ExecuteCommandDto CreateExecutionDto(ICommand command, string impersonatingUser)
        {
            return new ExecuteCommandDto()
            {
                Type = command.GetType().AssemblyQualifiedName,
                SerializedCommand = SerializeCommand(command),
                ImpersonatingUser = impersonatingUser,
            };
        }

        /// <summary>
        /// Cannot create a command without using the static method.
        /// </summary>
        private ExecuteCommandDto()
        {

        }

        public static ICommand Deserialize(ExecuteCommandDto dto)
        {
            var type = System.Type.GetType(dto.Type);
            if (type == null)
            {
                throw new ApplicationException($"Unable to load command type {dto.Type}");
            }

            var jsonSerializerSettings = GetSerializationSettings();
            var rawDeserializedObject = JsonConvert.DeserializeObject(dto.SerializedCommand, type, jsonSerializerSettings);

            var command = rawDeserializedObject as ICommand;
            if (command == null)
            {
                throw new ApplicationException($"Deserialzied type {dto.Type} does not implements ICommand interface");
            }

            return command;
        }

        public String SerializedCommand { get; set; }

        public String Type { get; set; }

        public String ImpersonatingUser { get; set; }

        /// <summary>
        /// This value is set only by an offline system and indicates
        /// the last checkpoint token when the offline session is started
        /// </summary>
        public Int64? OfflineCheckpointTokenFrom { get; set; }

        private static JsonSerializerSettings GetSerializationSettings()
        {
            return new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                ContractResolver = new MessagesContractResolver(),
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                Converters = new List<JsonConverter>()
                {
                    new StringValueJsonConverter()
                }
            };
        }

        private static string SerializeCommand(ICommand command)
        {
            JsonSerializerSettings jsonSerializerSettings = GetSerializationSettings();
            return JsonConvert.SerializeObject(command, jsonSerializerSettings);
        }
    }
}
